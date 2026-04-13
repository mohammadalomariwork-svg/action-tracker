using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.Projects.DTOs;
using ActionTracker.Application.Features.Projects.Interfaces;
using ActionTracker.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

[ApiController]
[Route("api/projects/workflow")]
[Authorize(AuthenticationSchemes = "LocalBearer,AzureAD")]
public class ProjectWorkflowController : ControllerBase
{
    private readonly IProjectWorkflowService _workflowService;
    private readonly ILogger<ProjectWorkflowController> _logger;

    public ProjectWorkflowController(
        IProjectWorkflowService workflowService,
        ILogger<ProjectWorkflowController> logger)
    {
        _workflowService = workflowService;
        _logger = logger;
    }

    private string GetUserId() =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

    /// <summary>Submit a project for start-approval (Draft → PendingApproval).</summary>
    [HttpPost("submit")]
    [Authorize(Policy = PermissionPolicies.ProjectsEdit)]
    public async Task<IActionResult> Submit([FromBody] SubmitProjectApprovalRequestDto dto)
    {
        try
        {
            var result = await _workflowService.SubmitForApprovalAsync(dto, GetUserId());
            return Ok(ApiResponse<ProjectApprovalRequestDto>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<ProjectApprovalRequestDto>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<ProjectApprovalRequestDto>.Fail(ex.Message));
        }
    }

    /// <summary>Approve or reject a project approval request.</summary>
    [HttpPut("requests/{requestId:guid}/review")]
    [Authorize(Policy = PermissionPolicies.ProjectsApprove)]
    public async Task<IActionResult> Review(Guid requestId, [FromBody] ReviewProjectApprovalRequestDto dto)
    {
        dto.RequestId = requestId;
        try
        {
            var result = await _workflowService.ReviewApprovalRequestAsync(dto, GetUserId());
            return Ok(ApiResponse<ProjectApprovalRequestDto>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<ProjectApprovalRequestDto>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<ProjectApprovalRequestDto>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<ProjectApprovalRequestDto>.Fail(ex.Message));
        }
    }

    /// <summary>Get all approval requests for a project.</summary>
    [HttpGet("project/{projectId:guid}")]
    [Authorize(Policy = PermissionPolicies.ProjectsView)]
    public async Task<IActionResult> GetForProject(Guid projectId)
    {
        var result = await _workflowService.GetApprovalRequestsForProjectAsync(projectId);
        return Ok(ApiResponse<List<ProjectApprovalRequestDto>>.Ok(result));
    }

    /// <summary>Get pending approval requests where the current user is a reviewer.</summary>
    [HttpGet("pending-reviews")]
    public async Task<IActionResult> GetPendingReviews()
    {
        var result = await _workflowService.GetPendingReviewsAsync(GetUserId());
        return Ok(ApiResponse<List<ProjectApprovalRequestDto>>.Ok(result));
    }

    /// <summary>Get approval requests submitted by the current user.</summary>
    [HttpGet("my-requests")]
    public async Task<IActionResult> GetMyRequests()
    {
        var result = await _workflowService.GetMyRequestsAsync(GetUserId());
        return Ok(ApiResponse<List<ProjectApprovalRequestDto>>.Ok(result));
    }

    /// <summary>Get pending count for header badge.</summary>
    [HttpGet("pending-summary")]
    public async Task<IActionResult> GetPendingSummary()
    {
        var result = await _workflowService.GetPendingSummaryAsync(GetUserId());
        return Ok(ApiResponse<ProjectApprovalSummaryDto>.Ok(result));
    }

    /// <summary>Check if current user can review a specific project.</summary>
    [HttpGet("can-review/{projectId:guid}")]
    public async Task<IActionResult> CanReview(Guid projectId)
    {
        var canReview = await _workflowService.CanReviewProjectAsync(projectId, GetUserId());
        return Ok(ApiResponse<object>.Ok(new { canReview }));
    }

    /// <summary>Validate whether a project can be submitted for approval.</summary>
    [HttpGet("validate-submit/{projectId:guid}")]
    public async Task<IActionResult> ValidateSubmit(Guid projectId)
    {
        var result = await _workflowService.ValidateSubmitForApprovalAsync(projectId, GetUserId());
        return Ok(ApiResponse<SubmitValidationResultDto>.Ok(result));
    }
}
