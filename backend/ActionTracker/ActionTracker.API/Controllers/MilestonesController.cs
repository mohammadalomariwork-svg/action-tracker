using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.Projects.DTOs;
using ActionTracker.Application.Features.Projects.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

/// <summary>
/// Manages milestones (work packages) within a project, including ordered-list
/// resequencing.
/// </summary>
[ApiController]
[Route("api/milestones")]
[Authorize(AuthenticationSchemes = "LocalBearer,AzureAD")]
public class MilestonesController : ControllerBase
{
    private readonly IMilestoneService _service;
    private readonly ILogger<MilestonesController> _logger;

    /// <summary>Initialises the controller with required services.</summary>
    public MilestonesController(IMilestoneService service, ILogger<MilestonesController> logger)
    {
        _service = service;
        _logger  = logger;
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    // ── GET api/milestones/project/{projectId} ────────────────────────────────

    /// <summary>Returns all active milestones for a project, ordered by SequenceOrder.</summary>
    [HttpGet("project/{projectId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<MilestoneListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByProject(Guid projectId)
    {
        _logger.LogInformation("GET api/milestones/project/{ProjectId}", projectId);
        var result = await _service.GetByProjectAsync(projectId);
        return Ok(ApiResponse<IEnumerable<MilestoneListDto>>.Ok(result));
    }

    // ── GET api/milestones/{id} ───────────────────────────────────────────────

    /// <summary>Returns a milestone's full detail including action items and comments.</summary>
    [HttpGet("{id:guid}", Name = nameof(GetMilestoneById))]
    [ProducesResponseType(typeof(ApiResponse<MilestoneDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMilestoneById(Guid id)
    {
        _logger.LogInformation("GET api/milestones/{Id}", id);
        var result = await _service.GetByIdAsync(id);
        if (result is null)
            return NotFound(ApiResponse<string>.Fail($"Milestone {id} not found."));
        return Ok(ApiResponse<MilestoneDetailDto>.Ok(result));
    }

    // ── POST api/milestones ───────────────────────────────────────────────────

    /// <summary>Creates a new milestone within a project. Restricted to Admin and Manager roles.</summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(ApiResponse<MilestoneDetailDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateMilestoneDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        _logger.LogInformation("POST api/milestones for project {ProjectId} by user {UserId}",
            dto.ProjectId, CurrentUserId);

        var created = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetMilestoneById), new { id = created.Id },
            ApiResponse<MilestoneDetailDto>.Ok(created));
    }

    // ── PUT api/milestones/{id} ───────────────────────────────────────────────

    /// <summary>Updates a milestone's fields. Restricted to Admin and Manager roles.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(ApiResponse<MilestoneDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMilestoneDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        _logger.LogInformation("PUT api/milestones/{Id} by user {UserId}", id, CurrentUserId);

        var updated = await _service.UpdateAsync(id, dto);
        if (updated is null)
            return NotFound(ApiResponse<string>.Fail($"Milestone {id} not found."));
        return Ok(ApiResponse<MilestoneDetailDto>.Ok(updated));
    }

    // ── DELETE api/milestones/{id} ────────────────────────────────────────────

    /// <summary>
    /// Soft-deletes a milestone and cascades the soft-delete to its child action items.
    /// Restricted to Admin and Manager roles.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        _logger.LogInformation("DELETE api/milestones/{Id} by user {UserId}", id, CurrentUserId);

        var deleted = await _service.DeleteAsync(id);
        if (!deleted)
            return NotFound(ApiResponse<string>.Fail($"Milestone {id} not found."));
        return NoContent();
    }

    // ── PUT api/milestones/project/{projectId}/reorder ────────────────────────

    /// <summary>
    /// Resequences all milestones within a project.
    /// The request body must contain every milestone ID in the desired display order.
    /// Restricted to Admin and Manager roles.
    /// </summary>
    [HttpPut("project/{projectId:guid}/reorder")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reorder(Guid projectId, [FromBody] List<Guid> orderedMilestoneIds)
    {
        if (orderedMilestoneIds is null || orderedMilestoneIds.Count == 0)
            return BadRequest(ApiResponse<string>.Fail("orderedMilestoneIds must not be empty."));

        _logger.LogInformation(
            "PUT api/milestones/project/{ProjectId}/reorder by user {UserId}", projectId, CurrentUserId);

        var success = await _service.ReorderMilestonesAsync(projectId, orderedMilestoneIds);
        if (!success)
            return NotFound(ApiResponse<string>.Fail(
                $"Project {projectId} not found or supplied IDs do not match its milestones."));
        return NoContent();
    }
}
