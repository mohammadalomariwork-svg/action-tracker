using ActionTracker.Application.Common.Extensions;
using ActionTracker.Domain.Enums;

namespace ActionTracker.Application.Features.ActionItems.DTOs;

public class ActionItemResponseDto
{
    public int Id { get; set; }
    public string ActionId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AssigneeId { get; set; } = string.Empty;
    public ActionCategory Category { get; set; }
    public ActionPriority Priority { get; set; }
    public ActionStatus Status { get; set; }
    public DateTime DueDate { get; set; }
    public int Progress { get; set; }
    public bool IsEscalated { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Assignee info
    public string AssigneeName { get; set; } = string.Empty;
    public string AssigneeEmail { get; set; } = string.Empty;

    // Human-readable enum labels sourced from [Description] attributes
    public string StatusLabel   => Status.GetDescription();
    public string PriorityLabel => Priority.GetDescription();
    public string CategoryLabel => Category.GetDescription();

    // Deadline helpers
    public int DaysUntilDue => (int)(DueDate.Date - DateTime.UtcNow.Date).TotalDays;
    public bool IsOverdue   => DaysUntilDue < 0 && Status != ActionStatus.Done;
}
