namespace ActionTracker.Application.Features.Workspaces.DTOs;

/// <summary>
/// Represents a single admin user within a workspace payload.
/// </summary>
public class WorkspaceAdminDto
{
    /// <summary>The AspNetUsers.Id of the admin user.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Display name of the admin user (denormalised for fast reads).</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Email address of the admin user.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Name of the organisation unit the admin user belongs to.</summary>
    public string OrgUnitName { get; set; } = string.Empty;
}
