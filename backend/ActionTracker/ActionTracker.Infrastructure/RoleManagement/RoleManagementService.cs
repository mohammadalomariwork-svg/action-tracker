using ActionTracker.Application.Permissions;
using ActionTracker.Application.Permissions.DTOs;
using ActionTracker.Application.RoleManagement.DTOs;
using ActionTracker.Application.RoleManagement.Services;
using ActionTracker.Domain.Entities;
using ActionTracker.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Infrastructure.RoleManagement;

public class RoleManagementService : IRoleManagementService
{
    private readonly AppDbContext _db;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<RoleManagementService> _logger;

    public RoleManagementService(
        AppDbContext db,
        RoleManager<IdentityRole> roleManager,
        UserManager<ApplicationUser> userManager,
        ILogger<RoleManagementService> logger)
    {
        _db          = db;
        _roleManager = roleManager;
        _userManager = userManager;
        _logger      = logger;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<List<AppRoleDto>> GetAllRolesAsync()
    {
        var roles = await _roleManager.Roles
            .OrderBy(r => r.Name)
            .ToListAsync();

        var result = new List<AppRoleDto>(roles.Count);

        foreach (var role in roles)
        {
            var userCount = (await _userManager.GetUsersInRoleAsync(role.Name!)).Count;
            var permCount = await _db.RolePermissions
                .CountAsync(p => p.RoleName == role.Name && p.IsActive && !p.IsDeleted);

            result.Add(new AppRoleDto
            {
                Id              = role.Id,
                Name            = role.Name!,
                UserCount       = userCount,
                PermissionCount = permCount,
            });
        }

        return result;
    }

    public async Task<AppRoleDto?> GetRoleByNameAsync(string roleName)
    {
        var role = await _roleManager.FindByNameAsync(roleName);
        if (role is null) return null;

        var userCount = (await _userManager.GetUsersInRoleAsync(role.Name!)).Count;
        var permCount = await _db.RolePermissions
            .CountAsync(p => p.RoleName == role.Name && p.IsActive && !p.IsDeleted);

        return new AppRoleDto
        {
            Id              = role.Id,
            Name            = role.Name!,
            UserCount       = userCount,
            PermissionCount = permCount,
        };
    }

    // ── Role lifecycle ────────────────────────────────────────────────────────

    public async Task<AppRoleDto> CreateRoleAsync(string roleName, string createdByUserId)
    {
        if (await _roleManager.RoleExistsAsync(roleName))
            throw new ArgumentException($"Role '{roleName}' already exists.");

        var role   = new IdentityRole(roleName);
        var result = await _roleManager.CreateAsync(role);

        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Failed to create role '{roleName}': " +
                string.Join(", ", result.Errors.Select(e => e.Description)));

        _logger.LogInformation("Role '{Role}' created by {User}", roleName, createdByUserId);

        return new AppRoleDto
        {
            Id              = role.Id,
            Name            = role.Name!,
            UserCount       = 0,
            PermissionCount = 0,
        };
    }

    public async Task<bool> DeleteRoleAsync(string roleName, string deletedByUserId)
    {
        var role = await _roleManager.FindByNameAsync(roleName);
        if (role is null) return false;

        var usersInRole = await _userManager.GetUsersInRoleAsync(roleName);
        if (usersInRole.Count > 0)
            throw new InvalidOperationException(
                $"Cannot delete role '{roleName}': {usersInRole.Count} user(s) are still assigned.");

        var result = await _roleManager.DeleteAsync(role);
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Failed to delete role '{roleName}': " +
                string.Join(", ", result.Errors.Select(e => e.Description)));

        _logger.LogInformation("Role '{Role}' deleted by {User}", roleName, deletedByUserId);
        return true;
    }

    // ── User–role assignments ─────────────────────────────────────────────────

    public async Task<List<RoleUserDto>> GetUsersInRoleAsync(string roleName)
    {
        var users = await _userManager.GetUsersInRoleAsync(roleName);
        if (users.Count == 0) return new List<RoleUserDto>();

        // Batch-load org unit names for the users that have one
        var orgUnitIds = users
            .Where(u => u.OrgUnitId.HasValue)
            .Select(u => u.OrgUnitId!.Value)
            .Distinct()
            .ToList();

        var orgUnitNames = orgUnitIds.Count > 0
            ? await _db.OrgUnits
                .Where(o => orgUnitIds.Contains(o.Id))
                .ToDictionaryAsync(o => o.Id, o => o.Name)
            : new Dictionary<Guid, string>();

        return users.Select(u => new RoleUserDto
        {
            UserId          = u.Id,
            UserDisplayName = u.DisplayName ?? u.UserName ?? u.Id,
            Email           = u.Email ?? string.Empty,
            OrgUnitName     = u.OrgUnitId.HasValue && orgUnitNames.TryGetValue(u.OrgUnitId.Value, out var n)
                                ? n : null,
        }).ToList();
    }

    public async Task<bool> AssignUsersToRoleAsync(AssignUsersToRoleDto dto, string actingUserId)
    {
        if (!await _roleManager.RoleExistsAsync(dto.RoleName))
            throw new ArgumentException($"Role '{dto.RoleName}' does not exist.");

        foreach (var userId in dto.UserIds)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
            {
                _logger.LogWarning("AssignUsersToRole: user '{UserId}' not found — skipping.", userId);
                continue;
            }

            if (await _userManager.IsInRoleAsync(user, dto.RoleName))
                continue; // already assigned — silent skip

            var result = await _userManager.AddToRoleAsync(user, dto.RoleName);
            if (!result.Succeeded)
                _logger.LogWarning(
                    "AssignUsersToRole: failed to add user '{UserId}' to '{Role}': {Errors}",
                    userId, dto.RoleName,
                    string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        _logger.LogInformation(
            "AssignUsersToRole: role '{Role}' updated by {Actor}", dto.RoleName, actingUserId);

        return true;
    }

    public async Task<bool> RemoveUsersFromRoleAsync(RemoveUsersFromRoleDto dto, string actingUserId)
    {
        if (!await _roleManager.RoleExistsAsync(dto.RoleName))
            throw new ArgumentException($"Role '{dto.RoleName}' does not exist.");

        foreach (var userId in dto.UserIds)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
            {
                _logger.LogWarning("RemoveUsersFromRole: user '{UserId}' not found — skipping.", userId);
                continue;
            }

            if (!await _userManager.IsInRoleAsync(user, dto.RoleName))
                continue; // not in role — silent skip

            var result = await _userManager.RemoveFromRoleAsync(user, dto.RoleName);
            if (!result.Succeeded)
                _logger.LogWarning(
                    "RemoveUsersFromRole: failed to remove user '{UserId}' from '{Role}': {Errors}",
                    userId, dto.RoleName,
                    string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        _logger.LogInformation(
            "RemoveUsersFromRole: role '{Role}' updated by {Actor}", dto.RoleName, actingUserId);

        return true;
    }

    // ── Permission assignment (full replace) ──────────────────────────────────

    public async Task<bool> AssignPermissionsToRoleAsync(
        AssignRolePermissionsDto dto, string actingUserId)
    {
        if (!await _roleManager.RoleExistsAsync(dto.RoleName))
            throw new ArgumentException($"Role '{dto.RoleName}' does not exist.");

        // Pre-load only the area/action records referenced by the incoming entries
        var areaIds   = dto.Permissions.Select(p => p.AreaId).Distinct().ToList();
        var actionIds = dto.Permissions.Select(p => p.ActionId).Distinct().ToList();

        var areas = await _db.PermissionAreas
            .Where(a => areaIds.Contains(a.Id) && a.IsActive)
            .ToDictionaryAsync(a => a.Id);

        var actions = await _db.PermissionActions
            .Where(a => actionIds.Contains(a.Id) && a.IsActive)
            .ToDictionaryAsync(a => a.Id);

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // 1. Soft-delete all existing active permissions for the role
            var existing = await _db.RolePermissions
                .Where(p => p.RoleName == dto.RoleName && !p.IsDeleted)
                .ToListAsync();

            var now = DateTime.UtcNow;
            foreach (var p in existing)
            {
                p.IsDeleted = true;
                p.IsActive  = false;
                p.UpdatedAt = now;
                p.UpdatedBy = actingUserId;
            }

            // 2. Bulk-insert the new permission set
            var newPermissions = new List<RolePermission>(dto.Permissions.Count);
            foreach (var entry in dto.Permissions)
            {
                if (!areas.TryGetValue(entry.AreaId, out var area))
                {
                    _logger.LogWarning(
                        "AssignPermissions: area '{AreaId}' not found or inactive — skipping.",
                        entry.AreaId);
                    continue;
                }

                if (!actions.TryGetValue(entry.ActionId, out var action))
                {
                    _logger.LogWarning(
                        "AssignPermissions: action '{ActionId}' not found or inactive — skipping.",
                        entry.ActionId);
                    continue;
                }

                newPermissions.Add(new RolePermission
                {
                    Id           = Guid.NewGuid(),
                    RoleName     = dto.RoleName,
                    AreaId       = area.Id,
                    AreaName     = area.Name,
                    ActionId     = action.Id,
                    ActionName   = action.Name,
                    OrgUnitScope = entry.OrgUnitScope,
                    OrgUnitId    = entry.OrgUnitId,
                    OrgUnitName  = entry.OrgUnitName,
                    IsActive     = true,
                    IsDeleted    = false,
                    CreatedAt    = now,
                    CreatedBy    = actingUserId,
                });
            }

            _db.RolePermissions.AddRange(newPermissions);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            _logger.LogInformation(
                "AssignPermissions: role '{Role}' — {Deleted} old permission(s) soft-deleted, " +
                "{Added} new permission(s) added by {Actor}",
                dto.RoleName, existing.Count, newPermissions.Count, actingUserId);

            return true;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ── Permission summary ────────────────────────────────────────────────────

    public async Task<PermissionMatrixDto> GetRolePermissionSummaryAsync(string roleName)
    {
        var permissions = await _db.RolePermissions
            .Where(p => p.RoleName == roleName && p.IsActive && !p.IsDeleted)
            .OrderBy(p => p.AreaName).ThenBy(p => p.ActionName)
            .ToListAsync();

        var mappingEntities = await _db.AreaPermissionMappings
            .OrderBy(m => m.AreaName).ThenBy(m => m.ActionName)
            .ToListAsync();

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
            AreaDisplayName   = areas.TryGetValue(m.AreaId,   out var area)   ? area.DisplayName   : m.AreaName,
            ActionId          = m.ActionId,
            ActionName        = m.ActionName,
            ActionDisplayName = actions.TryGetValue(m.ActionId, out var action) ? action.DisplayName : m.ActionName,
        }).ToList();

        return new PermissionMatrixDto
        {
            RoleName          = roleName,
            Permissions       = permissions.Select(MapPermissionToDto).ToList(),
            AvailableMappings = availableMappings,
        };
    }

    // ── Mapper ────────────────────────────────────────────────────────────────

    private static RolePermissionDto MapPermissionToDto(RolePermission rp) => new()
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
