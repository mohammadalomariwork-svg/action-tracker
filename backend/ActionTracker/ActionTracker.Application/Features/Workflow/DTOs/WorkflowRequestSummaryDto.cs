namespace ActionTracker.Application.Features.Workflow.DTOs;

public class WorkflowRequestSummaryDto
{
    public int PendingDateChanges { get; set; }
    public int PendingStatusChanges { get; set; }
    public int TotalPending { get; set; }
}
