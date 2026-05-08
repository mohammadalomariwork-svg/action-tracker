namespace ActionTracker.Domain.Entities;

public class OrgUnit
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Code { get; set; }
    public int Level { get; set; }
    public Guid? ParentId { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public string? DeletedBy { get; set; }

    /// <summary>
    /// Optional FK to the user designated as this org unit's head. Heads at
    /// level 3 act as the strategic-plan / KPI-actuals approver for their
    /// subtree (CR-001-H). Null when no head is configured.
    /// </summary>
    public string? HeadUserId { get; set; }

    // Navigation
    public OrgUnit? Parent { get; set; }
    public ICollection<OrgUnit> Children { get; set; } = new List<OrgUnit>();
    public ICollection<StrategicObjective> StrategicObjectives { get; set; } = new List<StrategicObjective>();
    public ICollection<StrategicPlan> StrategicPlans { get; set; } = new List<StrategicPlan>();
    public ApplicationUser? HeadUser { get; set; }
}
