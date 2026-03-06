namespace ActionTracker.Application.Features.Workspaces.DTOs;

/// <summary>
/// Lightweight workspace representation used in list views.
/// Omits audit timestamps and the admin user ID to reduce payload size.
/// </summary>
public class WorkspaceListDto
{
    /// <summary>Primary key of the workspace.</summary>
    public int Id { get; set; }

    /// <summary>Human-readable title of the workspace.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Name of the organisational unit this workspace belongs to.</summary>
    public string OrganizationUnit { get; set; } = string.Empty;

    /// <summary>Display name of the workspace admin.</summary>
    public string AdminUserName { get; set; } = string.Empty;

    /// <summary>Whether the workspace is currently active.</summary>
    public bool IsActive { get; set; }
}
