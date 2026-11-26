using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.ActionItems.DTOs;
using ActionTracker.Application.Features.ActionItems.Interfaces;
using ActionTracker.Application.Features.Calendars;
using ActionTracker.Application.Features.Tags;
using ActionTracker.Application.Helpers;
using ActionTracker.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

[ApiController]
[Route("api/action-items")]
[Authorize(AuthenticationSchemes = "LocalBearer,AzureAD")]
public class ActionItemsController : ControllerBase
{
    private readonly IActionItemService _service;
    private readonly IWorkspaceAccessResolver _accessResolver;
    private readonly IAuthorizationService _authorization;
    private readonly ILogger<ActionItemsController> _logger;

    public ActionItemsController(
        IActionItemService service,
        IWorkspaceAccessResolver accessResolver,
        IAuthorizationService authorization,
        ILogger<ActionItemsController> logger)
    {
        _service        = service;
        _accessResolver = accessResolver;
        _authorization  = authorization;
        _logger         = logger;
    }

    // ── Tags (WO-2026-11 A-5) ────────────────────────────────────────────────

    private const string TagFilterNeedsView = "You need the ActionItems.View permission to filter by tag.";

    /// <summary>
    /// The tag filter needs ActionItems.View; a list call without <c>tagIds</c>
    /// is unchanged (these list endpoints carry no policy of their own).
    /// </summary>
    private async Task<bool> MayFilterByTagsAsync(ActionItemFilterDto filter) =>
        filter.TagIds is not { Count: > 0 }
        || (await _authorization.AuthorizeAsync(User, PermissionPolicies.ActionItemsView)).Succeeded;

    /// <summary>The tag tables are missing (migration pending): 503 with the reason.</summary>
    private ObjectResult TagsUnavailable<T>(TagsUnavailableException ex) =>
        StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse<T>.Fail(ex.Message));

    /// <summary>A list query with the 403 / 503 tag cases (A-5) around it.</summary>
    private async Task<ActionResult<ApiResponse<PagedResult<ActionItemResponseDto>>>> ListAsync(
        ActionItemFilterDto filter, Func<Task<PagedResult<ActionItemResponseDto>>> query)
    {
        if (!await MayFilterByTagsAsync(filter))
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<PagedResult<ActionItemResponseDto>>.Fail(TagFilterNeedsView));
        try
        {
            return Ok(ApiResponse<PagedResult<ActionItemResponseDto>>.Ok(await query()));
        }
        catch (TagsUnavailableException ex)
        {
            return TagsUnavailable<PagedResult<ActionItemResponseDto>>(ex);
        }
    }

    // -------------------------------------------------------------------------
    // GET api/action-items
    // -------------------------------------------------------------------------

    /// <summary>Returns a paginated, filtered list of action items.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ActionItemResponseDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<ActionItemResponseDto>>>> GetAll(
        [FromQuery] ActionItemFilterDto filter, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        filter.VisibleWorkspaceIds = string.IsNullOrEmpty(userId)
            ? new List<Guid>()
            : await _accessResolver.GetVisibleWorkspaceIdsAsync(userId);
        // WO-2026-11 P-2: plus the projects where the caller holds a project role (never taken from the query string).
        filter.RoleProjectIds = string.IsNullOrEmpty(userId)
            ? new List<Guid>()
            : await _accessResolver.GetProjectRoleProjectIdsAsync(userId);

        return await ListAsync(filter, () => _service.GetAllAsync(filter, ct));
    }

    // -------------------------------------------------------------------------
    // GET api/action-items/my-actions
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns a paginated, filtered list of action items assigned to the
    /// currently authenticated user. AssigneeId is set from JWT claims —
    /// it cannot be overridden by the caller.
    /// </summary>
    [HttpGet("my-actions")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ActionItemResponseDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<ActionItemResponseDto>>>> GetMyActions(
        [FromQuery] ActionItemFilterDto filter, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        // Force the assignee to the caller — ignore any assigneeId from the query string.
        // Do NOT apply org-unit scoping: the user must see every item assigned to them
        // regardless of which workspace/org-unit it belongs to.
        filter.AssigneeId        = userId;
        filter.VisibleOrgUnitIds = null;
        filter.VisibleWorkspaceIds = null;

        _logger.LogInformation("GET /api/action-items/my-actions userId={UserId}", userId);

        return await ListAsync(filter, () => _service.GetAllAsync(filter, ct));
    }

    // -------------------------------------------------------------------------
    // GET api/action-items/assigned-via-sub-items   (WO-2026-11 A-2)
    // -------------------------------------------------------------------------

    /// <summary>
    /// My Actions → "Assigned through a sub-item": action items where the
    /// caller is assigned to a sub-item but not to the item itself. The user is
    /// taken from the token. These items are not part of /my-stats, /my-actions
    /// or any dashboard count, so every chart still equals its drill-down list.
    /// Status / priority / search filters, paging and sorting apply.
    /// </summary>
    [HttpGet("assigned-via-sub-items")]
    [Authorize(Policy = PermissionPolicies.ActionItemsView)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ActionItemResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<PagedResult<ActionItemResponseDto>>>> GetAssignedViaSubItems(
        [FromQuery] ActionItemFilterDto filter, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        return await ListAsync(filter, () => _service.GetAssignedViaSubItemsAsync(userId, filter, ct));
    }

    // -------------------------------------------------------------------------
    // GET api/action-items/created-by-me
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns a paginated, filtered list of action items created by the
    /// currently authenticated user. CreatedById is set from JWT claims —
    /// it cannot be overridden by the caller.
    /// </summary>
    [HttpGet("created-by-me")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ActionItemResponseDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<ActionItemResponseDto>>>> GetCreatedByMe(
        [FromQuery] ActionItemFilterDto filter, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        filter.CreatedById       = userId;
        filter.VisibleOrgUnitIds = null;
        filter.VisibleWorkspaceIds = null;

        _logger.LogInformation("GET /api/action-items/created-by-me userId={UserId}", userId);

        return await ListAsync(filter, () => _service.GetAllAsync(filter, ct));
    }

    // -------------------------------------------------------------------------
    // GET api/action-items/{id}
    // -------------------------------------------------------------------------

    /// <summary>Returns a single action item by GUID, or 404 if not found.</summary>
    [HttpGet("{id:guid}", Name = nameof(GetById))]
    [ProducesResponseType(typeof(ApiResponse<ActionItemResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ActionItemResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ActionItemResponseDto>>> GetById(
        Guid id, CancellationToken ct)
    {
        var item = await _service.GetByIdAsync(id, ct);

        if (item is null)
        {
            _logger.LogWarning("ActionItem {Id} not found", id);
            return NotFound(ApiResponse<ActionItemResponseDto>.Fail($"ActionItem {id} not found."));
        }

        return Ok(ApiResponse<ActionItemResponseDto>.Ok(item));
    }

    // -------------------------------------------------------------------------
    // POST api/action-items
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates a new action item. ActionId is auto-generated (ACT-001 format).
    /// The standalone flag is set from the project link; isStandalone in the
    /// body is ignored (WO-2026-11 A-1b).
    /// </summary>
    [HttpPost]
    [Authorize(Policy = PermissionPolicies.ActionItemsCreate)]
    [ProducesResponseType(typeof(ApiResponse<ActionItemResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<ActionItemResponseDto>>> Create(
        [FromBody] ActionItemCreateDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId  = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        ActionItemResponseDto created;
        try
        {
            created = await _service.CreateAsync(dto, userId, ct);
        }
        catch (NonWorkingDateException ex)
        {
            // Returned here because the exception middleware hides the
            // message in Production, and the user needs to see which date.
            return BadRequest(ApiResponse<ActionItemResponseDto>.Fail(ex.Message));
        }
        catch (TagRuleException ex)
        {
            return BadRequest(ApiResponse<ActionItemResponseDto>.Fail(ex.Message));
        }
        catch (TagsUnavailableException ex)
        {
            return TagsUnavailable<ActionItemResponseDto>(ex);
        }

        _logger.LogInformation("ActionItem {ActionId} created", created.ActionId);

        return CreatedAtRoute(
            nameof(GetById),
            new { id = created.Id },
            ApiResponse<ActionItemResponseDto>.Ok(created));
    }

    // -------------------------------------------------------------------------
    // PUT api/action-items/{id}
    // -------------------------------------------------------------------------

    /// <summary>
    /// Updates an existing action item. Only supplied fields are changed.
    /// StartDate / DueDate are the planned start / planned end: frozen once the
    /// parent project leaves Draft, sent for approval on standalone items, and a
    /// changed day must be a working day. ActualStartDate / ActualEndDate
    /// (WO-2026-11 A-1) are none of these; they may not be after today, actual
    /// end needs the item to be Done, and a Done item accepts a payload that
    /// only sets actual dates (anything else = 422). isStandalone in the body is
    /// ignored: the flag follows the project link and changes only when the save
    /// changes projectId (WO-2026-11 A-1b).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.ActionItemsEdit)]
    [ProducesResponseType(typeof(ApiResponse<ActionItemResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ActionItemResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ActionItemResponseDto>>> Update(
        Guid id, [FromBody] ActionItemUpdateDto dto, CancellationToken ct)
    {
        try
        {
            var userId  = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            var updated = await _service.UpdateAsync(id, dto, userId, ct);
            return Ok(ApiResponse<ActionItemResponseDto>.Ok(updated));
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "ActionItem {Id} not found for update", id);
            return NotFound(ApiResponse<ActionItemResponseDto>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            // Non-working planned dates (NonWorkingDateException), refused
            // actual dates and refused tags (TagRuleException). Returned here
            // because the exception middleware hides the message in
            // Production, and the user needs to see which value.
            return BadRequest(ApiResponse<ActionItemResponseDto>.Fail(ex.Message));
        }
        catch (TagsUnavailableException ex)
        {
            return TagsUnavailable<ActionItemResponseDto>(ex);
        }
    }

    // -------------------------------------------------------------------------
    // DELETE api/action-items/{id}   [Admin, Manager only]
    // -------------------------------------------------------------------------

    /// <summary>Soft-deletes an action item.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.ActionItemsDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await _service.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "ActionItem {Id} not found for deletion", id);
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>Restores a soft-deleted action item. Blocked if its parent project is deleted.</summary>
    [HttpPatch("{id:guid}/restore")]
    [Authorize(Policy = PermissionPolicies.ActionItemsEdit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct)
    {
        try
        {
            await _service.RestoreAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // -------------------------------------------------------------------------
    // PATCH api/action-items/{id}/status
    // -------------------------------------------------------------------------

    /// <summary>Updates the status of an action item. Completing an item sets Progress to 100.</summary>
    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = PermissionPolicies.ActionItemsEdit)]
    [ProducesResponseType(typeof(ApiResponse<ActionItemResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ActionItemResponseDto>>> UpdateStatus(
        Guid id, [FromBody] UpdateStatusRequest request, CancellationToken ct)
    {
        try
        {
            await _service.UpdateStatusAsync(id, request.Status, ct);

            // Re-fetch to return the full updated DTO
            var item = await _service.GetByIdAsync(id, ct);
            return Ok(ApiResponse<ActionItemResponseDto>.Ok(item!));
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "ActionItem {Id} not found for status update", id);
            return NotFound(ApiResponse<ActionItemResponseDto>.Fail(ex.Message));
        }
    }

    // -------------------------------------------------------------------------
    // GET api/action-items/my-stats
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns aggregate statistics for action items assigned to the currently
    /// authenticated user.
    /// </summary>
    [HttpGet("my-stats")]
    [ProducesResponseType(typeof(ApiResponse<ActionItemMyStatsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyStats(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        _logger.LogInformation("GET /api/action-items/my-stats userId={UserId}", userId);

        var stats = await _service.GetMyStatsAsync(userId, ct);
        return Ok(ApiResponse<ActionItemMyStatsDto>.Ok(stats));
    }

    // -------------------------------------------------------------------------
    // POST api/action-items/process-overdue   [Admin only]
    // -------------------------------------------------------------------------

    /// <summary>
    /// Scans all non-completed items past their due date and marks them Overdue.
    /// Restricted to Admin role.
    /// </summary>
    [HttpPost("process-overdue")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<int>>> ProcessOverdue(CancellationToken ct)
    {
        var count = await _service.ProcessOverdueItemsAsync(ct);

        _logger.LogInformation("ProcessOverdue completed: {Count} items marked as Overdue", count);

        return Ok(ApiResponse<int>.Ok(count, $"{count} item(s) marked as Overdue."));
    }
}
