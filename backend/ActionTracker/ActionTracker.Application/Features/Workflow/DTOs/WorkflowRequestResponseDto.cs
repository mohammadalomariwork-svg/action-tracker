namespace ActionTracker.Application.Features.Workflow.DTOs;

public class WorkflowRequestResponseDto
{
    public Guid Id { get; set; }
    public Guid ActionItemId { get; set; }
    public string ActionItemCode { get; set; } = string.Empty;
    public string ActionItemTitle { get; set; } = string.Empty;
    public string RequestType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string RequestedByUserId { get; set; } = string.Empty;
    public string RequestedByDisplayName { get; set; } = string.Empty;
    public DateTime? RequestedNewStartDate { get; set; }
    public DateTime? RequestedNewDueDate { get; set; }
    public string? RequestedNewStatus { get; set; }
    public DateTime? CurrentStartDate { get; set; }
    public DateTime? CurrentDueDate { get; set; }
    public string? CurrentStatus { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? ReviewedByUserId { get; set; }
    public string? ReviewedByDisplayName { get; set; }
    public string? ReviewComment { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
