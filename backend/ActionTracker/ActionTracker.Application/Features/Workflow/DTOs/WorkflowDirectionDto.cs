namespace ActionTracker.Application.Features.Workflow.DTOs;

public class WorkflowDirectionDto
{
    public Guid ActionItemId { get; set; }
    public string DirectionText { get; set; } = string.Empty;
}
