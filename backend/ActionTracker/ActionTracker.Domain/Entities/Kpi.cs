namespace ActionTracker.Domain.Entities;

public enum MeasurementPeriod
{
    Monthly    = 1,
    Quarterly  = 2,
    SemiAnnual = 3,
    Yearly     = 4,
}

public class Kpi
{
    public Guid Id { get; set; }
    public int KpiNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CalculationMethod { get; set; } = string.Empty;
    public MeasurementPeriod Period { get; set; }
    public string? Unit { get; set; }
    public Guid StrategicObjectiveId { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public string? DeletedBy { get; set; }

    // Navigation
    public StrategicObjective StrategicObjective { get; set; } = null!;
    public ICollection<KpiTarget> Targets { get; set; } = new List<KpiTarget>();
}
