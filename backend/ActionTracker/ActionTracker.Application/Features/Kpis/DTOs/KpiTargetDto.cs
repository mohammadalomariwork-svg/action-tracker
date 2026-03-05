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
}
