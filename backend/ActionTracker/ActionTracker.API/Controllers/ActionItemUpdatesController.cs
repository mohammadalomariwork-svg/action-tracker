using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.ActionItemUpdates;
using ActionTracker.Application.Features.ActionItemUpdates.DTOs;
using ActionTracker.Application.Features.ActionItemUpdates.Interfaces;
using ActionTracker.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

/// <summary>
/// Action Updates (WO-2026-11 A-3) — they replace comments on action items.
/// The same routes exist at action-item level
/// (<c>/api/action-items/{id}/updates</c>) and at sub-item level
/// (<c>/api/action-items/{id}/sub-items/{subItemId}/updates</c>).
/// <para>
/// Reads need <c>ActionItems.View</c>, writes <c>ActionItems.Edit</c>. A list
/// holds every submitted update plus the caller's own drafts. 404: the action
/// item or sub-item cannot be seen (deleted, or in a private workspace the
/// caller is not part of), or the update is not visible — someone else's draft
/// is never revealed. 409: edit, submit or delete of a submitted update, for
/// every role including Admin. 400: empty text or more than 2,000 characters.
/// Updates are allowed whatever the action item's status.
/// </para>
/// </summary>
[ApiController]
[Route("api/action-items/{actionItemId:guid}")]
[Authorize(AuthenticationSchemes = "LocalBearer,AzureAD")]
public class ActionItemUpdatesController : ControllerBase
{
    private readonly IActionItemUpdateService _service;
    private readonly ILogger<ActionItemUpdatesController> _logger;

    public ActionItemUpdatesController(IActionItemUpdateService service, ILogger<ActionItemUpdatesController> logger)
    {
        _service = service;
        _logger  = logger;
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    // ── Action-item level ────────────────────────────────────────────────────

    /// <summary>Updates on the action item itself: submitted ones plus my drafts (drafts first).</summary>
    [HttpGet("updates")]
    [Authorize(Policy = PermissionPolicies.ActionItemsView)]
    [ProducesResponseType(typeof(ApiResponse<ActionUpdateListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetItemUpdates(Guid actionItemId, CancellationToken ct) =>
        List(actionItemId, null, ct);

    /// <summary>Counts for the action item and for each of its sub-items (submitted + my drafts).</summary>
    [HttpGet("updates/counts")]
    [Authorize(Policy = PermissionPolicies.ActionItemsView)]
    [ProducesResponseType(typeof(ApiResponse<ActionUpdateCountsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetCounts(Guid actionItemId, CancellationToken ct) =>
        Run(async () => Ok(ApiResponse<ActionUpdateCountsDto>.Ok(
            await _service.GetCountsAsync(actionItemId, ct))));

    /// <summary>Saves a draft, or submits straight away when <c>submit</c> is true.</summary>
    [HttpPost("updates")]
    [Authorize(Policy = PermissionPolicies.ActionItemsEdit)]
    [ProducesResponseType(typeof(ApiResponse<ActionUpdateDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> CreateItemUpdate(
        Guid actionItemId, [FromBody] CreateActionUpdateDto dto, CancellationToken ct) =>
        Create(actionItemId, null, dto, ct);

    /// <summary>Edits my draft.</summary>
    [HttpPut("updates/{updateId:guid}")]
    [Authorize(Policy = PermissionPolicies.ActionItemsEdit)]
    [ProducesResponseType(typeof(ApiResponse<ActionUpdateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public Task<IActionResult> EditItemUpdate(
        Guid actionItemId, Guid updateId, [FromBody] EditActionUpdateDto dto, CancellationToken ct) =>
        Edit(actionItemId, null, updateId, dto, ct);

    /// <summary>Submits my draft. One-way: it cannot be changed or deleted afterwards.</summary>
    [HttpPost("updates/{updateId:guid}/submit")]
    [Authorize(Policy = PermissionPolicies.ActionItemsEdit)]
    [ProducesResponseType(typeof(ApiResponse<ActionUpdateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public Task<IActionResult> SubmitItemUpdate(Guid actionItemId, Guid updateId, CancellationToken ct) =>
        Submit(actionItemId, null, updateId, ct);

    /// <summary>Deletes my draft.</summary>
    [HttpDelete("updates/{updateId:guid}")]
    [Authorize(Policy = PermissionPolicies.ActionItemsEdit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public Task<IActionResult> DeleteItemUpdate(Guid actionItemId, Guid updateId, CancellationToken ct) =>
        Delete(actionItemId, null, updateId, ct);

    // ── Sub-item level ───────────────────────────────────────────────────────

    /// <summary>Updates on one sub-item: submitted ones plus my drafts (drafts first).</summary>
    [HttpGet("sub-items/{subItemId:guid}/updates")]
    [Authorize(Policy = PermissionPolicies.ActionItemsView)]
    [ProducesResponseType(typeof(ApiResponse<ActionUpdateListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetSubItemUpdates(Guid actionItemId, Guid subItemId, CancellationToken ct) =>
        List(actionItemId, subItemId, ct);

    /// <summary>Saves a draft on the sub-item, or submits straight away when <c>submit</c> is true.</summary>
    [HttpPost("sub-items/{subItemId:guid}/updates")]
    [Authorize(Policy = PermissionPolicies.ActionItemsEdit)]
    [ProducesResponseType(typeof(ApiResponse<ActionUpdateDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> CreateSubItemUpdate(
        Guid actionItemId, Guid subItemId, [FromBody] CreateActionUpdateDto dto, CancellationToken ct) =>
        Create(actionItemId, subItemId, dto, ct);

    /// <summary>Edits my draft on the sub-item.</summary>
    [HttpPut("sub-items/{subItemId:guid}/updates/{updateId:guid}")]
    [Authorize(Policy = PermissionPolicies.ActionItemsEdit)]
    [ProducesResponseType(typeof(ApiResponse<ActionUpdateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public Task<IActionResult> EditSubItemUpdate(
        Guid actionItemId, Guid subItemId, Guid updateId, [FromBody] EditActionUpdateDto dto, CancellationToken ct) =>
        Edit(actionItemId, subItemId, updateId, dto, ct);

    /// <summary>Submits my draft on the sub-item (one-way).</summary>
    [HttpPost("sub-items/{subItemId:guid}/updates/{updateId:guid}/submit")]
    [Authorize(Policy = PermissionPolicies.ActionItemsEdit)]
    [ProducesResponseType(typeof(ApiResponse<ActionUpdateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public Task<IActionResult> SubmitSubItemUpdate(Guid actionItemId, Guid subItemId, Guid updateId, CancellationToken ct) =>
        Submit(actionItemId, subItemId, updateId, ct);

    /// <summary>Deletes my draft on the sub-item.</summary>
    [HttpDelete("sub-items/{subItemId:guid}/updates/{updateId:guid}")]
    [Authorize(Policy = PermissionPolicies.ActionItemsEdit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public Task<IActionResult> DeleteSubItemUpdate(Guid actionItemId, Guid subItemId, Guid updateId, CancellationToken ct) =>
        Delete(actionItemId, subItemId, updateId, ct);

    // ── Shared bodies ────────────────────────────────────────────────────────

    private Task<IActionResult> List(Guid actionItemId, Guid? subItemId, CancellationToken ct) =>
        Run(async () => Ok(ApiResponse<ActionUpdateListDto>.Ok(
            await _service.GetAsync(actionItemId, subItemId, ct))));

    private Task<IActionResult> Create(Guid actionItemId, Guid? subItemId, CreateActionUpdateDto dto, CancellationToken ct) =>
        Run(async () =>
        {
            var created = await _service.CreateAsync(actionItemId, subItemId, dto, CurrentUserId, ct);
            return StatusCode(StatusCodes.Status201Created, ApiResponse<ActionUpdateDto>.Ok(created));
        });

    private Task<IActionResult> Edit(
        Guid actionItemId, Guid? subItemId, Guid updateId, EditActionUpdateDto dto, CancellationToken ct) =>
        Run(async () => Ok(ApiResponse<ActionUpdateDto>.Ok(
            await _service.EditAsync(actionItemId, subItemId, updateId, dto, CurrentUserId, ct))));

    private Task<IActionResult> Submit(Guid actionItemId, Guid? subItemId, Guid updateId, CancellationToken ct) =>
        Run(async () => Ok(ApiResponse<ActionUpdateDto>.Ok(
            await _service.SubmitAsync(actionItemId, subItemId, updateId, CurrentUserId, ct))));

    private Task<IActionResult> Delete(Guid actionItemId, Guid? subItemId, Guid updateId, CancellationToken ct) =>
        Run(async () =>
        {
            await _service.DeleteAsync(actionItemId, subItemId, updateId, CurrentUserId, ct);
            return NoContent();
        });

    /// <summary>Maps the service's error contract; messages are returned so the user sees the reason.</summary>
    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (ActionItemUpdateLockedException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            _logger.LogInformation("Action update request refused: {Reason}", ex.Message);
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
