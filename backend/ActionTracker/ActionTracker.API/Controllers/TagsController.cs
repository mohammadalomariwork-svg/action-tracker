using System.Security.Claims;
using ActionTracker.API.Filters;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.Tags.DTOs;
using ActionTracker.Application.Features.Tags.Interfaces;
using ActionTracker.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

/// <summary>
/// Tag look-ups for action items (WO-2026-11 A-5): suggestions and filter
/// options. Tags themselves are saved through POST / PUT
/// <c>/api/action-items</c> (<c>tags</c>), under those endpoints' policies.
/// Both reads need <c>ActionItems.View</c> and follow workspace visibility.
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = "LocalBearer,AzureAD")]
[WorkspaceLevelAccess] // P-2: the workspace tag suggestions stay workspace-level
public class TagsController : ControllerBase
{
    private readonly IActionItemTagService _tags;

    public TagsController(IActionItemTagService tags) => _tags = tags;

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    /// <summary>
    /// The tags of one workspace, by name — suggestions for the tag input and
    /// the options of the workspace action-item list's tag filter. Optional
    /// <paramref name="search"/> (part of the name, any case) and
    /// <paramref name="limit"/> (1–500, default 100). 404 when the caller cannot
    /// see the workspace (private, or not one of theirs). Empty while the tag
    /// tables are missing.
    /// </summary>
    [HttpGet("api/workspaces/{workspaceId:guid}/tags")]
    [Authorize(Policy = PermissionPolicies.ActionItemsView)]
    [ProducesResponseType(typeof(ApiResponse<List<WorkspaceTagDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<List<WorkspaceTagDto>>>> GetWorkspaceTags(
        Guid workspaceId, [FromQuery] string? search, [FromQuery] int limit = 100, CancellationToken ct = default)
    {
        try
        {
            var tags = await _tags.GetWorkspaceTagsAsync(workspaceId, CurrentUserId, search, limit, ct);
            return Ok(ApiResponse<List<WorkspaceTagDto>>.Ok(tags));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<List<WorkspaceTagDto>>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// My Actions tag filter options (D3): the tags on live items assigned to
    /// the caller, each with its workspace — the same name in two workspaces
    /// is two options. Empty while the tag tables are missing.
    /// </summary>
    [HttpGet("api/action-items/my-actions/tags")]
    [Authorize(Policy = PermissionPolicies.ActionItemsView)]
    [ProducesResponseType(typeof(ApiResponse<List<MyActionTagOptionDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<List<MyActionTagOptionDto>>>> GetMyActionTags(CancellationToken ct)
    {
        var options = await _tags.GetMyActionTagsAsync(CurrentUserId, ct);
        return Ok(ApiResponse<List<MyActionTagOptionDto>>.Ok(options));
    }
}
