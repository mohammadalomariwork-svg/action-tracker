using ActionTracker.Application.Features.ActionItems.DTOs;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Enums;

namespace ActionTracker.Application.Features.ActionItems.Interfaces;

public interface IActionItemService
{
    Task<PagedResult<ActionItemResponseDto>> GetAllAsync(ActionItemFilterDto filter, CancellationToken ct);
    Task<ActionItemResponseDto?>             GetByIdAsync(Guid id, CancellationToken ct);
    Task<ActionItemResponseDto>              CreateAsync(ActionItemCreateDto dto, string createdByUserId, CancellationToken ct);
    Task<ActionItemResponseDto>              UpdateAsync(Guid id, ActionItemUpdateDto dto, CancellationToken ct);
    Task                                     DeleteAsync(Guid id, CancellationToken ct);
    Task                                     UpdateStatusAsync(Guid id, ActionStatus newStatus, CancellationToken ct);
    Task<int>                                ProcessOverdueItemsAsync(CancellationToken ct);
    Task<List<AssignableUserDto>>             GetAssignableUsersAsync(CancellationToken ct);
}
