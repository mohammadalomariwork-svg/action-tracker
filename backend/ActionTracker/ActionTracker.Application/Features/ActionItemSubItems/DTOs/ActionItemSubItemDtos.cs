namespace ActionTracker.Application.Features.ActionItemSubItems.DTOs;

/// <summary>One sub-item as returned by the API (WO-2026-11 A-2).</summary>
public class ActionItemSubItemDto
{
    public Guid Id { get; set; }
    public Guid ActionItemId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int SortOrder { get; set; }

    public bool IsDone { get; set; }
    public DateTime? DoneAt { get; set; }
    public string? DoneByUserId { get; set; }
    public string? DoneByName { get; set; }

    public List<SubItemAssigneeDto> Assignees { get; set; } = new();

    /// <summary>
    /// True when the sub-item's dates no longer lie within the parent's planned
    /// dates (the parent's dates changed after the sub-item was saved). The
    /// sub-item is not changed; its next date edit has to fix it.
    /// </summary>
    public bool IsOutsideParentDates { get; set; }

    /// <summary>Files attached to this sub-item (WO-2026-11 A-4) — the paperclip count.</summary>
    public int DocumentCount { get; set; }

    public string? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class SubItemAssigneeDto
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

/// <summary>GET /api/action-items/{id}/sub-items — the sub-items plus what the form needs about the parent.</summary>
public class ActionItemSubItemListDto
{
    public Guid ActionItemId { get; set; }

    /// <summary>The parent's planned start (null = none); sub-item dates may not be before it.</summary>
    public DateTime? ParentStartDate { get; set; }

    /// <summary>The parent's planned end; sub-item dates may not be after it.</summary>
    public DateTime ParentDueDate { get; set; }

    /// <summary>False when the parent is Done or Cancelled: no sub-item may be added or changed.</summary>
    public bool ParentAcceptsChanges { get; set; }

    public int TotalCount { get; set; }
    public int DoneCount { get; set; }

    /// <summary>Ordered by <see cref="ActionItemSubItemDto.SortOrder"/>.</summary>
    public List<ActionItemSubItemDto> Items { get; set; } = new();
}

/// <summary>POST body. Dates are calendar days ('yyyy-MM-dd').</summary>
public class CreateActionItemSubItemDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<string> AssigneeIds { get; set; } = new();
}

/// <summary>
/// PUT body: replaces every field, including the assignee list. A null
/// <see cref="StartDate"/> removes the start date.
/// </summary>
public class UpdateActionItemSubItemDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<string> AssigneeIds { get; set; } = new();
}

/// <summary>PATCH /{subId}/done body.</summary>
public class SetActionItemSubItemDoneDto
{
    public bool IsDone { get; set; }
}

/// <summary>PUT /reorder body: every live sub-item of the parent, in the new order.</summary>
public class ReorderActionItemSubItemsDto
{
    public List<Guid> SubItemIds { get; set; } = new();
}
