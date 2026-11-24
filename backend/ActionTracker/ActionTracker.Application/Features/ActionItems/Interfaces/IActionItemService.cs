using ActionTracker.Application.Features.ActionItems.DTOs;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Enums;

namespace ActionTracker.Application.Features.ActionItems.Interfaces;

public interface IActionItemService
{
    Task<PagedResult<ActionItemResponseDto>> GetAllAsync(ActionItemFilterDto filter, CancellationToken ct);

    /// <summary>
    /// My Actions → "Assigned through a sub-item" (WO-2026-11 A-2): live items
    /// where <paramref name="userId"/> is assigned to a live sub-item but not to
    /// the item itself. Not part of <see cref="GetMyStatsAsync"/> or any
    /// dashboard count. Paging, sorting and the status / priority / search
    /// filters of <paramref name="filter"/> apply; its assignee, creator,
    /// include-deleted and scope fields are ignored.
    /// </summary>
    Task<PagedResult<ActionItemResponseDto>> GetAssignedViaSubItemsAsync(string userId, ActionItemFilterDto filter, CancellationToken ct);
    Task<ActionItemResponseDto?>             GetByIdAsync(Guid id, CancellationToken ct);
    Task<ActionItemResponseDto>              CreateAsync(ActionItemCreateDto dto, string createdByUserId, CancellationToken ct);
    Task<ActionItemResponseDto>              UpdateAsync(Guid id, ActionItemUpdateDto dto, string updatedByUserId, CancellationToken ct);
    Task                                     DeleteAsync(Guid id, CancellationToken ct);
    Task                                     RestoreAsync(Guid id, CancellationToken ct);
    Task                                     UpdateStatusAsync(Guid id, ActionStatus newStatus, CancellationToken ct);
    Task<int>                                ProcessOverdueItemsAsync(CancellationToken ct);

    /// <summary>
    /// Returns aggregate statistics for all action items assigned to the specified user.
    /// </summary>
    Task<ActionItemMyStatsDto> GetMyStatsAsync(string userId, CancellationToken ct);

    // Workflow bypass methods (called by workflow service after approval)
    Task ApplyApprovedDateChangeAsync(Guid actionItemId, DateTime? newStartDate, DateTime? newDueDate);
    Task ApplyApprovedStatusChangeAsync(Guid actionItemId, ActionStatus newStatus);

    // Comments
    Task<List<ActionItemCommentResponseDto>> GetCommentsAsync(Guid actionItemId, CancellationToken ct);
    Task<ActionItemCommentResponseDto>       AddCommentAsync(Guid actionItemId, CreateCommentDto dto, string userId, CancellationToken ct);
    Task<ActionItemCommentResponseDto>       UpdateCommentAsync(Guid actionItemId, Guid commentId, UpdateCommentDto dto, string userId, CancellationToken ct);
    Task                                     DeleteCommentAsync(Guid actionItemId, Guid commentId, string userId, CancellationToken ct);
}
