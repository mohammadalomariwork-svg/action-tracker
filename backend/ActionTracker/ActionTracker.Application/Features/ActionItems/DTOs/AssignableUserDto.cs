namespace ActionTracker.Application.Features.ActionItems.DTOs;

/// <summary>
/// A user eligible to be assigned to an action item in a given workspace.
/// Returned by <c>GET /api/workspaces/{id}/assignable-users</c> as the union of
/// the workspace's admins, members, and project managers.
/// </summary>
public class AssignableUserDto
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? OrgUnitName { get; set; }

    /// <summary>
    /// Why this user qualifies in the workspace. Any of: "Admin", "Member", "ProjectManager".
    /// A user may carry multiple sources if they hold more than one role in the workspace.
    /// </summary>
    public List<string> Sources { get; set; } = new();
}
