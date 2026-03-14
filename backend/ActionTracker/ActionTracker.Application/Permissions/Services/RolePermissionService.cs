using ActionTracker.Application.Common.Extensions;
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
            .Where(r => r.RoleName == roleName && r.IsActive)
            .OrderBy(r => r.Area).ThenBy(r => r.Action)
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
        var area   = (PermissionArea)  dto.Area;
        var action = (PermissionAction)dto.Action;

        var duplicate = await _db.RolePermissions
            .AnyAsync(r => r.RoleName == dto.RoleName
                        && r.Area   == area
                        && r.Action == action);

        if (duplicate)
            throw new ArgumentException(
                $"A permission for role '{dto.RoleName}', area '{area}', action '{action}' already exists.");

        var entity = new RolePermission
        {
            Id           = Guid.NewGuid(),
            RoleName     = dto.RoleName,
            Area         = area,
            Action       = action,
            OrgUnitScope = (OrgUnitScope)dto.OrgUnitScope,
            OrgUnitId    = dto.OrgUnitId,
            OrgUnitName  = dto.OrgUnitName,
            IsActive     = true,
            CreatedAt    = DateTime.UtcNow,
            CreatedBy    = createdByUserId,
        };

        _db.RolePermissions.Add(entity);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "RolePermission {Id} created for role '{Role}' area '{Area}' action '{Action}' by {User}",
            entity.Id, entity.RoleName, entity.Area, entity.Action, createdByUserId);

        return MapToDto(entity);
    }

    public async Task<RolePermissionDto> UpdateAsync(Guid id, UpdateRolePermissionDto dto, string updatedByUserId)
    {
        var entity = await _db.RolePermissions.FirstOrDefaultAsync(r => r.Id == id)
                     ?? throw new KeyNotFoundException($"RolePermission {id} not found.");

        entity.OrgUnitScope = (OrgUnitScope)dto.OrgUnitScope;
        entity.OrgUnitId    = dto.OrgUnitId;
        entity.OrgUnitName  = dto.OrgUnitName;
        entity.IsActive     = dto.IsActive;
        entity.UpdatedAt    = DateTime.UtcNow;
        entity.UpdatedBy    = updatedByUserId;

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "RolePermission {Id} updated by {User}", id, updatedByUserId);

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

        _logger.LogInformation(
            "RolePermission {Id} soft-deleted by {User}", id, deletedByUserId);
    }

    public async Task<PermissionMatrixDto> GetPermissionMatrixAsync(string roleName)
    {
        var permissions = await GetAllByRoleAsync(roleName);

        return new PermissionMatrixDto
        {
            RoleName    = roleName,
            Permissions = permissions,
        };
    }

    // ── Mapper ────────────────────────────────────────────────────────────────

    private static RolePermissionDto MapToDto(RolePermission rp) => new()
    {
        Id           = rp.Id,
        RoleName     = rp.RoleName,
        Area         = rp.Area.GetDescription(),
        Action       = rp.Action.GetDescription(),
        OrgUnitScope = rp.OrgUnitScope.GetDescription(),
        OrgUnitId    = rp.OrgUnitId,
        OrgUnitName  = rp.OrgUnitName,
        IsActive     = rp.IsActive,
        CreatedAt    = rp.CreatedAt,
        CreatedBy    = rp.CreatedBy,
    };
}
