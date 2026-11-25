using ActionTracker.Domain.Enums;

namespace ActionTracker.Application.Features.ActionItemUpdates.DTOs;

/// <summary>One action update (WO-2026-11 A-3).</summary>
public class ActionUpdateDto
{
    public Guid Id { get; set; }
    public Guid ActionItemId { get; set; }

    /// <summary>The sub-item the update is about; null for an update on the action item.</summary>
    public Guid? SubItemId { get; set; }

    public string Content { get; set; } = string.Empty;
    public string AuthorUserId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public bool IsHighImportance { get; set; }

    public ActionItemUpdateStatus Status { get; set; }
    public bool IsDraft => Status == ActionItemUpdateStatus.Draft;

    public DateTime? SubmittedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// True when the caller may edit, submit and delete it: only for the
    /// author's own draft. Always false for a submitted update.
    /// </summary>
    public bool CanEdit { get; set; }
}

/// <summary>The updates the caller can see at one level: every submitted update plus the caller's own drafts.</summary>
public class ActionUpdateListDto
{
    public Guid ActionItemId { get; set; }
    public Guid? SubItemId { get; set; }
    public int SubmittedCount { get; set; }

    /// <summary>The caller's own drafts (nobody else's are ever returned).</summary>
    public int DraftCount { get; set; }

    /// <summary>The caller's drafts first (newest first), then submitted updates (newest submitted first).</summary>
    public List<ActionUpdateDto> Items { get; set; } = new();
}

/// <summary>Update counts for an action item and each of its sub-items (for the "Updates (n)" toggles).</summary>
public class ActionUpdateCountsDto
{
    public Guid ActionItemId { get; set; }

    /// <summary>Updates on the action item itself (sub-item updates not included).</summary>
    public ActionUpdateCountDto Item { get; set; } = new();

    /// <summary>One entry per sub-item that has at least one update the caller can see.</summary>
    public List<SubItemActionUpdateCountDto> SubItems { get; set; } = new();
}

public class ActionUpdateCountDto
{
    public int SubmittedCount { get; set; }
    public int DraftCount { get; set; }
}

public class SubItemActionUpdateCountDto : ActionUpdateCountDto
{
    public Guid SubItemId { get; set; }
}

/// <summary>
/// New update. Saved as a draft, or submitted straight away when
/// <see cref="Submit"/> is true (one call, decision D3).
/// </summary>
public class CreateActionUpdateDto
{
    public string Content { get; set; } = string.Empty;
    public bool IsHighImportance { get; set; }
    public bool Submit { get; set; }
}

/// <summary>Changes to the caller's own draft.</summary>
public class EditActionUpdateDto
{
    public string Content { get; set; } = string.Empty;
    public bool IsHighImportance { get; set; }
}
