using ActionTracker.Application.Features.StrategicObjectives.DTOs;
using ActionTracker.Application.Features.StrategicObjectives.Interfaces;
using ActionTracker.Domain.Entities;
using ActionTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Infrastructure.Services;

public class StrategicObjectiveService : IStrategicObjectiveService
{
    private readonly AppDbContext                        _context;
    private readonly ILogger<StrategicObjectiveService> _logger;

    public StrategicObjectiveService(AppDbContext context, ILogger<StrategicObjectiveService> logger)
    {
        _context = context;
        _logger  = logger;
    }

    // -------------------------------------------------------------------------
    // GetAllAsync
    // -------------------------------------------------------------------------

    public async Task<StrategicObjectiveListResponseDto> GetAllAsync(
        int               page,
        int               pageSize,
        Guid?             orgUnitId      = null,
        bool              includeDeleted = false,
        CancellationToken ct             = default)
    {
        try
        {
            var query = includeDeleted
                ? _context.StrategicObjectives.IgnoreQueryFilters()
                : _context.StrategicObjectives.AsQueryable();

            query = query
                .Include(o => o.OrgUnit)
                .Include(o => o.Kpis);

            if (orgUnitId.HasValue)
                query = query.Where(o => o.OrgUnitId == orgUnitId.Value);

            query = query.OrderBy(o => o.ObjectiveCode);

            var totalCount = await query.CountAsync(ct);

            var objectives = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var dtos = objectives
                .Select(o => MapToDto(o, kpiCount: o.Kpis.Count(k => !k.IsDeleted)))
                .ToList();

            return new StrategicObjectiveListResponseDto
            {
                Objectives = dtos,
                TotalCount = totalCount,
                Page       = page,
                PageSize   = pageSize,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving strategic objectives (page={Page}, pageSize={PageSize}, orgUnitId={OrgUnitId})",
                page, pageSize, orgUnitId);
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // GetByIdAsync
    // -------------------------------------------------------------------------

    public async Task<StrategicObjectiveDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var objective = await _context.StrategicObjectives
                .IgnoreQueryFilters()
                .Include(o => o.OrgUnit)
                .Include(o => o.Kpis)
                .FirstOrDefaultAsync(o => o.Id == id, ct);

            if (objective is null) return null;

            return MapToDto(objective, kpiCount: objective.Kpis.Count(k => !k.IsDeleted));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving strategic objective {Id}", id);
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // CreateAsync
    // -------------------------------------------------------------------------

    public async Task<StrategicObjectiveDto> CreateAsync(
        CreateStrategicObjectiveRequestDto request,
        CancellationToken                  ct = default)
    {
        try
        {
            var orgUnit = await _context.OrgUnits
                .FirstOrDefaultAsync(o => o.Id == request.OrgUnitId, ct)
                ?? throw new ArgumentException(
                    $"Org unit '{request.OrgUnitId}' does not exist or has been deleted.",
                    nameof(request.OrgUnitId));

            // Count ALL objectives ever created (including deleted) to never reuse a code.
            var count = await _context.StrategicObjectives
                .IgnoreQueryFilters()
                .CountAsync(ct);

            var objectiveCode = $"SO-{count + 1}";

            var objective = new StrategicObjective
            {
                Id            = Guid.NewGuid(),
                ObjectiveCode = objectiveCode,
                Statement     = request.Statement,
                Description   = request.Description,
                OrgUnitId     = request.OrgUnitId,
                IsDeleted     = false,
                CreatedAt     = DateTime.UtcNow,
            };

            _context.StrategicObjectives.Add(objective);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Created StrategicObjective {Id} '{Code}' for OrgUnit {OrgUnitId}",
                objective.Id, objectiveCode, request.OrgUnitId);

            // Attach OrgUnit navigation for mapping.
            objective.OrgUnit = orgUnit;

            return MapToDto(objective, kpiCount: 0);
        }
        catch (ArgumentException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating strategic objective");
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // UpdateAsync
    // -------------------------------------------------------------------------

    public async Task<StrategicObjectiveDto> UpdateAsync(
        Guid                               id,
        UpdateStrategicObjectiveRequestDto request,
        CancellationToken                  ct = default)
    {
        try
        {
            var objective = await _context.StrategicObjectives
                .IgnoreQueryFilters()
                .Include(o => o.Kpis)
                .FirstOrDefaultAsync(o => o.Id == id, ct)
                ?? throw new KeyNotFoundException($"Strategic objective '{id}' not found.");

            var orgUnit = await _context.OrgUnits
                .FirstOrDefaultAsync(o => o.Id == request.OrgUnitId, ct)
                ?? throw new ArgumentException(
                    $"Org unit '{request.OrgUnitId}' does not exist or has been deleted.",
                    nameof(request.OrgUnitId));

            objective.Statement   = request.Statement;
            objective.Description = request.Description;
            objective.OrgUnitId   = request.OrgUnitId;
            objective.UpdatedAt   = DateTime.UtcNow;
            // ObjectiveCode is intentionally NOT updated.

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Updated StrategicObjective {Id}", id);

            objective.OrgUnit = orgUnit;

            return MapToDto(objective, kpiCount: objective.Kpis.Count(k => !k.IsDeleted));
        }
        catch (KeyNotFoundException) { throw; }
        catch (ArgumentException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating strategic objective {Id}", id);
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
            var objective = await _context.StrategicObjectives
                .FirstOrDefaultAsync(o => o.Id == id, ct)
                ?? throw new KeyNotFoundException($"Strategic objective '{id}' not found.");

            objective.IsDeleted = true;
            objective.DeletedAt = DateTime.UtcNow;
            objective.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Soft-deleted StrategicObjective {Id}", id);
        }
        catch (KeyNotFoundException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error soft-deleting strategic objective {Id}", id);
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
            var objective = await _context.StrategicObjectives
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Id == id, ct)
                ?? throw new KeyNotFoundException($"Strategic objective '{id}' not found.");

            objective.IsDeleted = false;
            objective.DeletedAt = null;
            objective.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Restored StrategicObjective {Id}", id);
        }
        catch (KeyNotFoundException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring strategic objective {Id}", id);
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // GetByOrgUnitAsync
    // -------------------------------------------------------------------------

    public async Task<List<StrategicObjectiveDto>> GetByOrgUnitAsync(
        Guid              orgUnitId,
        CancellationToken ct = default)
    {
        try
        {
            var objectives = await _context.StrategicObjectives
                .Include(o => o.OrgUnit)
                .Include(o => o.Kpis)
                .Where(o => o.OrgUnitId == orgUnitId)
                .OrderBy(o => o.ObjectiveCode)
                .ToListAsync(ct);

            return objectives
                .Select(o => MapToDto(o, kpiCount: o.Kpis.Count(k => !k.IsDeleted)))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving strategic objectives for OrgUnit {OrgUnitId}", orgUnitId);
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static StrategicObjectiveDto MapToDto(StrategicObjective o, int kpiCount) =>
        new()
        {
            Id             = o.Id,
            ObjectiveCode  = o.ObjectiveCode,
            Statement      = o.Statement,
            Description    = o.Description,
            OrgUnitId      = o.OrgUnitId,
            OrgUnitName    = o.OrgUnit?.Name   ?? string.Empty,
            OrgUnitCode    = o.OrgUnit?.Code,
            IsDeleted      = o.IsDeleted,
            CreatedAt      = o.CreatedAt,
            UpdatedAt      = o.UpdatedAt,
            KpiCount       = kpiCount,
        };
}
