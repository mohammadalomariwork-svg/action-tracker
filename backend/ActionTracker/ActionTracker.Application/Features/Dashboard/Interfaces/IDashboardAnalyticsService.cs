using ActionTracker.Application.Features.Dashboard.DTOs;

namespace ActionTracker.Application.Features.Dashboard.Interfaces;

/// <summary>
/// Aggregations behind the Overview, Action Items, Projects and Strategy
/// dashboards. Unlike the older <see cref="IDashboardService"/>, every method
/// here is scoped to what the calling user may see (workspace membership for
/// action items and projects, strategic org-unit scope for strategy).
/// </summary>
public interface IDashboardAnalyticsService
{
    /// <summary>Action items assigned to the caller.</summary>
    Task<MyActionsDashboardDto> GetMyActionsAsync(CancellationToken ct);

    /// <summary>Per-person counts across the action items the caller can see, busiest first.</summary>
    Task<List<TeamMemberActivityDto>> GetTeamActivityAsync(CancellationToken ct);

    Task<ActionItemsDashboardDto> GetActionItemsAsync(CancellationToken ct);

    Task<ProjectsDashboardDto> GetProjectsAsync(CancellationToken ct);

    /// <param name="year">Year to measure KPI achievement for; defaults to the current year.</param>
    Task<StrategyDashboardDto> GetStrategyAsync(int? year, CancellationToken ct);
}
