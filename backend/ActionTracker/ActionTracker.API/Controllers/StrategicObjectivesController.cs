using ActionTracker.API.Models;
using ActionTracker.Application.Features.StrategicObjectives.DTOs;
using ActionTracker.Application.Features.StrategicObjectives.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AdminOnly")]
public class StrategicObjectivesController : ControllerBase
{
    private readonly IStrategicObjectiveService          _service;
    private readonly ILogger<StrategicObjectivesController> _logger;

    public StrategicObjectivesController(
        IStrategicObjectiveService          service,
        ILogger<StrategicObjectivesController> logger)
    {
        _service = service;
        _logger  = logger;
    }

    // -------------------------------------------------------------------------
    // GET api/strategicobjectives
    // -------------------------------------------------------------------------

    /// <summary>Get a paged list of strategic objectives, optionally filtered by org unit.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<StrategicObjectiveListResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int   page           = 1,
        [FromQuery] int   pageSize       = 20,
        [FromQuery] Guid? orgUnitId      = null,
        [FromQuery] bool  includeDeleted = false,
        CancellationToken ct             = default)
    {
        _logger.LogInformation(
            "GET /api/strategicobjectives page={Page} pageSize={PageSize} orgUnitId={OrgUnitId}",
            page, pageSize, orgUnitId);

        var result = await _service.GetAllAsync(page, pageSize, orgUnitId, includeDeleted, ct);
        return Ok(ApiResponse<StrategicObjectiveListResponseDto>.Ok(result));
    }

    // -------------------------------------------------------------------------
    // GET api/strategicobjectives/{id}
    // -------------------------------------------------------------------------

    /// <summary>Get a single strategic objective by ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<StrategicObjectiveDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>),                StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("GET /api/strategicobjectives/{Id}", id);

        var result = await _service.GetByIdAsync(id, ct);
        if (result is null)
            return NotFound(ApiResponse<string>.Fail($"Strategic objective '{id}' not found."));

        return Ok(ApiResponse<StrategicObjectiveDto>.Ok(result));
    }

    // -------------------------------------------------------------------------
    // GET api/strategicobjectives/by-orgunit/{orgUnitId}
    // -------------------------------------------------------------------------

    /// <summary>Get all strategic objectives belonging to a specific org unit.</summary>
    [HttpGet("by-orgunit/{orgUnitId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<List<StrategicObjectiveDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByOrgUnit(Guid orgUnitId, CancellationToken ct = default)
    {
        _logger.LogInformation("GET /api/strategicobjectives/by-orgunit/{OrgUnitId}", orgUnitId);

        var result = await _service.GetByOrgUnitAsync(orgUnitId, ct);
        return Ok(ApiResponse<List<StrategicObjectiveDto>>.Ok(result));
    }

    // -------------------------------------------------------------------------
    // POST api/strategicobjectives
    // -------------------------------------------------------------------------

    /// <summary>Create a new strategic objective. ObjectiveCode is auto-generated.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<StrategicObjectiveDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<string>),                StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateStrategicObjectiveRequestDto request,
        CancellationToken ct = default)
    {
        _logger.LogInformation("POST /api/strategicobjectives orgUnitId={OrgUnitId}", request.OrgUnitId);

        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        try
        {
            var created = await _service.CreateAsync(request, ct);
            return CreatedAtAction(
                nameof(GetById),
                new { id = created.Id },
                ApiResponse<StrategicObjectiveDto>.Ok(created));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // -------------------------------------------------------------------------
    // PUT api/strategicobjectives/{id}
    // -------------------------------------------------------------------------

    /// <summary>Update an existing strategic objective.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<StrategicObjectiveDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>),                StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<string>),                StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateStrategicObjectiveRequestDto request,
        CancellationToken ct = default)
    {
        _logger.LogInformation("PUT /api/strategicobjectives/{Id}", id);

        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        try
        {
            var updated = await _service.UpdateAsync(id, request, ct);
            return Ok(ApiResponse<StrategicObjectiveDto>.Ok(updated));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // -------------------------------------------------------------------------
    // DELETE api/strategicobjectives/{id}
    // -------------------------------------------------------------------------

    /// <summary>Soft-delete a strategic objective.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDelete(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("DELETE /api/strategicobjectives/{Id}", id);

        try
        {
            await _service.SoftDeleteAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // -------------------------------------------------------------------------
    // POST api/strategicobjectives/{id}/restore
    // -------------------------------------------------------------------------

    /// <summary>Restore a soft-deleted strategic objective.</summary>
    [HttpPost("{id:guid}/restore")]
    [ProducesResponseType(typeof(ApiResponse<StrategicObjectiveDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>),                StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("POST /api/strategicobjectives/{Id}/restore", id);

        try
        {
            await _service.RestoreAsync(id, ct);
            var restored = await _service.GetByIdAsync(id, ct);
            return Ok(ApiResponse<StrategicObjectiveDto>.Ok(restored!));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
    }
}
