namespace ActionTracker.Application.Features.Dashboard.DTOs;

public class StatusBreakdownDto
{
    public string  Status     { get; set; } = string.Empty;
    public int     Count      { get; set; }
    public decimal Percentage { get; set; }
    public string  Color      { get; set; } = string.Empty;  // hex colour
}
