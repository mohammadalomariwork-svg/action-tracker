using ActionTracker.Domain.Enums;

namespace ActionTracker.Domain.Entities;

public class KpiTarget
{
    public Guid Id { get; set; }
    public Guid KpiId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal? Target { get; set; }
    public decimal? Actual { get; set; }
    public string? Notes { get; set; }

    /// <summary>Reference value used to compute progress.</summary>
    public decimal? Baseline { get; set; }

    /// <summary>Free text up to 500 chars (e.g. "BI Dashboard / SLA Tracker").</summary>
    public string? DataSource { get; set; }

    /// <summary>Up to 2000 chars — target-specific calculation notes.</summary>
    public string? CalculationNotes { get; set; }

    /// <summary>Direction this KPI moves to indicate good performance. Defaults to HigherIsBetter.</summary>
    public KpiDirection Direction { get; set; } = KpiDirection.HigherIsBetter;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    // Navigation
    public Kpi Kpi { get; set; } = null!;
}
