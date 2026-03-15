using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Permissions.DTOs;
using ActionTracker.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Application.Permissions.Services;

public class EffectivePermissionService : IEffectivePermissionService
{
    private readonly IAppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<EffectivePermissionService> _logger;

    public EffectivePermissionService(
        IAppDbContext db,
        UserManager<ApplicationUser> userManager,
        ILogger<EffectivePermissionService> logger)
    {
        _db          = db;
        _userManager = userManager;
        _logger      = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────

    public async Task<List<EffectivePermissionDto>> GetEffectivePermissionsAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            _logger.LogWarning("GetEffectivePermissions: user '{UserId}' not found.", userId);
            return new List<EffectivePermissionDto>();
        }

        var displayName = user.DisplayName ?? user.UserName ?? userId;

        // 1. Load role permissions for all roles the user belongs to.
        var roles = (await _userManager.GetRolesAsync(user)).ToList();

        var rolePermissions = roles.Count == 0
            ? new List<RolePermission>()
            : await _db.RolePermissions
                .Where(r => roles.Contains(r.RoleName) && !r.IsDeleted && r.IsActive)
                .ToListAsync();

        // 2. Load active, non-expired user overrides.
        var now = DateTime.UtcNow;
        var overrides = await _db.UserPermissionOverrides
            .Where(o => o.UserId == userId
                     && o.IsActive
                     && !o.IsDeleted
                     && (o.ExpiresAt == null || o.ExpiresAt > now))
            .ToListAsync();

        // 3. Build a mutable map keyed by (AreaId, ActionId).
        var map = new Dictionary<(Guid, Guid), EffectivePermissionDto>();

        foreach (var rp in rolePermissions)
        {
            var key = (rp.AreaId, rp.ActionId);
            if (!map.ContainsKey(key))
            {
                map[key] = new EffectivePermissionDto
                {
                    UserId          = userId,
                    UserDisplayName = displayName,
                    AreaId          = rp.AreaId,
                    AreaName        = rp.AreaName,
                    ActionId        = rp.ActionId,
                    ActionName      = rp.ActionName,
                    IsAllowed       = true,
                    Source          = "Role",
                    OrgUnitScope    = 0,
                    OrgUnitId       = null,
                    OrgUnitName     = null,
                };
            }
        }

        // 4. Apply user-level overrides — they always win over the role grant.
        foreach (var uo in overrides)
        {
            var key = (uo.AreaId, uo.ActionId);

            if (uo.IsGranted)
            {
                map[key] = new EffectivePermissionDto
                {
                    UserId          = userId,
                    UserDisplayName = displayName,
                    AreaId          = uo.AreaId,
                    AreaName        = uo.AreaName,
                    ActionId        = uo.ActionId,
                    ActionName      = uo.ActionName,
                    IsAllowed       = true,
                    Source          = "UserOverride-Granted",
                    OrgUnitScope    = uo.OrgUnitScope,
                    OrgUnitId       = uo.OrgUnitId,
                    OrgUnitName     = uo.OrgUnitName,
                };
            }
            else
            {
                // Explicit revocation — remove from the effective set so only
                // allowed permissions appear in the result.
                map.Remove(key);
            }
        }

        return map.Values.ToList();
    }

    public async Task<bool> HasPermissionAsync(string userId, string area, string action)
    {
        var permissions = await GetEffectivePermissionsAsync(userId);
        return permissions.Any(p =>
            string.Equals(p.AreaName,   area,   StringComparison.OrdinalIgnoreCase) &&
            string.Equals(p.ActionName, action, StringComparison.OrdinalIgnoreCase) &&
            p.IsAllowed);
    }

    public async Task<bool> HasPermissionForOrgUnitAsync(
        string userId, string area, string action, Guid orgUnitId)
    {
        var permissions = await GetEffectivePermissionsAsync(userId);

        var perm = permissions.FirstOrDefault(p =>
            string.Equals(p.AreaName,   area,   StringComparison.OrdinalIgnoreCase) &&
            string.Equals(p.ActionName, action, StringComparison.OrdinalIgnoreCase) &&
            p.IsAllowed);

        if (perm is null) return false;

        return perm.OrgUnitScope switch
        {
            0 => true,                            // All — unrestricted
            1 => perm.OrgUnitId == orgUnitId,     // SpecificOrgUnit
            2 => false,                           // OwnOnly — caller enforces ownership
            _ => false,
        };
    }
}
