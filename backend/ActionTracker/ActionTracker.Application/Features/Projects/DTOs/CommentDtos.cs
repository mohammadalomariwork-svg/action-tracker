using System;
using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Features.Projects.DTOs;

/// <summary>
/// Read model for a comment, used in detail views for projects, milestones,
/// and action items.
/// </summary>
public class CommentDto
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>Text body of the comment.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>AspNetUsers.Id of the author.</summary>
    public string AuthorUserId { get; set; } = string.Empty;

    /// <summary>Display name of the author.</summary>
    public string AuthorUserName { get; set; } = string.Empty;

    /// <summary>FK to the action item this comment is on, or <c>null</c>.</summary>
    public int? ActionItemId { get; set; }

    /// <summary>FK to the milestone this comment is on, or <c>null</c>.</summary>
    public int? MilestoneId { get; set; }

    /// <summary>FK to the project this comment is on, or <c>null</c>.</summary>
    public int? ProjectId { get; set; }

    /// <summary>UTC timestamp when the comment was posted.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the most recent edit, or <c>null</c> if never edited.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Whether the comment body has been edited after initial posting.</summary>
    public bool IsEdited { get; set; }
}

/// <summary>
/// Payload for posting a new comment on a project, milestone, or action item.
/// Exactly one of the three FK fields must be set — enforced at the service layer.
/// (Full definition completed in B-P05.)
/// </summary>
public class CreateCommentDto
{
    /// <summary>Comment text body (required, max 2000 chars).</summary>
    [Required]
    [MaxLength(2000)]
    public string Content { get; set; } = string.Empty;

    /// <summary>AspNetUsers.Id of the author (required).</summary>
    [Required]
    [MaxLength(450)]
    public string AuthorUserId { get; set; } = string.Empty;

    /// <summary>Author display name (required).</summary>
    [Required]
    [MaxLength(256)]
    public string AuthorUserName { get; set; } = string.Empty;

    /// <summary>FK of the action item to attach this comment to (nullable).</summary>
    public int? ActionItemId { get; set; }

    /// <summary>FK of the milestone to attach this comment to (nullable).</summary>
    public int? MilestoneId { get; set; }

    /// <summary>FK of the project to attach this comment to (nullable).</summary>
    public int? ProjectId { get; set; }
}

/// <summary>
/// Payload for editing an existing comment.
/// (Full definition completed in B-P05.)
/// </summary>
public class UpdateCommentDto
{
    /// <summary>Primary key of the comment to update (required).</summary>
    [Required]
    public int Id { get; set; }

    /// <summary>Updated comment text body (required, max 2000 chars).</summary>
    [Required]
    [MaxLength(2000)]
    public string Content { get; set; } = string.Empty;
}
