namespace ActionTracker.Domain.Entities;

public class WorkspaceAdmin
{
    public int Id { get; set; }

    public Guid WorkspaceId { get; set; }

    public string AdminUserId { get; set; } = string.Empty;

    public string AdminUserName { get; set; } = string.Empty;

    public Workspace Workspace { get; set; } = null!;
}
