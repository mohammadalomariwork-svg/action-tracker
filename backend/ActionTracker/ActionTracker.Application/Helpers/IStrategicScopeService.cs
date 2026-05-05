namespace ActionTracker.Application.Helpers;

/// <summary>
/// Resolves the org-unit scope that constrains a user's access to strategic
/// objectives, KPIs, and KPI targets. Used by service-layer code to filter
/// reads and to authorise writes.
///
/// Rule (matches the StrategyEditor role design):
///   • Admin              → unrestricted (returns null).
///   • StrategyEditor     → restricted to the user's level-2 ancestor org unit
///                          and all of its descendants.
///   • Any other role     → unrestricted (returns null) — the existing
///                          permission policies determine access.
/// </summary>
public interface IStrategicScopeService
{
    /// <summary>
    /// Returns the org-unit IDs visible to the user, or <c>null</c> when the
    /// user has unrestricted access. An empty list means "see nothing"
    /// (a StrategyEditor with no OrgUnit assigned).
    /// </summary>
    Task<List<Guid>?> GetVisibleOrgUnitIdsAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Throws <see cref="UnauthorizedAccessException"/> if the user is not
    /// permitted to write strategic-objective or KPI items in the given
    /// org unit. Returns silently when the user has unrestricted access.
    /// </summary>
    Task EnsureCanWriteAsync(string userId, Guid orgUnitId, CancellationToken ct = default);
}
