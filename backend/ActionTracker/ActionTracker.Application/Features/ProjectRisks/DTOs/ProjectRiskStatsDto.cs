namespace ActionTracker.Application.Features.ProjectRisks.DTOs;

public class ProjectRiskStatsDto
{
    public int TotalRisks { get; set; }
    public int OpenRisks { get; set; }
    public int CriticalCount { get; set; }
    public int HighCount { get; set; }
    public int MediumCount { get; set; }
    public int LowCount { get; set; }
    public int ClosedCount { get; set; }
    public int OverdueCount { get; set; }
}
