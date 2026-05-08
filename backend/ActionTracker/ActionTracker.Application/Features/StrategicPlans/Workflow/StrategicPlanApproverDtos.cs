using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Features.StrategicPlans.Workflow;

/// <summary>
/// One row of a strategic plan's configured approval chain.
/// </summary>
public class StrategicPlanApproverDto
{
    public Guid Id { get; set; }
    public Guid StrategicPlanId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? OrgUnitName { get; set; }
    public int Order { get; set; }
}

/// <summary>
/// Replace-the-chain payload. The service treats <see cref="Approvers"/> as the
/// authoritative new ordering — existing rows are wiped and re-inserted in one
/// transaction. Reorder client-side, then PUT.
/// </summary>
public class UpsertStrategicPlanApproversRequestDto
{
    [Required]
    public List<UpsertStrategicPlanApproverItemDto> Approvers { get; set; } = new();
}

public class UpsertStrategicPlanApproverItemDto
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    /// <summary>1-based. Server normalises gaps and duplicates; the only real
    /// requirement is "smaller value = earlier in the chain".</summary>
    public int Order { get; set; }
}
