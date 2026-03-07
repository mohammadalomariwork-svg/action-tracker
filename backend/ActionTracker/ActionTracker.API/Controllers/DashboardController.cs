using ActionTracker.API.Models;
using ActionTracker.Application.Features.Dashboard.DTOs;
using ActionTracker.Application.Features.Dashboard.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(AuthenticationSchemes = "LocalBearer,AzureAD")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(IDashboardService dashboardService, ILogger<DashboardController> logger)
    {
        _dashboardService = dashboardService;
        _logger           = logger;
    }

    // -------------------------------------------------------------------------
    // GET api/dashboard/kpis   [Admin, Manager]
    // -------------------------------------------------------------------------

    /// <summary>Returns high-level KPI metrics. Restricted to Admin and Manager roles.</summary>
    [HttpGet("kpis")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(ApiResponse<DashboardKpiDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<DashboardKpiDto>>> GetKpis(CancellationToken ct)
    {
        var kpis = await _dashboardService.GetKpisAsync(ct);

        _logger.LogInformation("KPI data requested");

        return Ok(ApiResponse<DashboardKpiDto>.Ok(kpis));
    }

    // -------------------------------------------------------------------------
    // GET api/dashboard/management   [Admin, Manager]
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the full management dashboard: KPIs, status breakdown, team workload,
    /// at-risk items, recent activity, and critical actions.
    /// Restricted to Admin and Manager roles.
    /// </summary>
    [HttpGet("management")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(ApiResponse<ManagementDashboardDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ManagementDashboardDto>>> GetManagementDashboard(
        CancellationToken ct)
    {
        var dashboard = await _dashboardService.GetManagementDashboardAsync(ct);

        _logger.LogInformation("Management dashboard requested");

        return Ok(ApiResponse<ManagementDashboardDto>.Ok(dashboard));
    }

    // -------------------------------------------------------------------------
    // GET api/dashboard/team-workload   [All authenticated users]
    // -------------------------------------------------------------------------

    /// <summary>Returns per-user workload statistics. Available to all authenticated users.</summary>
    [HttpGet("team-workload")]
    [ProducesResponseType(typeof(ApiResponse<List<TeamWorkloadDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<TeamWorkloadDto>>>> GetTeamWorkload(
        CancellationToken ct)
    {
        var workload = await _dashboardService.GetTeamWorkloadAsync(ct);

        _logger.LogInformation("Team workload data requested");

        return Ok(ApiResponse<List<TeamWorkloadDto>>.Ok(workload));
    }

    // -------------------------------------------------------------------------
    // GET api/dashboard/status-breakdown   [All authenticated users]
    // -------------------------------------------------------------------------

    /// <summary>Returns action item counts grouped by status. Available to all authenticated users.</summary>
    [HttpGet("status-breakdown")]
    [ProducesResponseType(typeof(ApiResponse<List<StatusBreakdownDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<StatusBreakdownDto>>>> GetStatusBreakdown(
        CancellationToken ct)
    {
        var breakdown = await _dashboardService.GetStatusBreakdownAsync(ct);

        _logger.LogInformation("Status breakdown data requested");

        return Ok(ApiResponse<List<StatusBreakdownDto>>.Ok(breakdown));
    }
}
