using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.ActionItemSubItems;
using ActionTracker.Application.Features.ActionItemSubItems.DTOs;
using ActionTracker.Application.Features.ActionItemSubItems.Interfaces;
using ActionTracker.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

/// <summary>
/// Sub-items on action items (WO-2026-11 A-2). Reads need
/// <c>ActionItems.View</c>, writes <c>ActionItems.Edit</c>; ticking a sub-item
/// done / not done is also allowed for a user assigned to that sub-item.
/// A parent the caller cannot open (deleted, or in a private workspace the
/// caller is not part of) is 404. A Done or Cancelled parent refuses every
/// write with 409. Rule failures (dates outside the parent's planned dates,
/// non-working days, assignees outside the workspace) are 400 with the reason
/// in <c>message</c>.
/// </summary>
[ApiController]
[Route("api/action-items/{actionItemId:guid}/sub-items")]
[Authorize(AuthenticationSchemes = "LocalBearer,AzureAD")]
public class ActionItemSubItemsController : ControllerBase
{
    private readonly IActionItemSubItemService _service;
    private readonly IAuthorizationService _authorization;
    private readonly ILogger<ActionItemSubItemsController> _logger;

    public ActionItemSubItemsController(
        IActionItemSubItemService service,
        IAuthorizationService authorization,
        ILogger<ActionItemSubItemsController> logger)
    {
        _service       = service;
        _authorization = authorization;
        _logger        = logger;
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    /// <summary>The action item's sub-items in order, with the parent's planned dates and whether it accepts changes.</summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.ActionItemsView)]
    [ProducesResponseType(typeof(ApiResponse<ActionItemSubItemListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetAll(Guid actionItemId, CancellationToken ct) =>
        Run(async () => Ok(ApiResponse<ActionItemSubItemListDto>.Ok(
            await _service.GetForActionItemAsync(actionItemId, ct))));

    /// <summary>Adds a sub-item at the end of the list.</summary>
    [HttpPost]
    [Authorize(Policy = PermissionPolicies.ActionItemsEdit)]
    [ProducesResponseType(typeof(ApiResponse<ActionItemSubItemDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<ActionItemSubItemDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<ActionItemSubItemDto>), StatusCodes.Status409Conflict)]
    public Task<IActionResult> Create(Guid actionItemId, [FromBody] CreateActionItemSubItemDto dto, CancellationToken ct) =>
        Run(async () =>
        {
            var created = await _service.CreateAsync(actionItemId, dto, CurrentUserId, ct);
            return StatusCode(StatusCodes.Status201Created, ApiResponse<ActionItemSubItemDto>.Ok(created));
        });

    /// <summary>Replaces a sub-item's title, description, dates and assignees.</summary>
    [HttpPut("{subItemId:guid}")]
    [Authorize(Policy = PermissionPolicies.ActionItemsEdit)]
    [ProducesResponseType(typeof(ApiResponse<ActionItemSubItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ActionItemSubItemDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<ActionItemSubItemDto>), StatusCodes.Status409Conflict)]
    public Task<IActionResult> Update(
        Guid actionItemId, Guid subItemId, [FromBody] UpdateActionItemSubItemDto dto, CancellationToken ct) =>
        Run(async () => Ok(ApiResponse<ActionItemSubItemDto>.Ok(
            await _service.UpdateAsync(actionItemId, subItemId, dto, CurrentUserId, ct))));

    /// <summary>Soft-deletes a sub-item (it stays deleted when the parent is later deleted and restored).</summary>
    [HttpDelete("{subItemId:guid}")]
    [Authorize(Policy = PermissionPolicies.ActionItemsEdit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public Task<IActionResult> Delete(Guid actionItemId, Guid subItemId, CancellationToken ct) =>
        Run(async () =>
        {
            await _service.DeleteAsync(actionItemId, subItemId, CurrentUserId, ct);
            return NoContent();
        });

    /// <summary>
    /// Marks a sub-item done or not done. Allowed with <c>ActionItems.Edit</c>,
    /// or for a user assigned to this sub-item; otherwise 403.
    /// </summary>
    [HttpPatch("{subItemId:guid}/done")]
    [ProducesResponseType(typeof(ApiResponse<ActionItemSubItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<ActionItemSubItemDto>), StatusCodes.Status409Conflict)]
    public Task<IActionResult> SetDone(
        Guid actionItemId, Guid subItemId, [FromBody] SetActionItemSubItemDoneDto dto, CancellationToken ct) =>
        Run(async () =>
        {
            var canEdit = (await _authorization.AuthorizeAsync(User, PermissionPolicies.ActionItemsEdit)).Succeeded;
            return Ok(ApiResponse<ActionItemSubItemDto>.Ok(
                await _service.SetDoneAsync(actionItemId, subItemId, dto.IsDone, CurrentUserId, canEdit, ct)));
        });

    /// <summary>New order; the body must list every sub-item of the action item once.</summary>
    [HttpPut("reorder")]
    [Authorize(Policy = PermissionPolicies.ActionItemsEdit)]
    [ProducesResponseType(typeof(ApiResponse<ActionItemSubItemListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ActionItemSubItemListDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<ActionItemSubItemListDto>), StatusCodes.Status409Conflict)]
    public Task<IActionResult> Reorder(
        Guid actionItemId, [FromBody] ReorderActionItemSubItemsDto dto, CancellationToken ct) =>
        Run(async () => Ok(ApiResponse<ActionItemSubItemListDto>.Ok(
            await _service.ReorderAsync(actionItemId, dto.SubItemIds, CurrentUserId, ct))));

    /// <summary>
    /// Maps the service's error contract. Messages are returned here because
    /// the exception middleware hides them in Production, and the user needs
    /// to see which date or which assignee was refused.
    /// </summary>
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
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(ex.Message));
        }
        catch (SubItemParentLockedException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            _logger.LogInformation("Sub-item request refused: {Reason}", ex.Message);
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
