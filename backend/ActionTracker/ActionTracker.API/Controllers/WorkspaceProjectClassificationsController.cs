using System.Security.Claims;
using ActionTracker.API.Filters;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.ProjectClassifications;
using ActionTracker.Application.Features.ProjectClassifications.DTOs;
using ActionTracker.Application.Features.ProjectClassifications.Interfaces;
using ActionTracker.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

/// <summary>
/// Internal project classifications of a workspace (WO-2026-11 P-1): the list a
/// workspace manager maintains on Workspace Settings and the options of the
/// project form's "Internal classification" dropdown. A project's
/// classification itself is saved with the project (POST / PUT
/// <c>/api/projects</c>, <c>internalClassificationId</c>).
/// <para>
/// Reads need <c>Projects.View</c> <b>or</b> <c>Workspaces.View</c> (project
/// managers hold the first, workspace admins the second) and a workspace the
/// caller can see — or whose Project Managers list they are on; writes need
/// <c>Workspaces.Edit</c> and a workspace the caller can see. A hidden workspace
/// (private, or not one of theirs) is 404.
/// </para>
/// </summary>
[ApiController]
[Route("api/workspaces/{workspaceId:guid}/project-classifications")]
[Authorize(AuthenticationSchemes = "LocalBearer,AzureAD")]
[WorkspaceLevelAccess] // P-2: a workspace's classification list stays workspace-level
public class WorkspaceProjectClassificationsController : ControllerBase
{
    private readonly IProjectClassificationService _service;
    private readonly IAuthorizationService _authorization;

    public WorkspaceProjectClassificationsController(
        IProjectClassificationService service, IAuthorizationService authorization)
    {
        _service       = service;
        _authorization = authorization;
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    /// <summary>
    /// The workspace's classifications by sort order — active and inactive —
    /// with <c>projectCount</c> / <c>deletedProjectCount</c> (both block a
    /// delete) and <c>canDelete</c>. Requires <c>Projects.View</c> or
    /// <c>Workspaces.View</c> (403 otherwise).
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "LocalOrAzureAD")]
    [ProducesResponseType(typeof(ApiResponse<List<ProjectClassificationDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAll(Guid workspaceId, CancellationToken ct)
    {
        var canRead = (await _authorization.AuthorizeAsync(User, PermissionPolicies.ProjectsView)).Succeeded
                   || (await _authorization.AuthorizeAsync(User, PermissionPolicies.WorkspacesView)).Succeeded;
        if (!canRead)
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<string>.Fail(
                "You need the Projects.View or Workspaces.View permission to see project classifications."));

        return await Run(async () => Ok(ApiResponse<List<ProjectClassificationDto>>.Ok(
            await _service.GetForWorkspaceAsync(workspaceId, CurrentUserId, ct))));
    }

    /// <summary>Adds a classification at the end of the list, active. 409 when the name is taken (any case).</summary>
    [HttpPost]
    [WorkspaceSettingsAccess]   // WO-2026-11 P-2b: this workspace's admins or an Admin
    [Authorize(Policy = PermissionPolicies.WorkspacesEdit)]
    [ProducesResponseType(typeof(ApiResponse<ProjectClassificationDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status409Conflict)]
    public Task<IActionResult> Create(
        Guid workspaceId, [FromBody] CreateProjectClassificationDto dto, CancellationToken ct) =>
        Run(async () => StatusCode(StatusCodes.Status201Created, ApiResponse<ProjectClassificationDto>.Ok(
            await _service.CreateAsync(workspaceId, dto, CurrentUserId, ct))));

    /// <summary>
    /// Sets the new order: <c>classificationIds</c> must hold every live
    /// classification of the workspace exactly once (400 otherwise).
    /// </summary>
    [HttpPut("reorder")]
    [WorkspaceSettingsAccess]   // WO-2026-11 P-2b: this workspace's admins or an Admin
    [Authorize(Policy = PermissionPolicies.WorkspacesEdit)]
    [ProducesResponseType(typeof(ApiResponse<List<ProjectClassificationDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public Task<IActionResult> Reorder(
        Guid workspaceId, [FromBody] ReorderProjectClassificationsDto dto, CancellationToken ct) =>
        Run(async () => Ok(ApiResponse<List<ProjectClassificationDto>>.Ok(
            await _service.ReorderAsync(workspaceId, dto, CurrentUserId, ct))));

    /// <summary>
    /// Renames and / or (de)activates a classification. A deactivated one stays
    /// on the projects that have it but cannot be newly chosen. 409 when the new
    /// name is taken (any case).
    /// </summary>
    [HttpPut("{classificationId:guid}")]
    [WorkspaceSettingsAccess]   // WO-2026-11 P-2b: this workspace's admins or an Admin
    [Authorize(Policy = PermissionPolicies.WorkspacesEdit)]
    [ProducesResponseType(typeof(ApiResponse<ProjectClassificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status409Conflict)]
    public Task<IActionResult> Update(
        Guid workspaceId, Guid classificationId, [FromBody] UpdateProjectClassificationDto dto, CancellationToken ct) =>
        Run(async () => Ok(ApiResponse<ProjectClassificationDto>.Ok(
            await _service.UpdateAsync(workspaceId, classificationId, dto, CurrentUserId, ct))));

    /// <summary>
    /// Soft-deletes a classification. 409 while any project — soft-deleted ones
    /// included — uses it; the message says how many.
    /// </summary>
    [HttpDelete("{classificationId:guid}")]
    [WorkspaceSettingsAccess]   // WO-2026-11 P-2b: this workspace's admins or an Admin
    [Authorize(Policy = PermissionPolicies.WorkspacesEdit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status409Conflict)]
    public Task<IActionResult> Delete(Guid workspaceId, Guid classificationId, CancellationToken ct) =>
        Run(async () =>
        {
            await _service.DeleteAsync(workspaceId, classificationId, CurrentUserId, ct);
            return NoContent();
        });

    /// <summary>Maps the service's exceptions; the reason always comes back as <c>ApiResponse.message</c>.</summary>
    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
        catch (ProjectClassificationConflictException ex)
        {
            return Conflict(ApiResponse<string>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
    }
}
