using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Permissions.Domain;
using ActionTracker.Application.Permissions.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ActionTracker.Application.Permissions.Services;

public class PermissionCatalogService : IPermissionCatalogService
{
    private readonly IAppDbContext _db;

    public PermissionCatalogService(IAppDbContext db)
    {
        _db = db;
    }

    // ── Areas ─────────────────────────────────────────────────────────────────

    public async Task<List<AppPermissionAreaDto>> GetAllAreasAsync()
    {
        var areas = await _db.PermissionAreas
            .Where(a => a.IsActive)
            .OrderBy(a => a.DisplayOrder)
            .ToListAsync();

        return areas.Select(MapAreaToDto).ToList();
    }

    public async Task<AppPermissionAreaDto?> GetAreaByIdAsync(Guid id)
    {
        var area = await _db.PermissionAreas
            .FirstOrDefaultAsync(a => a.Id == id);

        return area is null ? null : MapAreaToDto(area);
    }

    public async Task<AppPermissionAreaDto> CreateAreaAsync(CreateAreaDto dto, string createdBy)
    {
        var nameExists = await _db.PermissionAreas
            .AnyAsync(a => a.Name == dto.Name);

        if (nameExists)
            throw new InvalidOperationException($"An area with name '{dto.Name}' already exists.");

        var area = new AppPermissionArea
        {
            Id           = Guid.NewGuid(),
            Name         = dto.Name,
            DisplayName  = dto.DisplayName,
            Description  = dto.Description,
            DisplayOrder = dto.DisplayOrder,
            IsActive     = true,
            IsDeleted    = false,
            CreatedAt    = DateTime.UtcNow,
            CreatedBy    = createdBy,
        };

        _db.PermissionAreas.Add(area);
        await _db.SaveChangesAsync();

        return MapAreaToDto(area);
    }

    public async Task<AppPermissionAreaDto?> UpdateAreaAsync(Guid id, CreateAreaDto dto, string updatedBy)
    {
        var area = await _db.PermissionAreas
            .FirstOrDefaultAsync(a => a.Id == id);

        if (area is null)
            return null;

        var nameConflict = await _db.PermissionAreas
            .AnyAsync(a => a.Name == dto.Name && a.Id != id);

        if (nameConflict)
            throw new InvalidOperationException($"An area with name '{dto.Name}' already exists.");

        area.Name         = dto.Name;
        area.DisplayName  = dto.DisplayName;
        area.Description  = dto.Description;
        area.DisplayOrder = dto.DisplayOrder;

        await _db.SaveChangesAsync();

        return MapAreaToDto(area);
    }

    public async Task<bool> DeleteAreaAsync(Guid id, string deletedBy)
    {
        var area = await _db.PermissionAreas
            .FirstOrDefaultAsync(a => a.Id == id);

        if (area is null)
            return false;

        area.IsDeleted = true;
        area.IsActive  = false;

        await _db.SaveChangesAsync();

        return true;
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    public async Task<List<AppPermissionActionDto>> GetAllActionsAsync()
    {
        var actions = await _db.PermissionActions
            .Where(a => a.IsActive)
            .OrderBy(a => a.DisplayOrder)
            .ToListAsync();

        return actions.Select(MapActionToDto).ToList();
    }

    public async Task<AppPermissionActionDto?> GetActionByIdAsync(Guid id)
    {
        var action = await _db.PermissionActions
            .FirstOrDefaultAsync(a => a.Id == id);

        return action is null ? null : MapActionToDto(action);
    }

    public async Task<AppPermissionActionDto> CreateActionAsync(CreateActionDto dto, string createdBy)
    {
        var nameExists = await _db.PermissionActions
            .AnyAsync(a => a.Name == dto.Name);

        if (nameExists)
            throw new InvalidOperationException($"An action with name '{dto.Name}' already exists.");

        var action = new AppPermissionAction
        {
            Id           = Guid.NewGuid(),
            Name         = dto.Name,
            DisplayName  = dto.DisplayName,
            Description  = dto.Description,
            DisplayOrder = dto.DisplayOrder,
            IsActive     = true,
            IsDeleted    = false,
            CreatedAt    = DateTime.UtcNow,
            CreatedBy    = createdBy,
        };

        _db.PermissionActions.Add(action);
        await _db.SaveChangesAsync();

        return MapActionToDto(action);
    }

    public async Task<AppPermissionActionDto?> UpdateActionAsync(Guid id, CreateActionDto dto, string updatedBy)
    {
        var action = await _db.PermissionActions
            .FirstOrDefaultAsync(a => a.Id == id);

        if (action is null)
            return null;

        var nameConflict = await _db.PermissionActions
            .AnyAsync(a => a.Name == dto.Name && a.Id != id);

        if (nameConflict)
            throw new InvalidOperationException($"An action with name '{dto.Name}' already exists.");

        action.Name         = dto.Name;
        action.DisplayName  = dto.DisplayName;
        action.Description  = dto.Description;
        action.DisplayOrder = dto.DisplayOrder;

        await _db.SaveChangesAsync();

        return MapActionToDto(action);
    }

    public async Task<bool> DeleteActionAsync(Guid id, string deletedBy)
    {
        var action = await _db.PermissionActions
            .FirstOrDefaultAsync(a => a.Id == id);

        if (action is null)
            return false;

        action.IsDeleted = true;
        action.IsActive  = false;

        await _db.SaveChangesAsync();

        return true;
    }

    // ── Mappings ──────────────────────────────────────────────────────────────

    public async Task<List<AreaActionMappingDto>> GetAllMappingsAsync()
    {
        var mappings = await _db.AreaPermissionMappings
            .OrderBy(m => m.AreaName).ThenBy(m => m.ActionName)
            .ToListAsync();

        return await EnrichMappingsAsync(mappings);
    }

    public async Task<List<AreaActionMappingDto>> GetMappingsByAreaAsync(Guid areaId)
    {
        var mappings = await _db.AreaPermissionMappings
            .Where(m => m.AreaId == areaId)
            .OrderBy(m => m.ActionName)
            .ToListAsync();

        return await EnrichMappingsAsync(mappings);
    }

    public async Task<AreaActionMappingDto> CreateMappingAsync(CreateAreaActionMappingDto dto, string createdBy)
    {
        var area = await _db.PermissionAreas
            .FirstOrDefaultAsync(a => a.Id == dto.AreaId && a.IsActive);

        if (area is null)
            throw new InvalidOperationException($"Area '{dto.AreaId}' does not exist or is not active.");

        var action = await _db.PermissionActions
            .FirstOrDefaultAsync(a => a.Id == dto.ActionId && a.IsActive);

        if (action is null)
            throw new InvalidOperationException($"Action '{dto.ActionId}' does not exist or is not active.");

        var duplicate = await _db.AreaPermissionMappings
            .AnyAsync(m => m.AreaId == dto.AreaId && m.ActionId == dto.ActionId);

        if (duplicate)
            throw new InvalidOperationException(
                $"A mapping between area '{area.Name}' and action '{action.Name}' already exists.");

        var mapping = new AreaPermissionMapping
        {
            Id         = Guid.NewGuid(),
            AreaId     = area.Id,
            AreaName   = area.Name,
            ActionId   = action.Id,
            ActionName = action.Name,
            IsActive   = true,
            IsDeleted  = false,
            CreatedAt  = DateTime.UtcNow,
            CreatedBy  = createdBy,
        };

        _db.AreaPermissionMappings.Add(mapping);
        await _db.SaveChangesAsync();

        return new AreaActionMappingDto
        {
            Id              = mapping.Id,
            AreaId          = area.Id,
            AreaName        = area.Name,
            AreaDisplayName = area.DisplayName,
            ActionId        = action.Id,
            ActionName      = action.Name,
            ActionDisplayName = action.DisplayName,
        };
    }

    public async Task<bool> DeleteMappingAsync(Guid id, string deletedBy)
    {
        var mapping = await _db.AreaPermissionMappings
            .FirstOrDefaultAsync(m => m.Id == id);

        if (mapping is null)
            return false;

        mapping.IsDeleted = true;
        mapping.IsActive  = false;

        await _db.SaveChangesAsync();

        return true;
    }

    // ── Mapping helpers ───────────────────────────────────────────────────────

    private static AppPermissionAreaDto MapAreaToDto(AppPermissionArea a) => new()
    {
        Id           = a.Id,
        Name         = a.Name,
        DisplayName  = a.DisplayName,
        Description  = a.Description,
        DisplayOrder = a.DisplayOrder,
        IsActive     = a.IsActive,
    };

    private static AppPermissionActionDto MapActionToDto(AppPermissionAction a) => new()
    {
        Id           = a.Id,
        Name         = a.Name,
        DisplayName  = a.DisplayName,
        Description  = a.Description,
        DisplayOrder = a.DisplayOrder,
        IsActive     = a.IsActive,
    };

    /// <summary>
    /// Joins mapping rows against the in-memory area/action records to populate
    /// the DisplayName fields that are not stored on the mapping itself.
    /// </summary>
    private async Task<List<AreaActionMappingDto>> EnrichMappingsAsync(
        List<AreaPermissionMapping> mappings)
    {
        if (mappings.Count == 0)
            return new List<AreaActionMappingDto>();

        var areaIds   = mappings.Select(m => m.AreaId).Distinct().ToList();
        var actionIds = mappings.Select(m => m.ActionId).Distinct().ToList();

        var areas = await _db.PermissionAreas
            .Where(a => areaIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id);

        var actions = await _db.PermissionActions
            .Where(a => actionIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id);

        return mappings.Select(m => new AreaActionMappingDto
        {
            Id                = m.Id,
            AreaId            = m.AreaId,
            AreaName          = m.AreaName,
            AreaDisplayName   = areas.TryGetValue(m.AreaId,   out var area)   ? area.DisplayName   : m.AreaName,
            ActionId          = m.ActionId,
            ActionName        = m.ActionName,
            ActionDisplayName = actions.TryGetValue(m.ActionId, out var action) ? action.DisplayName : m.ActionName,
        }).ToList();
    }
}
