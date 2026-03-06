using System;
using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Features.Projects.DTOs;

/// <summary>
/// Read model for a comment attached to a project, milestone, or action item.
/// Exactly one of <see cref="ProjectId"/>, <see cref="MilestoneId"/>, or
/// <see cref="ActionItemId"/> will be non-null.
/// </summary>
public class CommentDto
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>Text body of the comment.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>AspNetUsers.Id of the comment author.</summary>
    public string AuthorUserId { get; set; } = string.Empty;

    /// <summary>Display name of the comment author.</summary>
    public string AuthorUserName { get; set; } = string.Empty;

    /// <summary>FK to the action item this comment is attached to, or <c>null</c>.</summary>
    public int? ActionItemId { get; set; }

    /// <summary>FK to the milestone this comment is attached to, or <c>null</c>.</summary>
    public int? MilestoneId { get; set; }

    /// <summary>FK to the project this comment is attached to, or <c>null</c>.</summary>
    public int? ProjectId { get; set; }

    /// <summary>UTC timestamp when the comment was posted.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the most recent edit, or <c>null</c> if never edited.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// <c>true</c> once the comment body has been edited after initial posting.
    /// Displayed as an "(edited)" indicator in the UI.
    /// </summary>
    public bool IsEdited { get; set; }
}

/// <summary>
/// Payload for posting a new comment on a project, milestone, or action item.
/// Exactly one of <see cref="ActionItemId"/>, <see cref="MilestoneId"/>, or
/// <see cref="ProjectId"/> must be set — this is enforced at the service layer,
/// not via data annotations.
/// </summary>
public class CreateCommentDto
{
    /// <summary>Text body of the comment (required, max 2000 chars).</summary>
    [Required]
    [MaxLength(2000)]
    public string Content { get; set; } = string.Empty;

    /// <summary>AspNetUsers.Id of the comment author (required).</summary>
    [Required]
    [MaxLength(450)]
    public string AuthorUserId { get; set; } = string.Empty;

    /// <summary>Display name of the comment author (required).</summary>
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
/// Payload for editing the text body of an existing comment.
/// Only the author or an admin may submit this DTO.
/// </summary>
public class UpdateCommentDto
{
    /// <summary>Primary key of the comment to update (required).</summary>
    [Required]
    public int Id { get; set; }

    /// <summary>Updated text body (required, max 2000 chars).</summary>
    [Required]
    [MaxLength(2000)]
    public string Content { get; set; } = string.Empty;
}
