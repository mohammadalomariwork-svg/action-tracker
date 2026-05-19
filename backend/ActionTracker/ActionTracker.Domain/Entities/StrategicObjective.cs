namespace ActionTracker.Domain.Entities;

public class StrategicObjective
{
    public Guid Id { get; set; }
    public string ObjectiveCode { get; set; } = string.Empty;
    public string Statement { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid OrgUnitId { get; set; }

    /// <summary>FK to the parent StrategicPlan. Nullable during the migration window;
    /// will be tightened to non-nullable in a follow-up migration after the data backfill runs.</summary>
    public Guid? StrategicPlanId { get; set; }

    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public string? DeletedBy { get; set; }

    // ── Draft / revision (CR-001-G) ─────────────────────────────────────────
    /// <summary>True when this row belongs to a draft plan graph. Hidden from
    /// live queries via a global filter.</summary>
    public bool IsDraft { get; set; } = false;

    /// <summary>On a draft objective that was cloned from the published plan,
    /// points back to the published row so the promote step can apply field
    /// updates in place. Null for objectives added newly in the draft.</summary>
    public Guid? OriginalObjectiveId { get; set; }

    // Navigation
    public OrgUnit OrgUnit { get; set; } = null!;
    public StrategicPlan? StrategicPlan { get; set; }
    public StrategicObjective? OriginalObjective { get; set; }
    public ICollection<Kpi> Kpis { get; set; } = new List<Kpi>();
}
