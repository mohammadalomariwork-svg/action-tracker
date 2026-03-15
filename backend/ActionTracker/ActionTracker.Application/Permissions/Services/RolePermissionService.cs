using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Permissions.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Application.Permissions.Services;

public class RolePermissionService : IRolePermissionService
{
    private readonly IAppDbContext _db;
    private readonly ILogger<RolePermissionService> _logger;

    public RolePermissionService(IAppDbContext db, ILogger<RolePermissionService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<List<RolePermissionDto>> GetAllByRoleAsync(string roleName)
    {
        var entities = await _db.RolePermissions
            .Where(r => r.RoleName == roleName && !r.IsDeleted)
            .OrderBy(r => r.AreaName).ThenBy(r => r.ActionName)
            .ToListAsync();

        return entities.Select(MapToDto).ToList();
    }

    public async Task<RolePermissionDto?> GetByIdAsync(Guid id)
    {
        var entity = await _db.RolePermissions.FirstOrDefaultAsync(r => r.Id == id);
        return entity is null ? null : MapToDto(entity);
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    public async Task<RolePermissionDto> CreateAsync(CreateRolePermissionDto dto, string createdByUserId)
    {
        var area = await _db.PermissionAreas
            .FirstOrDefaultAsync(a => a.Id == dto.AreaId && a.IsActive)
            ?? throw new ArgumentException($"Area '{dto.AreaId}' does not exist or is not active.");

        var action = await _db.PermissionActions
            .FirstOrDefaultAsync(a => a.Id == dto.ActionId && a.IsActive)
            ?? throw new ArgumentException($"Action '{dto.ActionId}' does not exist or is not active.");

        var duplicate = await _db.RolePermissions
            .AnyAsync(r => r.RoleName == dto.RoleName
                        && r.AreaId   == dto.AreaId
                        && r.ActionId == dto.ActionId
                        && !r.IsDeleted);

        if (duplicate)
            throw new ArgumentException(
                $"A permission for role '{dto.RoleName}', area '{area.Name}', action '{action.Name}' already exists.");

        var entity = new RolePermission
        {
            Id          = Guid.NewGuid(),
            RoleName    = dto.RoleName,
            AreaId      = area.Id,
            AreaName    = area.Name,
            ActionId    = action.Id,
            ActionName  = action.Name,
            OrgUnitScope = dto.OrgUnitScope,
            OrgUnitId   = dto.OrgUnitId,
            OrgUnitName = dto.OrgUnitName,
            IsActive    = true,
            CreatedAt   = DateTime.UtcNow,
            CreatedBy   = createdByUserId,
        };

        _db.RolePermissions.Add(entity);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "RolePermission {Id} created for role '{Role}' area '{Area}' action '{Action}' by {User}",
            entity.Id, entity.RoleName, entity.AreaName, entity.ActionName, createdByUserId);

        return MapToDto(entity);
    }

    public async Task<RolePermissionDto> UpdateAsync(Guid id, UpdateRolePermissionDto dto, string updatedByUserId)
    {
        var entity = await _db.RolePermissions.FirstOrDefaultAsync(r => r.Id == id)
                     ?? throw new KeyNotFoundException($"RolePermission {id} not found.");

        entity.OrgUnitScope = dto.OrgUnitScope;
        entity.OrgUnitId    = dto.OrgUnitId;
        entity.OrgUnitName  = dto.OrgUnitName;
        entity.IsActive     = dto.IsActive;
        entity.UpdatedAt    = DateTime.UtcNow;
        entity.UpdatedBy    = updatedByUserId;

        await _db.SaveChangesAsync();

        _logger.LogInformation("RolePermission {Id} updated by {User}", id, updatedByUserId);

        return MapToDto(entity);
    }

    public async Task DeleteAsync(Guid id, string deletedByUserId)
    {
        var entity = await _db.RolePermissions.FirstOrDefaultAsync(r => r.Id == id)
                     ?? throw new KeyNotFoundException($"RolePermission {id} not found.");

        entity.IsDeleted = true;
        entity.IsActive  = false;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = deletedByUserId;

        await _db.SaveChangesAsync();

        _logger.LogInformation("RolePermission {Id} soft-deleted by {User}", id, deletedByUserId);
    }

    public async Task<PermissionMatrixDto> GetPermissionMatrixAsync(string roleName)
    {
        var permissions = await GetAllByRoleAsync(roleName);

        var mappingEntities = await _db.AreaPermissionMappings
            .OrderBy(m => m.AreaName).ThenBy(m => m.ActionName)
            .ToListAsync();

        // Enrich with DisplayName from catalog tables
        var areaIds   = mappingEntities.Select(m => m.AreaId).Distinct().ToList();
        var actionIds = mappingEntities.Select(m => m.ActionId).Distinct().ToList();

        var areas = await _db.PermissionAreas
            .Where(a => areaIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id);

        var actions = await _db.PermissionActions
            .Where(a => actionIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id);

        var availableMappings = mappingEntities.Select(m => new AreaActionMappingDto
        {
            Id                = m.Id,
            AreaId            = m.AreaId,
            AreaName          = m.AreaName,
            AreaDisplayName   = areas.TryGetValue(m.AreaId, out var area)     ? area.DisplayName   : m.AreaName,
            ActionId          = m.ActionId,
            ActionName        = m.ActionName,
            ActionDisplayName = actions.TryGetValue(m.ActionId, out var action) ? action.DisplayName : m.ActionName,
        }).ToList();

        return new PermissionMatrixDto
        {
            RoleName          = roleName,
            Permissions       = permissions,
            AvailableMappings = availableMappings,
        };
    }

    // ── Mapper ────────────────────────────────────────────────────────────────

    private static RolePermissionDto MapToDto(RolePermission rp) => new()
    {
        Id           = rp.Id,
        RoleName     = rp.RoleName,
        AreaId       = rp.AreaId,
        AreaName     = rp.AreaName,
        ActionId     = rp.ActionId,
        ActionName   = rp.ActionName,
        OrgUnitScope = rp.OrgUnitScope,
        OrgUnitId    = rp.OrgUnitId,
        OrgUnitName  = rp.OrgUnitName,
        IsActive     = rp.IsActive,
        CreatedAt    = rp.CreatedAt,
        CreatedBy    = rp.CreatedBy,
    };
}
