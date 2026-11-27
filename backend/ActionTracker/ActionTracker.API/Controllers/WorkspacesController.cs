using System;
using System.Security.Claims;
using ActionTracker.API.Filters;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.ActionItems.DTOs;
using ActionTracker.Application.Features.Calendars.DTOs;
using ActionTracker.Application.Features.Calendars.Interfaces;
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
// P-2: every route with {id} keeps the workspace-level rule (404 for project-only people).
[WorkspaceLevelAccess("id")]
public class WorkspacesController : ControllerBase
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IWorkspaceAccessResolver _accessResolver;
    private readonly IWorkCalendarService _calendarService;
    private readonly ILogger<WorkspacesController> _logger;

    public WorkspacesController(
        IWorkspaceService workspaceService,
        IWorkspaceAccessResolver accessResolver,
        IWorkCalendarService calendarService,
        ILogger<WorkspacesController> logger)
    {
        _workspaceService = workspaceService;
        _accessResolver   = accessResolver;
        _calendarService  = calendarService;
        _logger           = logger;
    }

    // -------------------------------------------------------------------------
    // GET api/workspaces
    // -------------------------------------------------------------------------

    /// <summary>Returns all active workspaces ordered by title.</summary>
    [HttpGet]
    [Authorize(Policy = "LocalOrAzureAD")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<WorkspaceListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        _logger.LogInformation("GET /api/workspaces");

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var visibleWorkspaceIds = string.IsNullOrEmpty(userId)
            ? new List<Guid>()
            : await _accessResolver.GetVisibleWorkspaceIdsAsync(userId);

        var workspaces = await _workspaceService.GetAllWorkspacesAsync(visibleWorkspaceIds);
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
    public async Task<IActionResult> GetSummary([FromQuery] Guid? workspaceId = null)
    {
        _logger.LogInformation("GET /api/workspaces/summary workspaceId={WorkspaceId}", workspaceId);

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var visibleWorkspaceIds = string.IsNullOrEmpty(userId)
            ? new List<Guid>()
            : await _accessResolver.GetVisibleWorkspaceIdsAsync(userId);

        // Determine the effective scope: caller's visible workspaces, optionally
        // narrowed down to a single workspace chosen in the UI.
        List<Guid>? effectiveWorkspaceIds;
        if (workspaceId.HasValue)
        {
            var unrestricted = visibleWorkspaceIds == null;
            if (unrestricted || visibleWorkspaceIds!.Contains(workspaceId.Value))
                effectiveWorkspaceIds = new List<Guid> { workspaceId.Value };
            else
                effectiveWorkspaceIds = new List<Guid>(); // requested workspace not in scope → zeros
        }
        else
        {
            effectiveWorkspaceIds = visibleWorkspaceIds;
        }

        var summary = await _workspaceService.GetSummaryAsync(effectiveWorkspaceIds);
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
    [WorkspaceSettingsAccess("id")]   // WO-2026-11 P-2b: this workspace's admins or an Admin
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
    [WorkspaceSettingsAccess("id")]   // WO-2026-11 P-2b: this workspace's admins or an Admin
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
    [WorkspaceSettingsAccess("id")]   // WO-2026-11 P-2b: this workspace's admins or an Admin
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

    // -------------------------------------------------------------------------
    // Members
    // -------------------------------------------------------------------------

    /// <summary>Returns the regular members of a workspace.</summary>
    [HttpGet("{id:guid}/members")]
    [Authorize(Policy = PermissionPolicies.WorkspacesView)]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<WorkspaceMemberDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMembers(Guid id)
    {
        var members = await _workspaceService.GetMembersAsync(id);
        return Ok(ApiResponse<IEnumerable<WorkspaceMemberDto>>.Ok(members));
    }

    /// <summary>
    /// Returns the active users eligible to be assigned to an action item in
    /// this workspace (union of admins, members, and project managers).
    /// Used by the action-item create/edit form to restrict the Assignee(s)
    /// picker to workspace people.
    /// </summary>
    [HttpGet("{id:guid}/assignable-users")]
    [Authorize(Policy = "LocalOrAzureAD")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<AssignableUserDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAssignableUsers(Guid id)
    {
        var users = await _workspaceService.GetAssignableUsersAsync(id);
        return Ok(ApiResponse<IEnumerable<AssignableUserDto>>.Ok(users));
    }

    /// <summary>Assigns a regular user to a workspace.</summary>
    [HttpPost("{id:guid}/members")]
    [WorkspaceSettingsAccess("id")]   // WO-2026-11 P-2b: this workspace's admins or an Admin
    [Authorize(Policy = PermissionPolicies.WorkspacesEdit)]
    [ProducesResponseType(typeof(ApiResponse<WorkspaceMemberDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<string>),             StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddMember(Guid id, [FromBody] AddWorkspaceMemberDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        var addedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var member = await _workspaceService.AddMemberAsync(id, dto, addedBy);
        if (member is null)
            return BadRequest(ApiResponse<string>.Fail("Workspace not found or user already a member."));

        return Created(string.Empty, ApiResponse<WorkspaceMemberDto>.Ok(member));
    }

    /// <summary>Removes a regular member from a workspace.</summary>
    [HttpDelete("{id:guid}/members/{userId}")]
    [WorkspaceSettingsAccess("id")]   // WO-2026-11 P-2b: this workspace's admins or an Admin
    [Authorize(Policy = PermissionPolicies.WorkspacesEdit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMember(Guid id, string userId)
    {
        var removed = await _workspaceService.RemoveMemberAsync(id, userId);
        if (!removed)
            return NotFound(ApiResponse<string>.Fail("Member not found."));
        return NoContent();
    }

    /// <summary>
    /// Bulk-adds every active user assigned to the specified org unit as a
    /// workspace member. Already-existing members are skipped.
    /// </summary>
    [HttpPost("{id:guid}/members/from-org-unit")]
    [WorkspaceSettingsAccess("id")]   // WO-2026-11 P-2b: this workspace's admins or an Admin
    [Authorize(Policy = PermissionPolicies.WorkspacesEdit)]
    [ProducesResponseType(typeof(ApiResponse<AddMembersFromOrgUnitResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>),                         StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<string>),                         StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddMembersFromOrgUnit(
        Guid id, [FromBody] AddMembersFromOrgUnitRequestDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        var addedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var result  = await _workspaceService.AddMembersFromOrgUnitAsync(id, dto.OrgUnitId, addedBy);
        if (result is null)
            return NotFound(ApiResponse<string>.Fail("Workspace or org unit not found."));

        return Ok(ApiResponse<AddMembersFromOrgUnitResultDto>.Ok(result));
    }

    // -------------------------------------------------------------------------
    // Strategy linking
    // -------------------------------------------------------------------------

    /// <summary>Returns strategic plans available for assignment to a workspace.</summary>
    [HttpGet("strategic-plans")]
    [Authorize(Policy = PermissionPolicies.WorkspacesView)]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<StrategicPlanDropdownItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStrategicPlansForDropdown()
    {
        var items = await _workspaceService.GetStrategicPlansForDropdownAsync();
        return Ok(ApiResponse<IEnumerable<StrategicPlanDropdownItemDto>>.Ok(items));
    }

    /// <summary>Links or unlinks the strategic plan attached to a workspace.</summary>
    [HttpPut("{id:guid}/strategy")]
    [WorkspaceSettingsAccess("id")]   // WO-2026-11 P-2b: this workspace's admins or an Admin
    [Authorize(Policy = PermissionPolicies.WorkspacesEdit)]
    [ProducesResponseType(typeof(ApiResponse<WorkspaceResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>),               StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStrategy(Guid id, [FromBody] SetWorkspaceStrategyDto dto)
    {
        try
        {
            var updated = await _workspaceService.SetStrategyAsync(id, dto.StrategicPlanId);
            if (updated is null)
                return NotFound(ApiResponse<string>.Fail($"Workspace '{id}' not found."));
            return Ok(ApiResponse<WorkspaceResponseDto>.Ok(updated));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // -------------------------------------------------------------------------
    // Working calendar (WO-2026-11 C-3)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Active calendars a workspace can use — the Workspace Settings dropdown.
    /// Gated like the strategic-plan dropdown, so workspace editors without
    /// <c>Calendars.View</c> (e.g. PMO Head) can still pick one.
    /// </summary>
    [HttpGet("calendars")]
    [Authorize(Policy = PermissionPolicies.WorkspacesView)]
    [ProducesResponseType(typeof(ApiResponse<List<WorkspaceCalendarOptionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCalendarOptions(CancellationToken ct)
    {
        var items = await _calendarService.GetWorkspaceCalendarOptionsAsync(ct);
        return Ok(ApiResponse<List<WorkspaceCalendarOptionDto>>.Ok(items));
    }

    /// <summary>
    /// The calendar that applies to the workspace, with the exceptions in
    /// <paramref name="from"/>..<paramref name="to"/> (default: one year back
    /// to three years ahead, at most 3660 days). Read by every date form of
    /// the workspace, so it is open to any signed-in user like the other
    /// form lookups (assignable-users, project-managers); a private workspace
    /// the caller is not part of returns 404.
    /// </summary>
    [HttpGet("{id:guid}/calendar")]
    [Authorize(Policy = "LocalOrAzureAD")]
    [ProducesResponseType(typeof(ApiResponse<WorkspaceCalendarDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>),               StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<string>),               StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCalendar(
        Guid id, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        try
        {
            var calendar = await _calendarService.GetWorkspaceCalendarAsync(id, from, to, ct);
            return Ok(ApiResponse<WorkspaceCalendarDto>.Ok(calendar));
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

    /// <summary>
    /// Points the workspace at a calendar, or back at the default
    /// (<c>workCalendarId: null</c>). Dates saved afterwards follow the new
    /// calendar; existing records are not re-checked.
    /// </summary>
    [HttpPut("{id:guid}/calendar")]
    [WorkspaceSettingsAccess("id")]   // WO-2026-11 P-2b: this workspace's admins or an Admin
    [Authorize(Policy = PermissionPolicies.WorkspacesEdit)]
    [ProducesResponseType(typeof(ApiResponse<WorkspaceCalendarDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>),               StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<string>),               StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetCalendar(
        Guid id, [FromBody] SetWorkspaceCalendarDto dto, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        try
        {
            var calendar = await _calendarService.SetWorkspaceCalendarAsync(id, dto, userId, ct);
            return Ok(ApiResponse<WorkspaceCalendarDto>.Ok(calendar));
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

    // -------------------------------------------------------------------------
    // Project Managers (gating list for project create/edit)
    // -------------------------------------------------------------------------

    /// <summary>Returns the users eligible to be picked as Project Manager in this workspace.</summary>
    [HttpGet("{id:guid}/project-managers")]
    [Authorize(Policy = "LocalOrAzureAD")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<WorkspaceProjectManagerDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProjectManagers(Guid id)
    {
        var rows = await _workspaceService.GetProjectManagersAsync(id);
        return Ok(ApiResponse<IEnumerable<WorkspaceProjectManagerDto>>.Ok(rows));
    }

    /// <summary>Adds a user to the workspace's project-manager list.</summary>
    [HttpPost("{id:guid}/project-managers")]
    [WorkspaceSettingsAccess("id")]   // WO-2026-11 P-2b: this workspace's admins or an Admin
    [Authorize(Policy = PermissionPolicies.WorkspacesEdit)]
    [ProducesResponseType(typeof(ApiResponse<WorkspaceProjectManagerDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<string>),                    StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddProjectManager(Guid id, [FromBody] AddWorkspaceProjectManagerDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        var addedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        WorkspaceProjectManagerDto? row;
        try { row = await _workspaceService.AddProjectManagerAsync(id, dto, addedBy); }
        catch (ArgumentException ex) { return BadRequest(ApiResponse<string>.Fail(ex.Message)); }   // WO-2026-11 P-2b (fix 5)
        if (row is null)
            return BadRequest(ApiResponse<string>.Fail("Workspace not found or user already a project manager."));

        return Created(string.Empty, ApiResponse<WorkspaceProjectManagerDto>.Ok(row));
    }

    /// <summary>Removes a user from the workspace's project-manager list.</summary>
    [HttpDelete("{id:guid}/project-managers/{userId}")]
    [WorkspaceSettingsAccess("id")]   // WO-2026-11 P-2b: this workspace's admins or an Admin
    [Authorize(Policy = PermissionPolicies.WorkspacesEdit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveProjectManager(Guid id, string userId)
    {
        var removed = await _workspaceService.RemoveProjectManagerAsync(id, userId);
        if (!removed)
            return NotFound(ApiResponse<string>.Fail("Project manager not found."));
        return NoContent();
    }

    // -------------------------------------------------------------------------
    // Strategic Project Approvers (sequential approval chain)
    // -------------------------------------------------------------------------

    /// <summary>Returns the ordered approval chain for strategic projects in this workspace.</summary>
    [HttpGet("{id:guid}/strategic-project-approvers")]
    [Authorize(Policy = PermissionPolicies.WorkspacesView)]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<WorkspaceStrategicProjectApproverDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStrategicProjectApprovers(Guid id)
    {
        var rows = await _workspaceService.GetStrategicProjectApproversAsync(id);
        return Ok(ApiResponse<IEnumerable<WorkspaceStrategicProjectApproverDto>>.Ok(rows));
    }

    /// <summary>Appends a user to the workspace's strategic-project approver chain.</summary>
    [HttpPost("{id:guid}/strategic-project-approvers")]
    [WorkspaceSettingsAccess("id")]   // WO-2026-11 P-2b: this workspace's admins or an Admin
    [Authorize(Policy = PermissionPolicies.WorkspacesEdit)]
    [ProducesResponseType(typeof(ApiResponse<WorkspaceStrategicProjectApproverDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<string>),                              StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddStrategicProjectApprover(
        Guid id, [FromBody] AddWorkspaceStrategicProjectApproverDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        var addedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        WorkspaceStrategicProjectApproverDto? row;
        try { row = await _workspaceService.AddStrategicProjectApproverAsync(id, dto, addedBy); }
        catch (ArgumentException ex) { return BadRequest(ApiResponse<string>.Fail(ex.Message)); }   // WO-2026-11 P-2b (fix 5)
        if (row is null)
            return BadRequest(ApiResponse<string>.Fail("Workspace not found or user already an approver."));

        return Created(string.Empty, ApiResponse<WorkspaceStrategicProjectApproverDto>.Ok(row));
    }

    /// <summary>Removes a user from the workspace's strategic-project approver chain.</summary>
    [HttpDelete("{id:guid}/strategic-project-approvers/{userId}")]
    [WorkspaceSettingsAccess("id")]   // WO-2026-11 P-2b: this workspace's admins or an Admin
    [Authorize(Policy = PermissionPolicies.WorkspacesEdit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveStrategicProjectApprover(Guid id, string userId)
    {
        var removed = await _workspaceService.RemoveStrategicProjectApproverAsync(id, userId);
        if (!removed)
            return NotFound(ApiResponse<string>.Fail("Approver not found."));
        return NoContent();
    }

    /// <summary>
    /// Reorders the workspace's strategic-project approver chain. Body must list
    /// every current approver's UserId in the desired (1-based) order.
    /// </summary>
    [HttpPut("{id:guid}/strategic-project-approvers/reorder")]
    [WorkspaceSettingsAccess("id")]   // WO-2026-11 P-2b: this workspace's admins or an Admin
    [Authorize(Policy = PermissionPolicies.WorkspacesEdit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReorderStrategicProjectApprovers(
        Guid id, [FromBody] ReorderWorkspaceStrategicProjectApproversDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        var ok = await _workspaceService.ReorderStrategicProjectApproversAsync(id, dto.UserIds);
        if (!ok)
            return BadRequest(ApiResponse<string>.Fail(
                "Reorder rejected: the UserId list must include every current approver exactly once."));

        return NoContent();
    }
}
