namespace ActionTracker.Application.Features.StrategicPlans.Workflow;

/// <summary>
/// Strategic plan approval workflow (CR-001-D + CR-001-E). Mirrors the
/// shape of <c>IProjectWorkflowService</c>. Both Initial submissions and
/// Revision cycles flow through the same review pipeline.
/// </summary>
public interface IStrategicPlanWorkflowService
{
    /// <summary>Submit a Draft plan for initial approval (Draft → PendingApproval).</summary>
    Task<StrategicPlanApprovalRequestDto> SubmitAsync(
        SubmitStrategicPlanApprovalRequestDto dto, string currentUserId, CancellationToken ct = default);

    /// <summary>Open a Revision request against an Approved plan (CR-001-E).</summary>
    Task<StrategicPlanApprovalRequestDto> RequestRevisionAsync(
        RequestStrategicPlanRevisionDto dto, string currentUserId, CancellationToken ct = default);

    /// <summary>Approve or reject a pending request (works for Initial or Revision).</summary>
    Task<StrategicPlanApprovalRequestDto> ReviewAsync(
        ReviewStrategicPlanApprovalRequestDto dto, string reviewerUserId, CancellationToken ct = default);

    Task<List<StrategicPlanApprovalRequestDto>> GetForPlanAsync(Guid planId, CancellationToken ct = default);
    Task<List<StrategicPlanApprovalRequestDto>> GetPendingReviewsAsync(string userId, CancellationToken ct = default);
    Task<List<StrategicPlanApprovalRequestDto>> GetMyRequestsAsync(string userId, CancellationToken ct = default);
    Task<StrategicPlanApprovalSummaryDto> GetPendingSummaryAsync(string userId, CancellationToken ct = default);
    Task<bool> CanReviewAsync(Guid planId, string userId, CancellationToken ct = default);
    Task<StrategicPlanLockStateDto> GetLockStateAsync(Guid planId, CancellationToken ct = default);
}
