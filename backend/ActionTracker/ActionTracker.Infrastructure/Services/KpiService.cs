using System.Globalization;
using ActionTracker.Application.Common;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.Kpis.DTOs;
using ActionTracker.Application.Features.Kpis.Interfaces;
using ActionTracker.Application.Features.Notifications;
using ActionTracker.Application.Features.Notifications.DTOs;
using ActionTracker.Domain.Entities;
using ActionTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActionTracker.Infrastructure.Services;

public class KpiService : IKpiService
{
    private readonly AppDbContext         _context;
    private readonly IUserLookupService   _userLookup;
    private readonly ILogger<KpiService>  _logger;
    private readonly IEmailSender         _emailSender;
    private readonly INotificationService _notificationService;
    private readonly AppSettings          _appSettings;
    private readonly IServiceScopeFactory _scopeFactory;

    public KpiService(
        AppDbContext           context,
        IUserLookupService    userLookup,
        ILogger<KpiService>   logger,
        IEmailSender          emailSender,
        INotificationService  notificationService,
        IOptions<AppSettings> appSettings,
        IServiceScopeFactory  scopeFactory)
    {
        _context             = context;
        _userLookup          = userLookup;
        _logger              = logger;
        _emailSender         = emailSender;
        _notificationService = notificationService;
        _appSettings         = appSettings.Value;
        _scopeFactory        = scopeFactory;
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

            var kpiNames = await ResolveKpiNamesAsync(kpis, ct);
            var dtos = kpis
                .Select(k => MapToDto(k, targetCount: countLookup.TryGetValue(k.Id, out var c) ? c : 0, kpiNames))
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

            var kpiNames    = await ResolveKpiNamesAsync([kpi], ct);
            var targetNames = await ResolveTargetNamesAsync(targets, ct);

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
                CreatedBy            = kpi.CreatedBy,
                UpdatedBy            = kpi.UpdatedBy,
                DeletedBy            = kpi.DeletedBy,
                CreatedByName        = Resolve(kpi.CreatedBy, kpiNames),
                UpdatedByName        = Resolve(kpi.UpdatedBy, kpiNames),
                DeletedByName        = Resolve(kpi.DeletedBy, kpiNames),
                TargetCount          = targets.Count,
                Targets              = targets.Select(t => MapToTargetDto(t, targetNames)).ToList(),
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

    public async Task<KpiDto> CreateAsync(CreateKpiRequestDto request, string userId, CancellationToken ct = default)
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
                CreatedBy            = userId,
            };

            _context.Kpis.Add(kpi);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Created KPI {Id} #{KpiNumber} for Objective {ObjectiveId}",
                kpi.Id, kpiNumber, request.StrategicObjectiveId);

            // Fire-and-forget email notification
            var capturedKpi = kpi;
            var capturedObjective = objective;
            var capturedUserId = userId;
            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                var notifService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

                try
                {
                    var creatorName = await _userLookup.GetDisplayNameAsync(capturedUserId, CancellationToken.None);
                    var placeholders = new Dictionary<string, string>
                    {
                        ["KpiName"]           = capturedKpi.Name,
                        ["ObjectiveName"]     = capturedObjective.Statement,
                        ["ObjectiveCode"]     = capturedObjective.ObjectiveCode,
                        ["CalculationMethod"] = capturedKpi.CalculationMethod ?? string.Empty,
                        ["Period"]            = capturedKpi.Period.ToString(),
                        ["CreatedBy"]         = creatorName ?? capturedUserId,
                        ["ItemUrl"]           = $"{_appSettings.FrontendBaseUrl}/admin/kpis",
                    };

                    var creatorEmail = await db.Users
                        .Where(u => u.Id == capturedUserId && u.IsActive)
                        .Select(u => u.Email)
                        .FirstOrDefaultAsync();

                    if (creatorEmail is not null)
                    {
                        await emailSender.SendEmailAsync("Kpi.Created", placeholders,
                            [creatorEmail], "Kpi", capturedKpi.Id, capturedUserId);
                    }
                }
                catch (Exception ex2)
                {
                    _logger.LogError(ex2, "Error sending email for Kpi.Created {Id}", capturedKpi.Id);
                }

                // In-app notification (self-notification for confirmation)
                try
                {
                    var creatorName2 = await _userLookup.GetDisplayNameAsync(capturedUserId, CancellationToken.None);
                    await notifService.CreateAsync(new CreateNotificationDto
                    {
                        UserId               = capturedUserId,
                        Title                = "KPI Created",
                        Message              = $"{capturedKpi.Name} for {capturedObjective.Statement}",
                        Type                 = "Kpi",
                        ActionType           = "Created",
                        RelatedEntityType    = "Kpi",
                        RelatedEntityId      = capturedKpi.Id,
                        Url                  = $"{_appSettings.FrontendBaseUrl}/admin/kpis",
                        CreatedByUserId      = capturedUserId,
                        CreatedByDisplayName = creatorName2,
                    });
                }
                catch (Exception ex3)
                {
                    _logger.LogError(ex3, "Error creating notification for Kpi.Created {Id}", capturedKpi.Id);
                }
            });

            kpi.StrategicObjective = objective;

            var names = await ResolveKpiNamesAsync([kpi], ct);
            return MapToDto(kpi, targetCount: 0, names);
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
        Guid                id,
        UpdateKpiRequestDto request,
        string              userId,
        CancellationToken   ct = default)
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
            kpi.UpdatedBy         = userId;
            // StrategicObjectiveId and KpiNumber are intentionally NOT updated.

            var targetCount = await _context.KpiTargets
                .CountAsync(t => t.KpiId == id, ct);

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Updated KPI {Id}", id);

            var names = await ResolveKpiNamesAsync([kpi], ct);
            return MapToDto(kpi, targetCount, names);
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

    public async Task SoftDeleteAsync(Guid id, string userId, CancellationToken ct = default)
    {
        try
        {
            var kpi = await _context.Kpis
                .FirstOrDefaultAsync(k => k.Id == id, ct)
                ?? throw new KeyNotFoundException($"KPI '{id}' not found.");

            var now = DateTime.UtcNow;
            kpi.IsDeleted = true;
            kpi.DeletedAt = now;
            kpi.UpdatedAt = now;
            kpi.DeletedBy = userId;

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

    public async Task RestoreAsync(Guid id, string userId, CancellationToken ct = default)
    {
        try
        {
            var kpi = await _context.Kpis
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(k => k.Id == id, ct)
                ?? throw new KeyNotFoundException($"KPI '{id}' not found.");

            kpi.IsDeleted = false;
            kpi.DeletedAt = null;
            kpi.DeletedBy = null;
            kpi.UpdatedAt = DateTime.UtcNow;
            kpi.UpdatedBy = userId;

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

            var names = await ResolveKpiNamesAsync(kpis, ct);
            return kpis
                .Select(k => MapToDto(k, targetCount: countLookup.TryGetValue(k.Id, out var c) ? c : 0, names))
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
        string                    userId,
        CancellationToken         ct = default)
    {
        try
        {
            var existing = await _context.KpiTargets
                .FirstOrDefaultAsync(t =>
                    t.KpiId == request.KpiId &&
                    t.Year  == request.Year  &&
                    t.Month == request.Month, ct);

            var now = DateTime.UtcNow;
            if (existing is not null)
            {
                existing.Target    = request.Target;
                existing.Actual    = request.Actual;
                existing.Notes     = request.Notes;
                existing.UpdatedAt = now;
                existing.UpdatedBy = userId;
            }
            else
            {
                existing = new KpiTarget
                {
                    Id        = Guid.NewGuid(),
                    KpiId     = request.KpiId,
                    Year      = request.Year,
                    Month     = request.Month,
                    Target    = request.Target,
                    Actual    = request.Actual,
                    Notes     = request.Notes,
                    CreatedAt = now,
                    CreatedBy = userId,
                };
                _context.KpiTargets.Add(existing);
            }

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Upserted KpiTarget for KPI {KpiId} {Year}/{Month}", request.KpiId, request.Year, request.Month);

            var names = await ResolveTargetNamesAsync([existing], ct);
            return MapToTargetDto(existing, names);
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
        string                         userId,
        CancellationToken              ct = default)
    {
        try
        {
            var kpiExists = await _context.Kpis
                .AnyAsync(k => k.Id == request.KpiId, ct);

            if (!kpiExists)
                throw new KeyNotFoundException($"KPI '{request.KpiId}' not found.");

            var existingTargets = await _context.KpiTargets
                .Where(t => t.KpiId == request.KpiId && t.Year == request.Year)
                .ToListAsync(ct);

            var existingByMonth = existingTargets.ToDictionary(t => t.Month);
            var upserted        = new List<KpiTarget>(request.Targets.Count);
            var now             = DateTime.UtcNow;

            foreach (var monthDto in request.Targets)
            {
                if (existingByMonth.TryGetValue(monthDto.Month, out var existing))
                {
                    existing.Target    = monthDto.Target;
                    existing.Actual    = monthDto.Actual;
                    existing.Notes     = monthDto.Notes;
                    existing.UpdatedAt = now;
                    existing.UpdatedBy = userId;
                    upserted.Add(existing);
                }
                else
                {
                    var newTarget = new KpiTarget
                    {
                        Id        = Guid.NewGuid(),
                        KpiId     = request.KpiId,
                        Year      = request.Year,
                        Month     = monthDto.Month,
                        Target    = monthDto.Target,
                        Actual    = monthDto.Actual,
                        Notes     = monthDto.Notes,
                        CreatedAt = now,
                        CreatedBy = userId,
                    };
                    _context.KpiTargets.Add(newTarget);
                    upserted.Add(newTarget);
                }
            }

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Bulk-upserted {Count} KpiTargets for KPI {KpiId} year {Year}",
                upserted.Count, request.KpiId, request.Year);

            var names = await ResolveTargetNamesAsync(upserted, ct);
            return upserted.OrderBy(t => t.Month).Select(t => MapToTargetDto(t, names)).ToList();
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

            var names = await ResolveTargetNamesAsync(targets, ct);
            return targets.Select(t => MapToTargetDto(t, names)).ToList();
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

    private async Task<Dictionary<string, string>> ResolveKpiNamesAsync(
        IEnumerable<Kpi>  kpis,
        CancellationToken ct)
    {
        var ids = kpis
            .SelectMany(k => new[] { k.CreatedBy, k.UpdatedBy, k.DeletedBy })
            .Where(id => id != null).Cast<string>().Distinct();
        return await _userLookup.GetDisplayNamesAsync(ids, ct);
    }

    private async Task<Dictionary<string, string>> ResolveTargetNamesAsync(
        IEnumerable<KpiTarget> targets,
        CancellationToken      ct)
    {
        var ids = targets
            .SelectMany(t => new[] { t.CreatedBy, t.UpdatedBy })
            .Where(id => id != null).Cast<string>().Distinct();
        return await _userLookup.GetDisplayNamesAsync(ids, ct);
    }

    private static string? Resolve(string? userId, Dictionary<string, string> names)
        => userId != null && names.TryGetValue(userId, out var n) ? n : null;

    private static KpiDto MapToDto(Kpi k, int targetCount, Dictionary<string, string> names) =>
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
            DeletedAt            = k.DeletedAt,
            CreatedBy            = k.CreatedBy,
            UpdatedBy            = k.UpdatedBy,
            DeletedBy            = k.DeletedBy,
            CreatedByName        = Resolve(k.CreatedBy, names),
            UpdatedByName        = Resolve(k.UpdatedBy, names),
            DeletedByName        = Resolve(k.DeletedBy, names),
            TargetCount          = targetCount,
        };

    private static KpiTargetDto MapToTargetDto(KpiTarget t, Dictionary<string, string> names) =>
        new()
        {
            Id            = t.Id,
            KpiId         = t.KpiId,
            Year          = t.Year,
            Month         = t.Month,
            MonthName     = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(t.Month),
            Target        = t.Target,
            Actual        = t.Actual,
            Notes         = t.Notes,
            CreatedAt     = t.CreatedAt,
            UpdatedAt     = t.UpdatedAt,
            CreatedBy     = t.CreatedBy,
            UpdatedBy     = t.UpdatedBy,
            CreatedByName = Resolve(t.CreatedBy, names),
            UpdatedByName = Resolve(t.UpdatedBy, names),
        };
}
