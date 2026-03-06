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
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    // Navigation
    public Kpi Kpi { get; set; } = null!;
}
