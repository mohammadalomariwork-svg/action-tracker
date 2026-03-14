using ActionTracker.Application.Permissions.DTOs;

namespace ActionTracker.Application.Permissions.Services;

public interface IEffectivePermissionService
{
    /// <summary>
    /// Resolves the full set of effective permissions for a user by merging all
    /// role-based permissions with active, non-expired user-level overrides.
    /// User overrides always take precedence over role permissions.
    /// </summary>
    Task<List<EffectivePermissionDto>> GetEffectivePermissionsAsync(string userId);

    /// <summary>
    /// Returns true if the user has an effective (allowed) permission for the
    /// given area and action after applying all overrides.
    /// </summary>
    Task<bool> HasPermissionAsync(string userId, string area, string action);

    /// <summary>
    /// Returns true if the user has access to the given org unit for the
    /// specified area and action, taking OrgUnitScope rules into account:
    /// All → always true; SpecificOrgUnit → only if OrgUnitId matches;
    /// OwnOnly → deferred to the caller (returns true at this level).
    /// </summary>
    Task<bool> HasPermissionForOrgUnitAsync(string userId, string area, string action, Guid orgUnitId);
}
