using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.Projects.DTOs;
using ActionTracker.Application.Features.Projects.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

/// <summary>
/// Manages workspace-scoped strategic objectives (int PK) used for aligning
/// projects.  Distinct from the admin-panel strategic objectives (Guid PK).
/// </summary>
[ApiController]
[Route("api/strategic-objectives")]
[Authorize(AuthenticationSchemes = "LocalBearer,AzureAD")]
public class ProjectStrategicObjectivesController : ControllerBase
{
    private readonly IStrategicObjectiveService _service;
    private readonly ILogger<ProjectStrategicObjectivesController> _logger;

    /// <summary>Initialises the controller with required services.</summary>
    public ProjectStrategicObjectivesController(
        IStrategicObjectiveService service,
        ILogger<ProjectStrategicObjectivesController> logger)
    {
        _service = service;
        _logger  = logger;
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    // ── GET api/strategic-objectives ─────────────────────────────────────────

    /// <summary>Returns all active strategic objectives. Restricted to Admin and Manager roles.</summary>
    [HttpGet]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<StrategicObjectiveDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        _logger.LogInformation("GET api/strategic-objectives");
        var result = await _service.GetAllAsync();
        return Ok(ApiResponse<IEnumerable<StrategicObjectiveDto>>.Ok(result));
    }

    // ── GET api/strategic-objectives/by-org/{orgUnit} ─────────────────────────

    /// <summary>
    /// Returns active strategic objectives for a specific organisation unit.
    /// Used by the project creation form to populate the objective drop-down.
    /// </summary>
    [HttpGet("by-org/{orgUnit}")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<StrategicObjectiveDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByOrgUnit(string orgUnit)
    {
        _logger.LogInformation("GET api/strategic-objectives/by-org/{OrgUnit}", orgUnit);
        var result = await _service.GetByOrganizationUnitAsync(orgUnit);
        return Ok(ApiResponse<IEnumerable<StrategicObjectiveDto>>.Ok(result));
    }

    // ── GET api/strategic-objectives/{id} ────────────────────────────────────

    /// <summary>Returns a single strategic objective by its integer primary key.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<StrategicObjectiveDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        _logger.LogInformation("GET api/strategic-objectives/{Id}", id);
        var result = await _service.GetByIdAsync(id);
        if (result is null)
            return NotFound(ApiResponse<string>.Fail($"Strategic objective {id} not found."));
        return Ok(ApiResponse<StrategicObjectiveDto>.Ok(result));
    }

    // ── POST api/strategic-objectives ────────────────────────────────────────

    /// <summary>Creates a new strategic objective. Restricted to Admin role.</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<StrategicObjectiveDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateStrategicObjectiveDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        _logger.LogInformation("POST api/strategic-objectives by user {UserId}", CurrentUserId);

        var created = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id },
            ApiResponse<StrategicObjectiveDto>.Ok(created));
    }

    // ── PUT api/strategic-objectives/{id} ────────────────────────────────────

    /// <summary>Updates an existing strategic objective. Restricted to Admin role.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<StrategicObjectiveDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStrategicObjectiveDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        _logger.LogInformation("PUT api/strategic-objectives/{Id} by user {UserId}", id, CurrentUserId);

        var updated = await _service.UpdateAsync(id, dto);
        if (updated is null)
            return NotFound(ApiResponse<string>.Fail($"Strategic objective {id} not found."));
        return Ok(ApiResponse<StrategicObjectiveDto>.Ok(updated));
    }

    // ── DELETE api/strategic-objectives/{id} ─────────────────────────────────

    /// <summary>Soft-deletes a strategic objective. Restricted to Admin role.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        _logger.LogInformation("DELETE api/strategic-objectives/{Id} by user {UserId}", id, CurrentUserId);

        var deleted = await _service.DeleteAsync(id);
        if (!deleted)
            return NotFound(ApiResponse<string>.Fail($"Strategic objective {id} not found."));
        return NoContent();
    }
}
