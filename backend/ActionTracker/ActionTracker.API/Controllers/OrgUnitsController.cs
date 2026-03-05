using ActionTracker.API.Models;
using ActionTracker.Application.Features.OrgChart.DTOs;
using ActionTracker.Application.Features.OrgChart.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AdminOnly")]
public class OrgUnitsController : ControllerBase
{
    private readonly IOrgUnitService          _orgUnitService;
    private readonly ILogger<OrgUnitsController> _logger;

    public OrgUnitsController(IOrgUnitService orgUnitService, ILogger<OrgUnitsController> logger)
    {
        _orgUnitService = orgUnitService;
        _logger         = logger;
    }

    // -------------------------------------------------------------------------
    // GET api/orgunits/tree
    // -------------------------------------------------------------------------

    /// <summary>Get full org chart tree.</summary>
    [HttpGet("tree")]
    [ProducesResponseType(typeof(ApiResponse<OrgUnitTreeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTree(
        [FromQuery] bool includeDeleted = false,
        CancellationToken ct            = default)
    {
        _logger.LogInformation("GET /api/orgunits/tree includeDeleted={IncludeDeleted}", includeDeleted);

        var result = await _orgUnitService.GetTreeAsync(includeDeleted, ct);
        return Ok(ApiResponse<OrgUnitTreeDto>.Ok(result));
    }

    // -------------------------------------------------------------------------
    // GET api/orgunits
    // -------------------------------------------------------------------------

    /// <summary>Get a paged flat list of all org units.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<OrgUnitListResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int  page           = 1,
        [FromQuery] int  pageSize       = 50,
        [FromQuery] bool includeDeleted = false,
        CancellationToken ct            = default)
    {
        _logger.LogInformation(
            "GET /api/orgunits page={Page} pageSize={PageSize} includeDeleted={IncludeDeleted}",
            page, pageSize, includeDeleted);

        var result = await _orgUnitService.GetAllAsync(page, pageSize, includeDeleted, ct);
        return Ok(ApiResponse<OrgUnitListResponseDto>.Ok(result));
    }

    // -------------------------------------------------------------------------
    // GET api/orgunits/{id}
    // -------------------------------------------------------------------------

    /// <summary>Get a single org unit by ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<OrgUnitDto>),  StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>),      StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("GET /api/orgunits/{Id}", id);

        var result = await _orgUnitService.GetByIdAsync(id, ct);
        if (result is null)
            return NotFound(ApiResponse<string>.Fail($"Org unit '{id}' not found."));

        return Ok(ApiResponse<OrgUnitDto>.Ok(result));
    }

    // -------------------------------------------------------------------------
    // GET api/orgunits/{id}/children
    // -------------------------------------------------------------------------

    /// <summary>Get all direct children of an org unit.</summary>
    [HttpGet("{id:guid}/children")]
    [ProducesResponseType(typeof(ApiResponse<List<OrgUnitDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>),           StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChildren(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("GET /api/orgunits/{Id}/children", id);

        try
        {
            var result = await _orgUnitService.GetChildrenAsync(id, ct);
            return Ok(ApiResponse<List<OrgUnitDto>>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // -------------------------------------------------------------------------
    // POST api/orgunits
    // -------------------------------------------------------------------------

    /// <summary>Create a new org unit.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<OrgUnitDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<string>),     StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<string>),     StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateOrgUnitRequestDto request,
        CancellationToken ct = default)
    {
        _logger.LogInformation("POST /api/orgunits name={Name}", request.Name);

        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        try
        {
            var created = await _orgUnitService.CreateAsync(request, ct);
            return CreatedAtAction(
                nameof(GetById),
                new { id = created.Id },
                ApiResponse<OrgUnitDto>.Ok(created));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return Conflict(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // -------------------------------------------------------------------------
    // PUT api/orgunits/{id}
    // -------------------------------------------------------------------------

    /// <summary>Update an existing org unit.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<OrgUnitDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>),     StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<string>),     StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateOrgUnitRequestDto request,
        CancellationToken ct = default)
    {
        _logger.LogInformation("PUT /api/orgunits/{Id}", id);

        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        try
        {
            var updated = await _orgUnitService.UpdateAsync(id, request, ct);
            return Ok(ApiResponse<OrgUnitDto>.Ok(updated));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // -------------------------------------------------------------------------
    // DELETE api/orgunits/{id}
    // -------------------------------------------------------------------------

    /// <summary>Soft-delete an org unit and all its descendants.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDelete(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("DELETE /api/orgunits/{Id}", id);

        try
        {
            await _orgUnitService.SoftDeleteAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // -------------------------------------------------------------------------
    // POST api/orgunits/{id}/restore
    // -------------------------------------------------------------------------

    /// <summary>Restore a soft-deleted org unit.</summary>
    [HttpPost("{id:guid}/restore")]
    [ProducesResponseType(typeof(ApiResponse<OrgUnitDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>),     StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("POST /api/orgunits/{Id}/restore", id);

        try
        {
            await _orgUnitService.RestoreAsync(id, ct);
            var restored = await _orgUnitService.GetByIdAsync(id, ct);
            return Ok(ApiResponse<OrgUnitDto>.Ok(restored!));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
    }
}
