using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Features.Workspaces.Models;

/// <summary>
/// Represents a workspace — a logical grouping of action items scoped to an
/// organisational unit and owned by one or more designated admin users.
/// </summary>
public class Workspace
{
    /// <summary>
    /// Primary key — GUID primary key.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Human-readable title of the workspace.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Name of the organisational unit this workspace belongs to.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string OrganizationUnit { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the workspace was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// UTC timestamp of the most recent update, or <c>null</c> if never updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Whether the workspace is active. Defaults to <c>true</c>.
    /// Inactive workspaces are hidden from normal queries.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// The admin users assigned to this workspace.
    /// </summary>
    public ICollection<WorkspaceAdmin> Admins { get; set; } = new List<WorkspaceAdmin>();
}
