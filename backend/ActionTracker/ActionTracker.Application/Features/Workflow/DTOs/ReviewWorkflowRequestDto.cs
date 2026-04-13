namespace ActionTracker.Application.Features.Workflow.DTOs;

public class ReviewWorkflowRequestDto
{
    public bool IsApproved { get; set; }
    public string? ReviewComment { get; set; }
}
