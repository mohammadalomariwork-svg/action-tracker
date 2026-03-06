using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Features.Projects.Models;

/// <summary>
/// Represents a milestone (work package) within a project.
/// Milestones group related action items and track incremental progress
/// toward the overall project deliverables — analogous to a WBS work package
/// in PMI terminology.
/// </summary>
public class Milestone
{
    /// <summary>Primary key — auto-incremented integer identity.</summary>
    public int Id { get; set; }

    /// <summary>
    /// Foreign key of the project this milestone belongs to.
    /// </summary>
    [Required]
    public int ProjectId { get; set; }

    /// <summary>
    /// Human-readable title of the milestone / work package.
    /// </summary>
    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the milestone scope and acceptance criteria.
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// 1-based ordering of this milestone within its project.
    /// Used to display milestones in a logical, sequential order.
    /// </summary>
    [Required]
    public int SequenceOrder { get; set; }

    /// <summary>
    /// Current execution status of the milestone.
    /// </summary>
    public MilestoneStatus Status { get; set; } = MilestoneStatus.NotStarted;

    /// <summary>Planned start date for this milestone (UTC).</summary>
    [Required]
    public DateTime PlannedStartDate { get; set; }

    /// <summary>Planned completion date for this milestone (UTC).</summary>
    [Required]
    public DateTime PlannedEndDate { get; set; }

    /// <summary>Actual start date once work begins (UTC), or <c>null</c> if not started.</summary>
    public DateTime? ActualStartDate { get; set; }

    /// <summary>Actual completion date (UTC), or <c>null</c> if not yet completed.</summary>
    public DateTime? ActualEndDate { get; set; }

    /// <summary>
    /// Completion percentage in the range 0–100.
    /// Updated by the PM as action items are closed.
    /// </summary>
    public int CompletionPercentage { get; set; } = 0;

    /// <summary>
    /// Whether the milestone is active.
    /// <c>false</c> soft-deletes it from normal queries.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>UTC timestamp when this milestone was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the most recent update, or <c>null</c> if never updated.</summary>
    public DateTime? UpdatedAt { get; set; }

    // ── Navigations ───────────────────────────────────────────────────────────

    /// <summary>The project this milestone belongs to.</summary>
    public Project Project { get; set; } = null!;

    /// <summary>Action items (activities) assigned to this milestone.</summary>
    public ICollection<ActionItem> ActionItems { get; set; } = new List<ActionItem>();

    /// <summary>Discussion comments posted at the milestone level.</summary>
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
}
