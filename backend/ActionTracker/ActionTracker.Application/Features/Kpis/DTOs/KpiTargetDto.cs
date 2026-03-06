namespace ActionTracker.Application.Features.Kpis.DTOs;

public class KpiTargetDto
{
    public Guid Id { get; set; }
    public Guid KpiId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public decimal? Target { get; set; }
    public decimal? Actual { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public string? CreatedByName { get; set; }
    public string? UpdatedByName { get; set; }
}
