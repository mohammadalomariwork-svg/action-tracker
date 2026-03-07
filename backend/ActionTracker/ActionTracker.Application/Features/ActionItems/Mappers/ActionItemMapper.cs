using ActionTracker.Application.Features.ActionItems.DTOs;
using ActionTracker.Domain.Entities;

namespace ActionTracker.Application.Features.ActionItems.Mappers;

public static class ActionItemMapper
{
    public static ActionItemResponseDto ToDto(ActionItem item) => new()
    {
        Id            = item.Id,
        ActionId      = item.ActionId,
        Title         = item.Title,
        Description   = item.Description,
        AssigneeId    = item.AssigneeId,
        Category      = item.Category,
        Priority      = item.Priority,
        Status        = item.Status,
        DueDate       = item.DueDate,
        Progress      = item.Progress,
        IsEscalated   = item.IsEscalated,
        Notes         = item.Notes,
        CreatedAt     = item.CreatedAt,
        UpdatedAt     = item.UpdatedAt,
        AssigneeName  = item.Assignee?.FullName ?? string.Empty,
        AssigneeEmail = item.Assignee?.Email    ?? string.Empty,
    };
}
