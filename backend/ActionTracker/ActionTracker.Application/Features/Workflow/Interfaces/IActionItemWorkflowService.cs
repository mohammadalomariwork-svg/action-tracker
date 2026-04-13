using ActionTracker.Application.Features.Workflow.DTOs;
using ActionTracker.Application.Helpers;

namespace ActionTracker.Application.Features.Workflow.Interfaces;

public interface IActionItemWorkflowService
{
    Task<WorkflowRequestResponseDto> CreateDateChangeRequestAsync(CreateDateChangeRequestDto dto, string requestedByUserId);
    Task<WorkflowRequestResponseDto> CreateStatusChangeRequestAsync(CreateStatusChangeRequestDto dto, string requestedByUserId);
    Task<WorkflowRequestResponseDto> ReviewRequestAsync(Guid requestId, ReviewWorkflowRequestDto dto, string reviewerUserId);
    Task<PagedResult<WorkflowRequestResponseDto>> GetPendingRequestsForReviewerAsync(string reviewerUserId, int page, int pageSize);
    Task<PagedResult<WorkflowRequestResponseDto>> GetMyRequestsAsync(string userId, int page, int pageSize);
    Task<List<WorkflowRequestResponseDto>> GetRequestsForActionItemAsync(Guid actionItemId);
    Task<WorkflowRequestSummaryDto> GetPendingSummaryAsync(string userId);
    Task HandleEscalationAsync(Guid actionItemId, string escalatedByUserId, string reason);
    Task GiveDirectionAsync(WorkflowDirectionDto dto, string directorUserId);
    Task<bool> CanUserReviewAsync(Guid actionItemId, string userId);
    Task<bool> HasPendingRequestsAsync(Guid actionItemId);
}
