using System;
using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.Workspaces.DTOs;
using ActionTracker.Application.Features.Workspaces.Interfaces;
using ActionTracker.Application.Helpers;
using ActionTracker.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

/// <summary>
/// Manages workspace resources — creation, retrieval, update, and soft-delete.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "LocalOrAzureAD")]
public class WorkspacesController : ControllerBase
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IOrgUnitScopeResolver _scopeResolver;
    private readonly ILogger<WorkspacesController> _logger;

    public WorkspacesController(
        IWorkspaceService workspaceService,
        IOrgUnitScopeResolver scopeResolver,
        ILogger<WorkspacesController> logger)
    {
        _workspaceService = workspaceService;
        _scopeResolver    = scopeResolver;
        _logger           = logger;
    }

    // -------------------------------------------------------------------------
    // GET api/workspaces
    // -------------------------------------------------------------------------

    /// <summary>Returns all active workspaces ordered by title.</summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.WorkspacesView)]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<WorkspaceListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        _logger.LogInformation("GET /api/workspaces");

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var visibleOrgUnitIds = string.IsNullOrEmpty(userId)
            ? null
            : await _scopeResolver.GetUserOrgUnitIdsAsync(userId);

        var workspaces = await _workspaceService.GetAllWorkspacesAsync(
            visibleOrgUnitIds?.Count > 0 ? visibleOrgUnitIds : null);
        return Ok(ApiResponse<IEnumerable<WorkspaceListDto>>.Ok(workspaces));
    }

    // -------------------------------------------------------------------------
    // GET api/workspaces/summary
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns aggregate workspace statistics scoped to the caller's visible org units.
    /// An optional <paramref name="orgUnitId"/> query parameter narrows the scope further
    /// to a single org unit (must be within the caller's visible set).
    /// </summary>
    [HttpGet("summary")]
    [Authorize(Policy = PermissionPolicies.WorkspacesView)]
    [ProducesResponseType(typeof(ApiResponse<WorkspaceSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary([FromQuery] Guid? orgUnitId = null)
    {
        _logger.LogInformation("GET /api/workspaces/summary orgUnitId={OrgUnitId}", orgUnitId);

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var visibleOrgUnitIds = string.IsNullOrEmpty(userId)
            ? null
            : await _scopeResolver.GetUserOrgUnitIdsAsync(userId);

        // Determine the effective scope: user's visible org units, optionally narrowed
        // down to a single org unit chosen in the UI.
        List<Guid>? effectiveOrgUnitIds;
        if (orgUnitId.HasValue)
        {
            var hasScope = visibleOrgUnitIds != null && visibleOrgUnitIds.Count > 0;
            if (!hasScope || visibleOrgUnitIds!.Contains(orgUnitId.Value))
                effectiveOrgUnitIds = new List<Guid> { orgUnitId.Value };
            else
                effectiveOrgUnitIds = new List<Guid>(); // requested org unit not in scope → zeros
        }
        else
        {
            effectiveOrgUnitIds = visibleOrgUnitIds?.Count > 0 ? visibleOrgUnitIds : null;
        }

        var summary = await _workspaceService.GetSummaryAsync(effectiveOrgUnitIds);
        return Ok(ApiResponse<WorkspaceSummaryDto>.Ok(summary));
    }

    // -------------------------------------------------------------------------
    // GET api/workspaces/{id}/stats
    // -------------------------------------------------------------------------

    /// <summary>Returns per-workspace statistics for the workspace detail dashboard.</summary>
    /// <param name="id">Primary key of the workspace.</param>
    [HttpGet("{id:guid}/stats")]
    [Authorize(Policy = PermissionPolicies.WorkspacesView)]
    [ProducesResponseType(typeof(ApiResponse<WorkspaceStatsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>),            StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStats(Guid id)
    {
        _logger.LogInformation("GET /api/workspaces/{Id}/stats", id);

        var stats = await _workspaceService.GetWorkspaceStatsAsync(id);
        if (stats is null)
            return NotFound(ApiResponse<string>.Fail($"Workspace '{id}' not found."));

        return Ok(ApiResponse<WorkspaceStatsDto>.Ok(stats));
    }

    // -------------------------------------------------------------------------
    // GET api/workspaces/{id}
    // -------------------------------------------------------------------------

    /// <summary>Returns the full details of a single workspace.</summary>
    /// <param name="id">Primary key of the workspace.</param>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.WorkspacesView)]
    [ProducesResponseType(typeof(ApiResponse<WorkspaceResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>),               StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        _logger.LogInformation("GET /api/workspaces/{Id}", id);

        var workspace = await _workspaceService.GetWorkspaceByIdAsync(id);
        if (workspace is null)
            return NotFound(ApiResponse<string>.Fail($"Workspace '{id}' not found."));

        return Ok(ApiResponse<WorkspaceResponseDto>.Ok(workspace));
    }

    // -------------------------------------------------------------------------
    // GET api/workspaces/by-admin/{adminUserId}
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns all active workspaces where the specified user is the admin.
    /// </summary>
    /// <param name="adminUserId">The AspNetUsers.Id of the admin user.</param>
    [HttpGet("by-admin/{adminUserId}")]
    [Authorize(Policy = "LocalOrAzureAD")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<WorkspaceListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByAdminUserId(string adminUserId)
    {
        _logger.LogInformation("GET /api/workspaces/by-admin/{AdminUserId}", adminUserId);

        var workspaces = await _workspaceService.GetWorkspacesByAdminUserIdAsync(adminUserId);
        return Ok(ApiResponse<IEnumerable<WorkspaceListDto>>.Ok(workspaces));
    }

    // -------------------------------------------------------------------------
    // POST api/workspaces
    // -------------------------------------------------------------------------

    /// <summary>Creates a new workspace.</summary>
    [HttpPost]
    [Authorize(Policy = PermissionPolicies.WorkspacesCreate)]
    [ProducesResponseType(typeof(ApiResponse<WorkspaceResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<string>),               StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateWorkspaceDto dto)
    {
        _logger.LogInformation("POST /api/workspaces title={Title}", dto.Title);

        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        var created = await _workspaceService.CreateWorkspaceAsync(dto);

        return CreatedAtAction(
            nameof(GetById),
            new { id = created.Id },
            ApiResponse<WorkspaceResponseDto>.Ok(created));
    }

    // -------------------------------------------------------------------------
    // PUT api/workspaces/{id}
    // -------------------------------------------------------------------------

    /// <summary>Updates an existing workspace.</summary>
    /// <param name="id">Primary key of the workspace to update.</param>
    /// <param name="dto">Update payload. The <c>Id</c> field must match the route.</param>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.WorkspacesEdit)]
    [ProducesResponseType(typeof(ApiResponse<WorkspaceResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>),               StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<string>),               StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWorkspaceDto dto)
    {
        _logger.LogInformation("PUT /api/workspaces/{Id}", id);

        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        if (id != dto.Id)
            return BadRequest(ApiResponse<string>.Fail("Route id does not match the request body id."));

        var updated = await _workspaceService.UpdateWorkspaceAsync(id, dto);
        if (updated is null)
            return NotFound(ApiResponse<string>.Fail($"Workspace '{id}' not found."));

        return Ok(ApiResponse<WorkspaceResponseDto>.Ok(updated));
    }

    // -------------------------------------------------------------------------
    // GET api/workspaces/org-units  (dropdown data)
    // -------------------------------------------------------------------------

    /// <summary>Returns a flat list of non-deleted org units for the workspace form dropdown.</summary>
    [HttpGet("org-units")]
    [Authorize(Policy = PermissionPolicies.WorkspacesView)]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<OrgUnitDropdownItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrgUnitsForDropdown()
    {
        _logger.LogInformation("GET /api/workspaces/org-units");

        var items = await _workspaceService.GetOrgUnitsForDropdownAsync();
        return Ok(ApiResponse<IEnumerable<OrgUnitDropdownItemDto>>.Ok(items));
    }

    // -------------------------------------------------------------------------
    // GET api/workspaces/active-users  (dropdown data)
    // -------------------------------------------------------------------------

    /// <summary>Returns active users for the workspace admin dropdown.</summary>
    [HttpGet("active-users")]
    [Authorize(Policy = PermissionPolicies.WorkspacesView)]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<UserDropdownItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveUsersForDropdown()
    {
        _logger.LogInformation("GET /api/workspaces/active-users");

        var items = await _workspaceService.GetActiveUsersForDropdownAsync();
        return Ok(ApiResponse<IEnumerable<UserDropdownItemDto>>.Ok(items));
    }

    // -------------------------------------------------------------------------
    // DELETE api/workspaces/{id}
    // -------------------------------------------------------------------------

    /// <summary>Soft-deletes a workspace by setting it inactive.</summary>
    /// <param name="id">Primary key of the workspace to delete.</param>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.WorkspacesDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        _logger.LogInformation("DELETE /api/workspaces/{Id}", id);

        var deleted = await _workspaceService.DeleteWorkspaceAsync(id);
        if (!deleted)
            return NotFound(ApiResponse<string>.Fail($"Workspace '{id}' not found."));

        return NoContent();
    }

    // -------------------------------------------------------------------------
    // PATCH api/workspaces/{id}/restore
    // -------------------------------------------------------------------------

    /// <summary>Restores a soft-deleted workspace by setting it active again.</summary>
    /// <param name="id">Primary key of the workspace to restore.</param>
    [HttpPatch("{id:guid}/restore")]
    [Authorize(Policy = PermissionPolicies.WorkspacesEdit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Restore(Guid id)
    {
        _logger.LogInformation("PATCH /api/workspaces/{Id}/restore", id);

        var restored = await _workspaceService.RestoreWorkspaceAsync(id);
        if (!restored)
            return NotFound(ApiResponse<string>.Fail($"Workspace '{id}' not found."));

        return NoContent();
    }
}
