using System;
using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Features.Projects.Models;

/// <summary>
/// Represents a discussion comment that can be attached to a project,
/// a milestone, or an action item.
/// Exactly one of <see cref="ProjectId"/>, <see cref="MilestoneId"/>, or
/// <see cref="ActionItemId"/> will be set at a time — the others will be
/// <c>null</c>.
/// </summary>
public class Comment
{
    /// <summary>Primary key — auto-incremented integer identity.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Text body of the comment. Supports plain text; rendering is handled
    /// by the client.
    /// </summary>
    [Required]
    [MaxLength(2000)]
    public string Content { get; set; } = string.Empty;

    /// <summary>AspNetUsers.Id of the user who posted this comment.</summary>
    [Required]
    [MaxLength(450)]
    public string AuthorUserId { get; set; } = string.Empty;

    /// <summary>Denormalised display name of the comment author.</summary>
    [Required]
    [MaxLength(256)]
    public string AuthorUserName { get; set; } = string.Empty;

    // ── Polymorphic parent — exactly one will be non-null ─────────────────────

    /// <summary>
    /// FK to the action item this comment belongs to,
    /// or <c>null</c> if attached to a milestone or project instead.
    /// </summary>
    public Guid? ActionItemId { get; set; }

    /// <summary>
    /// FK to the milestone this comment belongs to,
    /// or <c>null</c> if attached to an action item or project instead.
    /// </summary>
    public Guid? MilestoneId { get; set; }

    /// <summary>
    /// FK to the project this comment belongs to,
    /// or <c>null</c> if attached to an action item or milestone instead.
    /// </summary>
    public Guid? ProjectId { get; set; }

    // ── Audit ─────────────────────────────────────────────────────────────────

    /// <summary>UTC timestamp when the comment was posted.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the most recent edit, or <c>null</c> if never edited.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// <c>true</c> once the comment body has been edited after initial posting.
    /// Displayed to readers as an "(edited)" indicator.
    /// </summary>
    public bool IsEdited { get; set; } = false;

    /// <summary>
    /// Whether this comment is active.
    /// <c>false</c> soft-deletes it (shows as "[deleted]" or hidden entirely).
    /// </summary>
    public bool IsActive { get; set; } = true;

    // ── Navigations ───────────────────────────────────────────────────────────

    /// <summary>Navigation to the action item, populated when <see cref="ActionItemId"/> is set.</summary>
    public ActionItem? ActionItem { get; set; }

    /// <summary>Navigation to the milestone, populated when <see cref="MilestoneId"/> is set.</summary>
    public Milestone? Milestone { get; set; }

    /// <summary>Navigation to the project, populated when <see cref="ProjectId"/> is set.</summary>
    public Project? Project { get; set; }
}
