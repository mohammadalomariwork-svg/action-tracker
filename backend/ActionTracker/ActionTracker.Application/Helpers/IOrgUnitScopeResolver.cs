namespace ActionTracker.Application.Helpers;

public interface IOrgUnitScopeResolver
{
    /// <summary>
    /// Returns all OrgUnit IDs that are visible to the user: their own org unit
    /// plus every descendant in the hierarchy.
    /// Returns an empty list when the user has no assigned OrgUnit.
    /// </summary>
    Task<List<Guid>> GetUserOrgUnitIdsAsync(string userId);

    /// <summary>
    /// Returns true if <paramref name="orgUnitId"/> is the user's own org unit
    /// or a descendant of it.
    /// </summary>
    Task<bool> IsOrgUnitVisibleToUserAsync(string userId, Guid orgUnitId);

    /// <summary>
    /// Returns all descendant OrgUnit IDs starting from <paramref name="rootOrgUnitId"/>
    /// (children, grandchildren, etc.), NOT including the root itself.
    /// </summary>
    Task<List<Guid>> GetDescendantOrgUnitIdsAsync(Guid rootOrgUnitId);
}
