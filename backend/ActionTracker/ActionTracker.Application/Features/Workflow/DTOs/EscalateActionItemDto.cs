namespace ActionTracker.Application.Features.Workflow.DTOs;

public class EscalateActionItemDto
{
    public Guid ActionItemId { get; set; }
    public string Reason { get; set; } = string.Empty;
}
