using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ActionTracker.Application.Features.Workspaces.Models;

namespace ActionTracker.Application.Features.Projects.Models;

/// <summary>
/// Represents a project within a workspace.
/// A project groups milestones, action items, documents, and comments under a
/// defined scope, schedule, and budget — managed by a Project Manager and
/// sponsored by a designated Sponsor.
/// </summary>
public class Project
{
    /// <summary>Primary key — auto-incremented integer identity.</summary>
    public int Id { get; set; }

    /// <summary>
    /// Foreign key of the workspace this project belongs to.
    /// </summary>
    [Required]
    public int WorkspaceId { get; set; }

    /// <summary>
    /// Human-readable title of the project.
    /// </summary>
    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Optional description covering scope, goals, and deliverables.
    /// </summary>
    [MaxLength(2000)]
    public string? Description { get; set; }

    /// <summary>
    /// Whether this is an operational or strategic project.
    /// Strategic projects must be linked to a <see cref="StrategicObjectiveId"/>.
    /// </summary>
    [Required]
    public ProjectType ProjectType { get; set; }

    /// <summary>
    /// Current lifecycle status of the project (Draft → Active → Completed/Cancelled).
    /// </summary>
    public ProjectStatus Status { get; set; } = ProjectStatus.Draft;

    /// <summary>
    /// FK to the strategic objective this project is aligned with.
    /// Required when <see cref="ProjectType"/> is <see cref="ProjectType.Strategic"/>;
    /// <c>null</c> for operational projects.
    /// </summary>
    public int? StrategicObjectiveId { get; set; }

    // ── Sponsor ───────────────────────────────────────────────────────────────

    /// <summary>
    /// The AspNetUsers.Id of the project sponsor (accountable owner).
    /// Stored without an EF FK constraint to IdentityUser.
    /// </summary>
    [Required]
    [MaxLength(450)]
    public string SponsorUserId { get; set; } = string.Empty;

    /// <summary>
    /// Denormalised display name of the project sponsor.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string SponsorUserName { get; set; } = string.Empty;

    // ── Project Manager ───────────────────────────────────────────────────────

    /// <summary>
    /// The AspNetUsers.Id of the project manager (responsible executor).
    /// Must belong to the same organisational unit as the workspace.
    /// </summary>
    [Required]
    [MaxLength(450)]
    public string ProjectManagerUserId { get; set; } = string.Empty;

    /// <summary>
    /// Denormalised display name of the project manager.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string ProjectManagerUserName { get; set; } = string.Empty;

    // ── Schedule ──────────────────────────────────────────────────────────────

    /// <summary>Planned project start date (UTC).</summary>
    [Required]
    public DateTime PlannedStartDate { get; set; }

    /// <summary>Planned project end date (UTC).</summary>
    [Required]
    public DateTime PlannedEndDate { get; set; }

    /// <summary>Actual start date once the project is activated (UTC).</summary>
    public DateTime? ActualStartDate { get; set; }

    /// <summary>Actual completion or cancellation date (UTC).</summary>
    public DateTime? ActualEndDate { get; set; }

    // ── Baseline ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Whether the project has been formally baselined.
    /// Once <c>true</c>, scope/schedule/cost changes require a
    /// <see cref="BaselineChangeRequest"/>.
    /// </summary>
    public bool IsBaselined { get; set; } = false;

    /// <summary>UTC timestamp when the project was first baselined, or <c>null</c>.</summary>
    public DateTime? BaselinedAt { get; set; }

    /// <summary>
    /// AspNetUsers.Id of the user who performed the baselineing action,
    /// or <c>null</c> if not yet baselined.
    /// </summary>
    [MaxLength(450)]
    public string? BaselinedByUserId { get; set; }

    // ── Soft-delete / Audit ───────────────────────────────────────────────────

    /// <summary>Whether the project is active. <c>false</c> soft-deletes it.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>UTC timestamp when the project was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the most recent update, or <c>null</c> if never updated.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// AspNetUsers.Id of the user who created this project record.
    /// </summary>
    [Required]
    [MaxLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;

    // ── Navigations ───────────────────────────────────────────────────────────

    /// <summary>The workspace this project belongs to.</summary>
    public Workspace Workspace { get; set; } = null!;

    /// <summary>
    /// The strategic objective this project is aligned with,
    /// or <c>null</c> for operational projects.
    /// </summary>
    public StrategicObjective? StrategicObjective { get; set; }

    /// <summary>Milestones (work packages) within this project.</summary>
    public ICollection<Milestone> Milestones { get; set; } = new List<Milestone>();

    /// <summary>Project-level action items not assigned to any milestone.</summary>
    public ICollection<ProjectActionItem> ActionItems { get; set; } = new List<ProjectActionItem>();

    /// <summary>Uploaded documents attached to this project.</summary>
    public ICollection<ProjectDocument> Documents { get; set; } = new List<ProjectDocument>();

    /// <summary>Discussion comments posted at the project level.</summary>
    public ICollection<ProjectComment> Comments { get; set; } = new List<ProjectComment>();

    /// <summary>Optional budget record for this project.</summary>
    public ProjectBudget? Budget { get; set; }

    /// <summary>Contracts associated with this project.</summary>
    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();

    /// <summary>
    /// The approved baseline snapshot, present once the project has been baselined.
    /// </summary>
    public ProjectBaseline? Baseline { get; set; }

    /// <summary>Change requests raised against the baseline after baselining.</summary>
    public ICollection<BaselineChangeRequest> ChangeRequests { get; set; } = new List<BaselineChangeRequest>();
}
