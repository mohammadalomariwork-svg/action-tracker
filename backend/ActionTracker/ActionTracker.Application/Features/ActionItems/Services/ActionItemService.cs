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
            .Include(a => a.Assignee)
            .AsQueryable();

        // Enum filters
        if (filter.Status.HasValue)
            query = query.Where(a => a.Status == filter.Status.Value);

        if (filter.Priority.HasValue)
            query = query.Where(a => a.Priority == filter.Priority.Value);

        if (filter.Category.HasValue)
            query = query.Where(a => a.Category == filter.Category.Value);

        if (!string.IsNullOrWhiteSpace(filter.AssigneeId))
            query = query.Where(a => a.AssigneeId == filter.AssigneeId);

        // Full-text search across title, description, and assignee identity
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(a =>
                a.Title.ToLower().Contains(term) ||
                a.Description.ToLower().Contains(term) ||
                a.Assignee.FirstName.ToLower().Contains(term) ||
                a.Assignee.LastName.ToLower().Contains(term) ||
                a.Assignee.Email!.ToLower().Contains(term));
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
            ("category",  false) => query.OrderBy(a => a.Category),
            ("category",  true)  => query.OrderByDescending(a => a.Category),
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
            Items      = paged.Items.Select(MapToDto).ToList(),
            TotalCount = paged.TotalCount,
            PageNumber = paged.PageNumber,
            PageSize   = paged.PageSize,
        };
    }

    public async Task<ActionItemResponseDto?> GetByIdAsync(int id, CancellationToken ct)
    {
        var item = await _dbContext.ActionItems
            .Include(a => a.Assignee)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        return item is null ? null : MapToDto(item);
    }

    // -------------------------------------------------------------------------
    // Write
    // -------------------------------------------------------------------------

    public async Task<ActionItemResponseDto> CreateAsync(
        ActionItemCreateDto dto, string createdByUserId, CancellationToken ct)
    {
        // Determine next ActionId sequence across all rows including soft-deleted ones
        var maxId = await _dbContext.ActionItems
            .IgnoreQueryFilters()
            .MaxAsync(a => (int?)a.Id, ct) ?? 0;

        var item = new ActionItem
        {
            ActionId    = $"ACT-{maxId + 1:000}",
            Title       = dto.Title,
            Description = dto.Description,
            AssigneeId  = dto.AssigneeId,
            Category    = dto.Category,
            Priority    = dto.Priority,
            Status      = dto.Status,
            DueDate     = dto.DueDate,
            Progress    = dto.Progress,
            IsEscalated = dto.IsEscalated,
            Notes       = dto.Notes,
            CreatedAt   = DateTime.UtcNow,
        };

        _dbContext.ActionItems.Add(item);
        await _dbContext.SaveChangesAsync(ct);

        // Re-fetch with navigation populated
        var created = await _dbContext.ActionItems
            .Include(a => a.Assignee)
            .FirstAsync(a => a.Id == item.Id, ct);

        _logger.LogInformation(
            "ActionItem {ActionId} created by user {UserId}", created.ActionId, createdByUserId);

        return MapToDto(created);
    }

    public async Task<ActionItemResponseDto> UpdateAsync(
        int id, ActionItemUpdateDto dto, CancellationToken ct)
    {
        var item = await _dbContext.ActionItems
            .Include(a => a.Assignee)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new KeyNotFoundException($"ActionItem {id} not found.");

        // Patch only the fields that were supplied
        if (dto.Title       is not null) item.Title       = dto.Title;
        if (dto.Description is not null) item.Description = dto.Description;
        if (dto.AssigneeId  is not null) item.AssigneeId  = dto.AssigneeId;
        if (dto.Category    is not null) item.Category    = dto.Category.Value;
        if (dto.Priority    is not null) item.Priority    = dto.Priority.Value;
        if (dto.Status      is not null) item.Status      = dto.Status.Value;
        if (dto.DueDate     is not null) item.DueDate     = dto.DueDate.Value;
        if (dto.Progress    is not null) item.Progress    = dto.Progress.Value;
        if (dto.IsEscalated is not null) item.IsEscalated = dto.IsEscalated.Value;
        if (dto.Notes       is not null) item.Notes       = dto.Notes;

        await _dbContext.SaveChangesAsync(ct);

        // Re-fetch so Assignee reflects any AssigneeId change
        var updated = await _dbContext.ActionItems
            .Include(a => a.Assignee)
            .FirstAsync(a => a.Id == id, ct);

        _logger.LogInformation("ActionItem {Id} updated", id);

        return MapToDto(updated);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var item = await _dbContext.ActionItems
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new KeyNotFoundException($"ActionItem {id} not found.");

        item.IsDeleted = true;
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("ActionItem {Id} soft-deleted", id);
    }

    public async Task UpdateStatusAsync(int id, ActionStatus newStatus, CancellationToken ct)
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

    private static ActionItemResponseDto MapToDto(ActionItem item) =>
        ActionItemMapper.ToDto(item);
}
