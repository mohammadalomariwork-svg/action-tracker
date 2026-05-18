namespace ActionTracker.Domain.Entities;

/// <summary>
/// Join table listing the users eligible to be picked as Project Manager for
/// projects created in a workspace. The Project Manager dropdown in the
/// project form is restricted to this list; saving a project whose
/// <see cref="Project.ProjectManagerUserId"/> isn't on the list is rejected.
/// </summary>
public class WorkspaceProjectManager
{
    public int Id { get; set; }

    public Guid WorkspaceId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public DateTime AddedAt { get; set; }

    public string? AddedByUserId { get; set; }

    public Workspace Workspace { get; set; } = null!;
}
