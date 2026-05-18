namespace ActionTracker.Application.Features.Workspaces.DTOs;

/// <summary>
/// A user eligible to be selected as Project Manager for projects in this
/// workspace. Returned by <c>GET /api/workspaces/{id}/project-managers</c>.
/// </summary>
public class WorkspaceProjectManagerDto
{
    public string UserId      { get; set; } = string.Empty;
    public string UserName    { get; set; } = string.Empty;
    public string Email       { get; set; } = string.Empty;
    public string OrgUnitName { get; set; } = string.Empty;
    public DateTime AddedAt   { get; set; }
}

/// <summary>Payload for POST /api/workspaces/{id}/project-managers.</summary>
public class AddWorkspaceProjectManagerDto
{
    public string UserId   { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
}
