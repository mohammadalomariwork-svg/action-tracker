namespace ActionTracker.Application.Features.StrategicPlans.Workflow;

/// <summary>
/// Manages the per-plan, ordered list of approvers used by the multi-level
/// sequential approval workflow. The list is consulted at submit time only;
/// once a plan is in PendingApproval the chain is "frozen" — Submit rejects
/// edits made after the workflow starts, so the in-flight requests' levels
/// remain consistent with what the user originally configured.
/// </summary>
public interface IStrategicPlanApproverService
{
    Task<IReadOnlyList<StrategicPlanApproverDto>> GetForPlanAsync(
        Guid planId,
        string currentUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Replace the entire chain for a plan. Allowed only while the plan is in
    /// <c>Draft</c>; rejects on PendingApproval / Approved / Archived. The
    /// service normalises the supplied <c>Order</c>s into a dense 1..N range
    /// after sorting, and rejects duplicate user ids.
    /// </summary>
    Task<IReadOnlyList<StrategicPlanApproverDto>> UpsertForPlanAsync(
        Guid planId,
        UpsertStrategicPlanApproversRequestDto request,
        string currentUserId,
        CancellationToken ct = default);
}
