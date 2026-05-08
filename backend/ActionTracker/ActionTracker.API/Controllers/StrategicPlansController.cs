using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.StrategicPlans.DTOs;
using ActionTracker.Application.Features.StrategicPlans.Interfaces;
using ActionTracker.Application.Features.StrategicPlans.Workflow;
using ActionTracker.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

[ApiController]
[Route("api/strategic-plans")]
[Authorize(Policy = "LocalOrAzureAD")]
public class StrategicPlansController : ControllerBase
{
    private readonly IStrategicPlanService _service;
    private readonly IStrategicPlanApproverService _approverService;
    private readonly ILogger<StrategicPlansController> _logger;

    public StrategicPlansController(
        IStrategicPlanService service,
        IStrategicPlanApproverService approverService,
        ILogger<StrategicPlansController> logger)
    {
        _service = service;
        _approverService = approverService;
        _logger = logger;
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Unknown";

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.StrategicPlansView)]
    [ProducesResponseType(typeof(ApiResponse<StrategicPlanListResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? orgUnitId = null,
        [FromQuery] int? status = null,
        [FromQuery] int? year = null,
        [FromQuery] bool includeDeleted = false,
        CancellationToken ct = default)
    {
        var result = await _service.GetAllAsync(page, pageSize, orgUnitId, status, year, includeDeleted, CurrentUserId, ct);
        return Ok(ApiResponse<StrategicPlanListResponseDto>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.StrategicPlansView)]
    [ProducesResponseType(typeof(ApiResponse<StrategicPlanDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await _service.GetByIdAsync(id, CurrentUserId, ct);
        if (result is null)
            return NotFound(ApiResponse<string>.Fail($"Strategic plan '{id}' not found."));
        return Ok(ApiResponse<StrategicPlanDto>.Ok(result));
    }

    [HttpGet("by-orgunit/{orgUnitId:guid}")]
    [Authorize(Policy = PermissionPolicies.StrategicPlansView)]
    [ProducesResponseType(typeof(ApiResponse<List<StrategicPlanDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByOrgUnit(Guid orgUnitId, CancellationToken ct = default)
    {
        var result = await _service.GetByOrgUnitAsync(orgUnitId, CurrentUserId, ct);
        return Ok(ApiResponse<List<StrategicPlanDto>>.Ok(result));
    }

    [HttpGet("by-orgunit/{orgUnitId:guid}/active")]
    [Authorize(Policy = PermissionPolicies.StrategicPlansView)]
    [ProducesResponseType(typeof(ApiResponse<StrategicPlanDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActiveByOrgUnit(Guid orgUnitId, CancellationToken ct = default)
    {
        var result = await _service.GetActiveForOrgUnitAsync(orgUnitId, CurrentUserId, ct);
        if (result is null)
            return NotFound(ApiResponse<string>.Fail("No active strategic plan for this org unit."));
        return Ok(ApiResponse<StrategicPlanDto>.Ok(result));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.StrategicPlansCreate)]
    [ProducesResponseType(typeof(ApiResponse<StrategicPlanDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateStrategicPlanRequestDto request,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        try
        {
            var created = await _service.CreateAsync(request, CurrentUserId, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id },
                ApiResponse<StrategicPlanDto>.Ok(created));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<string>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<string>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.StrategicPlansEdit)]
    [ProducesResponseType(typeof(ApiResponse<StrategicPlanDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateStrategicPlanRequestDto request,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        try
        {
            var updated = await _service.UpdateAsync(id, request, CurrentUserId, ct);
            return Ok(ApiResponse<StrategicPlanDto>.Ok(updated));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<string>.Fail(ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.StrategicPlansDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SoftDelete(Guid id, CancellationToken ct = default)
    {
        try
        {
            await _service.SoftDeleteAsync(id, CurrentUserId, ct);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<string>.Fail(ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<string>.Fail(ex.Message));
        }
    }

    [HttpPatch("{id:guid}/restore")]
    [Authorize(Policy = PermissionPolicies.StrategicPlansEdit)]
    [ProducesResponseType(typeof(ApiResponse<StrategicPlanDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct = default)
    {
        try
        {
            await _service.RestoreAsync(id, CurrentUserId, ct);
            var restored = await _service.GetByIdAsync(id, CurrentUserId, ct);
            return Ok(ApiResponse<StrategicPlanDto>.Ok(restored!));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<string>.Fail(ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // ── Sequential approval chain ──────────────────────────────────────────

    [HttpGet("{id:guid}/approvers")]
    [Authorize(Policy = PermissionPolicies.StrategicPlansView)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StrategicPlanApproverDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetApprovers(Guid id, CancellationToken ct = default)
    {
        try
        {
            var rows = await _approverService.GetForPlanAsync(id, CurrentUserId, ct);
            return Ok(ApiResponse<IReadOnlyList<StrategicPlanApproverDto>>.Ok(rows));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<string>.Fail(ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
    }

    [HttpPut("{id:guid}/approvers")]
    [Authorize(Policy = PermissionPolicies.StrategicPlansEdit)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StrategicPlanApproverDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpsertApprovers(
        Guid id,
        [FromBody] UpsertStrategicPlanApproversRequestDto request,
        CancellationToken ct = default)
    {
        if (request is null)
            return BadRequest(ApiResponse<string>.Fail("Request body is required."));
        try
        {
            var rows = await _approverService.UpsertForPlanAsync(id, request, CurrentUserId, ct);
            return Ok(ApiResponse<IReadOnlyList<StrategicPlanApproverDto>>.Ok(rows));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<string>.Fail(ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<string>.Fail(ex.Message));
        }
    }
}
