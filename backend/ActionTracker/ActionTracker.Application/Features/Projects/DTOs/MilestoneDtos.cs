using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ActionTracker.Application.Features.Projects.Models;

namespace ActionTracker.Application.Features.Projects.DTOs;

/// <summary>
/// Lightweight milestone representation for list views and as nested items
/// inside <see cref="ProjectDetailDto"/>.
/// Omits heavy nested collections — use <see cref="MilestoneDetailDto"/> for
/// the full record.
/// </summary>
public class MilestoneListDto
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>Project this milestone belongs to.</summary>
    public int ProjectId { get; set; }

    /// <summary>Human-readable title of the milestone.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>1-based ordering of the milestone within its project.</summary>
    public int SequenceOrder { get; set; }

    /// <summary>Current execution status.</summary>
    public MilestoneStatus Status { get; set; }

    /// <summary>Planned start date (UTC).</summary>
    public DateTime PlannedStartDate { get; set; }

    /// <summary>Planned completion date (UTC).</summary>
    public DateTime PlannedEndDate { get; set; }

    /// <summary>Actual start date (UTC), or <c>null</c> if not yet started.</summary>
    public DateTime? ActualStartDate { get; set; }

    /// <summary>Actual completion date (UTC), or <c>null</c> if not yet completed.</summary>
    public DateTime? ActualEndDate { get; set; }

    /// <summary>Overall completion percentage for the milestone (0–100).</summary>
    public int CompletionPercentage { get; set; }

    /// <summary>Total number of action items assigned to this milestone.</summary>
    public int ActionItemCount { get; set; }
}

/// <summary>
/// Full milestone record including description, nested action items, and
/// comments.
/// Returned by single-milestone GET endpoints.
/// </summary>
public class MilestoneDetailDto : MilestoneListDto
{
    /// <summary>Optional description of the milestone scope and acceptance criteria.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Action items assigned to this milestone.
    /// Populated as a flat list of <see cref="ActionItemListDto"/>.
    /// </summary>
    public List<ActionItemListDto> ActionItems { get; set; } = new();

    /// <summary>Discussion comments posted at the milestone level.</summary>
    public List<CommentDto> Comments { get; set; } = new();

    /// <summary>UTC timestamp of the most recent update, or <c>null</c> if never updated.</summary>
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Payload for creating a new milestone within a project.
/// </summary>
public class CreateMilestoneDto
{
    /// <summary>Project the milestone belongs to (required).</summary>
    [Required]
    public int ProjectId { get; set; }

    /// <summary>Human-readable milestone title (required, max 300 chars).</summary>
    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional scope and acceptance criteria description (max 1000 chars).</summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// 1-based display order of this milestone within the project (required).
    /// Service layer may auto-assign or re-sequence as needed.
    /// </summary>
    [Required]
    public int SequenceOrder { get; set; }

    /// <summary>Planned start date for this milestone (required).</summary>
    [Required]
    public DateTime PlannedStartDate { get; set; }

    /// <summary>Planned completion date for this milestone (required).</summary>
    [Required]
    public DateTime PlannedEndDate { get; set; }
}

/// <summary>
/// Payload for updating an existing milestone.
/// Only non-null fields are applied by the service layer.
/// </summary>
public class UpdateMilestoneDto
{
    /// <summary>Primary key of the milestone to update (required).</summary>
    [Required]
    public int Id { get; set; }

    /// <summary>Updated title (max 300 chars).</summary>
    [MaxLength(300)]
    public string? Title { get; set; }

    /// <summary>Updated description (max 1000 chars).</summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>Updated sequence order within the project.</summary>
    public int? SequenceOrder { get; set; }

    /// <summary>Updated lifecycle status.</summary>
    public MilestoneStatus? Status { get; set; }

    /// <summary>Updated planned start date.</summary>
    public DateTime? PlannedStartDate { get; set; }

    /// <summary>Updated planned end date.</summary>
    public DateTime? PlannedEndDate { get; set; }

    /// <summary>Actual start date to record (set once work begins).</summary>
    public DateTime? ActualStartDate { get; set; }

    /// <summary>Actual completion date to record.</summary>
    public DateTime? ActualEndDate { get; set; }

    /// <summary>Updated completion percentage (0–100).</summary>
    public int? CompletionPercentage { get; set; }
}
