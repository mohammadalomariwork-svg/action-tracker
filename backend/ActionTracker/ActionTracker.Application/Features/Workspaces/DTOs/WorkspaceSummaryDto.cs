namespace ActionTracker.Application.Features.Workspaces.DTOs;

/// <summary>
/// Aggregate statistics across all workspaces for the dashboard cards.
/// </summary>
public class WorkspaceSummaryDto
{
    public int TotalWorkspaces { get; set; }
    public int ActiveWorkspaces { get; set; }
    public int TotalAdmins { get; set; }
    public int TotalOpenActions { get; set; }
    public int NewThisMonth { get; set; }
}
