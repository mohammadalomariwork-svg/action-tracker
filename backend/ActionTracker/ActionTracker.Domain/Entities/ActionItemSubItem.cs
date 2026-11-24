namespace ActionTracker.Domain.Entities;

/// <summary>
/// A step inside an action item (WO-2026-11 A-2). One level only: a sub-item
/// never has sub-items of its own. Sub-items are not action items — they are
/// never counted by the dashboards, the stats or the reports, and their dates
/// are outside the planned-date freeze and the approval workflows.
/// </summary>
public class ActionItemSubItem
{
    public Guid Id { get; set; }

    /// <summary>The parent action item.</summary>
    public Guid ActionItemId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Optional start (calendar day at midnight, like the parent's planned dates).</summary>
    public DateTime? StartDate { get; set; }

    /// <summary>End (calendar day at midnight). Required, like the parent's planned end.</summary>
    public DateTime EndDate { get; set; }

    /// <summary>Position within the parent, 1-based and dense.</summary>
    public int SortOrder { get; set; }

    public bool IsDone { get; set; }

    /// <summary>When the sub-item was marked done (UTC); null while it is not done.</summary>
    public DateTime? DoneAt { get; set; }

    /// <summary>Who marked it done; null while it is not done.</summary>
    public string? DoneByUserId { get; set; }

    public string? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? UpdatedByUserId { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    /// <summary>
    /// True when the sub-item was soft-deleted because its parent was deleted
    /// (directly or with its project). Restoring the parent restores exactly
    /// these, so a sub-item deleted on its own stays deleted.
    /// </summary>
    public bool DeletedWithParent { get; set; }

    // Navigation properties
    public ActionItem ActionItem { get; set; } = null!;
    public ApplicationUser? DoneByUser { get; set; }
    public ICollection<ActionItemSubItemAssignee> Assignees { get; set; } = new List<ActionItemSubItemAssignee>();
}
