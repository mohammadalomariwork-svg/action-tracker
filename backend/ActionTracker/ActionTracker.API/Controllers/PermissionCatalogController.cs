using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Permissions.DTOs;
using ActionTracker.Application.Permissions.Services;
using ActionTracker.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

[ApiController]
[Route("api/permission-catalog")]
[Authorize]
public class PermissionCatalogController : ControllerBase
{
    private readonly IPermissionCatalogService _catalogService;
    private readonly ILogger<PermissionCatalogController> _logger;

    public PermissionCatalogController(
        IPermissionCatalogService catalogService,
        ILogger<PermissionCatalogController> logger)
    {
        _catalogService = catalogService;
        _logger         = logger;
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Unknown";

    // ══════════════════════════════════════════════════════════════════════════
    // AREAS
    // ══════════════════════════════════════════════════════════════════════════

    // ── GET api/permission-catalog/areas ─────────────────────────────────────

    /// <summary>Returns all active permission areas ordered by DisplayOrder.</summary>
    [HttpGet("areas")]
    [Authorize(Policy = PermissionPolicies.PermissionsManagementView)]
    [ProducesResponseType(typeof(ApiResponse<List<AppPermissionAreaDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAreas()
    {
        _logger.LogInformation("GET /api/permission-catalog/areas");
        var result = await _catalogService.GetAllAreasAsync();
        return Ok(ApiResponse<List<AppPermissionAreaDto>>.Ok(result));
    }

    // ── GET api/permission-catalog/areas/{id} ─────────────────────────────────

    /// <summary>Returns a single permission area by ID.</summary>
    [HttpGet("areas/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PermissionsManagementView)]
    [ProducesResponseType(typeof(ApiResponse<AppPermissionAreaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>),               StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAreaById(Guid id)
    {
        _logger.LogInformation("GET /api/permission-catalog/areas/{Id}", id);
        var result = await _catalogService.GetAreaByIdAsync(id);
        if (result is null)
            return NotFound(ApiResponse<string>.Fail($"Permission area '{id}' not found."));
        return Ok(ApiResponse<AppPermissionAreaDto>.Ok(result));
    }

    // ── POST api/permission-catalog/areas ────────────────────────────────────

    /// <summary>Creates a new permission area.</summary>
    [HttpPost("areas")]
    [Authorize(Policy = PermissionPolicies.PermissionsManagementCreate)]
    [ProducesResponseType(typeof(ApiResponse<AppPermissionAreaDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<string>),               StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<string>),               StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateArea([FromBody] CreateAreaDto dto)
    {
        _logger.LogInformation("POST /api/permission-catalog/areas name={Name}", dto.Name);

        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        try
        {
            var created = await _catalogService.CreateAreaAsync(dto, CurrentUserId);
            return CreatedAtAction(
                nameof(GetAreaById),
                new { id = created.Id },
                ApiResponse<AppPermissionAreaDto>.Ok(created));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // ── PUT api/permission-catalog/areas/{id} ─────────────────────────────────

    /// <summary>Updates an existing permission area.</summary>
    [HttpPut("areas/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PermissionsManagementEdit)]
    [ProducesResponseType(typeof(ApiResponse<AppPermissionAreaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>),               StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<string>),               StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<string>),               StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateArea(Guid id, [FromBody] CreateAreaDto dto)
    {
        _logger.LogInformation("PUT /api/permission-catalog/areas/{Id}", id);

        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        try
        {
            var updated = await _catalogService.UpdateAreaAsync(id, dto, CurrentUserId);
            if (updated is null)
                return NotFound(ApiResponse<string>.Fail($"Permission area '{id}' not found."));
            return Ok(ApiResponse<AppPermissionAreaDto>.Ok(updated));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // ── DELETE api/permission-catalog/areas/{id} ──────────────────────────────

    /// <summary>Soft-deletes a permission area.</summary>
    [HttpDelete("areas/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PermissionsManagementDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteArea(Guid id)
    {
        _logger.LogInformation("DELETE /api/permission-catalog/areas/{Id}", id);
        var deleted = await _catalogService.DeleteAreaAsync(id, CurrentUserId);
        if (!deleted)
            return NotFound(ApiResponse<string>.Fail($"Permission area '{id}' not found."));
        return NoContent();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ACTIONS
    // ══════════════════════════════════════════════════════════════════════════

    // ── GET api/permission-catalog/actions ────────────────────────────────────

    /// <summary>Returns all active permission actions ordered by DisplayOrder.</summary>
    [HttpGet("actions")]
    [Authorize(Policy = PermissionPolicies.PermissionsManagementView)]
    [ProducesResponseType(typeof(ApiResponse<List<AppPermissionActionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllActions()
    {
        _logger.LogInformation("GET /api/permission-catalog/actions");
        var result = await _catalogService.GetAllActionsAsync();
        return Ok(ApiResponse<List<AppPermissionActionDto>>.Ok(result));
    }

    // ── GET api/permission-catalog/actions/{id} ───────────────────────────────

    /// <summary>Returns a single permission action by ID.</summary>
    [HttpGet("actions/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PermissionsManagementView)]
    [ProducesResponseType(typeof(ApiResponse<AppPermissionActionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>),                 StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActionById(Guid id)
    {
        _logger.LogInformation("GET /api/permission-catalog/actions/{Id}", id);
        var result = await _catalogService.GetActionByIdAsync(id);
        if (result is null)
            return NotFound(ApiResponse<string>.Fail($"Permission action '{id}' not found."));
        return Ok(ApiResponse<AppPermissionActionDto>.Ok(result));
    }

    // ── POST api/permission-catalog/actions ───────────────────────────────────

    /// <summary>Creates a new permission action.</summary>
    [HttpPost("actions")]
    [Authorize(Policy = PermissionPolicies.PermissionsManagementCreate)]
    [ProducesResponseType(typeof(ApiResponse<AppPermissionActionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<string>),                 StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<string>),                 StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAction([FromBody] CreateActionDto dto)
    {
        _logger.LogInformation("POST /api/permission-catalog/actions name={Name}", dto.Name);

        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        try
        {
            var created = await _catalogService.CreateActionAsync(dto, CurrentUserId);
            return CreatedAtAction(
                nameof(GetActionById),
                new { id = created.Id },
                ApiResponse<AppPermissionActionDto>.Ok(created));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // ── PUT api/permission-catalog/actions/{id} ───────────────────────────────

    /// <summary>Updates an existing permission action.</summary>
    [HttpPut("actions/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PermissionsManagementEdit)]
    [ProducesResponseType(typeof(ApiResponse<AppPermissionActionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>),                 StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<string>),                 StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<string>),                 StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateAction(Guid id, [FromBody] CreateActionDto dto)
    {
        _logger.LogInformation("PUT /api/permission-catalog/actions/{Id}", id);

        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        try
        {
            var updated = await _catalogService.UpdateActionAsync(id, dto, CurrentUserId);
            if (updated is null)
                return NotFound(ApiResponse<string>.Fail($"Permission action '{id}' not found."));
            return Ok(ApiResponse<AppPermissionActionDto>.Ok(updated));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // ── DELETE api/permission-catalog/actions/{id} ────────────────────────────

    /// <summary>Soft-deletes a permission action.</summary>
    [HttpDelete("actions/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PermissionsManagementDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAction(Guid id)
    {
        _logger.LogInformation("DELETE /api/permission-catalog/actions/{Id}", id);
        var deleted = await _catalogService.DeleteActionAsync(id, CurrentUserId);
        if (!deleted)
            return NotFound(ApiResponse<string>.Fail($"Permission action '{id}' not found."));
        return NoContent();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // MAPPINGS
    // ══════════════════════════════════════════════════════════════════════════

    // ── GET api/permission-catalog/mappings ───────────────────────────────────

    /// <summary>Returns all area-action mappings.</summary>
    [HttpGet("mappings")]
    [Authorize(Policy = PermissionPolicies.PermissionsManagementView)]
    [ProducesResponseType(typeof(ApiResponse<List<AreaActionMappingDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllMappings()
    {
        _logger.LogInformation("GET /api/permission-catalog/mappings");
        var result = await _catalogService.GetAllMappingsAsync();
        return Ok(ApiResponse<List<AreaActionMappingDto>>.Ok(result));
    }

    // ── GET api/permission-catalog/mappings/by-area/{areaId} ─────────────────

    /// <summary>Returns all mappings for a specific area.</summary>
    [HttpGet("mappings/by-area/{areaId:guid}")]
    [Authorize(Policy = PermissionPolicies.PermissionsManagementView)]
    [ProducesResponseType(typeof(ApiResponse<List<AreaActionMappingDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMappingsByArea(Guid areaId)
    {
        _logger.LogInformation("GET /api/permission-catalog/mappings/by-area/{AreaId}", areaId);
        var result = await _catalogService.GetMappingsByAreaAsync(areaId);
        return Ok(ApiResponse<List<AreaActionMappingDto>>.Ok(result));
    }

    // ── POST api/permission-catalog/mappings ──────────────────────────────────

    /// <summary>Creates a new area-action mapping.</summary>
    [HttpPost("mappings")]
    [Authorize(Policy = PermissionPolicies.PermissionsManagementCreate)]
    [ProducesResponseType(typeof(ApiResponse<AreaActionMappingDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<string>),               StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<string>),               StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateMapping([FromBody] CreateAreaActionMappingDto dto)
    {
        _logger.LogInformation(
            "POST /api/permission-catalog/mappings area={AreaId} action={ActionId}",
            dto.AreaId, dto.ActionId);

        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        try
        {
            var created = await _catalogService.CreateMappingAsync(dto, CurrentUserId);
            return CreatedAtAction(
                nameof(DeleteMapping),
                new { id = created.Id },
                ApiResponse<AreaActionMappingDto>.Ok(created));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // ── DELETE api/permission-catalog/mappings/{id} ───────────────────────────

    /// <summary>Soft-deletes an area-action mapping.</summary>
    [HttpDelete("mappings/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PermissionsManagementDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMapping(Guid id)
    {
        _logger.LogInformation("DELETE /api/permission-catalog/mappings/{Id}", id);
        var deleted = await _catalogService.DeleteMappingAsync(id, CurrentUserId);
        if (!deleted)
            return NotFound(ApiResponse<string>.Fail($"Mapping '{id}' not found."));
        return NoContent();
    }
}
