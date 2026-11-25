using ActionTracker.Domain.Enums;

namespace ActionTracker.Domain.Entities;

/// <summary>
/// An action update (WO-2026-11 A-3) — the log entry that replaced comments on
/// action items. Stored in the existing <c>ActionItemComments</c> table (no
/// rename). An update belongs to an action item, and optionally to one of its
/// sub-items (<see cref="SubItemId"/>).
/// <para>
/// Rules (Q11): a <see cref="ActionItemUpdateStatus.Draft"/> is visible to its
/// author only (enforced by the query filter), can be edited and deleted by its
/// author; <c>Submit</c> is one-way; a
/// <see cref="ActionItemUpdateStatus.Submitted"/> update cannot be edited or
/// deleted by anyone, Admin included. Updates are allowed whatever the parent's
/// status. They are not action items and are never counted by the dashboards.
/// </para>
/// </summary>
public class ActionItemUpdate
{
    public Guid Id { get; set; }
    public Guid ActionItemId { get; set; }

    /// <summary>The sub-item the update is about; null for an update on the action item itself.</summary>
    public Guid? SubItemId { get; set; }

    public string Content { get; set; } = string.Empty;
    public string AuthorUserId { get; set; } = string.Empty;
    public bool IsHighImportance { get; set; }

    public ActionItemUpdateStatus Status { get; set; } = ActionItemUpdateStatus.Draft;

    /// <summary>When the update was submitted (UTC); null while it is a draft.</summary>
    public DateTime? SubmittedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Soft delete — only drafts are ever deleted.</summary>
    public bool IsDeleted { get; set; }

    // Navigation
    public ActionItem ActionItem { get; set; } = null!;
    public ActionItemSubItem? SubItem { get; set; }
    public ApplicationUser Author { get; set; } = null!;
}
