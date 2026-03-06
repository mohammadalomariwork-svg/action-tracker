using ActionTracker.Application.Features.OrgChart.DTOs;
using ActionTracker.Application.Features.OrgChart.Interfaces;
using ActionTracker.Domain.Entities;
using ActionTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Infrastructure.Services;

public class OrgUnitService : IOrgUnitService
{
    private readonly AppDbContext          _context;
    private readonly ILogger<OrgUnitService> _logger;

    public OrgUnitService(AppDbContext context, ILogger<OrgUnitService> logger)
    {
        _context = context;
        _logger  = logger;
    }

    // -------------------------------------------------------------------------
    // GetTreeAsync
    // -------------------------------------------------------------------------

    public async Task<OrgUnitTreeDto?> GetTreeAsync(
        bool              includeDeleted = false,
        CancellationToken ct             = default)
    {
        try
        {
            var query = includeDeleted
                ? _context.OrgUnits.IgnoreQueryFilters()
                : _context.OrgUnits;

            var all = await query
                .OrderBy(o => o.Level)
                .ThenBy(o => o.Name)
                .ToListAsync(ct);

            var root = all.FirstOrDefault(o => o.ParentId == null);
            if (root is null) return null;

            return MapToTreeDto(root, all);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building org unit tree (includeDeleted={IncludeDeleted})", includeDeleted);
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // GetAllAsync
    // -------------------------------------------------------------------------

    public async Task<OrgUnitListResponseDto> GetAllAsync(
        int               page,
        int               pageSize,
        bool              includeDeleted = false,
        CancellationToken ct             = default)
    {
        try
        {
            var query = includeDeleted
                ? _context.OrgUnits.IgnoreQueryFilters()
                : _context.OrgUnits;

            var totalCount = await query.CountAsync(ct);

            var units = await query
                .OrderBy(o => o.Level)
                .ThenBy(o => o.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            // Load all units to resolve parent names and children counts in memory.
            var allUnits = await _context.OrgUnits.IgnoreQueryFilters()
                .Select(o => new { o.Id, o.Name, o.ParentId })
                .ToListAsync(ct);

            var nameLookup     = allUnits.ToDictionary(o => o.Id, o => o.Name);
            var childrenCounts = allUnits
                .Where(o => o.ParentId != null)
                .GroupBy(o => o.ParentId!.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            var dtos = units.Select(u =>
            {
                var parentName    = u.ParentId.HasValue && nameLookup.TryGetValue(u.ParentId.Value, out var pn) ? pn : null;
                var childrenCount = childrenCounts.TryGetValue(u.Id, out var cc) ? cc : 0;
                return MapToDto(u, parentName, childrenCount);
            }).ToList();

            return new OrgUnitListResponseDto
            {
                OrgUnits   = dtos,
                TotalCount = totalCount,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving org units (page={Page}, pageSize={PageSize})", page, pageSize);
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // GetByIdAsync
    // -------------------------------------------------------------------------

    public async Task<OrgUnitDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var unit = await _context.OrgUnits
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Id == id, ct);

            if (unit is null) return null;

            string? parentName = null;
            if (unit.ParentId.HasValue)
            {
                parentName = await _context.OrgUnits
                    .IgnoreQueryFilters()
                    .Where(o => o.Id == unit.ParentId.Value)
                    .Select(o => o.Name)
                    .FirstOrDefaultAsync(ct);
            }

            var childrenCount = await _context.OrgUnits
                .IgnoreQueryFilters()
                .CountAsync(o => o.ParentId == id, ct);

            return MapToDto(unit, parentName, childrenCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving org unit {Id}", id);
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // CreateAsync
    // -------------------------------------------------------------------------

    public async Task<OrgUnitDto> CreateAsync(
        CreateOrgUnitRequestDto request,
        CancellationToken       ct = default)
    {
        try
        {
            string? parentName = null;
            int     level      = 1;

            if (request.ParentId is null)
            {
                var rootExists = await _context.OrgUnits
                    .IgnoreQueryFilters()
                    .AnyAsync(o => o.ParentId == null, ct);

                if (rootExists)
                    throw new InvalidOperationException(
                        "A root org unit already exists. Only one root is allowed.");
            }
            else
            {
                var parent = await _context.OrgUnits
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(o => o.Id == request.ParentId.Value, ct)
                    ?? throw new KeyNotFoundException(
                        $"Parent org unit '{request.ParentId}' not found.");

                level      = parent.Level + 1;
                parentName = parent.Name;

                if (level > 10)
                    throw new InvalidOperationException(
                        "Maximum org chart depth of 10 exceeded.");
            }

            var newId = Guid.NewGuid();
            var unit = new OrgUnit
            {
                Id          = newId,
                Name        = request.Name,
                Description = request.Description,
                Code        = $"OC-{newId.ToString("N")[..8].ToUpper()}",
                Level       = level,
                ParentId    = request.ParentId,
                IsDeleted   = false,
                CreatedAt   = DateTime.UtcNow,
            };

            _context.OrgUnits.Add(unit);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Created OrgUnit {Id} '{Name}' at level {Level}", unit.Id, unit.Name, unit.Level);

            return MapToDto(unit, parentName, childrenCount: 0);
        }
        catch (KeyNotFoundException) { throw; }
        catch (InvalidOperationException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating org unit '{Name}'", request.Name);
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // UpdateAsync
    // -------------------------------------------------------------------------

    public async Task<OrgUnitDto> UpdateAsync(
        Guid                    id,
        UpdateOrgUnitRequestDto request,
        CancellationToken       ct = default)
    {
        try
        {
            var unit = await _context.OrgUnits
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Id == id, ct)
                ?? throw new KeyNotFoundException($"Org unit '{id}' not found.");

            string? parentName = null;

            if (request.ParentId != unit.ParentId)
            {
                if (request.ParentId is null)
                {
                    // Moving to root — check no other root exists (excluding self).
                    var otherRoot = await _context.OrgUnits
                        .IgnoreQueryFilters()
                        .AnyAsync(o => o.ParentId == null && o.Id != id, ct);

                    if (otherRoot)
                        throw new InvalidOperationException(
                            "A root org unit already exists. Only one root is allowed.");

                    unit.Level = 1;
                }
                else
                {
                    // Validate no circular reference: walk up the new parent's chain.
                    await ValidateNoCircularReferenceAsync(id, request.ParentId.Value, ct);

                    var parent = await _context.OrgUnits
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(o => o.Id == request.ParentId.Value, ct)
                        ?? throw new KeyNotFoundException(
                            $"Parent org unit '{request.ParentId}' not found.");

                    unit.Level = parent.Level + 1;

                    if (unit.Level > 10)
                        throw new InvalidOperationException(
                            "Maximum org chart depth of 10 exceeded.");

                    parentName = parent.Name;
                }
            }
            else if (unit.ParentId.HasValue)
            {
                parentName = await _context.OrgUnits
                    .IgnoreQueryFilters()
                    .Where(o => o.Id == unit.ParentId.Value)
                    .Select(o => o.Name)
                    .FirstOrDefaultAsync(ct);
            }

            unit.Name        = request.Name;
            unit.Description = request.Description;
            unit.ParentId    = request.ParentId;
            unit.UpdatedAt   = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Updated OrgUnit {Id}", id);

            var childrenCount = await _context.OrgUnits
                .IgnoreQueryFilters()
                .CountAsync(o => o.ParentId == id, ct);

            return MapToDto(unit, parentName, childrenCount);
        }
        catch (KeyNotFoundException) { throw; }
        catch (InvalidOperationException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating org unit {Id}", id);
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
            var unit = await _context.OrgUnits
                .FirstOrDefaultAsync(o => o.Id == id, ct)
                ?? throw new KeyNotFoundException($"Org unit '{id}' not found.");

            // Load all units to find descendants in memory.
            var allUnits = await _context.OrgUnits
                .IgnoreQueryFilters()
                .ToListAsync(ct);

            var toDelete = new List<OrgUnit> { unit };
            CollectDescendants(id, allUnits, toDelete);

            var now = DateTime.UtcNow;
            foreach (var u in toDelete)
            {
                u.IsDeleted  = true;
                u.DeletedAt  = now;
                u.UpdatedAt  = now;
            }

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Soft-deleted OrgUnit {Id} and {Count} descendant(s)", id, toDelete.Count - 1);
        }
        catch (KeyNotFoundException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error soft-deleting org unit {Id}", id);
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
            var unit = await _context.OrgUnits
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Id == id, ct)
                ?? throw new KeyNotFoundException($"Org unit '{id}' not found.");

            unit.IsDeleted = false;
            unit.DeletedAt = null;
            unit.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Restored OrgUnit {Id}", id);
        }
        catch (KeyNotFoundException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring org unit {Id}", id);
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // GetChildrenAsync
    // -------------------------------------------------------------------------

    public async Task<List<OrgUnitDto>> GetChildrenAsync(Guid parentId, CancellationToken ct = default)
    {
        try
        {
            var parent = await _context.OrgUnits
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Id == parentId, ct)
                ?? throw new KeyNotFoundException($"Org unit '{parentId}' not found.");

            var children = await _context.OrgUnits
                .Where(o => o.ParentId == parentId)
                .OrderBy(o => o.Name)
                .ToListAsync(ct);

            // Children count per child (their direct children).
            var childIds = children.Select(c => c.Id).ToHashSet();
            var grandchildCounts = await _context.OrgUnits
                .IgnoreQueryFilters()
                .Where(o => o.ParentId != null && childIds.Contains(o.ParentId.Value))
                .GroupBy(o => o.ParentId!.Value)
                .Select(g => new { ParentId = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var countLookup = grandchildCounts.ToDictionary(g => g.ParentId, g => g.Count);

            return children
                .Select(c => MapToDto(
                    c,
                    parentName:    parent.Name,
                    childrenCount: countLookup.TryGetValue(c.Id, out var cc) ? cc : 0))
                .ToList();
        }
        catch (KeyNotFoundException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving children of org unit {ParentId}", parentId);
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task ValidateNoCircularReferenceAsync(
        Guid              unitId,
        Guid              newParentId,
        CancellationToken ct)
    {
        // Walk up the new parent's ancestor chain; if we encounter unitId it's circular.
        var all = await _context.OrgUnits
            .IgnoreQueryFilters()
            .Select(o => new { o.Id, o.ParentId })
            .ToListAsync(ct);

        var lookup = all.ToDictionary(o => o.Id, o => o.ParentId);
        var current = (Guid?)newParentId;

        while (current.HasValue)
        {
            if (current.Value == unitId)
                throw new InvalidOperationException(
                    "Cannot set parent: this would create a circular reference in the org chart.");

            current = lookup.TryGetValue(current.Value, out var pid) ? pid : null;
        }
    }

    private static void CollectDescendants(
        Guid            parentId,
        List<OrgUnit>   all,
        List<OrgUnit>   result)
    {
        foreach (var child in all.Where(o => o.ParentId == parentId))
        {
            result.Add(child);
            CollectDescendants(child.Id, all, result);
        }
    }

    private static OrgUnitDto MapToDto(OrgUnit u, string? parentName = null, int childrenCount = 0) =>
        new()
        {
            Id            = u.Id,
            Name          = u.Name,
            Description   = u.Description,
            Code          = u.Code,
            Level         = u.Level,
            ParentId      = u.ParentId,
            ParentName    = parentName,
            IsDeleted     = u.IsDeleted,
            CreatedAt     = u.CreatedAt,
            UpdatedAt     = u.UpdatedAt,
            ChildrenCount = childrenCount,
        };

    private static OrgUnitTreeDto MapToTreeDto(OrgUnit node, List<OrgUnit> all) =>
        new()
        {
            Id          = node.Id,
            Name        = node.Name,
            Code        = node.Code,
            Description = node.Description,
            Level       = node.Level,
            ParentId    = node.ParentId,
            IsDeleted   = node.IsDeleted,
            Children    = all
                .Where(o => o.ParentId == node.Id)
                .OrderBy(o => o.Name)
                .Select(child => MapToTreeDto(child, all))
                .ToList(),
        };
}
