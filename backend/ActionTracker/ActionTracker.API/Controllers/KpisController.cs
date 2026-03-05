using ActionTracker.API.Models;
using ActionTracker.Application.Features.Kpis.DTOs;
using ActionTracker.Application.Features.Kpis.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AdminOnly")]
public class KpisController : ControllerBase
{
    private readonly IKpiService          _service;
    private readonly ILogger<KpisController> _logger;

    public KpisController(IKpiService service, ILogger<KpisController> logger)
    {
        _service = service;
        _logger  = logger;
    }

    // -------------------------------------------------------------------------
    // GET api/kpis
    // -------------------------------------------------------------------------

    /// <summary>Get a paged list of KPIs, optionally filtered by strategic objective.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<KpiListResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int   page           = 1,
        [FromQuery] int   pageSize       = 20,
        [FromQuery] Guid? objectiveId    = null,
        [FromQuery] bool  includeDeleted = false,
        CancellationToken ct             = default)
    {
        _logger.LogInformation(
            "GET /api/kpis page={Page} pageSize={PageSize} objectiveId={ObjectiveId}",
            page, pageSize, objectiveId);

        var result = await _service.GetAllAsync(page, pageSize, objectiveId, includeDeleted, ct);
        return Ok(ApiResponse<KpiListResponseDto>.Ok(result));
    }

    // -------------------------------------------------------------------------
    // GET api/kpis/{id}
    // -------------------------------------------------------------------------

    /// <summary>Get a single KPI with its targets, optionally filtered by year.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<KpiWithTargetsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>),            StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromQuery] int? year = null,
        CancellationToken ct  = default)
    {
        _logger.LogInformation("GET /api/kpis/{Id} year={Year}", id, year);

        var result = await _service.GetByIdAsync(id, year, ct);
        if (result is null)
            return NotFound(ApiResponse<string>.Fail($"KPI '{id}' not found."));

        return Ok(ApiResponse<KpiWithTargetsDto>.Ok(result));
    }

    // -------------------------------------------------------------------------
    // GET api/kpis/by-objective/{objectiveId}
    // -------------------------------------------------------------------------

    /// <summary>Get all active KPIs for a specific strategic objective.</summary>
    [HttpGet("by-objective/{objectiveId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<List<KpiDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByObjective(Guid objectiveId, CancellationToken ct = default)
    {
        _logger.LogInformation("GET /api/kpis/by-objective/{ObjectiveId}", objectiveId);

        var result = await _service.GetByObjectiveAsync(objectiveId, ct);
        return Ok(ApiResponse<List<KpiDto>>.Ok(result));
    }

    // -------------------------------------------------------------------------
    // POST api/kpis
    // -------------------------------------------------------------------------

    /// <summary>Create a new KPI. KpiNumber is auto-assigned per strategic objective.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<KpiDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateKpiRequestDto request,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "POST /api/kpis objectiveId={ObjectiveId}", request.StrategicObjectiveId);

        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        try
        {
            var created = await _service.CreateAsync(request, ct);
            return CreatedAtAction(
                nameof(GetById),
                new { id = created.Id },
                ApiResponse<KpiDto>.Ok(created));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // -------------------------------------------------------------------------
    // PUT api/kpis/{id}
    // -------------------------------------------------------------------------

    /// <summary>Update an existing KPI. StrategicObjectiveId and KpiNumber cannot be changed.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<KpiDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateKpiRequestDto request,
        CancellationToken ct = default)
    {
        _logger.LogInformation("PUT /api/kpis/{Id}", id);

        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        try
        {
            var updated = await _service.UpdateAsync(id, request, ct);
            return Ok(ApiResponse<KpiDto>.Ok(updated));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // -------------------------------------------------------------------------
    // DELETE api/kpis/{id}
    // -------------------------------------------------------------------------

    /// <summary>Soft-delete a KPI.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDelete(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("DELETE /api/kpis/{Id}", id);

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
    // POST api/kpis/{id}/restore
    // -------------------------------------------------------------------------

    /// <summary>Restore a soft-deleted KPI.</summary>
    [HttpPost("{id:guid}/restore")]
    [ProducesResponseType(typeof(ApiResponse<KpiDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("POST /api/kpis/{Id}/restore", id);

        try
        {
            await _service.RestoreAsync(id, ct);
            var restored = await _service.GetByIdAsync(id, ct);
            return Ok(ApiResponse<KpiDto>.Ok(restored!));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // -------------------------------------------------------------------------
    // POST api/kpis/targets/upsert
    // -------------------------------------------------------------------------

    /// <summary>Insert or update a single KPI target for a specific month.</summary>
    [HttpPost("targets/upsert")]
    [ProducesResponseType(typeof(ApiResponse<KpiTargetDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>),       StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpsertTarget(
        [FromBody] UpsertKpiTargetRequestDto request,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "POST /api/kpis/targets/upsert kpiId={KpiId} {Year}/{Month}",
            request.KpiId, request.Year, request.Month);

        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        try
        {
            var result = await _service.UpsertTargetAsync(request, ct);
            return Ok(ApiResponse<KpiTargetDto>.Ok(result));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // -------------------------------------------------------------------------
    // POST api/kpis/targets/bulk-upsert
    // -------------------------------------------------------------------------

    /// <summary>Insert or update all monthly targets for a KPI/year combination in one transaction.</summary>
    [HttpPost("targets/bulk-upsert")]
    [ProducesResponseType(typeof(ApiResponse<List<KpiTargetDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>),             StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BulkUpsertTargets(
        [FromBody] BulkUpsertKpiTargetsRequestDto request,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "POST /api/kpis/targets/bulk-upsert kpiId={KpiId} year={Year} count={Count}",
            request.KpiId, request.Year, request.Targets?.Count);

        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        try
        {
            var result = await _service.BulkUpsertTargetsAsync(request, ct);
            return Ok(ApiResponse<List<KpiTargetDto>>.Ok(result));
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
    // GET api/kpis/{id}/targets
    // -------------------------------------------------------------------------

    /// <summary>Get all targets for a KPI in a specific year.</summary>
    [HttpGet("{id:guid}/targets")]
    [ProducesResponseType(typeof(ApiResponse<List<KpiTargetDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTargets(
        Guid id,
        [FromQuery] int year,
        CancellationToken ct = default)
    {
        _logger.LogInformation("GET /api/kpis/{Id}/targets year={Year}", id, year);

        var result = await _service.GetTargetsAsync(id, year, ct);
        return Ok(ApiResponse<List<KpiTargetDto>>.Ok(result));
    }
}
