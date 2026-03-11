using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.Projects.DTOs;
using ActionTracker.Application.Features.Projects.Interfaces;
using ActionTracker.Application.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

[ApiController]
[Route("api/projects")]
[Authorize(AuthenticationSchemes = "LocalBearer,AzureAD")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _service;
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(IProjectService service, ILogger<ProjectsController> logger)
    {
        _service = service;
        _logger  = logger;
    }

    /// <summary>Returns a paginated, filtered list of projects.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ProjectResponseDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<ProjectResponseDto>>>> GetAll(
        [FromQuery] ProjectFilterDto filter, CancellationToken ct)
    {
        var result = await _service.GetAllAsync(filter, ct);
        return Ok(ApiResponse<PagedResult<ProjectResponseDto>>.Ok(result));
    }

    /// <summary>Returns a single project by GUID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ProjectResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ProjectResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ProjectResponseDto>>> GetById(Guid id, CancellationToken ct)
    {
        var project = await _service.GetByIdAsync(id, ct);
        if (project is null)
            return NotFound(ApiResponse<ProjectResponseDto>.Fail($"Project {id} not found."));

        return Ok(ApiResponse<ProjectResponseDto>.Ok(project));
    }

    /// <summary>Creates a new project. Status is always Draft on creation.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ProjectResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ProjectResponseDto>>> Create(
        [FromBody] ProjectCreateDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            var created = await _service.CreateAsync(dto, userId, ct);

            _logger.LogInformation("Project {ProjectCode} created", created.ProjectCode);

            return Created(string.Empty, ApiResponse<ProjectResponseDto>.Ok(created));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<ProjectResponseDto>.Fail(ex.Message));
        }
    }

    /// <summary>Updates an existing project.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ProjectResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ProjectResponseDto>>> Update(
        Guid id, [FromBody] ProjectUpdateDto dto, CancellationToken ct)
    {
        try
        {
            var updated = await _service.UpdateAsync(id, dto, ct);
            return Ok(ApiResponse<ProjectResponseDto>.Ok(updated));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<ProjectResponseDto>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<ProjectResponseDto>.Fail(ex.Message));
        }
    }

    /// <summary>Returns aggregated stats for a project (milestones, action items, rates).</summary>
    [HttpGet("{id:guid}/stats")]
    [ProducesResponseType(typeof(ApiResponse<ProjectStatsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ProjectStatsDto>>> GetStats(Guid id, CancellationToken ct)
    {
        var stats = await _service.GetStatsAsync(id, ct);
        return Ok(ApiResponse<ProjectStatsDto>.Ok(stats));
    }

    /// <summary>Returns strategic objectives scoped to the workspace's org unit (with parent fallback).</summary>
    [HttpGet("strategic-objectives-for-workspace/{workspaceId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<List<StrategicObjectiveOptionDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<StrategicObjectiveOptionDto>>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<List<StrategicObjectiveOptionDto>>>> GetStrategicObjectivesForWorkspace(
        Guid workspaceId, CancellationToken ct)
    {
        try
        {
            var result = await _service.GetStrategicObjectivesForWorkspaceAsync(workspaceId, ct);
            return Ok(ApiResponse<List<StrategicObjectiveOptionDto>>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<List<StrategicObjectiveOptionDto>>.Fail(ex.Message));
        }
    }

    /// <summary>Soft-deletes a project and its milestones and action items.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await _service.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>Restores a soft-deleted project and its milestones and action items.</summary>
    [HttpPatch("{id:guid}/restore")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct)
    {
        try
        {
            await _service.RestoreAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
