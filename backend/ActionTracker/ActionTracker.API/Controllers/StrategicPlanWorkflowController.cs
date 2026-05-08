using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.StrategicPlans.Workflow;
using ActionTracker.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

[ApiController]
[Route("api/strategic-plans/workflow")]
[Authorize(Policy = "LocalOrAzureAD")]
public class StrategicPlanWorkflowController : ControllerBase
{
    private readonly IStrategicPlanWorkflowService _workflow;
    private readonly ILogger<StrategicPlanWorkflowController> _logger;

    public StrategicPlanWorkflowController(
        IStrategicPlanWorkflowService workflow,
        ILogger<StrategicPlanWorkflowController> logger)
    {
        _workflow = workflow;
        _logger = logger;
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    /// <summary>Submit a Draft strategic plan for initial approval.</summary>
    [HttpPost("submit")]
    [Authorize(Policy = PermissionPolicies.StrategicPlansEdit)]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitStrategicPlanApprovalRequestDto dto,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _workflow.SubmitAsync(dto, CurrentUserId, ct);
            return Ok(ApiResponse<StrategicPlanApprovalRequestDto>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<string>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<string>.Fail(ex.Message));
        }
    }

    /// <summary>Open a Revision request against an Approved plan (CR-001-E).</summary>
    [HttpPost("request-revision")]
    [Authorize(Policy = PermissionPolicies.StrategicPlansEdit)]
    public async Task<IActionResult> RequestRevision(
        [FromBody] RequestStrategicPlanRevisionDto dto,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _workflow.RequestRevisionAsync(dto, CurrentUserId, ct);
            return Ok(ApiResponse<StrategicPlanApprovalRequestDto>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<string>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<string>.Fail(ex.Message));
        }
    }

    /// <summary>Approve or reject a pending request (Initial or Revision).</summary>
    [HttpPut("requests/{requestId:guid}/review")]
    [Authorize(Policy = PermissionPolicies.StrategicPlansView)]
    public async Task<IActionResult> Review(
        Guid requestId,
        [FromBody] ReviewStrategicPlanApprovalRequestDto dto,
        CancellationToken ct = default)
    {
        dto.RequestId = requestId;
        try
        {
            var result = await _workflow.ReviewAsync(dto, CurrentUserId, ct);
            return Ok(ApiResponse<StrategicPlanApprovalRequestDto>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<string>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<string>.Fail(ex.Message));
        }
    }

    /// <summary>List all approval requests (history) for a plan.</summary>
    [HttpGet("plan/{planId:guid}")]
    [Authorize(Policy = PermissionPolicies.StrategicPlansView)]
    public async Task<IActionResult> GetForPlan(Guid planId, CancellationToken ct = default)
    {
        var rows = await _workflow.GetForPlanAsync(planId, ct);
        return Ok(ApiResponse<List<StrategicPlanApprovalRequestDto>>.Ok(rows));
    }

    /// <summary>Get pending requests where the current user is the assigned reviewer.</summary>
    [HttpGet("pending-reviews")]
    [Authorize(Policy = PermissionPolicies.StrategicPlansView)]
    public async Task<IActionResult> GetPendingReviews(CancellationToken ct = default)
    {
        var rows = await _workflow.GetPendingReviewsAsync(CurrentUserId, ct);
        return Ok(ApiResponse<List<StrategicPlanApprovalRequestDto>>.Ok(rows));
    }

    /// <summary>Get requests submitted by the current user.</summary>
    [HttpGet("my-requests")]
    [Authorize(Policy = PermissionPolicies.StrategicPlansView)]
    public async Task<IActionResult> GetMyRequests(CancellationToken ct = default)
    {
        var rows = await _workflow.GetMyRequestsAsync(CurrentUserId, ct);
        return Ok(ApiResponse<List<StrategicPlanApprovalRequestDto>>.Ok(rows));
    }

    /// <summary>Pending count for the header badge aggregator.</summary>
    [HttpGet("pending-summary")]
    [Authorize(Policy = PermissionPolicies.StrategicPlansView)]
    public async Task<IActionResult> GetPendingSummary(CancellationToken ct = default)
    {
        var summary = await _workflow.GetPendingSummaryAsync(CurrentUserId, ct);
        return Ok(ApiResponse<StrategicPlanApprovalSummaryDto>.Ok(summary));
    }

    /// <summary>Whether the current user can review the plan's latest pending request.</summary>
    [HttpGet("can-review/{planId:guid}")]
    [Authorize(Policy = PermissionPolicies.StrategicPlansView)]
    public async Task<IActionResult> CanReview(Guid planId, CancellationToken ct = default)
    {
        var canReview = await _workflow.CanReviewAsync(planId, CurrentUserId, ct);
        return Ok(ApiResponse<object>.Ok(new { canReview }));
    }

    /// <summary>Lock state for the plan (CR-001-E).</summary>
    [HttpGet("lock-state/{planId:guid}")]
    [Authorize(Policy = PermissionPolicies.StrategicPlansView)]
    public async Task<IActionResult> GetLockState(Guid planId, CancellationToken ct = default)
    {
        try
        {
            var state = await _workflow.GetLockStateAsync(planId, ct);
            return Ok(ApiResponse<StrategicPlanLockStateDto>.Ok(state));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
    }
}
