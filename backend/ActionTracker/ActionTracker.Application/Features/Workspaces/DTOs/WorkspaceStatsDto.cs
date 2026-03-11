namespace ActionTracker.Application.Features.Workspaces.DTOs;

/// <summary>
/// Per-workspace statistics for the workspace detail dashboard cards.
/// </summary>
public class WorkspaceStatsDto
{
    public int TotalProjects { get; set; }
    public int StrategicProjects { get; set; }
    public int BaselinedProjects { get; set; }
    public int NonBaselinedProjects { get; set; }
    public decimal ProjectCompletionRate { get; set; }
    public decimal ProjectOnTimeDeliveryRate { get; set; }
    public int TotalActionItems { get; set; }
    public int StandaloneActionItems { get; set; }
    public int EscalatedActionItems { get; set; }
    public decimal StandaloneCompletionRate { get; set; }
    public decimal StandaloneOnTimeDeliveryRate { get; set; }
}
