using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Constants;
using ActionTracker.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace ActionTracker.Infrastructure.Services;

/// <summary>
/// Implements <see cref="IStrategicScopeService"/> by combining the user's
/// roles with <see cref="IOrgUnitScopeResolver"/>. Admins are unrestricted;
/// users holding the StrategyEditor role are limited to their level-2
/// ancestor org unit + descendants.
/// </summary>
public class StrategicScopeService : IStrategicScopeService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IOrgUnitScopeResolver        _scopeResolver;

    public StrategicScopeService(
        UserManager<ApplicationUser> userManager,
        IOrgUnitScopeResolver        scopeResolver)
    {
        _userManager   = userManager;
        _scopeResolver = scopeResolver;
    }

    /// <summary>
    /// Roles that are explicitly read-unrestricted across all org units for
    /// strategic plans, objectives, and KPIs (CR-001-I). Write access remains
    /// gated by the regular {Area}.{Action} permission policies.
    /// </summary>
    private static readonly string[] ReadUnrestrictedRoles =
    {
        AppRoles.Admin,
        AppRoles.PmoHead,
        AppRoles.PmoAnalyst,
        AppRoles.ProjectManager,
        AppRoles.WorkspaceAdmin,
    };

    public async Task<List<Guid>?> GetVisibleOrgUnitIdsAsync(
        string userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return new List<Guid>();

        // Roles that are intentionally treated as read-unrestricted (CR-001-I).
        // Write paths are still gated by *.Create / *.Edit / *.Delete policies,
        // so PMO/PM users with View-only permissions cannot mutate data.
        foreach (var role in ReadUnrestrictedRoles)
        {
            if (await _userManager.IsInRoleAsync(user, role))
                return null;
        }

        // No StrategyEditor role: not affected by this scope (other policies apply).
        if (!await _userManager.IsInRoleAsync(user, AppRoles.StrategyEditor))
            return null;

        // StrategyEditor without an OrgUnit cannot see anything.
        if (user.OrgUnitId is null) return new List<Guid>();

        return await _scopeResolver.GetUserOrgUnitIdsAsync(userId);
    }

    public async Task EnsureCanWriteAsync(
        string userId, Guid orgUnitId, CancellationToken ct = default)
    {
        var visible = await GetVisibleOrgUnitIdsAsync(userId, ct);
        if (visible is null) return; // unrestricted

        if (!visible.Contains(orgUnitId))
            throw new UnauthorizedAccessException(
                "You are not authorised to manage strategic items in this org unit.");
    }
}
