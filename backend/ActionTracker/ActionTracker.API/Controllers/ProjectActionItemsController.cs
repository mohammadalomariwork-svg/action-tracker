using System;
using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.Projects.DTOs;
using ActionTracker.Application.Features.Projects.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

/// <summary>
/// Manages project/milestone-scoped action items (int PK).
/// Distinct from the legacy action items controller (domain ActionItem, int PK,
/// different feature set).
/// Route: <c>api/action-items</c> is already used by the legacy controller,
/// so this controller uses <c>api/project-action-items</c>.
/// </summary>
[ApiController]
[Route("api/project-action-items")]
[Authorize(AuthenticationSchemes = "LocalBearer,AzureAD")]
public class ProjectActionItemsController : ControllerBase
{
    private readonly IActionItemService _service;
    private readonly ILogger<ProjectActionItemsController> _logger;

    /// <summary>Initialises the controller with required services.</summary>
    public ProjectActionItemsController(
        IActionItemService service,
        ILogger<ProjectActionItemsController> logger)
    {
        _service = service;
        _logger  = logger;
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    // ── GET api/project-action-items/workspace/{workspaceId}/standalone ───────

    /// <summary>
    /// Returns standalone action items in a workspace — those with no project or
    /// milestone association.
    /// </summary>
    [HttpGet("workspace/{workspaceId:guid}/standalone")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ActionItemListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStandaloneByWorkspace(Guid workspaceId)
    {
        _logger.LogInformation(
            "GET api/project-action-items/workspace/{WorkspaceId}/standalone", workspaceId);
        var result = await _service.GetByWorkspaceAsync(workspaceId);
        return Ok(ApiResponse<IEnumerable<ActionItemListDto>>.Ok(result));
    }

    // ── GET api/project-action-items/project/{projectId} ─────────────────────

    /// <summary>
    /// Returns all active action items that belong to the given project, including
    /// project-level and milestone-nested items.
    /// </summary>
    [HttpGet("project/{projectId:int}")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ActionItemListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByProject(int projectId)
    {
        _logger.LogInformation("GET api/project-action-items/project/{ProjectId}", projectId);
        var result = await _service.GetByProjectAsync(projectId);
        return Ok(ApiResponse<IEnumerable<ActionItemListDto>>.Ok(result));
    }

    // ── GET api/project-action-items/milestone/{milestoneId} ─────────────────

    /// <summary>Returns all active action items assigned to a specific milestone.</summary>
    [HttpGet("milestone/{milestoneId:int}")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ActionItemListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByMilestone(int milestoneId)
    {
        _logger.LogInformation(
            "GET api/project-action-items/milestone/{MilestoneId}", milestoneId);
        var result = await _service.GetByMilestoneAsync(milestoneId);
        return Ok(ApiResponse<IEnumerable<ActionItemListDto>>.Ok(result));
    }

    // ── GET api/project-action-items/{id} ────────────────────────────────────

    /// <summary>Returns an action item's full detail including documents and comments.</summary>
    [HttpGet("{id:int}", Name = nameof(GetActionItemById))]
    [ProducesResponseType(typeof(ApiResponse<ActionItemDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActionItemById(int id)
    {
        _logger.LogInformation("GET api/project-action-items/{Id}", id);
        var result = await _service.GetByIdAsync(id);
        if (result is null)
            return NotFound(ApiResponse<string>.Fail($"Action item {id} not found."));
        return Ok(ApiResponse<ActionItemDetailDto>.Ok(result));
    }

    // ── POST api/project-action-items ────────────────────────────────────────

    /// <summary>
    /// Creates a new action item. External assignee rules are enforced at the
    /// service layer.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ActionItemDetailDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateActionItemDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        _logger.LogInformation("POST api/project-action-items by user {UserId}", CurrentUserId);

        try
        {
            var created = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetActionItemById), new { id = created.Id },
                ApiResponse<ActionItemDetailDto>.Ok(created));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // ── PUT api/project-action-items/{id} ─────────────────────────────────────

    /// <summary>Updates an existing action item using patch semantics (only non-null fields applied).</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<ActionItemDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateActionItemDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        _logger.LogInformation("PUT api/project-action-items/{Id} by user {UserId}", id, CurrentUserId);

        var updated = await _service.UpdateAsync(id, dto);
        if (updated is null)
            return NotFound(ApiResponse<string>.Fail($"Action item {id} not found."));
        return Ok(ApiResponse<ActionItemDetailDto>.Ok(updated));
    }

    // ── DELETE api/project-action-items/{id} ──────────────────────────────────

    /// <summary>
    /// Soft-deletes an action item and cascades to its attached documents.
    /// Restricted to Admin and Manager roles.
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        _logger.LogInformation(
            "DELETE api/project-action-items/{Id} by user {UserId}", id, CurrentUserId);

        var deleted = await _service.DeleteAsync(id);
        if (!deleted)
            return NotFound(ApiResponse<string>.Fail($"Action item {id} not found."));
        return NoContent();
    }
}
