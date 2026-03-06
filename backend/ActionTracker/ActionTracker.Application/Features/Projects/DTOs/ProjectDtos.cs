using System;
using System.ComponentModel.DataAnnotations;
using ActionTracker.Application.Features.Projects.Models;

namespace ActionTracker.Application.Features.Projects.DTOs;

/// <summary>
/// Lightweight project representation for list views and grids.
/// Omits heavy nested collections — use <see cref="ProjectDetailDto"/> for the
/// full record.
/// </summary>
public class ProjectListDto
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>Workspace this project belongs to.</summary>
    public int WorkspaceId { get; set; }

    /// <summary>Human-readable title of the project.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Whether this is a strategic or operational project.</summary>
    public ProjectType ProjectType { get; set; }

    /// <summary>Current lifecycle status (Draft, Active, Completed, etc.).</summary>
    public ProjectStatus Status { get; set; }

    /// <summary>Display name of the project manager.</summary>
    public string ProjectManagerUserName { get; set; } = string.Empty;

    /// <summary>Display name of the project sponsor.</summary>
    public string SponsorUserName { get; set; } = string.Empty;

    /// <summary>Planned project start date (UTC).</summary>
    public DateTime PlannedStartDate { get; set; }

    /// <summary>Planned project end date (UTC).</summary>
    public DateTime PlannedEndDate { get; set; }

    /// <summary>Whether the project has been formally baselined.</summary>
    public bool IsBaselined { get; set; }

    /// <summary>
    /// Calculated overall completion percentage (0–100).
    /// Derived from milestone and action-item completion at the service layer.
    /// </summary>
    public int CompletionPercentage { get; set; }
}

/// <summary>
/// Full project record including all schedule, baseline, and aggregate fields.
/// Returned by single-project GET endpoints.
/// </summary>
public class ProjectDetailDto : ProjectListDto
{
    /// <summary>Optional description covering scope, goals, and deliverables.</summary>
    public string? Description { get; set; }

    /// <summary>FK to the strategic objective this project is aligned with (null for operational).</summary>
    public int? StrategicObjectiveId { get; set; }

    /// <summary>Title of the linked strategic objective, or <c>null</c> if not aligned.</summary>
    public string? StrategicObjectiveTitle { get; set; }

    /// <summary>AspNetUsers.Id of the sponsor.</summary>
    public string SponsorUserId { get; set; } = string.Empty;

    // SponsorUserName is inherited from ProjectListDto.

    /// <summary>AspNetUsers.Id of the project manager.</summary>
    public string ProjectManagerUserId { get; set; } = string.Empty;

    // ProjectManagerUserName is inherited from ProjectListDto.

    /// <summary>Actual project start date (UTC), or <c>null</c> if not yet started.</summary>
    public DateTime? ActualStartDate { get; set; }

    /// <summary>Actual project completion or cancellation date (UTC), or <c>null</c>.</summary>
    public DateTime? ActualEndDate { get; set; }

    /// <summary>UTC timestamp when the project was first baselined, or <c>null</c>.</summary>
    public DateTime? BaselinedAt { get; set; }

    /// <summary>UTC timestamp when the project record was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the most recent update, or <c>null</c> if never updated.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>AspNetUsers.Id of the user who created the project.</summary>
    public string CreatedByUserId { get; set; } = string.Empty;

    /// <summary>Total number of milestones in the project.</summary>
    public int MilestoneCount { get; set; }

    /// <summary>Total number of action items across all milestones and the project itself.</summary>
    public int ActionItemCount { get; set; }

    /// <summary>Whether a budget record has been attached to this project.</summary>
    public bool HasBudget { get; set; }
}

/// <summary>
/// Payload for creating a new project.
/// </summary>
public class CreateProjectDto
{
    /// <summary>Workspace the project will belong to (required).</summary>
    [Required]
    public int WorkspaceId { get; set; }

    /// <summary>Human-readable project title (required, max 300 chars).</summary>
    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional scope and goals description (max 2000 chars).</summary>
    [MaxLength(2000)]
    public string? Description { get; set; }

    /// <summary>Operational or Strategic classification (required).</summary>
    [Required]
    public ProjectType ProjectType { get; set; }

    /// <summary>
    /// FK to the strategic objective (nullable in the DTO).
    /// Service layer enforces this is set when <see cref="ProjectType"/> is
    /// <see cref="ProjectType.Strategic"/>.
    /// </summary>
    public int? StrategicObjectiveId { get; set; }

    /// <summary>AspNetUsers.Id of the sponsor (required).</summary>
    [Required]
    public string SponsorUserId { get; set; } = string.Empty;

    /// <summary>Display name of the sponsor (required).</summary>
    [Required]
    [MaxLength(256)]
    public string SponsorUserName { get; set; } = string.Empty;

    /// <summary>AspNetUsers.Id of the project manager (required).</summary>
    [Required]
    public string ProjectManagerUserId { get; set; } = string.Empty;

    /// <summary>Display name of the project manager (required).</summary>
    [Required]
    [MaxLength(256)]
    public string ProjectManagerUserName { get; set; } = string.Empty;

    /// <summary>Planned project start date (required).</summary>
    [Required]
    public DateTime PlannedStartDate { get; set; }

    /// <summary>Planned project end date (required).</summary>
    [Required]
    public DateTime PlannedEndDate { get; set; }

    /// <summary>AspNetUsers.Id of the user creating this project (required).</summary>
    [Required]
    public string CreatedByUserId { get; set; } = string.Empty;
}

/// <summary>
/// Payload for updating an existing project.
/// Only fields included in this DTO are changed; the service layer applies
/// each non-null value to the stored entity.
/// </summary>
public class UpdateProjectDto
{
    /// <summary>Primary key of the project to update.</summary>
    [Required]
    public int Id { get; set; }

    /// <summary>Updated project title (max 300 chars).</summary>
    [MaxLength(300)]
    public string? Title { get; set; }

    /// <summary>Updated description (max 2000 chars).</summary>
    [MaxLength(2000)]
    public string? Description { get; set; }

    /// <summary>Updated project type classification.</summary>
    public ProjectType? ProjectType { get; set; }

    /// <summary>Updated lifecycle status.</summary>
    public ProjectStatus? Status { get; set; }

    /// <summary>Updated strategic objective alignment (set to <c>null</c> to remove alignment).</summary>
    public int? StrategicObjectiveId { get; set; }

    /// <summary>Updated sponsor user ID.</summary>
    [MaxLength(450)]
    public string? SponsorUserId { get; set; }

    /// <summary>Updated sponsor display name.</summary>
    [MaxLength(256)]
    public string? SponsorUserName { get; set; }

    /// <summary>Updated project manager user ID.</summary>
    [MaxLength(450)]
    public string? ProjectManagerUserId { get; set; }

    /// <summary>Updated project manager display name.</summary>
    [MaxLength(256)]
    public string? ProjectManagerUserName { get; set; }

    /// <summary>Updated planned start date.</summary>
    public DateTime? PlannedStartDate { get; set; }

    /// <summary>Updated planned end date.</summary>
    public DateTime? PlannedEndDate { get; set; }

    /// <summary>Actual start date to record (set once the project is activated).</summary>
    public DateTime? ActualStartDate { get; set; }

    /// <summary>Actual completion or cancellation date to record.</summary>
    public DateTime? ActualEndDate { get; set; }
}
