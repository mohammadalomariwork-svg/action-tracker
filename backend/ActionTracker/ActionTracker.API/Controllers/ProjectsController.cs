using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.Projects.DTOs;
using ActionTracker.Application.Features.Projects.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

/// <summary>
/// Manages project lifecycle: creation, retrieval, updates, soft-delete,
/// baselining, and full-detail fetch with nested collections.
/// </summary>
[ApiController]
[Route("api/projects")]
[Authorize(AuthenticationSchemes = "LocalBearer,AzureAD")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _service;
    private readonly IBaselineService _baselineService;
    private readonly ILogger<ProjectsController> _logger;

    /// <summary>Initialises the controller with required services.</summary>
    public ProjectsController(
        IProjectService service,
        IBaselineService baselineService,
        ILogger<ProjectsController> logger)
    {
        _service         = service;
        _baselineService = baselineService;
        _logger          = logger;
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    private string CurrentUserName =>
        User.FindFirstValue(ClaimTypes.Name)
        ?? User.FindFirstValue("name")
        ?? string.Empty;

    // ── GET api/projects/workspace/{workspaceId} ──────────────────────────────

    /// <summary>Returns a lightweight list of all active projects in the given workspace.</summary>
    [HttpGet("workspace/{workspaceId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ProjectListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByWorkspace(Guid workspaceId)
    {
        _logger.LogInformation("GET api/projects/workspace/{WorkspaceId}", workspaceId);
        var result = await _service.GetByWorkspaceAsync(workspaceId);
        return Ok(ApiResponse<IEnumerable<ProjectListDto>>.Ok(result));
    }

    // ── GET api/projects/{id} ────────────────────────────────────────────────

    /// <summary>Returns a single project's detail record, or 404 if not found.</summary>
    [HttpGet("{id:guid}", Name = nameof(GetProjectById))]
    [ProducesResponseType(typeof(ApiResponse<ProjectDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProjectById(Guid id)
    {
        _logger.LogInformation("GET api/projects/{Id}", id);
        var result = await _service.GetByIdAsync(id);
        if (result is null)
            return NotFound(ApiResponse<string>.Fail($"Project {id} not found."));
        return Ok(ApiResponse<ProjectDetailDto>.Ok(result));
    }

    // ── GET api/projects/{id}/full ───────────────────────────────────────────

    /// <summary>
    /// Returns the project with all nested collections: milestones, action items,
    /// budget, and contracts. Use for detail pages requiring a single round-trip.
    /// </summary>
    [HttpGet("{id:guid}/full")]
    [ProducesResponseType(typeof(ApiResponse<ProjectDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFullDetails(Guid id)
    {
        _logger.LogInformation("GET api/projects/{Id}/full", id);
        var result = await _service.GetProjectWithFullDetailsAsync(id);
        if (result is null)
            return NotFound(ApiResponse<string>.Fail($"Project {id} not found."));
        return Ok(ApiResponse<ProjectDetailDto>.Ok(result));
    }

    // ── POST api/projects ────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new project. Strategic projects require a StrategicObjectiveId.
    /// Restricted to Admin and Manager roles.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(ApiResponse<ProjectDetailDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateProjectDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        _logger.LogInformation("POST api/projects by user {UserId}", CurrentUserId);

        try
        {
            var created = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetProjectById), new { id = created.Id },
                ApiResponse<ProjectDetailDto>.Ok(created));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // ── PUT api/projects/{id} ────────────────────────────────────────────────

    /// <summary>
    /// Updates an existing project. Returns 409 Conflict when the project is
    /// baselined and the request attempts to change schedule dates.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ProjectDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProjectDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        _logger.LogInformation("PUT api/projects/{Id} by user {UserId}", id, CurrentUserId);

        try
        {
            var updated = await _service.UpdateAsync(id, dto);
            if (updated is null)
                return NotFound(ApiResponse<string>.Fail($"Project {id} not found."));
            return Ok(ApiResponse<ProjectDetailDto>.Ok(updated));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // ── DELETE api/projects/{id} ─────────────────────────────────────────────

    /// <summary>Soft-deletes a project. Restricted to Admin role.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        _logger.LogInformation("DELETE api/projects/{Id} by user {UserId}", id, CurrentUserId);

        var deleted = await _service.DeleteAsync(id);
        if (!deleted)
            return NotFound(ApiResponse<string>.Fail($"Project {id} not found."));
        return NoContent();
    }

    // ── POST api/projects/{id}/baseline ──────────────────────────────────────

    /// <summary>
    /// Creates an immutable baseline snapshot of the project's current schedule.
    /// The current user's identity is extracted from JWT claims.
    /// Restricted to Admin and Manager roles.
    /// </summary>
    [HttpPost("{id:guid}/baseline")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(ApiResponse<ProjectBaselineDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BaselineProject(Guid id)
    {
        _logger.LogInformation("POST api/projects/{Id}/baseline by user {UserId}", id, CurrentUserId);

        try
        {
            var baseline = await _baselineService.CreateBaselineAsync(
                id, CurrentUserId, CurrentUserName);
            return CreatedAtAction(nameof(GetBaseline), new { id },
                ApiResponse<ProjectBaselineDto>.Ok(baseline));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // ── GET api/projects/{id}/baseline ───────────────────────────────────────

    /// <summary>Returns the approved baseline snapshot for the given project, or 404.</summary>
    [HttpGet("{id:guid}/baseline")]
    [ProducesResponseType(typeof(ApiResponse<ProjectBaselineDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBaseline(Guid id)
    {
        _logger.LogInformation("GET api/projects/{Id}/baseline", id);
        var baseline = await _baselineService.GetBaselineByProjectAsync(id);
        if (baseline is null)
            return NotFound(ApiResponse<string>.Fail($"No baseline found for project {id}."));
        return Ok(ApiResponse<ProjectBaselineDto>.Ok(baseline));
    }
}
