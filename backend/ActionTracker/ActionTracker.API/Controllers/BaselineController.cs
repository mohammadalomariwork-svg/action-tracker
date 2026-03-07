using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.Projects.DTOs;
using ActionTracker.Application.Features.Projects.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

/// <summary>
/// Manages project baselines and the Sponsor-gated change-request workflow.
/// Workflow: PM submits change request → Sponsor approves or rejects →
/// PM implements approved change, which unfreezes the project schedule.
/// </summary>
[ApiController]
[Route("api/baseline")]
[Authorize(AuthenticationSchemes = "LocalBearer,AzureAD")]
public class BaselineController : ControllerBase
{
    private readonly IBaselineService _service;
    private readonly ILogger<BaselineController> _logger;

    /// <summary>Initialises the controller with required services.</summary>
    public BaselineController(IBaselineService service, ILogger<BaselineController> logger)
    {
        _service = service;
        _logger  = logger;
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    private string CurrentUserName =>
        User.FindFirstValue(ClaimTypes.Name)
        ?? User.FindFirstValue("name")
        ?? string.Empty;

    // ── GET api/baseline/project/{projectId} ──────────────────────────────────

    /// <summary>
    /// Returns the approved baseline snapshot for the given project, or 404 if
    /// the project has not yet been baselined.
    /// </summary>
    [HttpGet("project/{projectId:int}")]
    [ProducesResponseType(typeof(ApiResponse<ProjectBaselineDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBaseline(int projectId)
    {
        _logger.LogInformation("GET api/baseline/project/{ProjectId}", projectId);
        var result = await _service.GetBaselineByProjectAsync(projectId);
        if (result is null)
            return NotFound(ApiResponse<string>.Fail($"No baseline found for project {projectId}."));
        return Ok(ApiResponse<ProjectBaselineDto>.Ok(result));
    }

    // ── GET api/baseline/project/{projectId}/change-requests ─────────────────

    /// <summary>
    /// Returns all baseline change requests for a project, ordered by
    /// submission date descending.
    /// </summary>
    [HttpGet("project/{projectId:int}/change-requests")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<BaselineChangeRequestDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChangeRequests(int projectId)
    {
        _logger.LogInformation(
            "GET api/baseline/project/{ProjectId}/change-requests", projectId);
        var result = await _service.GetChangeRequestsByProjectAsync(projectId);
        return Ok(ApiResponse<IEnumerable<BaselineChangeRequestDto>>.Ok(result));
    }

    // ── POST api/baseline/change-requests ────────────────────────────────────

    /// <summary>
    /// Submits a new baseline change request on behalf of the project manager.
    /// The project must already be baselined and have no pending request.
    /// </summary>
    [HttpPost("change-requests")]
    [ProducesResponseType(typeof(ApiResponse<BaselineChangeRequestDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitChangeRequest(
        [FromBody] CreateBaselineChangeRequestDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        _logger.LogInformation(
            "POST api/baseline/change-requests for project {ProjectId} by user {UserId}",
            dto.ProjectId, CurrentUserId);

        try
        {
            var created = await _service.SubmitChangeRequestAsync(dto);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<BaselineChangeRequestDto>.Ok(created));
        }
        catch (InvalidOperationException ex)
        {
            // Covers "not baselined" and "pending request already exists".
            return Conflict(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // ── PUT api/baseline/change-requests/{id}/review ──────────────────────────

    /// <summary>
    /// Records the Sponsor's decision on a pending change request.
    /// Only <c>ApprovedBySponsor</c> or <c>Rejected</c> are accepted.
    /// Restricted to Admin and Manager roles.
    /// </summary>
    [HttpPut("change-requests/{id:int}/review")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(ApiResponse<BaselineChangeRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReviewChangeRequest(
        int id, [FromBody] ReviewChangeRequestDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        dto.ChangeRequestId    = id;
        dto.ReviewedByUserId   = CurrentUserId;
        dto.ReviewedByUserName = CurrentUserName;

        _logger.LogInformation(
            "PUT api/baseline/change-requests/{Id}/review by user {UserId}", id, CurrentUserId);

        try
        {
            var updated = await _service.ReviewChangeRequestAsync(dto);
            if (updated is null)
                return NotFound(ApiResponse<string>.Fail($"Change request {id} not found."));
            return Ok(ApiResponse<BaselineChangeRequestDto>.Ok(updated));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            // Request is not in Pending status.
            return Conflict(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // ── PUT api/baseline/change-requests/{id}/implement ───────────────────────

    /// <summary>
    /// Marks an approved change request as Implemented and unfreezes the
    /// project so the PM can edit schedule dates.
    /// The change request must be in <c>ApprovedBySponsor</c> status.
    /// Restricted to Admin and Manager roles.
    /// </summary>
    [HttpPut("change-requests/{id:int}/implement")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ImplementChangeRequest(int id)
    {
        _logger.LogInformation(
            "PUT api/baseline/change-requests/{Id}/implement by user {UserId}", id, CurrentUserId);

        try
        {
            var success = await _service.ImplementApprovedChangeAsync(id, CurrentUserId);
            if (!success)
                return NotFound(ApiResponse<string>.Fail($"Change request {id} not found."));
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            // Request is not in ApprovedBySponsor status.
            return Conflict(ApiResponse<string>.Fail(ex.Message));
        }
    }
}
