using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.ActionItems.DTOs;
using ActionTracker.Application.Features.ActionItems.Interfaces;
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
    private readonly IOrgUnitScopeResolver _scopeResolver;
    private readonly ILogger<ActionItemsController> _logger;

    public ActionItemsController(IActionItemService service, IOrgUnitScopeResolver scopeResolver, ILogger<ActionItemsController> logger)
    {
        _service       = service;
        _scopeResolver = scopeResolver;
        _logger        = logger;
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
        if (!string.IsNullOrEmpty(userId))
        {
            var visibleIds = await _scopeResolver.GetUserOrgUnitIdsAsync(userId);
            if (visibleIds.Count > 0)
                filter.VisibleOrgUnitIds = visibleIds;
        }

        var result = await _service.GetAllAsync(filter, ct);
        return Ok(ApiResponse<PagedResult<ActionItemResponseDto>>.Ok(result));
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

    /// <summary>Creates a new action item. ActionId is auto-generated (ACT-001 format).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ActionItemResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ActionItemResponseDto>>> Create(
        [FromBody] ActionItemCreateDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId  = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var created = await _service.CreateAsync(dto, userId, ct);

        _logger.LogInformation("ActionItem {ActionId} created", created.ActionId);

        return CreatedAtRoute(
            nameof(GetById),
            new { id = created.Id },
            ApiResponse<ActionItemResponseDto>.Ok(created));
    }

    // -------------------------------------------------------------------------
    // PUT api/action-items/{id}
    // -------------------------------------------------------------------------

    /// <summary>Updates an existing action item. Only supplied fields are changed.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ActionItemResponseDto>), StatusCodes.Status200OK)]
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
    [ProducesResponseType(typeof(ApiResponse<ActionItemResponseDto>), StatusCodes.Status200OK)]
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
    // GET api/action-items/assignable-users
    // -------------------------------------------------------------------------

    /// <summary>Returns all active users that can be assigned to an action item.</summary>
    [HttpGet("assignable-users")]
    [ProducesResponseType(typeof(ApiResponse<List<ActionItemResponseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAssignableUsers(CancellationToken ct)
    {
        var users = await _service.GetAssignableUsersAsync(ct);
        return Ok(ApiResponse<List<AssignableUserDto>>.Ok(users));
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

    // -------------------------------------------------------------------------
    // GET api/action-items/{id}/comments
    // -------------------------------------------------------------------------

    [HttpGet("{id:guid}/comments")]
    [ProducesResponseType(typeof(ApiResponse<List<ActionItemCommentResponseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetComments(Guid id, CancellationToken ct)
    {
        var comments = await _service.GetCommentsAsync(id, ct);
        return Ok(ApiResponse<List<ActionItemCommentResponseDto>>.Ok(comments));
    }

    // -------------------------------------------------------------------------
    // POST api/action-items/{id}/comments
    // -------------------------------------------------------------------------

    [HttpPost("{id:guid}/comments")]
    [ProducesResponseType(typeof(ApiResponse<ActionItemCommentResponseDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddComment(
        Guid id, [FromBody] CreateCommentDto dto, CancellationToken ct)
    {
        try
        {
            var userId  = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            var comment = await _service.AddCommentAsync(id, dto, userId, ct);
            return Created(string.Empty, ApiResponse<ActionItemCommentResponseDto>.Ok(comment));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<ActionItemCommentResponseDto>.Fail(ex.Message));
        }
    }

    // -------------------------------------------------------------------------
    // PUT api/action-items/{actionItemId}/comments/{commentId}
    // -------------------------------------------------------------------------

    [HttpPut("{actionItemId:guid}/comments/{commentId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ActionItemCommentResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateComment(
        Guid actionItemId, Guid commentId, [FromBody] UpdateCommentDto dto, CancellationToken ct)
    {
        try
        {
            var userId  = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            var comment = await _service.UpdateCommentAsync(actionItemId, commentId, dto, userId, ct);
            return Ok(ApiResponse<ActionItemCommentResponseDto>.Ok(comment));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<ActionItemCommentResponseDto>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<ActionItemCommentResponseDto>.Fail(ex.Message));
        }
    }

    // -------------------------------------------------------------------------
    // DELETE api/action-items/{actionItemId}/comments/{commentId}
    // -------------------------------------------------------------------------

    [HttpDelete("{actionItemId:guid}/comments/{commentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteComment(
        Guid actionItemId, Guid commentId, CancellationToken ct)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            await _service.DeleteCommentAsync(actionItemId, commentId, userId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
        }
    }
}
