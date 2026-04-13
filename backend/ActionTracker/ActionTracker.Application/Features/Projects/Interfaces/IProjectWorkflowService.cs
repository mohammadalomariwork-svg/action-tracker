using ActionTracker.Application.Features.Projects.DTOs;

namespace ActionTracker.Application.Features.Projects.Interfaces;

public interface IProjectWorkflowService
{
    Task SendProjectCreatedNotificationsAsync(Guid projectId, string createdByUserId);
    Task<ProjectApprovalRequestDto> SubmitForApprovalAsync(SubmitProjectApprovalRequestDto dto, string requestedByUserId);
    Task<ProjectApprovalRequestDto> ReviewApprovalRequestAsync(ReviewProjectApprovalRequestDto dto, string reviewerUserId);
    Task<List<ProjectApprovalRequestDto>> GetApprovalRequestsForProjectAsync(Guid projectId);
    Task<List<ProjectApprovalRequestDto>> GetPendingReviewsAsync(string userId);
    Task<List<ProjectApprovalRequestDto>> GetMyRequestsAsync(string userId);
    Task<ProjectApprovalSummaryDto> GetPendingSummaryAsync(string userId);
    Task<bool> CanReviewProjectAsync(Guid projectId, string userId);
    Task<SubmitValidationResultDto> ValidateSubmitForApprovalAsync(Guid projectId, string userId);
}
