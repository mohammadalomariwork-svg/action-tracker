using System;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.Workspaces.DTOs;
using ActionTracker.Application.Features.Workspaces.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

/// <summary>
/// Manages workspace resources — creation, retrieval, update, and soft-delete.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class WorkspacesController : ControllerBase
{
    private readonly IWorkspaceService _workspaceService;
    private readonly ILogger<WorkspacesController> _logger;

    /// <summary>
    /// Initialises a new instance of <see cref="WorkspacesController"/>.
    /// </summary>
    public WorkspacesController(
        IWorkspaceService workspaceService,
        ILogger<WorkspacesController> logger)
    {
        _workspaceService = workspaceService;
        _logger           = logger;
    }

    // -------------------------------------------------------------------------
    // GET api/workspaces
    // -------------------------------------------------------------------------

    /// <summary>Returns all active workspaces ordered by title.</summary>
    [HttpGet]
    [Authorize(Policy = "AdminOrManager")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<WorkspaceListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        _logger.LogInformation("GET /api/workspaces");

        var workspaces = await _workspaceService.GetAllWorkspacesAsync();
        return Ok(ApiResponse<IEnumerable<WorkspaceListDto>>.Ok(workspaces));
    }

    // -------------------------------------------------------------------------
    // GET api/workspaces/summary
    // -------------------------------------------------------------------------

    /// <summary>Returns aggregate workspace statistics for the dashboard.</summary>
    [HttpGet("summary")]
    [Authorize(Policy = "AdminOrManager")]
    [ProducesResponseType(typeof(ApiResponse<WorkspaceSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary()
    {
        _logger.LogInformation("GET /api/workspaces/summary");

        var summary = await _workspaceService.GetSummaryAsync();
        return Ok(ApiResponse<WorkspaceSummaryDto>.Ok(summary));
    }

    // -------------------------------------------------------------------------
    // GET api/workspaces/{id}
    // -------------------------------------------------------------------------

    /// <summary>Returns the full details of a single workspace.</summary>
    /// <param name="id">Primary key of the workspace.</param>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "AdminOrManager")]
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
    [Authorize(Policy = "AdminOnly")]
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
    [Authorize(Policy = "AdminOnly")]
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
    [Authorize(Policy = "AdminOnly")]
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
    [Authorize(Policy = "AdminOnly")]
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
    [Authorize(Policy = "AdminOnly")]
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
    [Authorize(Policy = "AdminOnly")]
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
