using ActionTracker.Application.Features.ActionItems.DTOs;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Enums;

namespace ActionTracker.Application.Features.ActionItems.Interfaces;

public interface IActionItemService
{
    Task<PagedResult<ActionItemResponseDto>> GetAllAsync(ActionItemFilterDto filter, CancellationToken ct);
    Task<ActionItemResponseDto?>             GetByIdAsync(Guid id, CancellationToken ct);
    Task<ActionItemResponseDto>              CreateAsync(ActionItemCreateDto dto, string createdByUserId, CancellationToken ct);
    Task<ActionItemResponseDto>              UpdateAsync(Guid id, ActionItemUpdateDto dto, string updatedByUserId, CancellationToken ct);
    Task                                     DeleteAsync(Guid id, CancellationToken ct);
    Task                                     RestoreAsync(Guid id, CancellationToken ct);
    Task                                     UpdateStatusAsync(Guid id, ActionStatus newStatus, CancellationToken ct);
    Task<int>                                ProcessOverdueItemsAsync(CancellationToken ct);
    Task<List<AssignableUserDto>>             GetAssignableUsersAsync(CancellationToken ct);

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
