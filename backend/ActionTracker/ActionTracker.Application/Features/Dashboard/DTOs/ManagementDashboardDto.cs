using ActionTracker.Application.Features.ActionItems.DTOs;

namespace ActionTracker.Application.Features.Dashboard.DTOs;

public class ManagementDashboardDto
{
    public DashboardKpiDto           Kpis            { get; set; } = null!;
    public List<StatusBreakdownDto>  StatusBreakdown { get; set; } = new();
    public List<TeamWorkloadDto>     TeamWorkload    { get; set; } = new();

    /// <summary>At-risk / overdue items — max 5, ordered by urgency.</summary>
    public List<AtRiskItemDto>       AtRiskItems     { get; set; } = new();

    /// <summary>Most recently created items — max 5.</summary>
    public List<RecentActivityDto>   RecentActivity  { get; set; } = new();

    /// <summary>Critical/High priority items not yet Done.</summary>
    public List<ActionItemResponseDto> CriticalActions { get; set; } = new();
}
