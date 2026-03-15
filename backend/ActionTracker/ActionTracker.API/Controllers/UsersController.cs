using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.UserManagement.DTOs;
using ActionTracker.Application.Features.UserManagement.Interfaces;
using ActionTracker.Application.Helpers;
using ActionTracker.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "LocalOrAzureAD")]
public class UsersController : ControllerBase
{
    private readonly IUserManagementService      _userManagement;
    private readonly IOrgUnitScopeResolver       _orgUnitScopeResolver;
    private readonly ILogger<UsersController>    _logger;

    public UsersController(
        IUserManagementService   userManagement,
        IOrgUnitScopeResolver    orgUnitScopeResolver,
        ILogger<UsersController> logger)
    {
        _userManagement       = userManagement;
        _orgUnitScopeResolver = orgUnitScopeResolver;
        _logger               = logger;
    }

    // -------------------------------------------------------------------------
    // GET api/users/roles
    // -------------------------------------------------------------------------

    /// <summary>Returns all role names defined in the system, sorted A → Z.</summary>
    [HttpGet("roles")]
    [Authorize(Policy = PermissionPolicies.UserManagementView)]
    [ProducesResponseType(typeof(ApiResponse<List<string>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoles(CancellationToken ct = default)
    {
        var roles = await _userManagement.GetAllRolesAsync(ct);
        return Ok(ApiResponse<List<string>>.Ok(roles));
    }

    // -------------------------------------------------------------------------
    // GET api/users
    // -------------------------------------------------------------------------

    /// <summary>Get all users with their roles (paged, searchable, sortable).</summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.UserManagementView)]
    [ProducesResponseType(typeof(ApiResponse<UserListResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllUsers(
        [FromQuery] int    page     = 1,
        [FromQuery] int    pageSize = 10,
        [FromQuery] string search   = "",
        [FromQuery] string sortBy   = "fullName",
        [FromQuery] string sortDir  = "asc",
        CancellationToken  ct       = default)
    {
        _logger.LogInformation(
            "GET /api/users page={Page} pageSize={PageSize} search={Search} sortBy={SortBy} sortDir={SortDir}",
            page, pageSize, search, sortBy, sortDir);

        var result = await _userManagement.GetAllUsersAsync(page, pageSize, search, sortBy, sortDir, ct);
        return Ok(ApiResponse<UserListResponseDto>.Ok(result));
    }

    // -------------------------------------------------------------------------
    // GET api/users/{id}
    // -------------------------------------------------------------------------

    /// <summary>Get a single user by ID.</summary>
    [HttpGet("{id}", Name = nameof(GetUserById))]
    [Authorize(Policy = PermissionPolicies.UserManagementView)]
    [ProducesResponseType(typeof(ApiResponse<UserListItemDto>),  StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>),           StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserById(string id, CancellationToken ct = default)
    {
        _logger.LogInformation("GET /api/users/{Id}", id);

        var user = await _userManagement.GetUserByIdAsync(id, ct);
        if (user is null)
            return NotFound(ApiResponse<string>.Fail($"User '{id}' not found."));

        return Ok(ApiResponse<UserListItemDto>.Ok(user));
    }

    // -------------------------------------------------------------------------
    // POST api/users/register-external
    // -------------------------------------------------------------------------

    /// <summary>Register a new local (username/password) user and assign a role.</summary>
    [HttpPost("register-external")]
    [Authorize(Policy = PermissionPolicies.UserManagementCreate)]
    [ProducesResponseType(typeof(ApiResponse<RegisterUserResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<string>),                  StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<string>),                  StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterExternalUser(
        [FromBody] RegisterExternalUserRequestDto request,
        CancellationToken ct = default)
    {
        _logger.LogInformation("POST /api/users/register-external email={Email}", request.Email);

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var created = await _userManagement.RegisterExternalUserAsync(request, ct);
            return CreatedAtRoute(
                nameof(GetUserById),
                new { id = created.UserId },
                ApiResponse<RegisterUserResponseDto>.Ok(created));
        }
        catch (ArgumentException ex)
        {
            return Conflict(ApiResponse<string>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // -------------------------------------------------------------------------
    // POST api/users/register-ad
    // -------------------------------------------------------------------------

    /// <summary>Register a new Azure AD user (no password) and assign a role.</summary>
    [HttpPost("register-ad")]
    [Authorize(Policy = PermissionPolicies.UserManagementCreate)]
    [ProducesResponseType(typeof(ApiResponse<RegisterUserResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<string>),                  StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<string>),                  StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterAdUser(
        [FromBody] RegisterADUserRequestDto request,
        CancellationToken ct = default)
    {
        _logger.LogInformation("POST /api/users/register-ad email={Email}", request.Email);

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var created = await _userManagement.RegisterADUserAsync(request, ct);
            return CreatedAtRoute(
                nameof(GetUserById),
                new { id = created.UserId },
                ApiResponse<RegisterUserResponseDto>.Ok(created));
        }
        catch (ArgumentException ex)
        {
            return Conflict(ApiResponse<string>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // -------------------------------------------------------------------------
    // GET api/users/search-employees
    // -------------------------------------------------------------------------

    /// <summary>
    /// Search the KU employee directory by name, email, or employee ID.
    /// Each result indicates whether the employee is already registered in the system.
    /// </summary>
    [HttpGet("search-employees")]
    [Authorize(Policy = PermissionPolicies.UserManagementView)]
    [ProducesResponseType(typeof(ApiResponse<EmployeeSearchResultDto[]>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchEmployees(
        [FromQuery] string searchTerm,
        [FromQuery] int    page     = 1,
        [FromQuery] int    pageSize = 10,
        CancellationToken  ct       = default)
    {
        _logger.LogInformation(
            "GET /api/users/search-employees term={Term} page={Page}", searchTerm, page);

        var request = new EmployeeSearchRequestDto
        {
            SearchTerm = searchTerm ?? string.Empty,
            Page       = page,
            PageSize   = pageSize,
        };

        var results = await _userManagement.SearchEmployeesAsync(request, ct);
        return Ok(ApiResponse<EmployeeSearchResultDto[]>.Ok(results));
    }

    // -------------------------------------------------------------------------
    // PUT api/users/update-role
    // -------------------------------------------------------------------------

    /// <summary>Replace all roles for a user with the specified role.</summary>
    [HttpPut("update-role")]
    [Authorize(Policy = PermissionPolicies.UserManagementEdit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateUserRole(
        [FromBody] UpdateUserRoleRequestDto request,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "PUT /api/users/update-role userId={UserId} role={Role}",
            request.UserId, request.RoleName);

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            await _userManagement.UpdateUserRoleAsync(request, ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // -------------------------------------------------------------------------
    // PUT api/users/{id}/deactivate
    // -------------------------------------------------------------------------

    /// <summary>Deactivate a user account, preventing future logins.</summary>
    [HttpPut("{id}/deactivate")]
    [Authorize(Policy = PermissionPolicies.UserManagementEdit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeactivateUser(string id, CancellationToken ct = default)
    {
        _logger.LogInformation("PUT /api/users/{Id}/deactivate", id);

        try
        {
            await _userManagement.DeactivateUserAsync(id, ct);
            return NoContent();
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
    // PUT api/users/{id}/reactivate
    // -------------------------------------------------------------------------

    /// <summary>Reactivate a previously deactivated user account.</summary>
    [HttpPut("{id}/reactivate")]
    [Authorize(Policy = PermissionPolicies.UserManagementEdit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReactivateUser(string id, CancellationToken ct = default)
    {
        _logger.LogInformation("PUT /api/users/{Id}/reactivate", id);

        try
        {
            await _userManagement.ReactivateUserAsync(id, ct);
            return NoContent();
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
    // PUT api/users/{id}/assign-org-unit
    // -------------------------------------------------------------------------

    /// <summary>Assign (or unassign) an org unit for a user.</summary>
    [HttpPut("{id}/assign-org-unit")]
    [Authorize(Policy = PermissionPolicies.UserManagementEdit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AssignOrgUnit(
        string id,
        [FromBody] AssignUserOrgUnitRequestDto request,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "PUT /api/users/{Id}/assign-org-unit orgUnitId={OrgUnitId}", id, request.OrgUnitId);

        try
        {
            await _userManagement.AssignUserOrgUnitAsync(id, request.OrgUnitId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // -------------------------------------------------------------------------
    // GET api/users/me/org-units
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the org unit IDs visible to the currently authenticated user.
    /// Includes the user's own org unit and all descendants.
    /// Returns an empty list when the user has no assigned org unit.
    /// </summary>
    [HttpGet("me/org-units")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyOrgUnits(CancellationToken ct = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var orgUnitIds = await _orgUnitScopeResolver.GetUserOrgUnitIdsAsync(userId);

        return Ok(new
        {
            orgUnitIds = orgUnitIds.Select(id => id.ToString()).ToList(),
            count      = orgUnitIds.Count,
        });
    }
}
