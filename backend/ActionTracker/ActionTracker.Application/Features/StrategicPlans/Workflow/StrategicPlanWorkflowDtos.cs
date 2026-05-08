using ActionTracker.Domain.Enums;

namespace ActionTracker.Application.Features.StrategicPlans.Workflow;

public class SubmitStrategicPlanApprovalRequestDto
{
    public Guid PlanId { get; set; }
    public string Justification { get; set; } = string.Empty;
}

public class RequestStrategicPlanRevisionDto
{
    public Guid PlanId { get; set; }
    public string Justification { get; set; } = string.Empty;
}

public class ReviewStrategicPlanApprovalRequestDto
{
    public Guid RequestId { get; set; }
    public bool IsApproved { get; set; }
    public string? ReviewComment { get; set; }
}

public class StrategicPlanApprovalRequestDto
{
    public Guid Id { get; set; }
    public Guid StrategicPlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public Guid OrgUnitId { get; set; }
    public string OrgUnitName { get; set; } = string.Empty;
    public StrategicPlanApprovalRequestType RequestType { get; set; }
    public string RequestTypeName => RequestType.ToString();
    public StrategicPlanApprovalStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string RequestedByUserId { get; set; } = string.Empty;
    public string RequestedByDisplayName { get; set; } = string.Empty;
    public string? AssignedReviewerUserId { get; set; }
    public string? AssignedReviewerDisplayName { get; set; }
    public string? ReviewedByUserId { get; set; }
    public string? ReviewedByDisplayName { get; set; }
    public string Justification { get; set; } = string.Empty;
    public string? ReviewerComment { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }

    /// <summary>1-based position in the chain (always 1 for legacy single-level plans).</summary>
    public int Level { get; set; } = 1;
    /// <summary>Snapshot of the chain length at submit time (1 for legacy single-level plans).</summary>
    public int TotalLevels { get; set; } = 1;
}

public class StrategicPlanApprovalSummaryDto
{
    public int PendingPlanApprovals { get; set; }
}

public class StrategicPlanLockStateDto
{
    public Guid PlanId { get; set; }
    public StrategicPlanStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public bool IsLocked { get; set; }
    public Guid? PendingRevisionId { get; set; }
}
