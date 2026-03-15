using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Permissions.DTOs;
using ActionTracker.Application.RoleManagement.DTOs;
using ActionTracker.Application.RoleManagement.Services;
using ActionTracker.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

[ApiController]
[Route("api/roles")]
[Authorize]
public class RoleManagementController : ControllerBase
{
    private readonly IRoleManagementService _roleService;
    private readonly ILogger<RoleManagementController> _logger;

    public RoleManagementController(
        IRoleManagementService roleService,
        ILogger<RoleManagementController> logger)
    {
        _roleService = roleService;
        _logger      = logger;
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Unknown";

    // ── GET api/roles ─────────────────────────────────────────────────────────

    /// <summary>Returns all roles with user and permission counts.</summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.RolesView)]
    [ProducesResponseType(typeof(ApiResponse<List<AppRoleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        _logger.LogInformation("GET /api/roles");
        var result = await _roleService.GetAllRolesAsync();
        return Ok(ApiResponse<List<AppRoleDto>>.Ok(result));
    }

    // ── GET api/roles/{roleName} ──────────────────────────────────────────────

    /// <summary>Returns a single role by name.</summary>
    [HttpGet("{roleName}")]
    [Authorize(Policy = PermissionPolicies.RolesView)]
    [ProducesResponseType(typeof(ApiResponse<AppRoleDto>),  StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>),      StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByName(string roleName)
    {
        _logger.LogInformation("GET /api/roles/{RoleName}", roleName);
        var result = await _roleService.GetRoleByNameAsync(roleName);
        if (result is null)
            return NotFound(ApiResponse<string>.Fail($"Role '{roleName}' not found."));
        return Ok(ApiResponse<AppRoleDto>.Ok(result));
    }

    // ── GET api/roles/{roleName}/users ────────────────────────────────────────

    /// <summary>Returns all users assigned to the role.</summary>
    [HttpGet("{roleName}/users")]
    [Authorize(Policy = PermissionPolicies.RolesView)]
    [ProducesResponseType(typeof(ApiResponse<List<RoleUserDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers(string roleName)
    {
        _logger.LogInformation("GET /api/roles/{RoleName}/users", roleName);
        var result = await _roleService.GetUsersInRoleAsync(roleName);
        return Ok(ApiResponse<List<RoleUserDto>>.Ok(result));
    }

    // ── GET api/roles/{roleName}/permissions ──────────────────────────────────

    /// <summary>Returns the full permission matrix for the role.</summary>
    [HttpGet("{roleName}/permissions")]
    [Authorize(Policy = PermissionPolicies.RolesView)]
    [ProducesResponseType(typeof(ApiResponse<PermissionMatrixDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPermissions(string roleName)
    {
        _logger.LogInformation("GET /api/roles/{RoleName}/permissions", roleName);
        var result = await _roleService.GetRolePermissionSummaryAsync(roleName);
        return Ok(ApiResponse<PermissionMatrixDto>.Ok(result));
    }

    // ── POST api/roles ────────────────────────────────────────────────────────

    /// <summary>Creates a new application role.</summary>
    [HttpPost]
    [Authorize(Policy = PermissionPolicies.RolesCreate)]
    [ProducesResponseType(typeof(ApiResponse<AppRoleDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<string>),     StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<string>),     StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateRoleDto dto)
    {
        _logger.LogInformation("POST /api/roles name={Name}", dto.Name);

        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        try
        {
            var created = await _roleService.CreateRoleAsync(dto.Name, CurrentUserId);
            return CreatedAtAction(
                nameof(GetByName),
                new { roleName = created.Name },
                ApiResponse<AppRoleDto>.Ok(created));
        }
        catch (ArgumentException ex)
        {
            return Conflict(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // ── DELETE api/roles/{roleName} ───────────────────────────────────────────

    /// <summary>Deletes a role. Returns 409 if users are still assigned.</summary>
    [HttpDelete("{roleName}")]
    [Authorize(Policy = PermissionPolicies.RolesDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(string roleName)
    {
        _logger.LogInformation("DELETE /api/roles/{RoleName}", roleName);

        try
        {
            var deleted = await _roleService.DeleteRoleAsync(roleName, CurrentUserId);
            if (!deleted)
                return NotFound(ApiResponse<string>.Fail($"Role '{roleName}' not found."));
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // ── POST api/roles/{roleName}/permissions ─────────────────────────────────

    /// <summary>
    /// Full-replace permission assignment for the role.
    /// The body must contain the complete desired permission set.
    /// </summary>
    [HttpPost("{roleName}/permissions")]
    [Authorize(Policy = PermissionPolicies.RolesEdit)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignPermissions(
        string roleName,
        [FromBody] AssignRolePermissionsDto dto)
    {
        _logger.LogInformation(
            "POST /api/roles/{RoleName}/permissions count={Count}", roleName, dto.Permissions.Count);

        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        // Route param is authoritative — keep DTO consistent
        dto.RoleName = roleName;

        try
        {
            await _roleService.AssignPermissionsToRoleAsync(dto, CurrentUserId);
            return Ok(ApiResponse<string>.Ok("Permissions updated."));
        }
        catch (ArgumentException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // ── POST api/roles/{roleName}/users/assign ────────────────────────────────

    /// <summary>Assigns one or more users to the role.</summary>
    [HttpPost("{roleName}/users/assign")]
    [Authorize(Policy = PermissionPolicies.RolesAssign)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignUsers(
        string roleName,
        [FromBody] AssignUsersToRoleDto dto)
    {
        _logger.LogInformation(
            "POST /api/roles/{RoleName}/users/assign count={Count}", roleName, dto.UserIds.Count);

        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        dto.RoleName = roleName;

        try
        {
            await _roleService.AssignUsersToRoleAsync(dto, CurrentUserId);
            return Ok(ApiResponse<string>.Ok("Users assigned."));
        }
        catch (ArgumentException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // ── POST api/roles/{roleName}/users/remove ────────────────────────────────

    /// <summary>Removes one or more users from the role.</summary>
    [HttpPost("{roleName}/users/remove")]
    [Authorize(Policy = PermissionPolicies.RolesAssign)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveUsers(
        string roleName,
        [FromBody] RemoveUsersFromRoleDto dto)
    {
        _logger.LogInformation(
            "POST /api/roles/{RoleName}/users/remove count={Count}", roleName, dto.UserIds.Count);

        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        dto.RoleName = roleName;

        try
        {
            await _roleService.RemoveUsersFromRoleAsync(dto, CurrentUserId);
            return Ok(ApiResponse<string>.Ok("Users removed."));
        }
        catch (ArgumentException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
    }
}
