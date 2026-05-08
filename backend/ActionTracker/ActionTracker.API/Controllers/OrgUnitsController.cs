using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.OrgChart.DTOs;
using ActionTracker.Application.Features.OrgChart.Interfaces;
using ActionTracker.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "LocalOrAzureAD")]
public class OrgUnitsController : ControllerBase
{
    private readonly IOrgUnitService          _orgUnitService;
    private readonly IApprovalAuthorityResolver _approvalAuthorityResolver;
    private readonly ILogger<OrgUnitsController> _logger;

    public OrgUnitsController(
        IOrgUnitService orgUnitService,
        IApprovalAuthorityResolver approvalAuthorityResolver,
        ILogger<OrgUnitsController> logger)
    {
        _orgUnitService = orgUnitService;
        _approvalAuthorityResolver = approvalAuthorityResolver;
        _logger         = logger;
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Unknown";

    // -------------------------------------------------------------------------
    // GET api/orgunits/tree
    // -------------------------------------------------------------------------

    /// <summary>Get full org chart tree.</summary>
    [HttpGet("tree")]
    [Authorize(Policy = PermissionPolicies.OrgChartView)]
    [ProducesResponseType(typeof(ApiResponse<OrgUnitTreeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTree(
        [FromQuery] bool includeDeleted = false,
        CancellationToken ct            = default)
    {
        _logger.LogInformation("GET /api/orgunits/tree includeDeleted={IncludeDeleted}", includeDeleted);

        var result = await _orgUnitService.GetTreeAsync(includeDeleted, ct);
        return Ok(ApiResponse<OrgUnitTreeDto?>.Ok(result));
    }

    // -------------------------------------------------------------------------
    // GET api/orgunits
    // -------------------------------------------------------------------------

    /// <summary>Get a paged flat list of all org units.</summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.OrgChartView)]
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
    [Authorize(Policy = PermissionPolicies.OrgChartView)]
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
    [Authorize(Policy = PermissionPolicies.OrgChartView)]
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
    [Authorize(Policy = PermissionPolicies.OrgChartCreate)]
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
            var created = await _orgUnitService.CreateAsync(request, CurrentUserId, ct);
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
    [Authorize(Policy = PermissionPolicies.OrgChartEdit)]
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
            var updated = await _orgUnitService.UpdateAsync(id, request, CurrentUserId, ct);
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
    [Authorize(Policy = PermissionPolicies.OrgChartDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDelete(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("DELETE /api/orgunits/{Id}", id);

        try
        {
            await _orgUnitService.SoftDeleteAsync(id, CurrentUserId, ct);
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
    [Authorize(Policy = PermissionPolicies.OrgChartEdit)]
    [ProducesResponseType(typeof(ApiResponse<OrgUnitDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>),     StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("POST /api/orgunits/{Id}/restore", id);

        try
        {
            await _orgUnitService.RestoreAsync(id, CurrentUserId, ct);
            var restored = await _orgUnitService.GetByIdAsync(id, ct);
            return Ok(ApiResponse<OrgUnitDto>.Ok(restored!));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // -------------------------------------------------------------------------
    // CR-001-H: Org-unit head endpoints
    // -------------------------------------------------------------------------

    /// <summary>Get the user designated as this org unit's head.</summary>
    [HttpGet("{id:guid}/head")]
    [Authorize(Policy = PermissionPolicies.OrgChartView)]
    [ProducesResponseType(typeof(ApiResponse<OrgUnitHeadDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>),         StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHead(Guid id, CancellationToken ct = default)
    {
        try
        {
            var head = await _orgUnitService.GetHeadAsync(id, ct);
            return Ok(ApiResponse<OrgUnitHeadDto?>.Ok(head));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
    }

    /// <summary>Assign or replace the org unit head.</summary>
    [HttpPut("{id:guid}/head")]
    [Authorize(Policy = PermissionPolicies.OrgChartAssignHead)]
    [ProducesResponseType(typeof(ApiResponse<OrgUnitHeadDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>),         StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<string>),         StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignHead(
        Guid id,
        [FromBody] AssignOrgUnitHeadRequestDto request,
        CancellationToken ct = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.UserId))
            return BadRequest(ApiResponse<string>.Fail("UserId is required."));

        try
        {
            var head = await _orgUnitService.AssignHeadAsync(id, request.UserId, CurrentUserId, ct);
            return Ok(ApiResponse<OrgUnitHeadDto>.Ok(head));
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

    /// <summary>Clear the org unit head assignment.</summary>
    [HttpDelete("{id:guid}/head")]
    [Authorize(Policy = PermissionPolicies.OrgChartAssignHead)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveHead(Guid id, CancellationToken ct = default)
    {
        try
        {
            await _orgUnitService.RemoveHeadAsync(id, CurrentUserId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Returns the user who would receive a strategic-plan approval request
    /// authored within this org unit (the level-3 ancestor's head).
    /// Returns 200 with null body when no approver can be resolved.
    /// </summary>
    [HttpGet("{id:guid}/plan-approver")]
    [Authorize(Policy = PermissionPolicies.StrategicPlansView)]
    [ProducesResponseType(typeof(ApiResponse<ApprovalAuthorityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlanApprover(Guid id, CancellationToken ct = default)
    {
        var approver = await _approvalAuthorityResolver.ResolvePlanApproverAsync(id, ct);
        return Ok(ApiResponse<ApprovalAuthorityDto?>.Ok(approver));
    }
}

public class AssignOrgUnitHeadRequestDto
{
    public string UserId { get; set; } = string.Empty;
}
