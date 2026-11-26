using ActionTracker.API.Models;
using ActionTracker.Application.Features.ActionItems.DTOs;
using ActionTracker.Application.Features.Calendars.DTOs;
using ActionTracker.Application.Features.Calendars.Interfaces;
using ActionTracker.Application.Features.ProjectPeople.DTOs;
using ActionTracker.Application.Features.ProjectPeople.Interfaces;
using ActionTracker.Application.Features.ProjectPeople.Services;
using ActionTracker.Application.Features.Workspaces.DTOs;
using ActionTracker.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

/// <summary>
/// WO-2026-11 P-2: the people of a project — lead PM, additional project managers,
/// sponsors, viewers and team members — and the look-ups their forms need.
/// A project the caller cannot see is 404; a visible project whose people the
/// caller may not change is 403.
/// </summary>
[ApiController]
[Route("api/projects")]
[Authorize(AuthenticationSchemes = "LocalBearer,AzureAD")]
public class ProjectPeopleController : ControllerBase
{
    private readonly IProjectPeopleService _people;
    private readonly IProjectAccessService _access;
    private readonly IWorkCalendarService _calendars;

    public ProjectPeopleController(IProjectPeopleService people, IProjectAccessService access, IWorkCalendarService calendars)
    {
        _people = people;
        _access = access;
        _calendars = calendars;
    }

    /// <summary>Everyone on the project, by role, with the caller's own roles and rights.</summary>
    [HttpGet("{projectId:guid}/people")]
    [Authorize(Policy = PermissionPolicies.ProjectsView)]
    [ProducesResponseType(typeof(ApiResponse<ProjectPeopleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid projectId)
    {
        try
        {
            return Ok(ApiResponse<ProjectPeopleDto>.Ok(await _people.GetAsync(projectId)));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<ProjectPeopleDto>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Adds one person in one role (<c>projectManager</c>, <c>sponsor</c>, <c>viewer</c>,
    /// <c>teamMember</c>) and notifies them. 400 for a rule (not on the workspace's
    /// Project Managers list, inactive user, the lead PM as an additional PM), 409
    /// when they already have that role.
    /// </summary>
    [HttpPost("{projectId:guid}/people")]
    [Authorize(Policy = PermissionPolicies.ProjectsEdit)]
    [ProducesResponseType(typeof(ApiResponse<ProjectPeopleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Add(Guid projectId, [FromBody] AddProjectPersonDto dto)
    {
        try
        {
            return Ok(ApiResponse<ProjectPeopleDto>.Ok(await _people.AddAsync(projectId, dto)));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<ProjectPeopleDto>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<ProjectPeopleDto>.Fail(ex.Message));
        }
        catch (ProjectPersonConflictException ex)
        {
            return Conflict(ApiResponse<ProjectPeopleDto>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<ProjectPeopleDto>.Fail(ex.Message));
        }
    }

    /// <summary>Removes one role of one person; their access through it ends on the next request. 400 for the last sponsor.</summary>
    [HttpDelete("{projectId:guid}/people/{role}/{userId}")]
    [Authorize(Policy = PermissionPolicies.ProjectsEdit)]
    [ProducesResponseType(typeof(ApiResponse<ProjectPeopleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(Guid projectId, string role, string userId)
    {
        try
        {
            return Ok(ApiResponse<ProjectPeopleDto>.Ok(await _people.RemoveAsync(projectId, role, userId)));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<ProjectPeopleDto>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<ProjectPeopleDto>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<ProjectPeopleDto>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Active users for the sponsor / viewer / team-member pickers (decision D13):
    /// any active user, by name or e-mail. Not the admin typeahead, which needs
    /// <c>Workspaces.View</c> (project managers do not hold it in production).
    /// </summary>
    [HttpGet("people-lookup")]
    [Authorize(Policy = PermissionPolicies.ProjectsEdit)]
    [ProducesResponseType(typeof(ApiResponse<List<UserDropdownItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Lookup([FromQuery] string? search, [FromQuery] int take = 20)
        => Ok(ApiResponse<List<UserDropdownItemDto>>.Ok(await _people.LookupUsersAsync(search, take)));

    /// <summary>
    /// The assignee picker of the project's action items and sub-items: the
    /// workspace's assignable users plus the project's team members — or, for a
    /// caller who reaches the project only through a project role, the project's own
    /// people (decision D4).
    /// </summary>
    [HttpGet("{projectId:guid}/assignable-users")]
    [Authorize(Policy = PermissionPolicies.ProjectsView)]
    [ProducesResponseType(typeof(ApiResponse<List<AssignableUserDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignableUsers(Guid projectId)
    {
        try
        {
            return Ok(ApiResponse<List<AssignableUserDto>>.Ok(await _people.GetAssignableUsersAsync(projectId)));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<List<AssignableUserDto>>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// The working calendar of the project's workspace, for the date forms
    /// (decision D6) — the same shape as <c>GET /api/workspaces/{id}/calendar</c>,
    /// reached through the project so project people get it without seeing the
    /// workspace.
    /// </summary>
    [HttpGet("{projectId:guid}/calendar")]
    [Authorize(Policy = PermissionPolicies.ProjectsView)]
    [ProducesResponseType(typeof(ApiResponse<WorkspaceCalendarDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Calendar(
        Guid projectId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var access = await _access.GetAsync(projectId);
        if (access == null)
            return NotFound(ApiResponse<WorkspaceCalendarDto>.Fail($"Project {projectId} not found."));
        try
        {
            return Ok(ApiResponse<WorkspaceCalendarDto>.Ok(
                await _calendars.GetWorkspaceCalendarAsync(access.WorkspaceId, from, to, ct)));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<WorkspaceCalendarDto>.Fail(ex.Message));
        }
    }
}
