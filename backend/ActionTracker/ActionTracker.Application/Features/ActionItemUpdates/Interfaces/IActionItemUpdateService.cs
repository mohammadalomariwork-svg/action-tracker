using ActionTracker.Application.Features.ActionItemUpdates.DTOs;

namespace ActionTracker.Application.Features.ActionItemUpdates.Interfaces;

/// <summary>
/// Action updates on action items and sub-items (WO-2026-11 A-3). Every
/// method takes the action item and, for a sub-item update, the sub-item
/// (<paramref name="subItemId"/> null = the action item itself).
/// <para>
/// Errors: <see cref="KeyNotFoundException"/> (404) when the action item or
/// sub-item cannot be seen (deleted, or in a private workspace the caller is
/// not part of) or the update is not visible to the caller — someone else's
/// draft included, so its existence is not revealed;
/// <see cref="ActionItemUpdateLockedException"/> (409) for an edit, submit or
/// delete of a submitted update. Updates are allowed whatever the action
/// item's status, Done and Cancelled included.
/// </para>
/// </summary>
public interface IActionItemUpdateService
{
    Task<ActionUpdateListDto> GetAsync(Guid actionItemId, Guid? subItemId, CancellationToken ct = default);

    Task<ActionUpdateCountsDto> GetCountsAsync(Guid actionItemId, CancellationToken ct = default);

    /// <summary>Creates a draft, or a submitted update when <c>dto.Submit</c> is true (then notifies).</summary>
    Task<ActionUpdateDto> CreateAsync(
        Guid actionItemId, Guid? subItemId, CreateActionUpdateDto dto, string userId, CancellationToken ct = default);

    /// <summary>Edits the caller's own draft.</summary>
    Task<ActionUpdateDto> EditAsync(
        Guid actionItemId, Guid? subItemId, Guid updateId, EditActionUpdateDto dto, string userId, CancellationToken ct = default);

    /// <summary>Submits the caller's own draft (one-way), then notifies.</summary>
    Task<ActionUpdateDto> SubmitAsync(
        Guid actionItemId, Guid? subItemId, Guid updateId, string userId, CancellationToken ct = default);

    /// <summary>Soft-deletes the caller's own draft.</summary>
    Task DeleteAsync(Guid actionItemId, Guid? subItemId, Guid updateId, string userId, CancellationToken ct = default);
}
