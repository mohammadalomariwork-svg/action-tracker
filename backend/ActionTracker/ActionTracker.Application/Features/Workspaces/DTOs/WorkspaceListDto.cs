namespace ActionTracker.Application.Features.Workspaces.DTOs;

/// <summary>
/// Lightweight workspace representation used in list views.
/// Omits audit timestamps to reduce payload size.
/// </summary>
public class WorkspaceListDto
{
    /// <summary>Primary key of the workspace.</summary>
    public Guid Id { get; set; }

    /// <summary>Human-readable title of the workspace.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Name of the organisational unit this workspace belongs to.</summary>
    public string OrganizationUnit { get; set; } = string.Empty;

    /// <summary>Comma-separated display names of all workspace admins.</summary>
    public string AdminUserNames { get; set; } = string.Empty;

    /// <summary>Whether the workspace is currently active.</summary>
    public bool IsActive { get; set; }
}
