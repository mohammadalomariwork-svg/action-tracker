using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ActionTracker.Domain.Entities;

namespace ActionTracker.Application.Features.Projects.Models;

/// <summary>
/// Represents an action item (task / activity) that can exist in three contexts:
/// <list type="bullet">
///   <item>Standalone workspace action — <see cref="ProjectId"/> and <see cref="MilestoneId"/> are <c>null</c>.</item>
///   <item>Project-level action — <see cref="ProjectId"/> is set, <see cref="MilestoneId"/> is <c>null</c>.</item>
///   <item>Milestone action — both <see cref="ProjectId"/> and <see cref="MilestoneId"/> are set.</item>
/// </list>
/// Assignees may be internal users (looked up from AspNetUsers) or external
/// parties identified by name and e-mail only.
/// </summary>
public class ActionItem
{
    /// <summary>Primary key — auto-incremented integer identity.</summary>
    public int Id { get; set; }

    /// <summary>
    /// Foreign key of the workspace this action item belongs to.
    /// Every action item — regardless of context — is scoped to a workspace.
    /// </summary>
    [Required]
    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// Foreign key of the project this action item belongs to,
    /// or <c>null</c> if it is a standalone workspace action.
    /// </summary>
    public int? ProjectId { get; set; }

    /// <summary>
    /// Foreign key of the milestone this action item is assigned to,
    /// or <c>null</c> if it is a project-level or standalone action.
    /// </summary>
    public int? MilestoneId { get; set; }

    /// <summary>Short, descriptive title of the action item.</summary>
    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Optional detailed description of what needs to be done and
    /// how completion will be measured.
    /// </summary>
    [MaxLength(2000)]
    public string? Description { get; set; }

    /// <summary>Current execution status of the action item.</summary>
    public ActionItemStatus Status { get; set; } = ActionItemStatus.NotStarted;

    /// <summary>Urgency/importance classification of this action item.</summary>
    public ActionItemPriority Priority { get; set; } = ActionItemPriority.Medium;

    /// <summary>Planned date work is expected to begin (UTC).</summary>
    [Required]
    public DateTime PlannedStartDate { get; set; }

    /// <summary>Target completion date for this action item (UTC).</summary>
    [Required]
    public DateTime DueDate { get; set; }

    /// <summary>Date the action item was actually completed (UTC), or <c>null</c>.</summary>
    public DateTime? ActualCompletionDate { get; set; }

    // ── Assignee — internal user ──────────────────────────────────────────────

    /// <summary>
    /// AspNetUsers.Id of the internal user assigned to this action item,
    /// or <c>null</c> if the assignee is external.
    /// </summary>
    [MaxLength(450)]
    public string? AssignedToUserId { get; set; }

    /// <summary>
    /// Denormalised display name of the internal assignee,
    /// or <c>null</c> if the assignee is external.
    /// </summary>
    [MaxLength(256)]
    public string? AssignedToUserName { get; set; }

    // ── Assignee — external party ─────────────────────────────────────────────

    /// <summary>
    /// Full name of an external assignee who does not have a system account.
    /// Set only when <see cref="IsExternalAssignee"/> is <c>true</c>.
    /// </summary>
    [MaxLength(256)]
    public string? AssignedToExternalName { get; set; }

    /// <summary>
    /// E-mail address of the external assignee.
    /// Used for notifications when the external party has no system login.
    /// </summary>
    [MaxLength(256)]
    public string? AssignedToExternalEmail { get; set; }

    /// <summary>
    /// <c>true</c> when the assignee is an external party (no system account);
    /// <c>false</c> for internal users looked up from AspNetUsers.
    /// </summary>
    public bool IsExternalAssignee { get; set; } = false;

    // ── Progress ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Self-reported completion percentage in the range 0–100.
    /// Updated by the assignee or PM.
    /// </summary>
    public int CompletionPercentage { get; set; } = 0;

    // ── Audit ─────────────────────────────────────────────────────────────────

    /// <summary>AspNetUsers.Id of the user who created this action item.</summary>
    [Required]
    [MaxLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;

    /// <summary>
    /// Whether this action item is active.
    /// <c>false</c> soft-deletes it from normal queries.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>UTC timestamp when the action item was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the most recent update, or <c>null</c> if never updated.</summary>
    public DateTime? UpdatedAt { get; set; }

    // ── Navigations ───────────────────────────────────────────────────────────

    /// <summary>The workspace this action item is scoped to.</summary>
    public Workspace Workspace { get; set; } = null!;

    /// <summary>
    /// The project this action item belongs to,
    /// or <c>null</c> for standalone workspace actions.
    /// </summary>
    public Project? Project { get; set; }

    /// <summary>
    /// The milestone this action item is assigned to,
    /// or <c>null</c> for project-level or standalone actions.
    /// </summary>
    public Milestone? Milestone { get; set; }

    /// <summary>Documents attached to this action item.</summary>
    public ICollection<ActionDocument> Documents { get; set; } = new List<ActionDocument>();

    /// <summary>Discussion comments posted on this action item.</summary>
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
}
