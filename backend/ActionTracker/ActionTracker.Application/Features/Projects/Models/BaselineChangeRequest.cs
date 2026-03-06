using System;
using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Features.Projects.Models;

/// <summary>
/// Represents a formal request to modify an approved project baseline
/// (schedule, scope, or cost) after the project has been baselined.
/// Change requests follow a Sponsor-approval gate:
/// PM submits → Sponsor approves/rejects → PM implements if approved.
/// </summary>
public class BaselineChangeRequest
{
    /// <summary>Primary key — auto-incremented integer identity.</summary>
    public int Id { get; set; }

    /// <summary>Foreign key of the project this change request is raised against.</summary>
    [Required]
    public int ProjectId { get; set; }

    // ── Requester ─────────────────────────────────────────────────────────────

    /// <summary>AspNetUsers.Id of the user (typically the PM) who submitted the request.</summary>
    [Required]
    [MaxLength(450)]
    public string RequestedByUserId { get; set; } = string.Empty;

    /// <summary>Denormalised display name of the requester.</summary>
    [Required]
    [MaxLength(256)]
    public string RequestedByUserName { get; set; } = string.Empty;

    // ── Request content ───────────────────────────────────────────────────────

    /// <summary>
    /// Business justification for why the baseline change is necessary.
    /// Must be provided by the requester; reviewers will use this to decide.
    /// </summary>
    [Required]
    [MaxLength(2000)]
    public string ChangeJustification { get; set; } = string.Empty;

    /// <summary>
    /// JSON payload describing the proposed changes in detail —
    /// e.g. new planned dates per milestone, revised budget figures.
    /// Stored as <c>nvarchar(max)</c> to accommodate complex change sets.
    /// </summary>
    public string ProposedChangesJson { get; set; } = string.Empty;

    // ── Approval workflow ─────────────────────────────────────────────────────

    /// <summary>
    /// Current state of the change request in the approval workflow.
    /// Starts as <see cref="ChangeRequestStatus.Pending"/>.
    /// </summary>
    public ChangeRequestStatus Status { get; set; } = ChangeRequestStatus.Pending;

    /// <summary>
    /// AspNetUsers.Id of the Sponsor (or delegate) who reviewed the request,
    /// or <c>null</c> if not yet reviewed.
    /// </summary>
    [MaxLength(450)]
    public string? ReviewedByUserId { get; set; }

    /// <summary>
    /// Denormalised display name of the reviewer, or <c>null</c> if not yet reviewed.
    /// </summary>
    [MaxLength(256)]
    public string? ReviewedByUserName { get; set; }

    /// <summary>UTC timestamp when the Sponsor reviewed the request, or <c>null</c>.</summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// Optional notes from the reviewer explaining the approval decision or
    /// requesting additional information before a decision is made.
    /// </summary>
    [MaxLength(1000)]
    public string? ReviewNotes { get; set; }

    // ── Audit ─────────────────────────────────────────────────────────────────

    /// <summary>UTC timestamp when this change request was submitted.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the most recent update, or <c>null</c> if never updated.</summary>
    public DateTime? UpdatedAt { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────────

    /// <summary>The project this change request is associated with.</summary>
    public Project Project { get; set; } = null!;
}
