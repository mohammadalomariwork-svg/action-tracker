using System;
using System.ComponentModel.DataAnnotations;
using ActionTracker.Application.Features.Projects.Models;

namespace ActionTracker.Application.Features.Projects.DTOs;

// ── Baseline snapshot ─────────────────────────────────────────────────────────

/// <summary>
/// Read model for a project's approved baseline snapshot.
/// Returned when querying a project's baseline record.
/// </summary>
public class ProjectBaselineDto
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>Project this baseline belongs to.</summary>
    public int ProjectId { get; set; }

    /// <summary>UTC timestamp when the project was formally baselined.</summary>
    public DateTime BaselinedAt { get; set; }

    /// <summary>Display name of the user who performed the baselining action.</summary>
    public string BaselinedByUserName { get; set; } = string.Empty;

    /// <summary>
    /// Approved baseline planned start date, captured at the moment of baselining.
    /// </summary>
    public DateTime BaselinePlannedStartDate { get; set; }

    /// <summary>
    /// Approved baseline planned end date, captured at the moment of baselining.
    /// </summary>
    public DateTime BaselinePlannedEndDate { get; set; }

    /// <summary>
    /// JSON snapshot of all milestones and action items at the time of baselining.
    /// Used for schedule-variance reporting and change-request comparison.
    /// </summary>
    public string BaselineSnapshotJson { get; set; } = string.Empty;
}

// ── Change requests ───────────────────────────────────────────────────────────

/// <summary>
/// Read model for a baseline change request.
/// </summary>
public class BaselineChangeRequestDto
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>Project this change request is raised against.</summary>
    public int ProjectId { get; set; }

    /// <summary>Display name of the user (typically the PM) who submitted the request.</summary>
    public string RequestedByUserName { get; set; } = string.Empty;

    /// <summary>Business justification provided by the requester.</summary>
    public string ChangeJustification { get; set; } = string.Empty;

    /// <summary>
    /// JSON payload describing the proposed changes (e.g. revised dates,
    /// updated budget figures).
    /// </summary>
    public string ProposedChangesJson { get; set; } = string.Empty;

    /// <summary>Current state of the change request in the approval workflow.</summary>
    public ChangeRequestStatus Status { get; set; }

    /// <summary>
    /// Display name of the Sponsor who reviewed the request, or <c>null</c> if
    /// not yet reviewed.
    /// </summary>
    public string? ReviewedByUserName { get; set; }

    /// <summary>UTC timestamp when the Sponsor reviewed the request, or <c>null</c>.</summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>Optional notes from the reviewer explaining the decision.</summary>
    public string? ReviewNotes { get; set; }

    /// <summary>UTC timestamp when the change request was submitted.</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Payload for submitting a new baseline change request.
/// Only the project manager (or a workspace admin) may submit this.
/// </summary>
public class CreateBaselineChangeRequestDto
{
    /// <summary>Project the change request is raised against (required).</summary>
    [Required]
    public int ProjectId { get; set; }

    /// <summary>
    /// AspNetUsers.Id of the user submitting the request (required).
    /// Must match the authenticated caller.
    /// </summary>
    [Required]
    [MaxLength(450)]
    public string RequestedByUserId { get; set; } = string.Empty;

    /// <summary>Display name of the requester (required).</summary>
    [Required]
    [MaxLength(256)]
    public string RequestedByUserName { get; set; } = string.Empty;

    /// <summary>
    /// Business justification explaining why this baseline change is necessary
    /// (required, max 2000 chars).
    /// </summary>
    [Required]
    [MaxLength(2000)]
    public string ChangeJustification { get; set; } = string.Empty;

    /// <summary>
    /// JSON payload describing the proposed changes in detail (required).
    /// Content is validated and applied by the service layer if approved.
    /// </summary>
    [Required]
    public string ProposedChangesJson { get; set; } = string.Empty;
}

/// <summary>
/// Payload for the Sponsor to approve or reject a pending change request.
/// Only <see cref="ChangeRequestStatus.ApprovedBySponsor"/> or
/// <see cref="ChangeRequestStatus.Rejected"/> are valid status values —
/// enforced at the service layer.
/// </summary>
public class ReviewChangeRequestDto
{
    /// <summary>Primary key of the change request to review (required).</summary>
    [Required]
    public int ChangeRequestId { get; set; }

    /// <summary>
    /// AspNetUsers.Id of the reviewing Sponsor (required).
    /// Must match the project's <c>SponsorUserId</c>.
    /// </summary>
    [Required]
    [MaxLength(450)]
    public string ReviewedByUserId { get; set; } = string.Empty;

    /// <summary>Display name of the reviewing Sponsor (required).</summary>
    [Required]
    [MaxLength(256)]
    public string ReviewedByUserName { get; set; } = string.Empty;

    /// <summary>
    /// Decision status — must be
    /// <see cref="ChangeRequestStatus.ApprovedBySponsor"/> or
    /// <see cref="ChangeRequestStatus.Rejected"/> (required).
    /// </summary>
    [Required]
    public ChangeRequestStatus Status { get; set; }

    /// <summary>
    /// Optional notes explaining the approval decision or reason for rejection
    /// (max 1000 chars).
    /// </summary>
    [MaxLength(1000)]
    public string? ReviewNotes { get; set; }
}
