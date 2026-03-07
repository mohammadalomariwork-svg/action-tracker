using ActionTracker.Application.Features.Dashboard.DTOs;
using ActionTracker.Application.Features.Reports.DTOs;

namespace ActionTracker.Application.Features.Reports.Interfaces;

public interface IReportService
{
    Task<byte[]>         ExportToCsvAsync(ExportRequestDto filter, CancellationToken ct);
    Task<DashboardKpiDto> GetSummaryStatisticsAsync(CancellationToken ct);
}
