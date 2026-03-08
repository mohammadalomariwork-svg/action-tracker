using ActionTracker.Domain.Enums;

namespace ActionTracker.Domain.Entities;

public class ActionItem
{
    public Guid Id { get; set; }

    /// <summary>Human-readable identifier in format ACT-001.</summary>
    public string ActionId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>The workspace this action item belongs to.</summary>
    public Guid WorkspaceId { get; set; }

    public ActionPriority Priority { get; set; }
    public ActionStatus Status { get; set; } = ActionStatus.ToDo;

    /// <summary>Optional start date.</summary>
    public DateTime? StartDate { get; set; }

    /// <summary>Required. Must be a valid date.</summary>
    public DateTime DueDate { get; set; }

    private int _progress;

    /// <summary>Completion percentage. Must be between 0 and 100.</summary>
    public int Progress
    {
        get => _progress;
        set => _progress = value < 0 ? 0 : value > 100 ? 100 : value;
    }

    public bool IsEscalated { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // Navigation properties
    public Workspace Workspace { get; set; } = null!;
    public ICollection<ActionItemAssignee> Assignees { get; set; } = new List<ActionItemAssignee>();
}
