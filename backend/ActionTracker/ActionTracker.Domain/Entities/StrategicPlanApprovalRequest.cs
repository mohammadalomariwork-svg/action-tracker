using ActionTracker.Domain.Enums;

namespace ActionTracker.Domain.Entities;

/// <summary>
/// Approval request raised against a <see cref="StrategicPlan"/>. Mirrors the
/// shape of <see cref="ProjectApprovalRequest"/>. Both Initial submissions
/// (Draft → PendingApproval → Approved) and post-approval Revision cycles
/// (CR-001-E) reuse this entity, distinguished by <see cref="RequestType"/>.
/// </summary>
public class StrategicPlanApprovalRequest
{
    public Guid Id { get; set; }

    public Guid StrategicPlanId { get; set; }

    public StrategicPlanApprovalRequestType RequestType { get; set; }
        = StrategicPlanApprovalRequestType.Initial;

    public string RequestedByUserId { get; set; } = string.Empty;
    public string RequestedByDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// User who actually reviewed the request (captured at review time).
    /// May differ from <see cref="AssignedReviewerUserId"/> if an Admin
    /// intervened.
    /// </summary>
    public string? ReviewedByUserId { get; set; }
    public string? ReviewedByDisplayName { get; set; }

    /// <summary>
    /// User who is expected to review the request (resolved at submit
    /// time via <c>IApprovalAuthorityResolver</c>). Drives the My
    /// Approvals → Pending Reviews list.
    /// </summary>
    public string? AssignedReviewerUserId { get; set; }
    public string? AssignedReviewerDisplayName { get; set; }

    public StrategicPlanApprovalStatus Status { get; set; }
        = StrategicPlanApprovalStatus.Pending;

    /// <summary>
    /// 1-based position in the plan's sequential approval chain. Always 1 for
    /// plans that fall back to the legacy single-level resolver (no configured
    /// approvers). Each level is its own row; the workflow advances by creating
    /// a new request with <c>Level + 1</c> when the current level approves.
    /// </summary>
    public int Level { get; set; } = 1;

    /// <summary>
    /// Snapshot of the chain length at submit time. Stays constant across the
    /// rows for a single Submit / Revision cycle, so consumers can render
    /// "Level 2 of 4" without re-reading the configured approvers (which may
    /// have been edited after submit on a future Draft).
    /// </summary>
    public int TotalLevels { get; set; } = 1;

    public string Justification { get; set; } = string.Empty;
    public string? ReviewerComment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }

    public bool IsDeleted { get; set; } = false;

    // Navigation
    public StrategicPlan StrategicPlan { get; set; } = null!;
}
