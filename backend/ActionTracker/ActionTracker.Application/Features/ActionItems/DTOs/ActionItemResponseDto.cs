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

    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }

    public Guid? MilestoneId { get; set; }
    public string? MilestoneName { get; set; }

    public bool IsStandalone { get; set; }

    public ActionPriority Priority { get; set; }
    public ActionStatus Status { get; set; }

    /// <summary>Planned start.</summary>
    public DateTime? StartDate { get; set; }

    /// <summary>Planned end.</summary>
    public DateTime DueDate { get; set; }

    /// <summary>Actual start (WO-2026-11 A-1).</summary>
    public DateTime? ActualStartDate { get; set; }

    /// <summary>Actual end (WO-2026-11 A-1); set only while the item is Done.</summary>
    public DateTime? ActualEndDate { get; set; }

    /// <summary>
    /// Actual end minus planned end, in calendar days: positive = late,
    /// 0 = on time, negative = early. Null when there is no actual end.
    /// </summary>
    public int? ActualEndVarianceDays =>
        ActualEndDate.HasValue ? (int)(ActualEndDate.Value.Date - DueDate.Date).TotalDays : null;

    public int Progress { get; set; }
    public bool IsEscalated { get; set; }

    // ── Sub-items (WO-2026-11 A-2) — shown as "x of y done"; never counted as action items ──

    /// <summary>Live sub-items of this item.</summary>
    public int SubItemCount { get; set; }

    /// <summary>Live sub-items marked done.</summary>
    public int SubItemDoneCount { get; set; }

    /// <summary>
    /// How many of the sub-items the caller is assigned to. Filled only by
    /// <c>GET /api/action-items/assigned-via-sub-items</c>; null elsewhere.
    /// </summary>
    public int? MySubItemCount { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // Multi-assignee info
    public List<AssigneeDto> Assignees { get; set; } = new();

    // Escalation history
    public List<EscalationDto> Escalations { get; set; } = new();

    // Comments
    public List<CommentDto> Comments { get; set; } = new();

    // Numeric enum codes for frontend comparisons
    public int StatusCode   => (int)Status;
    public int PriorityCode => (int)Priority;

    // Human-readable enum labels sourced from [Description] attributes
    public string StatusLabel   => Status.GetDescription();
    public string PriorityLabel => Priority.GetDescription();

    // Deadline helpers
    public int DaysUntilDue => (int)(DueDate.Date - DateTime.UtcNow.Date).TotalDays;
    public bool IsOverdue   => DaysUntilDue < 0 && Status != ActionStatus.Done;
}

public class EscalationDto
{
    public Guid Id { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public string EscalatedByUserId { get; set; } = string.Empty;
    public string EscalatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class AssigneeDto
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class CommentDto
{
    public Guid Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public string AuthorUserId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public bool IsHighImportance { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
