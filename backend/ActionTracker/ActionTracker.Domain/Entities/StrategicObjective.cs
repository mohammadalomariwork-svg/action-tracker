namespace ActionTracker.Domain.Entities;

public class StrategicObjective
{
    public Guid Id { get; set; }
    public string ObjectiveCode { get; set; } = string.Empty;
    public string Statement { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid OrgUnitId { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation
    public OrgUnit OrgUnit { get; set; } = null!;
    public ICollection<Kpi> Kpis { get; set; } = new List<Kpi>();
}
