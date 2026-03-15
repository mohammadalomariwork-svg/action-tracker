using ActionTracker.API.Models;
using ActionTracker.Application.Features.Dashboard.DTOs;
using ActionTracker.Application.Features.Reports.DTOs;
using ActionTracker.Application.Features.Reports.Interfaces;
using ActionTracker.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(AuthenticationSchemes = "LocalBearer,AzureAD")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(IReportService reportService, ILogger<ReportsController> logger)
    {
        _reportService = reportService;
        _logger        = logger;
    }

    // -------------------------------------------------------------------------
    // GET api/reports/export-csv
    // -------------------------------------------------------------------------

    /// <summary>
    /// Exports filtered action items to a UTF-8 CSV file (BOM-prefixed for Excel).
    /// </summary>
    [HttpGet("export-csv")]
    [Authorize(Policy = PermissionPolicies.ReportsExport)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] ExportRequestDto filter, CancellationToken ct)
    {
        var bytes    = await _reportService.ExportToCsvAsync(filter, ct);
        var fileName = $"action-tracker-{DateTime.UtcNow:yyyyMMdd}.csv";

        _logger.LogInformation(
            "CSV export downloaded: {FileName} ({Bytes} bytes)", fileName, bytes.Length);

        return File(bytes, "text/csv", fileName);
    }

    // -------------------------------------------------------------------------
    // GET api/reports/summary
    // -------------------------------------------------------------------------

    /// <summary>Returns high-level KPI summary statistics.</summary>
    [HttpGet("summary")]
    [Authorize(Policy = PermissionPolicies.ReportsView)]
    [ProducesResponseType(typeof(ApiResponse<DashboardKpiDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<DashboardKpiDto>>> GetSummary(CancellationToken ct)
    {
        var stats = await _reportService.GetSummaryStatisticsAsync(ct);

        _logger.LogInformation("Summary statistics requested");

        return Ok(ApiResponse<DashboardKpiDto>.Ok(stats));
    }
}
