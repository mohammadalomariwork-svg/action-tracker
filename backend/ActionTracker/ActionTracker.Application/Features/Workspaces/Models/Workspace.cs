using System;
using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Features.Workspaces.Models;

/// <summary>
/// Represents a workspace — a logical grouping of action items scoped to an
/// organisational unit and owned by a designated admin user.
/// </summary>
public class Workspace
{
    /// <summary>
    /// Primary key — auto-incremented integer identity.
    /// </summary>
    public int Id { get; set; }

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
    /// The <c>Id</c> of the AspNetUsers record for the workspace admin.
    /// Stored as a plain string — no foreign key constraint or navigation
    /// property to <c>IdentityUser</c>.
    /// </summary>
    [Required]
    [MaxLength(450)]
    public string AdminUserId { get; set; } = string.Empty;

    /// <summary>
    /// Denormalised display name of the workspace admin, cached here to
    /// avoid joining AspNetUsers on every read.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string AdminUserName { get; set; } = string.Empty;

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
}
