using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.KpiActuals.DTOs;
using ActionTracker.Application.Features.KpiActuals.Interfaces;
using ActionTracker.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

[ApiController]
[Route("api/kpis/actuals/workflow")]
[Authorize(Policy = "LocalOrAzureAD")]
public class KpiActualsWorkflowController : ControllerBase
{
    private readonly IKpiActualWorkflowService _workflow;
    private readonly ILogger<KpiActualsWorkflowController> _logger;

    public KpiActualsWorkflowController(
        IKpiActualWorkflowService workflow,
        ILogger<KpiActualsWorkflowController> logger)
    {
        _workflow = workflow;
        _logger = logger;
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpPost("submit")]
    [Authorize(Policy = PermissionPolicies.KpiActualsCreate)]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitKpiActualRequestDto dto, CancellationToken ct = default)
    {
        try
        {
            var result = await _workflow.SubmitAsync(dto, CurrentUserId, ct);
            return Ok(ApiResponse<KpiActualDto>.Ok(result));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse<string>.Fail(ex.Message)); }
        catch (ArgumentException ex) { return BadRequest(ApiResponse<string>.Fail(ex.Message)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<string>.Fail(ex.Message)); }
        catch (InvalidOperationException ex) { return Conflict(ApiResponse<string>.Fail(ex.Message)); }
    }

    [HttpPut("requests/{actualId:guid}/review")]
    [Authorize(Policy = PermissionPolicies.KpiActualsView)]
    public async Task<IActionResult> Review(
        Guid actualId,
        [FromBody] ReviewKpiActualRequestDto dto,
        CancellationToken ct = default)
    {
        dto.ActualId = actualId;
        try
        {
            var result = await _workflow.ReviewAsync(dto, CurrentUserId, ct);
            return Ok(ApiResponse<KpiActualDto>.Ok(result));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse<string>.Fail(ex.Message)); }
        catch (ArgumentException ex) { return BadRequest(ApiResponse<string>.Fail(ex.Message)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<string>.Fail(ex.Message)); }
        catch (InvalidOperationException ex) { return Conflict(ApiResponse<string>.Fail(ex.Message)); }
    }

    [HttpGet("pending-reviews")]
    [Authorize(Policy = PermissionPolicies.KpiActualsView)]
    public async Task<IActionResult> GetPendingReviews(CancellationToken ct = default)
    {
        var rows = await _workflow.GetPendingReviewsAsync(CurrentUserId, ct);
        return Ok(ApiResponse<List<KpiActualDto>>.Ok(rows));
    }

    [HttpGet("my-requests")]
    [Authorize(Policy = PermissionPolicies.KpiActualsView)]
    public async Task<IActionResult> GetMyRequests(CancellationToken ct = default)
    {
        var rows = await _workflow.GetMyRequestsAsync(CurrentUserId, ct);
        return Ok(ApiResponse<List<KpiActualDto>>.Ok(rows));
    }

    [HttpGet("pending-summary")]
    [Authorize(Policy = PermissionPolicies.KpiActualsView)]
    public async Task<IActionResult> GetPendingSummary(CancellationToken ct = default)
    {
        var summary = await _workflow.GetPendingSummaryAsync(CurrentUserId, ct);
        return Ok(ApiResponse<KpiActualSummaryDto>.Ok(summary));
    }

    [HttpGet("can-review/{actualId:guid}")]
    [Authorize(Policy = PermissionPolicies.KpiActualsView)]
    public async Task<IActionResult> CanReview(Guid actualId, CancellationToken ct = default)
    {
        var canReview = await _workflow.CanReviewAsync(actualId, CurrentUserId, ct);
        return Ok(ApiResponse<object>.Ok(new { canReview }));
    }
}
