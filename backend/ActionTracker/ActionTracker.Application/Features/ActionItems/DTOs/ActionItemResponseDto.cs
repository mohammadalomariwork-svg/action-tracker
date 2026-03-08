using ActionTracker.Application.Common.Extensions;
using ActionTracker.Domain.Enums;

namespace ActionTracker.Application.Features.ActionItems.DTOs;

public class ActionItemResponseDto
{
    public Guid Id { get; set; }
    public string ActionId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public Guid WorkspaceId { get; set; }
    public string WorkspaceTitle { get; set; } = string.Empty;

    public ActionPriority Priority { get; set; }
    public ActionStatus Status { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime DueDate { get; set; }
    public int Progress { get; set; }
    public bool IsEscalated { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Multi-assignee info
    public List<AssigneeDto> Assignees { get; set; } = new();

    // Human-readable enum labels sourced from [Description] attributes
    public string StatusLabel   => Status.GetDescription();
    public string PriorityLabel => Priority.GetDescription();

    // Deadline helpers
    public int DaysUntilDue => (int)(DueDate.Date - DateTime.UtcNow.Date).TotalDays;
    public bool IsOverdue   => DaysUntilDue < 0 && Status != ActionStatus.Done;
}

public class AssigneeDto
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
