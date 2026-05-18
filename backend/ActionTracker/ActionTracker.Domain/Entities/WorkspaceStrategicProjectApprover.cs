namespace ActionTracker.Domain.Entities;

/// <summary>
/// One row per configured approver in a workspace's sequential strategic-project
/// approval chain. Mirrors <see cref="StrategicPlanApprover"/>: <see cref="Order"/>
/// is 1-based and dense — gaps are normalised by the service when the chain is
/// updated. Every strategic project in the workspace uses this same chain at
/// submit time: a single <see cref="ProjectApprovalRequest"/> is created for
/// level 1, and on each Approve the workflow advances to the next level.
/// Operational projects bypass this chain entirely (the PM has full authority).
/// </summary>
public class WorkspaceStrategicProjectApprover
{
    public int Id { get; set; }

    public Guid WorkspaceId { get; set; }

    /// <summary>The Identity user id of the approver at this position.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Snapshot of the user's display name at the time they were added.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>1-based position in the sequential chain.</summary>
    public int Order { get; set; }

    public DateTime AddedAt { get; set; }

    public string? AddedByUserId { get; set; }

    public Workspace Workspace { get; set; } = null!;
}
