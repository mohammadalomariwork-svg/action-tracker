using ActionTracker.Application.Common.Extensions;
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
                .Where(r => roles.Contains(r.RoleName) && r.IsActive)
                .ToListAsync();

        // 2. Load active, non-expired user overrides.
        var now = DateTime.UtcNow;
        var overrides = await _db.UserPermissionOverrides
            .Where(o => o.UserId == userId
                     && o.IsActive
                     && (o.ExpiresAt == null || o.ExpiresAt > now))
            .ToListAsync();

        // 3. Build a mutable map keyed by (Area, Action).
        var map = new Dictionary<(PermissionArea, PermissionAction), EffectivePermissionDto>();

        foreach (var rp in rolePermissions)
        {
            var key = (rp.Area, rp.Action);
            if (!map.ContainsKey(key))
            {
                map[key] = new EffectivePermissionDto
                {
                    UserId          = userId,
                    UserDisplayName = displayName,
                    Area            = rp.Area.GetDescription(),
                    Action          = rp.Action.GetDescription(),
                    IsAllowed       = true,
                    Source          = "Role",
                    OrgUnitScope    = rp.OrgUnitScope.GetDescription(),
                    OrgUnitId       = rp.OrgUnitId,
                    OrgUnitName     = rp.OrgUnitName,
                };
            }
        }

        // 4. Apply user-level overrides — they always win over the role grant.
        foreach (var uo in overrides)
        {
            var key = (uo.Area, uo.Action);
            map[key] = new EffectivePermissionDto
            {
                UserId          = userId,
                UserDisplayName = displayName,
                Area            = uo.Area.GetDescription(),
                Action          = uo.Action.GetDescription(),
                IsAllowed       = uo.IsGranted,
                Source          = uo.IsGranted ? "UserOverride-Granted" : "UserOverride-Revoked",
                OrgUnitScope    = uo.OrgUnitScope.GetDescription(),
                OrgUnitId       = uo.OrgUnitId,
                OrgUnitName     = uo.OrgUnitName,
            };
        }

        return map.Values.ToList();
    }

    public async Task<bool> HasPermissionAsync(string userId, string area, string action)
    {
        // Normalize by stripping spaces so that policy keys like "PermissionsManagement"
        // match Description values like "Permissions Management".
        static string N(string s) => s.Replace(" ", "").ToLowerInvariant();
        var normArea   = N(area);
        var normAction = N(action);

        var permissions = await GetEffectivePermissionsAsync(userId);
        return permissions.Any(p =>
            N(p.Area)   == normArea &&
            N(p.Action) == normAction &&
            p.IsAllowed);
    }

    public async Task<bool> HasPermissionForOrgUnitAsync(
        string userId, string area, string action, Guid orgUnitId)
    {
        static string N(string s) => s.Replace(" ", "").ToLowerInvariant();
        var normArea   = N(area);
        var normAction = N(action);

        var permissions = await GetEffectivePermissionsAsync(userId);

        var perm = permissions.FirstOrDefault(p =>
            N(p.Area)   == normArea &&
            N(p.Action) == normAction &&
            p.IsAllowed);

        if (perm is null) return false;

        return perm.OrgUnitScope switch
        {
            // "All" → unrestricted access
            var s when s == OrgUnitScope.All.GetDescription()
                => true,

            // "Specific Org Unit" → only the designated org unit
            var s when s == OrgUnitScope.SpecificOrgUnit.GetDescription()
                => perm.OrgUnitId == orgUnitId,

            // "Own Only" → ownership check is context-dependent; allow at this layer
            // and let the calling service enforce record-level ownership.
            _ => true,
        };
    }
}
