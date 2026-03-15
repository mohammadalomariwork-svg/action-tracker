using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Permissions.DTOs;
using ActionTracker.Application.Permissions.Services;
using ActionTracker.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

[ApiController]
[Route("api/user-permissions")]
[Authorize]
public class UserPermissionsController : ControllerBase
{
    private readonly IUserPermissionOverrideService _overrideService;
    private readonly IEffectivePermissionService    _effectiveService;
    private readonly ILogger<UserPermissionsController> _logger;

    public UserPermissionsController(
        IUserPermissionOverrideService overrideService,
        IEffectivePermissionService    effectiveService,
        ILogger<UserPermissionsController> logger)
    {
        _overrideService  = overrideService;
        _effectiveService = effectiveService;
        _logger           = logger;
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Unknown";

    // ─────────────────────────────────────────────────────────────────────────
    // GET api/user-permissions/{userId}/overrides
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Returns all active permission overrides for the given user.</summary>
    [HttpGet("{userId}/overrides")]
    [Authorize(Policy = PermissionPolicies.PermissionsManagementView)]
    [ProducesResponseType(typeof(ApiResponse<List<UserPermissionOverrideDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverridesByUser(string userId)
    {
        _logger.LogInformation("GET /api/user-permissions/{UserId}/overrides", userId);

        var result = await _overrideService.GetAllByUserAsync(userId);
        return Ok(ApiResponse<List<UserPermissionOverrideDto>>.Ok(result));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET api/user-permissions/overrides/{id:guid}
    // Must be declared before /{userId}/... so the literal "overrides" wins.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Returns a single permission override by ID.</summary>
    [HttpGet("overrides/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PermissionsManagementView)]
    [ProducesResponseType(typeof(ApiResponse<UserPermissionOverrideDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>),                   StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOverrideById(Guid id)
    {
        _logger.LogInformation("GET /api/user-permissions/overrides/{Id}", id);

        var result = await _overrideService.GetByIdAsync(id);
        if (result is null)
            return NotFound(ApiResponse<string>.Fail($"UserPermissionOverride '{id}' not found."));

        return Ok(ApiResponse<UserPermissionOverrideDto>.Ok(result));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST api/user-permissions/overrides
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Creates a new user-level permission override.</summary>
    [HttpPost("overrides")]
    [Authorize(Policy = PermissionPolicies.PermissionsManagementCreate)]
    [ProducesResponseType(typeof(ApiResponse<UserPermissionOverrideDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<string>),                   StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<string>),                   StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateOverride([FromBody] CreateUserPermissionOverrideDto dto)
    {
        _logger.LogInformation(
            "POST /api/user-permissions/overrides userId={UserId} areaId={AreaId} actionId={ActionId}",
            dto.UserId, dto.AreaId, dto.ActionId);

        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        try
        {
            var created = await _overrideService.CreateAsync(dto, CurrentUserId);
            return CreatedAtAction(
                nameof(GetOverrideById),
                new { id = created.Id },
                ApiResponse<UserPermissionOverrideDto>.Ok(created));
        }
        catch (ArgumentException ex)
        {
            return Conflict(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PUT api/user-permissions/overrides/{id:guid}
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Updates a user permission override.</summary>
    [HttpPut("overrides/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PermissionsManagementEdit)]
    [ProducesResponseType(typeof(ApiResponse<UserPermissionOverrideDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>),                   StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<string>),                   StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateOverride(Guid id, [FromBody] UpdateUserPermissionOverrideDto dto)
    {
        _logger.LogInformation("PUT /api/user-permissions/overrides/{Id}", id);

        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        try
        {
            var updated = await _overrideService.UpdateAsync(id, dto, CurrentUserId);
            return Ok(ApiResponse<UserPermissionOverrideDto>.Ok(updated));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DELETE api/user-permissions/overrides/{id:guid}
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Soft-deletes a user permission override.</summary>
    [HttpDelete("overrides/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PermissionsManagementDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteOverride(Guid id)
    {
        _logger.LogInformation("DELETE /api/user-permissions/overrides/{Id}", id);

        try
        {
            await _overrideService.DeleteAsync(id, CurrentUserId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET api/user-permissions/{userId}/effective
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Returns the merged effective permissions for a given user (admin view).</summary>
    [HttpGet("{userId}/effective")]
    [Authorize(Policy = PermissionPolicies.PermissionsManagementView)]
    [ProducesResponseType(typeof(ApiResponse<List<EffectivePermissionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEffectivePermissions(string userId)
    {
        _logger.LogInformation("GET /api/user-permissions/{UserId}/effective", userId);

        var result = await _effectiveService.GetEffectivePermissionsAsync(userId);
        return Ok(ApiResponse<List<EffectivePermissionDto>>.Ok(result));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET api/user-permissions/me/effective
    // Must be declared before /{userId}/effective so "me" wins routing.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Returns the effective permissions for the currently authenticated user.</summary>
    [HttpGet("me/effective")]
    [ProducesResponseType(typeof(ApiResponse<List<EffectivePermissionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyEffectivePermissions()
    {
        _logger.LogInformation("GET /api/user-permissions/me/effective userId={UserId}", CurrentUserId);

        var result = await _effectiveService.GetEffectivePermissionsAsync(CurrentUserId);
        return Ok(ApiResponse<List<EffectivePermissionDto>>.Ok(result));
    }
}
