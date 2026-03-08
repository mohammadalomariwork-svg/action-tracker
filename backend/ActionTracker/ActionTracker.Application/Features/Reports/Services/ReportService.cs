using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.ActionItems.Mappers;
using ActionTracker.Application.Features.Dashboard.DTOs;
using ActionTracker.Application.Features.Dashboard.Interfaces;
using ActionTracker.Application.Features.Reports.DTOs;
using ActionTracker.Application.Features.Reports.Interfaces;
using ActionTracker.Application.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Application.Features.Reports.Services;

public class ReportService : IReportService
{
    private readonly IAppDbContext _dbContext;
    private readonly IDashboardService _dashboardService;
    private readonly CsvExportHelper _csvExportHelper;
    private readonly ILogger<ReportService> _logger;

    public ReportService(
        IAppDbContext dbContext,
        IDashboardService dashboardService,
        CsvExportHelper csvExportHelper,
        ILogger<ReportService> logger)
    {
        _dbContext        = dbContext;
        _dashboardService = dashboardService;
        _csvExportHelper  = csvExportHelper;
        _logger           = logger;
    }

    public async Task<byte[]> ExportToCsvAsync(ExportRequestDto filter, CancellationToken ct)
    {
        var query = _dbContext.ActionItems
            .Include(a => a.Workspace)
            .Include(a => a.Assignees).ThenInclude(aa => aa.User)
            .AsQueryable();

        if (filter.Status.HasValue)
            query = query.Where(a => a.Status == filter.Status.Value);

        if (filter.Priority.HasValue)
            query = query.Where(a => a.Priority == filter.Priority.Value);

        if (!string.IsNullOrWhiteSpace(filter.AssigneeId))
            query = query.Where(a => a.Assignees.Any(aa => aa.UserId == filter.AssigneeId));

        if (filter.DateFrom.HasValue)
            query = query.Where(a => a.DueDate >= filter.DateFrom.Value);

        if (filter.DateTo.HasValue)
            query = query.Where(a => a.DueDate <= filter.DateTo.Value);

        var items = await query
            .OrderBy(a => a.DueDate)
            .ThenBy(a => a.Priority)
            .ToListAsync(ct);

        var dtos = items.Select(ActionItemMapper.ToDto).ToList();

        _logger.LogInformation(
            "Exporting {Count} action items to CSV with filters: Status={Status}, Priority={Priority}",
            dtos.Count, filter.Status, filter.Priority);

        return await _csvExportHelper.ExportActionItemsToCsvAsync(dtos, ct);
    }

    public Task<DashboardKpiDto> GetSummaryStatisticsAsync(CancellationToken ct)
        => _dashboardService.GetKpisAsync(ct);
}
