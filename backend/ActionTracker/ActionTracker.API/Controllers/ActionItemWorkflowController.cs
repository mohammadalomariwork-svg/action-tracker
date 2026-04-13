using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.Workflow.DTOs;
using ActionTracker.Application.Features.Workflow.Interfaces;
using ActionTracker.Application.Helpers;
using ActionTracker.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

[ApiController]
[Route("api/action-items/workflow")]
[Authorize(AuthenticationSchemes = "LocalBearer,AzureAD")]
public class ActionItemWorkflowController : ControllerBase
{
    private readonly IActionItemWorkflowService _workflowService;
    private readonly ILogger<ActionItemWorkflowController> _logger;

    public ActionItemWorkflowController(
        IActionItemWorkflowService workflowService,
        ILogger<ActionItemWorkflowController> logger)
    {
        _workflowService = workflowService;
        _logger          = logger;
    }

    private string GetUserId() =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

    // -------------------------------------------------------------------------
    // POST api/action-items/workflow/date-change-request
    // -------------------------------------------------------------------------

    /// <summary>Creates a date change request for a standalone action item.</summary>
    [HttpPost("date-change-request")]
    [Authorize(Policy = PermissionPolicies.ActionItemsEdit)]
    [ProducesResponseType(typeof(ApiResponse<WorkflowRequestResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateDateChangeRequest(
        [FromBody] CreateDateChangeRequestDto dto)
    {
        var userId = GetUserId();
        _logger.LogInformation(
            "POST /api/action-items/workflow/date-change-request by {UserId} for ActionItem {ActionItemId}",
            userId, dto.ActionItemId);

        var result = await _workflowService.CreateDateChangeRequestAsync(dto, userId);
        return Ok(ApiResponse<WorkflowRequestResponseDto>.Ok(result));
    }

    // -------------------------------------------------------------------------
    // POST api/action-items/workflow/status-change-request
    // -------------------------------------------------------------------------

    /// <summary>Creates a status change request for a standalone action item.</summary>
    [HttpPost("status-change-request")]
    [Authorize(Policy = PermissionPolicies.ActionItemsEdit)]
    [ProducesResponseType(typeof(ApiResponse<WorkflowRequestResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateStatusChangeRequest(
        [FromBody] CreateStatusChangeRequestDto dto)
    {
        var userId = GetUserId();
        _logger.LogInformation(
            "POST /api/action-items/workflow/status-change-request by {UserId} for ActionItem {ActionItemId}",
            userId, dto.ActionItemId);

        var result = await _workflowService.CreateStatusChangeRequestAsync(dto, userId);
        return Ok(ApiResponse<WorkflowRequestResponseDto>.Ok(result));
    }

    // -------------------------------------------------------------------------
    // PUT api/action-items/workflow/requests/{requestId}/review
    // -------------------------------------------------------------------------

    /// <summary>Reviews (approve or reject) a pending workflow request.</summary>
    [HttpPut("requests/{requestId:guid}/review")]
    [Authorize(Policy = PermissionPolicies.ActionItemsEdit)]
    [ProducesResponseType(typeof(ApiResponse<WorkflowRequestResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReviewRequest(
        Guid requestId, [FromBody] ReviewWorkflowRequestDto dto)
    {
        var userId = GetUserId();
        _logger.LogInformation(
            "PUT /api/action-items/workflow/requests/{RequestId}/review by {UserId}",
            requestId, userId);

        var result = await _workflowService.ReviewRequestAsync(requestId, dto, userId);
        return Ok(ApiResponse<WorkflowRequestResponseDto>.Ok(result));
    }

    // -------------------------------------------------------------------------
    // GET api/action-items/workflow/pending-reviews
    // -------------------------------------------------------------------------

    /// <summary>Gets pending workflow requests for the current user to review.</summary>
    [HttpGet("pending-reviews")]
    [Authorize(Policy = PermissionPolicies.ActionItemsView)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<WorkflowRequestResponseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingReviews(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = GetUserId();
        var result = await _workflowService.GetPendingRequestsForReviewerAsync(userId, page, pageSize);
        return Ok(ApiResponse<PagedResult<WorkflowRequestResponseDto>>.Ok(result));
    }

    // -------------------------------------------------------------------------
    // GET api/action-items/workflow/my-requests
    // -------------------------------------------------------------------------

    /// <summary>Gets workflow requests submitted by the current user.</summary>
    [HttpGet("my-requests")]
    [Authorize(Policy = PermissionPolicies.ActionItemsView)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<WorkflowRequestResponseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyRequests(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = GetUserId();
        var result = await _workflowService.GetMyRequestsAsync(userId, page, pageSize);
        return Ok(ApiResponse<PagedResult<WorkflowRequestResponseDto>>.Ok(result));
    }

    // -------------------------------------------------------------------------
    // GET api/action-items/workflow/action-item/{actionItemId}
    // -------------------------------------------------------------------------

    /// <summary>Gets all workflow requests for a specific action item.</summary>
    [HttpGet("action-item/{actionItemId:guid}")]
    [Authorize(Policy = PermissionPolicies.ActionItemsView)]
    [ProducesResponseType(typeof(ApiResponse<List<WorkflowRequestResponseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRequestsForActionItem(Guid actionItemId)
    {
        var result = await _workflowService.GetRequestsForActionItemAsync(actionItemId);
        return Ok(ApiResponse<List<WorkflowRequestResponseDto>>.Ok(result));
    }

    // -------------------------------------------------------------------------
    // GET api/action-items/workflow/pending-summary
    // -------------------------------------------------------------------------

    /// <summary>Gets a summary of pending workflow request counts for the current user.</summary>
    [HttpGet("pending-summary")]
    [Authorize(Policy = PermissionPolicies.ActionItemsView)]
    [ProducesResponseType(typeof(ApiResponse<WorkflowRequestSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingSummary()
    {
        var userId = GetUserId();
        var result = await _workflowService.GetPendingSummaryAsync(userId);
        return Ok(ApiResponse<WorkflowRequestSummaryDto>.Ok(result));
    }

    // -------------------------------------------------------------------------
    // POST api/action-items/workflow/escalate
    // -------------------------------------------------------------------------

    /// <summary>Escalates an action item.</summary>
    [HttpPost("escalate")]
    [Authorize(Policy = PermissionPolicies.ActionItemsEdit)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Escalate([FromBody] EscalateActionItemDto dto)
    {
        var userId = GetUserId();
        _logger.LogInformation(
            "POST /api/action-items/workflow/escalate by {UserId} for ActionItem {ActionItemId}",
            userId, dto.ActionItemId);

        await _workflowService.HandleEscalationAsync(dto.ActionItemId, userId, dto.Reason);
        return Ok(ApiResponse<object>.Ok(null!, "Action item escalated successfully."));
    }

    // -------------------------------------------------------------------------
    // POST api/action-items/workflow/give-direction
    // -------------------------------------------------------------------------

    /// <summary>Gives direction on an escalated action item.</summary>
    [HttpPost("give-direction")]
    [Authorize(Policy = PermissionPolicies.ActionItemsEdit)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GiveDirection([FromBody] WorkflowDirectionDto dto)
    {
        var userId = GetUserId();
        _logger.LogInformation(
            "POST /api/action-items/workflow/give-direction by {UserId} for ActionItem {ActionItemId}",
            userId, dto.ActionItemId);

        await _workflowService.GiveDirectionAsync(dto, userId);
        return Ok(ApiResponse<object>.Ok(null!, "Direction given successfully."));
    }

    // -------------------------------------------------------------------------
    // GET api/action-items/workflow/can-review/{actionItemId}
    // -------------------------------------------------------------------------

    /// <summary>Checks if the current user can review workflow requests for an action item.</summary>
    [HttpGet("can-review/{actionItemId:guid}")]
    [Authorize(Policy = PermissionPolicies.ActionItemsView)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CanReview(Guid actionItemId)
    {
        var userId = GetUserId();
        var canReview = await _workflowService.CanUserReviewAsync(actionItemId, userId);
        return Ok(ApiResponse<bool>.Ok(canReview));
    }
}
