using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Permissions.DTOs;
using ActionTracker.Application.Permissions.Services;
using ActionTracker.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

[ApiController]
[Route("api/role-permissions")]
[Authorize]
public class RolePermissionsController : ControllerBase
{
    private readonly IRolePermissionService           _rolePermissionService;
    private readonly RoleManager<IdentityRole>        _roleManager;
    private readonly ILogger<RolePermissionsController> _logger;

    public RolePermissionsController(
        IRolePermissionService           rolePermissionService,
        RoleManager<IdentityRole>        roleManager,
        ILogger<RolePermissionsController> logger)
    {
        _rolePermissionService = rolePermissionService;
        _roleManager           = roleManager;
        _logger                = logger;
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Unknown";

    // ─────────────────────────────────────────────────────────────────────────
    // GET api/role-permissions/roles
    // Must be declared before /{roleName} so the literal "roles" wins routing.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Returns all role names currently defined in the system.</summary>
    [HttpGet("roles")]
    [Authorize(Policy = PermissionPolicies.PermissionsManagementView)]
    [ProducesResponseType(typeof(ApiResponse<List<string>>), StatusCodes.Status200OK)]
    public IActionResult GetRoles()
    {
        _logger.LogInformation("GET /api/role-permissions/roles");

        var roles = _roleManager.Roles
            .OrderBy(r => r.Name)
            .Select(r => r.Name!)
            .ToList();

        return Ok(ApiResponse<List<string>>.Ok(roles));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET api/role-permissions/{roleName}
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Returns all active permissions for the given role.</summary>
    [HttpGet("{roleName}")]
    [Authorize(Policy = PermissionPolicies.PermissionsManagementView)]
    [ProducesResponseType(typeof(ApiResponse<List<RolePermissionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByRole(string roleName)
    {
        _logger.LogInformation("GET /api/role-permissions/{RoleName}", roleName);

        var result = await _rolePermissionService.GetAllByRoleAsync(roleName);
        return Ok(ApiResponse<List<RolePermissionDto>>.Ok(result));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET api/role-permissions/{id:guid}/detail
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Returns a single role permission by ID.</summary>
    [HttpGet("{id:guid}/detail")]
    [Authorize(Policy = PermissionPolicies.PermissionsManagementView)]
    [ProducesResponseType(typeof(ApiResponse<RolePermissionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>),            StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        _logger.LogInformation("GET /api/role-permissions/{Id}/detail", id);

        var result = await _rolePermissionService.GetByIdAsync(id);
        if (result is null)
            return NotFound(ApiResponse<string>.Fail($"RolePermission '{id}' not found."));

        return Ok(ApiResponse<RolePermissionDto>.Ok(result));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET api/role-permissions/matrix/{roleName}
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Returns the full permission matrix for a role.</summary>
    [HttpGet("matrix/{roleName}")]
    [Authorize(Policy = PermissionPolicies.PermissionsManagementView)]
    [ProducesResponseType(typeof(ApiResponse<PermissionMatrixDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMatrix(string roleName)
    {
        _logger.LogInformation("GET /api/role-permissions/matrix/{RoleName}", roleName);

        var result = await _rolePermissionService.GetPermissionMatrixAsync(roleName);
        return Ok(ApiResponse<PermissionMatrixDto>.Ok(result));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST api/role-permissions
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Creates a new role permission.</summary>
    [HttpPost]
    [Authorize(Policy = PermissionPolicies.PermissionsManagementCreate)]
    [ProducesResponseType(typeof(ApiResponse<RolePermissionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<string>),            StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<string>),            StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateRolePermissionDto dto)
    {
        _logger.LogInformation(
            "POST /api/role-permissions role={Role} areaId={AreaId} actionId={ActionId}",
            dto.RoleName, dto.AreaId, dto.ActionId);

        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        try
        {
            var created = await _rolePermissionService.CreateAsync(dto, CurrentUserId);
            return CreatedAtAction(
                nameof(GetById),
                new { id = created.Id },
                ApiResponse<RolePermissionDto>.Ok(created));
        }
        catch (ArgumentException ex)
        {
            return Conflict(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PUT api/role-permissions/{id:guid}
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Updates a role permission's scope and active flag.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PermissionsManagementEdit)]
    [ProducesResponseType(typeof(ApiResponse<RolePermissionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>),            StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<string>),            StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRolePermissionDto dto)
    {
        _logger.LogInformation("PUT /api/role-permissions/{Id}", id);

        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        try
        {
            var updated = await _rolePermissionService.UpdateAsync(id, dto, CurrentUserId);
            return Ok(ApiResponse<RolePermissionDto>.Ok(updated));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DELETE api/role-permissions/{id:guid}
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Soft-deletes a role permission.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PermissionsManagementDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        _logger.LogInformation("DELETE /api/role-permissions/{Id}", id);

        try
        {
            await _rolePermissionService.DeleteAsync(id, CurrentUserId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
    }
}
