using System;

namespace ActionTracker.Application.Features.Workspaces.Models;

/// <summary>
/// Represents a single admin user assigned to a workspace.
/// A workspace may have one or more admins.
/// </summary>
public class WorkspaceAdmin
{
    /// <summary>Primary key — auto-incremented integer identity.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Foreign key to the owning workspace.</summary>
    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// The <c>Id</c> of the AspNetUsers record.
    /// Stored as a plain string — no FK constraint to IdentityUser.
    /// </summary>
    public string AdminUserId { get; set; } = string.Empty;

    /// <summary>
    /// Denormalised display name of the admin, cached to avoid joining
    /// AspNetUsers on every read.
    /// </summary>
    public string AdminUserName { get; set; } = string.Empty;

    /// <summary>Navigation property back to the owning workspace.</summary>
    public Workspace Workspace { get; set; } = null!;
}
