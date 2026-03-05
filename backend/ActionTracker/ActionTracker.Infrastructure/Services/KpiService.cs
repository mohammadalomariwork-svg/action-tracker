using System.Globalization;
using ActionTracker.Application.Features.Kpis.DTOs;
using ActionTracker.Application.Features.Kpis.Interfaces;
using ActionTracker.Domain.Entities;
using ActionTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Infrastructure.Services;

public class KpiService : IKpiService
{
    private readonly AppDbContext      _context;
    private readonly ILogger<KpiService> _logger;

    public KpiService(AppDbContext context, ILogger<KpiService> logger)
    {
        _context = context;
        _logger  = logger;
    }

    // -------------------------------------------------------------------------
    // GetAllAsync
    // -------------------------------------------------------------------------

    public async Task<KpiListResponseDto> GetAllAsync(
        int               page,
        int               pageSize,
        Guid?             objectiveId    = null,
        bool              includeDeleted = false,
        CancellationToken ct             = default)
    {
        try
        {
            var query = includeDeleted
                ? _context.Kpis.IgnoreQueryFilters()
                : _context.Kpis.AsQueryable();

            query = query.Include(k => k.StrategicObjective);

            if (objectiveId.HasValue)
                query = query.Where(k => k.StrategicObjectiveId == objectiveId.Value);

            query = query
                .OrderBy(k => k.StrategicObjectiveId)
                .ThenBy(k => k.KpiNumber);

            var totalCount = await query.CountAsync(ct);

            var kpis = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            // Resolve target counts in one query.
            var kpiIds = kpis.Select(k => k.Id).ToList();
            var targetCounts = await _context.KpiTargets
                .Where(t => kpiIds.Contains(t.KpiId))
                .GroupBy(t => t.KpiId)
                .Select(g => new { KpiId = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var countLookup = targetCounts.ToDictionary(g => g.KpiId, g => g.Count);

            var dtos = kpis
                .Select(k => MapToDto(k, targetCount: countLookup.TryGetValue(k.Id, out var c) ? c : 0))
                .ToList();

            return new KpiListResponseDto
            {
                Kpis       = dtos,
                TotalCount = totalCount,
                Page       = page,
                PageSize   = pageSize,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving KPIs (page={Page}, pageSize={PageSize}, objectiveId={ObjectiveId})",
                page, pageSize, objectiveId);
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // GetByIdAsync
    // -------------------------------------------------------------------------

    public async Task<KpiWithTargetsDto?> GetByIdAsync(
        Guid              id,
        int?              year = null,
        CancellationToken ct   = default)
    {
        try
        {
            var kpi = await _context.Kpis
                .IgnoreQueryFilters()
                .Include(k => k.StrategicObjective)
                .FirstOrDefaultAsync(k => k.Id == id, ct);

            if (kpi is null) return null;

            var targetsQuery = _context.KpiTargets
                .Where(t => t.KpiId == id);

            if (year.HasValue)
                targetsQuery = targetsQuery.Where(t => t.Year == year.Value);

            var targets = await targetsQuery
                .OrderBy(t => t.Year)
                .ThenBy(t => t.Month)
                .ToListAsync(ct);

            return new KpiWithTargetsDto
            {
                Id                   = kpi.Id,
                KpiNumber            = kpi.KpiNumber,
                Name                 = kpi.Name,
                Description          = kpi.Description,
                CalculationMethod    = kpi.CalculationMethod,
                Period               = kpi.Period.ToString(),
                PeriodValue          = (int)kpi.Period,
                Unit                 = kpi.Unit,
                StrategicObjectiveId = kpi.StrategicObjectiveId,
                ObjectiveCode        = kpi.StrategicObjective?.ObjectiveCode  ?? string.Empty,
                ObjectiveStatement   = kpi.StrategicObjective?.Statement      ?? string.Empty,
                IsDeleted            = kpi.IsDeleted,
                CreatedAt            = kpi.CreatedAt,
                UpdatedAt            = kpi.UpdatedAt,
                TargetCount          = targets.Count,
                Targets              = targets.Select(MapToTargetDto).ToList(),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving KPI {Id}", id);
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // CreateAsync
    // -------------------------------------------------------------------------

    public async Task<KpiDto> CreateAsync(CreateKpiRequestDto request, CancellationToken ct = default)
    {
        try
        {
            var objective = await _context.StrategicObjectives
                .FirstOrDefaultAsync(o => o.Id == request.StrategicObjectiveId, ct)
                ?? throw new ArgumentException(
                    $"Strategic objective '{request.StrategicObjectiveId}' does not exist or has been deleted.",
                    nameof(request.StrategicObjectiveId));

            // Auto-assign KpiNumber: max existing (including deleted) + 1.
            var maxNumber = await _context.Kpis
                .IgnoreQueryFilters()
                .Where(k => k.StrategicObjectiveId == request.StrategicObjectiveId)
                .Select(k => (int?)k.KpiNumber)
                .MaxAsync(ct);

            var kpiNumber = (maxNumber ?? 0) + 1;

            var kpi = new Kpi
            {
                Id                   = Guid.NewGuid(),
                KpiNumber            = kpiNumber,
                Name                 = request.Name,
                Description          = request.Description,
                CalculationMethod    = request.CalculationMethod,
                Period               = (MeasurementPeriod)request.Period,
                Unit                 = request.Unit,
                StrategicObjectiveId = request.StrategicObjectiveId,
                IsDeleted            = false,
                CreatedAt            = DateTime.UtcNow,
            };

            _context.Kpis.Add(kpi);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Created KPI {Id} #{KpiNumber} for Objective {ObjectiveId}",
                kpi.Id, kpiNumber, request.StrategicObjectiveId);

            kpi.StrategicObjective = objective;

            return MapToDto(kpi, targetCount: 0);
        }
        catch (ArgumentException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating KPI for objective {ObjectiveId}", request.StrategicObjectiveId);
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // UpdateAsync
    // -------------------------------------------------------------------------

    public async Task<KpiDto> UpdateAsync(
        Guid              id,
        UpdateKpiRequestDto request,
        CancellationToken ct = default)
    {
        try
        {
            var kpi = await _context.Kpis
                .IgnoreQueryFilters()
                .Include(k => k.StrategicObjective)
                .FirstOrDefaultAsync(k => k.Id == id, ct)
                ?? throw new KeyNotFoundException($"KPI '{id}' not found.");

            kpi.Name              = request.Name;
            kpi.Description       = request.Description;
            kpi.CalculationMethod = request.CalculationMethod;
            kpi.Period            = (MeasurementPeriod)request.Period;
            kpi.Unit              = request.Unit;
            kpi.UpdatedAt         = DateTime.UtcNow;
            // StrategicObjectiveId and KpiNumber are intentionally NOT updated.

            var targetCount = await _context.KpiTargets
                .CountAsync(t => t.KpiId == id, ct);

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Updated KPI {Id}", id);

            return MapToDto(kpi, targetCount);
        }
        catch (KeyNotFoundException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating KPI {Id}", id);
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // SoftDeleteAsync
    // -------------------------------------------------------------------------

    public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var kpi = await _context.Kpis
                .FirstOrDefaultAsync(k => k.Id == id, ct)
                ?? throw new KeyNotFoundException($"KPI '{id}' not found.");

            kpi.IsDeleted = true;
            kpi.DeletedAt = DateTime.UtcNow;
            kpi.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Soft-deleted KPI {Id}", id);
        }
        catch (KeyNotFoundException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error soft-deleting KPI {Id}", id);
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // RestoreAsync
    // -------------------------------------------------------------------------

    public async Task RestoreAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var kpi = await _context.Kpis
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(k => k.Id == id, ct)
                ?? throw new KeyNotFoundException($"KPI '{id}' not found.");

            kpi.IsDeleted = false;
            kpi.DeletedAt = null;
            kpi.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Restored KPI {Id}", id);
        }
        catch (KeyNotFoundException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring KPI {Id}", id);
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // GetByObjectiveAsync
    // -------------------------------------------------------------------------

    public async Task<List<KpiDto>> GetByObjectiveAsync(Guid objectiveId, CancellationToken ct = default)
    {
        try
        {
            var kpis = await _context.Kpis
                .Include(k => k.StrategicObjective)
                .Where(k => k.StrategicObjectiveId == objectiveId)
                .OrderBy(k => k.KpiNumber)
                .ToListAsync(ct);

            var kpiIds = kpis.Select(k => k.Id).ToList();
            var targetCounts = await _context.KpiTargets
                .Where(t => kpiIds.Contains(t.KpiId))
                .GroupBy(t => t.KpiId)
                .Select(g => new { KpiId = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var countLookup = targetCounts.ToDictionary(g => g.KpiId, g => g.Count);

            return kpis
                .Select(k => MapToDto(k, targetCount: countLookup.TryGetValue(k.Id, out var c) ? c : 0))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving KPIs for objective {ObjectiveId}", objectiveId);
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // UpsertTargetAsync
    // -------------------------------------------------------------------------

    public async Task<KpiTargetDto> UpsertTargetAsync(
        UpsertKpiTargetRequestDto request,
        CancellationToken         ct = default)
    {
        try
        {
            var existing = await _context.KpiTargets
                .FirstOrDefaultAsync(t =>
                    t.KpiId == request.KpiId &&
                    t.Year  == request.Year  &&
                    t.Month == request.Month, ct);

            if (existing is not null)
            {
                existing.Target = request.Target;
                existing.Actual = request.Actual;
                existing.Notes  = request.Notes;
            }
            else
            {
                existing = new KpiTarget
                {
                    Id     = Guid.NewGuid(),
                    KpiId  = request.KpiId,
                    Year   = request.Year,
                    Month  = request.Month,
                    Target = request.Target,
                    Actual = request.Actual,
                    Notes  = request.Notes,
                };
                _context.KpiTargets.Add(existing);
            }

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Upserted KpiTarget for KPI {KpiId} {Year}/{Month}", request.KpiId, request.Year, request.Month);

            return MapToTargetDto(existing);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error upserting KpiTarget for KPI {KpiId} {Year}/{Month}",
                request.KpiId, request.Year, request.Month);
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // BulkUpsertTargetsAsync
    // -------------------------------------------------------------------------

    public async Task<List<KpiTargetDto>> BulkUpsertTargetsAsync(
        BulkUpsertKpiTargetsRequestDto request,
        CancellationToken              ct = default)
    {
        try
        {
            var kpiExists = await _context.Kpis
                .AnyAsync(k => k.Id == request.KpiId, ct);

            if (!kpiExists)
                throw new KeyNotFoundException($"KPI '{request.KpiId}' not found.");

            // Load all existing targets for this KPI/year in one query.
            var existingTargets = await _context.KpiTargets
                .Where(t => t.KpiId == request.KpiId && t.Year == request.Year)
                .ToListAsync(ct);

            var existingByMonth = existingTargets.ToDictionary(t => t.Month);
            var upserted        = new List<KpiTarget>(request.Targets.Count);

            foreach (var monthDto in request.Targets)
            {
                if (existingByMonth.TryGetValue(monthDto.Month, out var existing))
                {
                    existing.Target = monthDto.Target;
                    existing.Actual = monthDto.Actual;
                    existing.Notes  = monthDto.Notes;
                    upserted.Add(existing);
                }
                else
                {
                    var newTarget = new KpiTarget
                    {
                        Id     = Guid.NewGuid(),
                        KpiId  = request.KpiId,
                        Year   = request.Year,
                        Month  = monthDto.Month,
                        Target = monthDto.Target,
                        Actual = monthDto.Actual,
                        Notes  = monthDto.Notes,
                    };
                    _context.KpiTargets.Add(newTarget);
                    upserted.Add(newTarget);
                }
            }

            // Single SaveChangesAsync — all changes tracked in one transaction.
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Bulk-upserted {Count} KpiTargets for KPI {KpiId} year {Year}",
                upserted.Count, request.KpiId, request.Year);

            return upserted.OrderBy(t => t.Month).Select(MapToTargetDto).ToList();
        }
        catch (KeyNotFoundException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error bulk-upserting KpiTargets for KPI {KpiId} year {Year}",
                request.KpiId, request.Year);
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // GetTargetsAsync
    // -------------------------------------------------------------------------

    public async Task<List<KpiTargetDto>> GetTargetsAsync(
        Guid              kpiId,
        int               year,
        CancellationToken ct = default)
    {
        try
        {
            var targets = await _context.KpiTargets
                .Where(t => t.KpiId == kpiId && t.Year == year)
                .OrderBy(t => t.Month)
                .ToListAsync(ct);

            return targets.Select(MapToTargetDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving targets for KPI {KpiId} year {Year}", kpiId, year);
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static KpiDto MapToDto(Kpi k, int targetCount) =>
        new()
        {
            Id                   = k.Id,
            KpiNumber            = k.KpiNumber,
            Name                 = k.Name,
            Description          = k.Description,
            CalculationMethod    = k.CalculationMethod,
            Period               = k.Period.ToString(),
            PeriodValue          = (int)k.Period,
            Unit                 = k.Unit,
            StrategicObjectiveId = k.StrategicObjectiveId,
            ObjectiveCode        = k.StrategicObjective?.ObjectiveCode ?? string.Empty,
            ObjectiveStatement   = k.StrategicObjective?.Statement     ?? string.Empty,
            IsDeleted            = k.IsDeleted,
            CreatedAt            = k.CreatedAt,
            UpdatedAt            = k.UpdatedAt,
            TargetCount          = targetCount,
        };

    private static KpiTargetDto MapToTargetDto(KpiTarget t) =>
        new()
        {
            Id        = t.Id,
            KpiId     = t.KpiId,
            Year      = t.Year,
            Month     = t.Month,
            MonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(t.Month),
            Target    = t.Target,
            Actual    = t.Actual,
            Notes     = t.Notes,
        };
}
