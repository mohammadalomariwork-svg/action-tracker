namespace ActionTracker.Domain.Entities;

/// <summary>
/// One row per configured approver in a strategic plan's sequential approval
/// chain. Order is 1-based and dense — gaps are normalised by the service when
/// the chain is updated. The chain is consulted at submit time: a single
/// <see cref="StrategicPlanApprovalRequest"/> is created for level 1, and on
/// each Approve the workflow advances to the next level (or finalises the plan
/// when the last level approves). When no rows exist for a plan the workflow
/// falls back to the legacy single-level resolver (level-3 head).
/// </summary>
public class StrategicPlanApprover
{
    public Guid Id { get; set; }
    public Guid StrategicPlanId { get; set; }

    /// <summary>The Identity user id of the approver at this position.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>1-based position in the sequential chain.</summary>
    public int Order { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }

    // Navigation
    public StrategicPlan StrategicPlan { get; set; } = null!;
}
