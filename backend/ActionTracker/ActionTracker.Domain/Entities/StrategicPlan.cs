using ActionTracker.Domain.Enums;

namespace ActionTracker.Domain.Entities;

public class StrategicPlan
{
    public Guid Id { get; set; }
    public Guid OrgUnitId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int StartYear { get; set; }
    public int DurationYears { get; set; }

    public int EndYear => StartYear + DurationYears - 1;

    public StrategicPlanStatus Status { get; set; } = StrategicPlanStatus.Draft;

    public DateTime? ApprovedAtUtc { get; set; }
    public string? ApprovedByUserId { get; set; }

    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public string? DeletedBy { get; set; }

    // Navigation
    public OrgUnit OrgUnit { get; set; } = null!;
    public ICollection<StrategicObjective> Objectives { get; set; } = new List<StrategicObjective>();
    public ICollection<StrategicPlanApprover> Approvers { get; set; } = new List<StrategicPlanApprover>();
}
