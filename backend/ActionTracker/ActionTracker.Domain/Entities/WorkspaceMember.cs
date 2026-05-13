namespace ActionTracker.Domain.Entities;

/// <summary>
/// Join table assigning a regular user to a workspace.
/// A user listed here gains access to the workspace's projects and action items
/// regardless of their org-unit scoping.
/// </summary>
public class WorkspaceMember
{
    public int Id { get; set; }

    public Guid WorkspaceId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public DateTime AddedAt { get; set; }

    public string? AddedByUserId { get; set; }

    public Workspace Workspace { get; set; } = null!;
}
