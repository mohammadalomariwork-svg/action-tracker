using ActionTracker.Application.Features.KpiActuals.DTOs;

namespace ActionTracker.Application.Features.KpiActuals.Interfaces;

/// <summary>
/// CR-001-F — workflow for KPI actuals. Mirrors
/// <c>IStrategicPlanWorkflowService</c>. The actual itself acts as the
/// workflow record (Status field), so there is no separate "request" entity.
/// </summary>
public interface IKpiActualWorkflowService
{
    Task<KpiActualDto> SubmitAsync(SubmitKpiActualRequestDto dto, string currentUserId, CancellationToken ct = default);
    Task<KpiActualDto> ReviewAsync(ReviewKpiActualRequestDto dto, string reviewerUserId, CancellationToken ct = default);

    Task<List<KpiActualDto>> GetPendingReviewsAsync(string userId, CancellationToken ct = default);
    Task<List<KpiActualDto>> GetMyRequestsAsync(string userId, CancellationToken ct = default);
    Task<KpiActualSummaryDto> GetPendingSummaryAsync(string userId, CancellationToken ct = default);
    Task<bool> CanReviewAsync(Guid actualId, string userId, CancellationToken ct = default);
}
