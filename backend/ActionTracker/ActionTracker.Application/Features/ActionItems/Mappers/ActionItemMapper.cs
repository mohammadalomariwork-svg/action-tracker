using ActionTracker.Application.Features.ActionItems.DTOs;
using ActionTracker.Domain.Entities;

namespace ActionTracker.Application.Features.ActionItems.Mappers;

public static class ActionItemMapper
{
    public static ActionItemResponseDto ToDto(ActionItem item) => new()
    {
        Id             = item.Id,
        ActionId       = item.ActionId,
        Title          = item.Title,
        Description    = item.Description,
        WorkspaceId    = item.WorkspaceId,
        WorkspaceTitle = item.Workspace?.Title ?? string.Empty,
        MilestoneId    = item.MilestoneId,
        MilestoneName  = item.Milestone?.Name,
        Priority       = item.Priority,
        Status         = item.Status,
        StartDate      = item.StartDate,
        DueDate        = item.DueDate,
        Progress       = item.Progress,
        IsEscalated    = item.IsEscalated,
        CreatedAt      = item.CreatedAt,
        UpdatedAt      = item.UpdatedAt,
        IsDeleted      = item.IsDeleted,
        Assignees      = item.Assignees?.Select(a => new AssigneeDto
        {
            UserId   = a.UserId,
            FullName = a.User?.FullName ?? string.Empty,
            Email    = a.User?.Email    ?? string.Empty,
        }).ToList() ?? new(),
        Escalations    = item.Escalations?.OrderByDescending(e => e.CreatedAt).Select(e => new EscalationDto
        {
            Id              = e.Id,
            Explanation     = e.Explanation,
            EscalatedByUserId = e.EscalatedByUserId,
            EscalatedByName = e.EscalatedByUser?.FullName ?? string.Empty,
            CreatedAt       = e.CreatedAt,
        }).ToList() ?? new(),
        Comments       = item.Comments?.OrderByDescending(c => c.CreatedAt).Select(c => new CommentDto
        {
            Id               = c.Id,
            Content          = c.Content,
            AuthorUserId     = c.AuthorUserId,
            AuthorName       = c.Author?.FullName ?? string.Empty,
            IsHighImportance = c.IsHighImportance,
            CreatedAt        = c.CreatedAt,
            UpdatedAt        = c.UpdatedAt,
        }).ToList() ?? new(),
    };
}
