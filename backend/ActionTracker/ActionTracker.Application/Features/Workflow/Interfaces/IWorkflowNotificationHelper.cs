using ActionTracker.Domain.Entities;

namespace ActionTracker.Application.Features.Workflow.Interfaces;

public interface IWorkflowNotificationHelper
{
    Task NotifyDateChangeRequestedAsync(ActionItem actionItem, ActionItemWorkflowRequest request);
    Task NotifyDateChangeReviewedAsync(ActionItem actionItem, ActionItemWorkflowRequest request);
    Task NotifyStatusChangeRequestedAsync(ActionItem actionItem, ActionItemWorkflowRequest request);
    Task NotifyStatusChangeReviewedAsync(ActionItem actionItem, ActionItemWorkflowRequest request);
    Task NotifyEscalationAsync(ActionItem actionItem, string escalatedByUserId, string reason);
    Task NotifyDirectionGivenAsync(ActionItem actionItem, string directorUserId, string directionText);
}
