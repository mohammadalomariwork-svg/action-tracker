namespace ActionTracker.Application.Features.Workflow.DTOs;

public class CreateDateChangeRequestDto
{
    public Guid ActionItemId { get; set; }
    public DateTime? NewStartDate { get; set; }
    public DateTime? NewDueDate { get; set; }
    public string Reason { get; set; } = string.Empty;
}
