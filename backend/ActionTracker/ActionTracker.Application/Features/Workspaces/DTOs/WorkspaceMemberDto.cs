namespace ActionTracker.Application.Features.Workspaces.DTOs;

/// <summary>
/// Represents a regular user assigned to a workspace.
/// </summary>
public class WorkspaceMemberDto
{
    /// <summary>AspNetUsers.Id of the member.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Display name of the member.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Email of the member (lazy-populated for the detail view).</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Organisation unit name of the member (lazy-populated for the detail view).</summary>
    public string OrgUnitName { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the user was added to the workspace.</summary>
    public DateTime AddedAt { get; set; }
}
