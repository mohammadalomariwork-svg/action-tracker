using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.ActionItems.DTOs;
using ActionTracker.Application.Features.ActionItems.Interfaces;
using ActionTracker.Application.Features.ActionItems.Mappers;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Application.Features.ActionItems.Services;

public class ActionItemService : IActionItemService
{
    private readonly IAppDbContext _dbContext;
    private readonly ILogger<ActionItemService> _logger;

    public ActionItemService(IAppDbContext dbContext, ILogger<ActionItemService> logger)
    {
        _dbContext = dbContext;
        _logger    = logger;
    }

    // -------------------------------------------------------------------------
    // Query
    // -------------------------------------------------------------------------

    public async Task<PagedResult<ActionItemResponseDto>> GetAllAsync(
        ActionItemFilterDto filter, CancellationToken ct)
    {
        var query = _dbContext.ActionItems
            .Include(a => a.Workspace)
            .Include(a => a.Assignees).ThenInclude(aa => aa.User)
            .Include(a => a.Escalations).ThenInclude(e => e.EscalatedByUser)
            .AsQueryable();

        if (filter.IncludeDeleted)
            query = query.IgnoreQueryFilters();

        // Enum filters
        if (filter.Status.HasValue)
            query = query.Where(a => a.Status == filter.Status.Value);

        if (filter.Priority.HasValue)
            query = query.Where(a => a.Priority == filter.Priority.Value);

        if (filter.WorkspaceId.HasValue)
            query = query.Where(a => a.WorkspaceId == filter.WorkspaceId.Value);

        if (!string.IsNullOrWhiteSpace(filter.AssigneeId))
            query = query.Where(a => a.Assignees.Any(aa => aa.UserId == filter.AssigneeId));

        // Full-text search across title, description, and assignee identity
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(a =>
                a.Title.ToLower().Contains(term) ||
                a.Description.ToLower().Contains(term) ||
                a.Assignees.Any(aa =>
                    aa.User.FirstName.ToLower().Contains(term) ||
                    aa.User.LastName.ToLower().Contains(term) ||
                    aa.User.Email!.ToLower().Contains(term)));
        }

        // Dynamic sorting
        query = (filter.SortBy.Trim().ToLower(), filter.SortDescending) switch
        {
            ("title",     false) => query.OrderBy(a => a.Title),
            ("title",     true)  => query.OrderByDescending(a => a.Title),
            ("priority",  false) => query.OrderBy(a => a.Priority),
            ("priority",  true)  => query.OrderByDescending(a => a.Priority),
            ("status",    false) => query.OrderBy(a => a.Status),
            ("status",    true)  => query.OrderByDescending(a => a.Status),
            ("createdat", false) => query.OrderBy(a => a.CreatedAt),
            ("createdat", true)  => query.OrderByDescending(a => a.CreatedAt),
            ("progress",  false) => query.OrderBy(a => a.Progress),
            ("progress",  true)  => query.OrderByDescending(a => a.Progress),
            (_,           false) => query.OrderBy(a => a.DueDate),      // default
            (_,           true)  => query.OrderByDescending(a => a.DueDate),
        };

        // Paginate at the entity level, then map to DTOs in memory
        var paged = await PagedResult<ActionItem>.CreateAsync(query, filter.PageNumber, filter.PageSize, ct);

        _logger.LogInformation(
            "GetAllAsync returned {Count}/{Total} action items (page {Page})",
            paged.Items.Count, paged.TotalCount, paged.PageNumber);

        return new PagedResult<ActionItemResponseDto>
        {
            Items      = paged.Items.Select(ActionItemMapper.ToDto).ToList(),
            TotalCount = paged.TotalCount,
            PageNumber = paged.PageNumber,
            PageSize   = paged.PageSize,
        };
    }

    public async Task<ActionItemResponseDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var item = await _dbContext.ActionItems
            .Include(a => a.Workspace)
            .Include(a => a.Assignees).ThenInclude(aa => aa.User)
            .Include(a => a.Escalations).ThenInclude(e => e.EscalatedByUser)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        return item is null ? null : ActionItemMapper.ToDto(item);
    }

    // -------------------------------------------------------------------------
    // Write
    // -------------------------------------------------------------------------

    public async Task<ActionItemResponseDto> CreateAsync(
        ActionItemCreateDto dto, string createdByUserId, CancellationToken ct)
    {
        // Determine next ActionId sequence across all rows including soft-deleted ones
        var maxSeq = await _dbContext.ActionItems
            .IgnoreQueryFilters()
            .CountAsync(ct);

        var item = new ActionItem
        {
            Id          = Guid.NewGuid(),
            ActionId    = $"ACT-{maxSeq + 1:000}",
            Title       = dto.Title,
            Description = dto.Description,
            WorkspaceId = dto.WorkspaceId,
            Priority    = dto.Priority,
            Status      = dto.Status,
            StartDate   = dto.StartDate,
            DueDate     = dto.DueDate,
            Progress    = dto.Progress,
            IsEscalated = dto.IsEscalated,
            CreatedAt   = DateTime.UtcNow,
        };

        // Auto-set progress to 100 when status is Done
        if (dto.Status == ActionStatus.Done)
            item.Progress = 100;

        // Add assignees
        foreach (var userId in dto.AssigneeIds.Distinct())
        {
            item.Assignees.Add(new ActionItemAssignee
            {
                ActionItemId = item.Id,
                UserId       = userId,
            });
        }

        // Add escalation entry when escalated
        if (dto.IsEscalated && !string.IsNullOrWhiteSpace(dto.EscalationExplanation))
        {
            item.Escalations.Add(new ActionItemEscalation
            {
                Id              = Guid.NewGuid(),
                ActionItemId    = item.Id,
                Explanation     = dto.EscalationExplanation.Trim(),
                EscalatedByUserId = createdByUserId,
                CreatedAt       = DateTime.UtcNow,
            });
        }

        _dbContext.ActionItems.Add(item);
        await _dbContext.SaveChangesAsync(ct);

        // Re-fetch with navigations populated
        var created = await _dbContext.ActionItems
            .Include(a => a.Workspace)
            .Include(a => a.Assignees).ThenInclude(aa => aa.User)
            .Include(a => a.Escalations).ThenInclude(e => e.EscalatedByUser)
            .FirstAsync(a => a.Id == item.Id, ct);

        _logger.LogInformation(
            "ActionItem {ActionId} created by user {UserId}", created.ActionId, createdByUserId);

        return ActionItemMapper.ToDto(created);
    }

    public async Task<ActionItemResponseDto> UpdateAsync(
        Guid id, ActionItemUpdateDto dto, string updatedByUserId, CancellationToken ct)
    {
        // Load without User navigations to avoid tracking ApplicationUser entities
        // (IdentityUser.ConcurrencyStamp is an IsConcurrencyToken column — tracking
        //  users here can cause DbUpdateConcurrencyException during SaveChanges).
        var item = await _dbContext.ActionItems
            .Include(a => a.Workspace)
            .Include(a => a.Assignees)
            .Include(a => a.Escalations)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new KeyNotFoundException($"ActionItem {id} not found.");

        // Patch only the fields that were supplied
        if (dto.Title       is not null) item.Title       = dto.Title;
        if (dto.Description is not null) item.Description = dto.Description;
        if (dto.WorkspaceId is not null) item.WorkspaceId = dto.WorkspaceId.Value;
        if (dto.Priority    is not null) item.Priority    = dto.Priority.Value;
        if (dto.Status      is not null) item.Status      = dto.Status.Value;
        if (dto.StartDate   is not null) item.StartDate   = dto.StartDate.Value;
        if (dto.DueDate     is not null) item.DueDate     = dto.DueDate.Value;
        if (dto.Progress    is not null) item.Progress    = dto.Progress.Value;
        if (dto.IsEscalated is not null) item.IsEscalated = dto.IsEscalated.Value;

        // Auto-set progress to 100 when status is Done
        if (dto.Status == ActionStatus.Done)
            item.Progress = 100;

        // Add escalation entry when escalated with explanation
        if (dto.IsEscalated == true && !string.IsNullOrWhiteSpace(dto.EscalationExplanation))
        {
            _dbContext.ActionItemEscalations.Add(new ActionItemEscalation
            {
                Id                = Guid.NewGuid(),
                ActionItemId      = item.Id,
                Explanation       = dto.EscalationExplanation.Trim(),
                EscalatedByUserId = updatedByUserId,
                CreatedAt         = DateTime.UtcNow,
            });
        }

        // Replace assignees when a new list is supplied
        if (dto.AssigneeIds is not null)
        {
            // Remove existing
            _dbContext.ActionItemAssignees.RemoveRange(item.Assignees);

            // Add new
            item.Assignees.Clear();
            foreach (var userId in dto.AssigneeIds.Distinct())
            {
                item.Assignees.Add(new ActionItemAssignee
                {
                    ActionItemId = item.Id,
                    UserId       = userId,
                });
            }
        }

        await _dbContext.SaveChangesAsync(ct);

        // Re-fetch so navigations reflect changes
        var updated = await _dbContext.ActionItems
            .Include(a => a.Workspace)
            .Include(a => a.Assignees).ThenInclude(aa => aa.User)
            .Include(a => a.Escalations).ThenInclude(e => e.EscalatedByUser)
            .FirstAsync(a => a.Id == id, ct);

        _logger.LogInformation("ActionItem {Id} updated", id);

        return ActionItemMapper.ToDto(updated);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var item = await _dbContext.ActionItems
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new KeyNotFoundException($"ActionItem {id} not found.");

        item.IsDeleted = true;
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("ActionItem {Id} soft-deleted", id);
    }

    public async Task UpdateStatusAsync(Guid id, ActionStatus newStatus, CancellationToken ct)
    {
        var item = await _dbContext.ActionItems
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new KeyNotFoundException($"ActionItem {id} not found.");

        item.Status = newStatus;

        // Completing an item auto-sets progress to 100
        if (newStatus == ActionStatus.Done)
        {
            item.Progress = 100;
        }
        // If the caller marks it anything other than Done but it's already past due, force Overdue
        else if (item.DueDate < DateTime.UtcNow)
        {
            item.Status = ActionStatus.Overdue;
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("ActionItem {Id} status set to {Status}", id, item.Status);
    }

    public async Task<int> ProcessOverdueItemsAsync(CancellationToken ct)
    {
        var overdueItems = await _dbContext.ActionItems
            .Where(a =>
                a.Status != ActionStatus.Done &&
                a.Status != ActionStatus.Overdue &&
                a.DueDate < DateTime.UtcNow)
            .ToListAsync(ct);

        foreach (var item in overdueItems)
            item.Status = ActionStatus.Overdue;

        if (overdueItems.Count > 0)
            await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Marked {Count} action items as Overdue", overdueItems.Count);

        return overdueItems.Count;
    }

    public async Task<List<AssignableUserDto>> GetAssignableUsersAsync(CancellationToken ct)
    {
        return await _dbContext.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Select(u => new AssignableUserDto
            {
                Id       = u.Id,
                FullName = u.FirstName + " " + u.LastName,
                Email    = u.Email ?? string.Empty,
            })
            .ToListAsync(ct);
    }
}
