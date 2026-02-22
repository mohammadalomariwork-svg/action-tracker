using ActionTracker.Application.Features.ActionItems.DTOs;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Enums;

namespace ActionTracker.Application.Features.ActionItems.Interfaces;

public interface IActionItemService
{
    Task<PagedResult<ActionItemResponseDto>> GetAllAsync(ActionItemFilterDto filter, CancellationToken ct);
    Task<ActionItemResponseDto?>             GetByIdAsync(int id, CancellationToken ct);
    Task<ActionItemResponseDto>              CreateAsync(ActionItemCreateDto dto, string createdByUserId, CancellationToken ct);
    Task<ActionItemResponseDto>              UpdateAsync(int id, ActionItemUpdateDto dto, CancellationToken ct);
    Task                                     DeleteAsync(int id, CancellationToken ct);
    Task                                     UpdateStatusAsync(int id, ActionStatus newStatus, CancellationToken ct);
    Task<int>                                ProcessOverdueItemsAsync(CancellationToken ct);
}
