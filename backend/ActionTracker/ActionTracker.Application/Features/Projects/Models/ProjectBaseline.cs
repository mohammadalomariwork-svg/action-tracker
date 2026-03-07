using System;
using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Features.Projects.Models;

/// <summary>
/// Stores an immutable snapshot of a project's approved schedule and scope
/// at the point of baselining.
/// A project may have at most one baseline record; subsequent changes are
/// tracked through <see cref="BaselineChangeRequest"/> records.
/// </summary>
public class ProjectBaseline
{
    /// <summary>Primary key — auto-incremented integer identity.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Foreign key of the baselined project.
    /// Unique — each project may have at most one baseline record.
    /// </summary>
    [Required]
    public Guid ProjectId { get; set; }

    /// <summary>UTC timestamp when the project was baselined.</summary>
    public DateTime BaselinedAt { get; set; }

    /// <summary>AspNetUsers.Id of the user who performed the baselining action.</summary>
    [Required]
    [MaxLength(450)]
    public string BaselinedByUserId { get; set; } = string.Empty;

    /// <summary>Denormalised display name of the user who baselined the project.</summary>
    [Required]
    [MaxLength(256)]
    public string BaselinedByUserName { get; set; } = string.Empty;

    /// <summary>Approved baseline planned start date, captured at the time of baselining.</summary>
    public DateTime BaselinePlannedStartDate { get; set; }

    /// <summary>Approved baseline planned end date, captured at the time of baselining.</summary>
    public DateTime BaselinePlannedEndDate { get; set; }

    /// <summary>
    /// JSON snapshot of all milestones and action items at the time of baselining.
    /// Stored as <c>nvarchar(max)</c> to accommodate large projects.
    /// Used for schedule variance reporting and change-request comparison.
    /// </summary>
    public string BaselineSnapshotJson { get; set; } = string.Empty;

    // ── Navigation ────────────────────────────────────────────────────────────

    /// <summary>The project this baseline record belongs to.</summary>
    public Project Project { get; set; } = null!;
}
