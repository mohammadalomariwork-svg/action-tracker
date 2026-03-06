using ActionTracker.Application.Common.Interfaces;
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
    private readonly IUserLookupService                 _userLookup;
    private readonly ILogger<StrategicObjectiveService> _logger;

    public StrategicObjectiveService(
        AppDbContext                        context,
        IUserLookupService                 userLookup,
        ILogger<StrategicObjectiveService> logger)
    {
        _context    = context;
        _userLookup = userLookup;
        _logger     = logger;
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

            var names = await ResolveNamesAsync(objectives, ct);

            var dtos = objectives
                .Select(o => MapToDto(o, kpiCount: o.Kpis.Count(k => !k.IsDeleted), names))
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

            var names = await ResolveNamesAsync([objective], ct);
            return MapToDto(objective, kpiCount: objective.Kpis.Count(k => !k.IsDeleted), names);
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
        string                             userId,
        CancellationToken                  ct = default)
    {
        try
        {
            var orgUnit = await _context.OrgUnits
                .FirstOrDefaultAsync(o => o.Id == request.OrgUnitId, ct)
                ?? throw new ArgumentException(
                    $"Org unit '{request.OrgUnitId}' does not exist or has been deleted.",
                    nameof(request.OrgUnitId));

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
                CreatedBy     = userId,
            };

            _context.StrategicObjectives.Add(objective);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Created StrategicObjective {Id} '{Code}' for OrgUnit {OrgUnitId}",
                objective.Id, objectiveCode, request.OrgUnitId);

            objective.OrgUnit = orgUnit;

            var names = await ResolveNamesAsync([objective], ct);
            return MapToDto(objective, kpiCount: 0, names);
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
        string                             userId,
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
            objective.UpdatedBy   = userId;

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Updated StrategicObjective {Id}", id);

            objective.OrgUnit = orgUnit;

            var names = await ResolveNamesAsync([objective], ct);
            return MapToDto(objective, kpiCount: objective.Kpis.Count(k => !k.IsDeleted), names);
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

    public async Task SoftDeleteAsync(Guid id, string userId, CancellationToken ct = default)
    {
        try
        {
            var objective = await _context.StrategicObjectives
                .FirstOrDefaultAsync(o => o.Id == id, ct)
                ?? throw new KeyNotFoundException($"Strategic objective '{id}' not found.");

            var now = DateTime.UtcNow;
            objective.IsDeleted = true;
            objective.DeletedAt = now;
            objective.UpdatedAt = now;
            objective.DeletedBy = userId;

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

    public async Task RestoreAsync(Guid id, string userId, CancellationToken ct = default)
    {
        try
        {
            var objective = await _context.StrategicObjectives
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Id == id, ct)
                ?? throw new KeyNotFoundException($"Strategic objective '{id}' not found.");

            objective.IsDeleted = false;
            objective.DeletedAt = null;
            objective.DeletedBy = null;
            objective.UpdatedAt = DateTime.UtcNow;
            objective.UpdatedBy = userId;

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

            var names = await ResolveNamesAsync(objectives, ct);

            return objectives
                .Select(o => MapToDto(o, kpiCount: o.Kpis.Count(k => !k.IsDeleted), names))
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

    private async Task<Dictionary<string, string>> ResolveNamesAsync(
        IEnumerable<StrategicObjective> objectives,
        CancellationToken               ct)
    {
        var ids = objectives
            .SelectMany(o => new[] { o.CreatedBy, o.UpdatedBy, o.DeletedBy })
            .Where(id => id != null)
            .Cast<string>()
            .Distinct();
        return await _userLookup.GetDisplayNamesAsync(ids, ct);
    }

    private static string? Resolve(string? userId, Dictionary<string, string> names)
        => userId != null && names.TryGetValue(userId, out var n) ? n : null;

    private static StrategicObjectiveDto MapToDto(
        StrategicObjective      o,
        int                     kpiCount,
        Dictionary<string, string> names) =>
        new()
        {
            Id             = o.Id,
            ObjectiveCode  = o.ObjectiveCode,
            Statement      = o.Statement,
            Description    = o.Description,
            OrgUnitId      = o.OrgUnitId,
            OrgUnitName    = o.OrgUnit?.Name ?? string.Empty,
            OrgUnitCode    = o.OrgUnit?.Code,
            IsDeleted      = o.IsDeleted,
            CreatedAt      = o.CreatedAt,
            UpdatedAt      = o.UpdatedAt,
            DeletedAt      = o.DeletedAt,
            CreatedBy      = o.CreatedBy,
            UpdatedBy      = o.UpdatedBy,
            DeletedBy      = o.DeletedBy,
            CreatedByName  = Resolve(o.CreatedBy, names),
            UpdatedByName  = Resolve(o.UpdatedBy, names),
            DeletedByName  = Resolve(o.DeletedBy, names),
            KpiCount       = kpiCount,
        };
}
