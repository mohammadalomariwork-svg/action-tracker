using ActionTracker.Application.Features.ActionItemSubItems.DTOs;

namespace ActionTracker.Application.Features.ActionItemSubItems.Interfaces;

/// <summary>
/// Sub-items on action items (WO-2026-11 A-2).
///
/// Visibility is the parent's: a parent the caller cannot open through
/// <c>GET /api/action-items/{id}</c> (deleted, or in a private workspace the
/// caller is not part of) gives <see cref="KeyNotFoundException"/>.
///
/// Error contract (mapped by <c>ActionItemSubItemsController</c>):
/// <see cref="KeyNotFoundException"/> = 404,
/// <see cref="ArgumentException"/> (incl. <c>NonWorkingDateException</c>) = 400,
/// <see cref="UnauthorizedAccessException"/> = 403,
/// <see cref="SubItemParentLockedException"/> = 409 (parent is Done or Cancelled).
/// </summary>
public interface IActionItemSubItemService
{
    Task<ActionItemSubItemListDto> GetForActionItemAsync(Guid actionItemId, CancellationToken ct = default);

    Task<ActionItemSubItemDto> CreateAsync(
        Guid actionItemId, CreateActionItemSubItemDto dto, string userId, CancellationToken ct = default);

    Task<ActionItemSubItemDto> UpdateAsync(
        Guid actionItemId, Guid subItemId, UpdateActionItemSubItemDto dto, string userId, CancellationToken ct = default);

    Task DeleteAsync(Guid actionItemId, Guid subItemId, string userId, CancellationToken ct = default);

    /// <summary>
    /// Ticks or unticks a sub-item. Allowed when <paramref name="canEdit"/>
    /// (the caller holds <c>ActionItems.Edit</c>) or when the caller is
    /// assigned to this sub-item; otherwise <see cref="UnauthorizedAccessException"/>.
    /// </summary>
    Task<ActionItemSubItemDto> SetDoneAsync(
        Guid actionItemId, Guid subItemId, bool isDone, string userId, bool canEdit, CancellationToken ct = default);

    /// <summary>New order; <paramref name="subItemIds"/> must list every live sub-item of the parent once.</summary>
    Task<ActionItemSubItemListDto> ReorderAsync(
        Guid actionItemId, IReadOnlyList<Guid> subItemIds, string userId, CancellationToken ct = default);
}
