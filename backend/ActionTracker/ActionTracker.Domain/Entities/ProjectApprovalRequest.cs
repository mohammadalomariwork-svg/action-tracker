using ActionTracker.Domain.Enums;

namespace ActionTracker.Domain.Entities;

public class ProjectApprovalRequest
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public string RequestedByUserId { get; set; } = string.Empty;
    public string RequestedByDisplayName { get; set; } = string.Empty;

    public string? ReviewedByUserId { get; set; }
    public string? ReviewedByDisplayName { get; set; }

    /// <summary>
    /// User who is expected to review the request (resolved at submit time
    /// from the workspace's <c>WorkspaceStrategicProjectApprover</c> chain).
    /// Drives the My Approvals → Project Approvals → Pending Reviews list.
    /// Null for legacy rows created before the workspace-chain rewire.
    /// </summary>
    public string? AssignedReviewerUserId { get; set; }
    public string? AssignedReviewerDisplayName { get; set; }

    public ProjectApprovalStatus Status { get; set; } = ProjectApprovalStatus.Pending;

    /// <summary>
    /// 1-based position in the workspace's sequential strategic-project approval
    /// chain. Each level is its own row; the workflow advances by creating a new
    /// request with <c>Level + 1</c> when the current level approves.
    /// </summary>
    public int Level { get; set; } = 1;

    /// <summary>
    /// Snapshot of the chain length at submit time. Stays constant across the
    /// rows for a single Submit cycle so consumers can render "Level 2 of 4"
    /// without re-reading the workspace's configured approvers.
    /// </summary>
    public int TotalLevels { get; set; } = 1;

    public string Reason { get; set; } = string.Empty;
    public string? ReviewComment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }

    public bool IsDeleted { get; set; } = false;

    // Navigation
    public Project Project { get; set; } = null!;
}
