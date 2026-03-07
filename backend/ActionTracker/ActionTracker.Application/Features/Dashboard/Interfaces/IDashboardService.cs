using ActionTracker.Application.Features.Dashboard.DTOs;

namespace ActionTracker.Application.Features.Dashboard.Interfaces;

public interface IDashboardService
{
    Task<DashboardKpiDto>          GetKpisAsync(CancellationToken ct);
    Task<ManagementDashboardDto>   GetManagementDashboardAsync(CancellationToken ct);
    Task<List<TeamWorkloadDto>>    GetTeamWorkloadAsync(CancellationToken ct);
    Task<List<StatusBreakdownDto>> GetStatusBreakdownAsync(CancellationToken ct);
}
