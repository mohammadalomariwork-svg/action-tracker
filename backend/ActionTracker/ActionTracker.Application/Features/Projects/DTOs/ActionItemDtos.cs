using System;
using System.ComponentModel.DataAnnotations;
using ActionTracker.Application.Features.Projects.Models;

namespace ActionTracker.Application.Features.Projects.DTOs;

/// <summary>
/// Lightweight action-item representation used in list views and as nested items
/// inside <see cref="MilestoneDetailDto"/>.
/// Full create/update DTOs are defined alongside this class (B-P05).
/// </summary>
public class ActionItemListDto
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>Workspace this action item belongs to.</summary>
    public int WorkspaceId { get; set; }

    /// <summary>Project this action item belongs to, or <c>null</c> for standalone actions.</summary>
    public int? ProjectId { get; set; }

    /// <summary>Milestone this action item is assigned to, or <c>null</c>.</summary>
    public int? MilestoneId { get; set; }

    /// <summary>Short descriptive title of the action item.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Current execution status.</summary>
    public ActionItemStatus Status { get; set; }

    /// <summary>Urgency/importance classification.</summary>
    public ActionItemPriority Priority { get; set; }

    /// <summary>Planned date work is expected to begin (UTC).</summary>
    public DateTime PlannedStartDate { get; set; }

    /// <summary>Target completion date (UTC).</summary>
    public DateTime DueDate { get; set; }

    /// <summary>Actual completion date (UTC), or <c>null</c> if still open.</summary>
    public DateTime? ActualCompletionDate { get; set; }

    /// <summary>AspNetUsers.Id of the internal assignee, or <c>null</c> for external.</summary>
    public string? AssignedToUserId { get; set; }

    /// <summary>Display name of the internal assignee, or <c>null</c> for external.</summary>
    public string? AssignedToUserName { get; set; }

    /// <summary>Full name of the external assignee when <see cref="IsExternalAssignee"/> is <c>true</c>.</summary>
    public string? AssignedToExternalName { get; set; }

    /// <summary>Whether the assignee is an external party without a system account.</summary>
    public bool IsExternalAssignee { get; set; }

    /// <summary>Self-reported completion percentage (0–100).</summary>
    public int CompletionPercentage { get; set; }
}

/// <summary>
/// Full action-item record including description, documents, and comments.
/// Returned by single action-item GET endpoints.
/// (Full definition completed in B-P05.)
/// </summary>
public class ActionItemDetailDto : ActionItemListDto
{
    /// <summary>Optional detailed description of the work and acceptance criteria.</summary>
    public string? Description { get; set; }

    /// <summary>UTC timestamp when the action item was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the most recent update, or <c>null</c>.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>AspNetUsers.Id of the user who created this action item.</summary>
    public string CreatedByUserId { get; set; } = string.Empty;

    /// <summary>Discussion comments on this action item.</summary>
    public List<CommentDto> Comments { get; set; } = new();
}

/// <summary>
/// Payload for creating a new action item.
/// (Full definition completed in B-P05.)
/// </summary>
public class CreateActionItemDto
{
    /// <summary>Workspace the action item belongs to (required).</summary>
    [Required]
    public int WorkspaceId { get; set; }

    /// <summary>Project the action item belongs to (nullable).</summary>
    public int? ProjectId { get; set; }

    /// <summary>Milestone the action item is assigned to (nullable).</summary>
    public int? MilestoneId { get; set; }

    /// <summary>Short descriptive title (required, max 300 chars).</summary>
    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional detailed description (max 2000 chars).</summary>
    [MaxLength(2000)]
    public string? Description { get; set; }

    /// <summary>Urgency/importance classification (required).</summary>
    [Required]
    public ActionItemPriority Priority { get; set; }

    /// <summary>Planned start date (required).</summary>
    [Required]
    public DateTime PlannedStartDate { get; set; }

    /// <summary>Target completion date (required).</summary>
    [Required]
    public DateTime DueDate { get; set; }

    /// <summary>Internal assignee user ID (nullable — use when <see cref="IsExternalAssignee"/> is false).</summary>
    [MaxLength(450)]
    public string? AssignedToUserId { get; set; }

    /// <summary>Internal assignee display name (nullable).</summary>
    [MaxLength(256)]
    public string? AssignedToUserName { get; set; }

    /// <summary>External assignee full name (nullable — use when <see cref="IsExternalAssignee"/> is true).</summary>
    [MaxLength(256)]
    public string? AssignedToExternalName { get; set; }

    /// <summary>External assignee e-mail (nullable).</summary>
    [MaxLength(256)]
    public string? AssignedToExternalEmail { get; set; }

    /// <summary>Whether the assignee is external (no system account).</summary>
    public bool IsExternalAssignee { get; set; }

    /// <summary>AspNetUsers.Id of the user creating this action item (required).</summary>
    [Required]
    public string CreatedByUserId { get; set; } = string.Empty;
}

/// <summary>
/// Payload for updating an existing action item.
/// (Full definition completed in B-P05.)
/// </summary>
public class UpdateActionItemDto
{
    /// <summary>Primary key of the action item to update (required).</summary>
    [Required]
    public int Id { get; set; }

    /// <summary>Updated title (max 300 chars).</summary>
    [MaxLength(300)]
    public string? Title { get; set; }

    /// <summary>Updated description (max 2000 chars).</summary>
    [MaxLength(2000)]
    public string? Description { get; set; }

    /// <summary>Updated status.</summary>
    public ActionItemStatus? Status { get; set; }

    /// <summary>Updated priority.</summary>
    public ActionItemPriority? Priority { get; set; }

    /// <summary>Updated planned start date.</summary>
    public DateTime? PlannedStartDate { get; set; }

    /// <summary>Updated due date.</summary>
    public DateTime? DueDate { get; set; }

    /// <summary>Actual completion date to record.</summary>
    public DateTime? ActualCompletionDate { get; set; }

    /// <summary>Updated internal assignee user ID.</summary>
    [MaxLength(450)]
    public string? AssignedToUserId { get; set; }

    /// <summary>Updated internal assignee display name.</summary>
    [MaxLength(256)]
    public string? AssignedToUserName { get; set; }

    /// <summary>Updated external assignee full name.</summary>
    [MaxLength(256)]
    public string? AssignedToExternalName { get; set; }

    /// <summary>Updated external assignee e-mail.</summary>
    [MaxLength(256)]
    public string? AssignedToExternalEmail { get; set; }

    /// <summary>Updated external-assignee flag.</summary>
    public bool? IsExternalAssignee { get; set; }

    /// <summary>Updated completion percentage (0–100).</summary>
    public int? CompletionPercentage { get; set; }
}
