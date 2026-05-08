namespace ActionTracker.Application.Features.StrategicPlans.Workflow;

/// <summary>
/// Centralised lockdown guard for the strategy graph (CR-001-E). Once a
/// plan is <c>Approved</c>, all descendant entities (objectives, KPIs,
/// KPI targets) become read-only unless an in-flight Revision request
/// exists. <c>Archived</c> plans are read-only with no escape hatch.
/// </summary>
public interface IStrategicPlanLockGuard
{
    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> with a "plan-locked"
    /// message when the plan is locked. Service handlers call this before
    /// any mutation.
    /// </summary>
    Task EnsureNotLockedAsync(Guid strategicPlanId, CancellationToken ct = default);

    /// <summary>Returns true when edits to the plan / its descendants are blocked.</summary>
    Task<bool> IsLockedAsync(Guid strategicPlanId, CancellationToken ct = default);

    /// <summary>
    /// Convenience wrapper for handlers that have an org unit but no plan id —
    /// resolves the org unit's current Approved or PendingApproval plan, then
    /// applies the same lockdown rules.
    /// </summary>
    Task EnsureNotLockedForOrgUnitAsync(Guid orgUnitId, CancellationToken ct = default);

    /// <summary>
    /// Non-throwing companion to <see cref="EnsureNotLockedForOrgUnitAsync"/>.
    /// Returns true when the org unit's active plan is currently locked
    /// (Approved without pending revision, or Archived). Used by handlers
    /// that need field-level lock semantics (e.g. KPI target bulk-upsert
    /// allowing Actual / Notes through while pinning the Target value).
    /// </summary>
    Task<bool> IsLockedForOrgUnitAsync(Guid orgUnitId, CancellationToken ct = default);
}
