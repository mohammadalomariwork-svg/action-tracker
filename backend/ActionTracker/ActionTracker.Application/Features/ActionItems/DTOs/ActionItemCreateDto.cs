using System.ComponentModel.DataAnnotations;
using ActionTracker.Domain.Enums;

namespace ActionTracker.Application.Features.ActionItems.DTOs;

public class ActionItemCreateDto
{
    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(5000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public Guid WorkspaceId { get; set; }

    /// <summary>Optional link to a project.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>Optional link to a milestone.</summary>
    public Guid? MilestoneId { get; set; }

    /// <summary>
    /// Ignored (WO-2026-11 A-1b). The server sets the standalone flag from the
    /// project link: true exactly when no <see cref="ProjectId"/> is given.
    /// Kept so older clients that still send it keep working.
    /// </summary>
    public bool IsStandalone { get; set; } = true;

    /// <summary>At least one assignee is required.</summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one assignee is required.")]
    public List<string> AssigneeIds { get; set; } = new();

    [Required]
    public ActionPriority Priority { get; set; }

    /// <summary>
    /// Initial status. Creating an item as In Progress sets its actual start to
    /// today; creating it as Done sets its actual end to today (WO-2026-11 A-1).
    /// </summary>
    public ActionStatus Status { get; set; } = ActionStatus.ToDo;

    /// <summary>Planned start (optional). Must be a working day of the workspace's calendar.</summary>
    public DateTime? StartDate { get; set; }

    /// <summary>Planned end. Must be a working day of the workspace's calendar.</summary>
    [Required]
    public DateTime DueDate { get; set; }

    [Range(0, 100)]
    public int Progress { get; set; } = 0;

    public bool IsEscalated { get; set; } = false;

    /// <summary>Required when IsEscalated is true.</summary>
    [MaxLength(2000)]
    public string? EscalationExplanation { get; set; }
}
