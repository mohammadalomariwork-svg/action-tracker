namespace ActionTracker.Application.Features.Workspaces.DTOs;

/// <summary>
/// Full representation of a workspace returned from the API.
/// </summary>
public class WorkspaceResponseDto
{
    /// <summary>Primary key of the workspace.</summary>
    public int Id { get; set; }

    /// <summary>Human-readable title of the workspace.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Name of the organisational unit this workspace belongs to.</summary>
    public string OrganizationUnit { get; set; } = string.Empty;

    /// <summary>The <c>Id</c> of the AspNetUsers record for the workspace admin.</summary>
    public string AdminUserId { get; set; } = string.Empty;

    /// <summary>Display name of the workspace admin.</summary>
    public string AdminUserName { get; set; } = string.Empty;

    /// <summary>Whether the workspace is currently active.</summary>
    public bool IsActive { get; set; }

    /// <summary>UTC timestamp when the workspace was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the most recent update, or <c>null</c> if never updated.</summary>
    public DateTime? UpdatedAt { get; set; }
}
