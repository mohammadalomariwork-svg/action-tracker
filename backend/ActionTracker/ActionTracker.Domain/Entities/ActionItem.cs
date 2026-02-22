using ActionTracker.Domain.Common;
using ActionTracker.Domain.Enums;

namespace ActionTracker.Domain.Entities;

public class ActionItem : BaseEntity
{
    /// <summary>Human-readable identifier in format ACT-001.</summary>
    public string ActionId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AssigneeId { get; set; } = string.Empty;
    public ActionCategory Category { get; set; }
    public ActionPriority Priority { get; set; }
    public ActionStatus Status { get; set; }

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
    public string Notes { get; set; } = string.Empty;

    public ApplicationUser Assignee { get; set; } = null!;
}
