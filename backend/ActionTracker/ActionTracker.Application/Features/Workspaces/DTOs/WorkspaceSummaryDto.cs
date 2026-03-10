namespace ActionTracker.Application.Features.Workspaces.DTOs;

/// <summary>
/// Aggregate statistics across all workspaces for the dashboard cards.
/// </summary>
public class WorkspaceSummaryDto
{
    public int TotalWorkspaces { get; set; }
    public int ActiveWorkspaces { get; set; }
    public int StrategicProjects { get; set; }
    public int OperationalProjects { get; set; }
    public int StandaloneActionItems { get; set; }
    public int ProjectActionItems { get; set; }
    public int StrategicActionItems { get; set; }
}
