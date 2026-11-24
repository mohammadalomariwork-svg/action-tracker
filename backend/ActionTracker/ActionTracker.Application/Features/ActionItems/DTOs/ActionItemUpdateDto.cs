using System.ComponentModel.DataAnnotations;
using ActionTracker.Domain.Enums;

namespace ActionTracker.Application.Features.ActionItems.DTOs;

public class ActionItemUpdateDto
{
    [Required]
    public Guid Id { get; set; }

    [MaxLength(255)]
    public string? Title { get; set; }

    [MaxLength(5000)]
    public string? Description { get; set; }

    public Guid? WorkspaceId { get; set; }

    /// <summary>Optional link to a project. Set to Guid.Empty to clear.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>Optional link to a milestone. Set to Guid.Empty to clear.</summary>
    public Guid? MilestoneId { get; set; }

    /// <summary>When supplied, updates the standalone flag.</summary>
    public bool? IsStandalone { get; set; }

    /// <summary>When supplied, replaces the full assignee list.</summary>
    public List<string>? AssigneeIds { get; set; }

    public ActionPriority? Priority { get; set; }

    public ActionStatus? Status { get; set; }

    /// <summary>
    /// Planned start. Frozen once the parent project leaves Draft; on a
    /// standalone item a change becomes a date-change request. A changed day
    /// must be a working day of the workspace's calendar.
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>Planned end. Same rules as <see cref="StartDate"/>.</summary>
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// Actual start (WO-2026-11 A-1); null = leave it. Any day up to today.
    /// Not frozen, not sent for approval, not checked against the calendar.
    /// </summary>
    public DateTime? ActualStartDate { get; set; }

    /// <summary>True clears the actual start. Cannot be combined with <see cref="ActualStartDate"/>.</summary>
    public bool? ClearActualStartDate { get; set; }

    /// <summary>
    /// Actual end (WO-2026-11 A-1); null = leave it. Only when the item is Done
    /// or becomes Done in this save; any day up to today and not before the
    /// actual start. It cannot be cleared while the item is Done.
    /// </summary>
    public DateTime? ActualEndDate { get; set; }

    [Range(0, 100)]
    public int? Progress { get; set; }

    public bool? IsEscalated { get; set; }

    /// <summary>Required when IsEscalated is true.</summary>
    [MaxLength(2000)]
    public string? EscalationExplanation { get; set; }

    /// <summary>
    /// True when the payload changes nothing but the actual dates. A Done item
    /// accepts only such a payload (WO-2026-11 A-1).
    /// </summary>
    public bool ChangesOnlyActualDates() =>
        Title is null && Description is null && WorkspaceId is null && ProjectId is null
        && MilestoneId is null && IsStandalone is null && AssigneeIds is null && Priority is null
        && Status is null && StartDate is null && DueDate is null && Progress is null
        && IsEscalated is null && EscalationExplanation is null;

    /// <summary>True when the payload sets or clears an actual date.</summary>
    public bool HasActualDates() =>
        ActualStartDate is not null || ClearActualStartDate == true || ActualEndDate is not null;
}
