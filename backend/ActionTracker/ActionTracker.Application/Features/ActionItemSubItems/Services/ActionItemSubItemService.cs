using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.ActionItemSubItems.DTOs;
using ActionTracker.Application.Features.ActionItemSubItems.Interfaces;
using ActionTracker.Application.Features.Calendars;
using ActionTracker.Application.Features.Calendars.Interfaces;
using ActionTracker.Application.Features.Workflow.Interfaces;
using ActionTracker.Application.Features.Workspaces.Interfaces;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Application.Features.ActionItemSubItems.Services;

/// <inheritdoc />
public class ActionItemSubItemService : IActionItemSubItemService
{
    private readonly IAppDbContext _db;
    private readonly IWorkCalendarService _calendars;
    private readonly IWorkspaceService _workspaces;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ActionItemSubItemService> _logger;

    public ActionItemSubItemService(
        IAppDbContext db,
        IWorkCalendarService calendars,
        IWorkspaceService workspaces,
        IServiceScopeFactory scopeFactory,
        ILogger<ActionItemSubItemService> logger)
    {
        _db           = db;
        _calendars    = calendars;
        _workspaces   = workspaces;
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    // ── Read ─────────────────────────────────────────────────────────────────

    public async Task<ActionItemSubItemListDto> GetForActionItemAsync(Guid actionItemId, CancellationToken ct = default)
    {
        var parent = await LoadParentAsync(actionItemId, ct);
        return await BuildListAsync(parent, ct);
    }

    // ── Write ────────────────────────────────────────────────────────────────

    public async Task<ActionItemSubItemDto> CreateAsync(
        Guid actionItemId, CreateActionItemSubItemDto dto, string userId, CancellationToken ct = default)
    {
        var parent = await LoadParentAsync(actionItemId, ct);
        EnsureParentAcceptsChanges(parent);

        var start = dto.StartDate.HasValue ? ActionItemSubItemRules.ToStoredDay(dto.StartDate.Value) : (DateTime?)null;
        var end   = ActionItemSubItemRules.ToStoredDay(dto.EndDate);

        var rangeProblem = ActionItemSubItemRules.DateRangeProblem(start, end, parent.StartDate, parent.DueDate);
        if (rangeProblem != null)
            throw new ArgumentException(rangeProblem);

        // Working-day rule (WO-2026-11 C-3) — the parent's workspace calendar.
        await _calendars.EnsureWorkingDaysAsync(parent.WorkspaceId, new[]
        {
            WorkingDateCheck.New(ActionItemSubItemRules.StartField, start),
            WorkingDateCheck.New(ActionItemSubItemRules.EndField,   end),
        }, ct);

        var assigneeIds = DistinctIds(dto.AssigneeIds);
        await EnsureAssignableAsync(parent.WorkspaceId, assigneeIds, ct);

        var nextOrder = (await _db.ActionItemSubItems
            .Where(s => s.ActionItemId == actionItemId)
            .Select(s => (int?)s.SortOrder)
            .MaxAsync(ct) ?? 0) + 1;

        var subItem = new ActionItemSubItem
        {
            Id              = Guid.NewGuid(),
            ActionItemId    = actionItemId,
            Title           = dto.Title.Trim(),
            Description     = NormalizeDescription(dto.Description),
            StartDate       = start,
            EndDate         = end,
            SortOrder       = nextOrder,
            CreatedByUserId = userId,
            CreatedAt       = DateTime.UtcNow,
        };
        foreach (var id in assigneeIds)
            subItem.Assignees.Add(new ActionItemSubItemAssignee { SubItemId = subItem.Id, UserId = id });

        _db.ActionItemSubItems.Add(subItem);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Sub-item {SubItemId} added to ActionItem {ActionItemId} by {UserId}",
            subItem.Id, actionItemId, userId);

        QueueAssignedNotification(subItem.Id, assigneeIds, userId);
        return await LoadDtoAsync(parent, subItem.Id, ct);
    }

    public async Task<ActionItemSubItemDto> UpdateAsync(
        Guid actionItemId, Guid subItemId, UpdateActionItemSubItemDto dto, string userId, CancellationToken ct = default)
    {
        var parent = await LoadParentAsync(actionItemId, ct);
        EnsureParentAcceptsChanges(parent);
        var subItem = await LoadTrackedSubItemAsync(actionItemId, subItemId, ct);

        var start = dto.StartDate.HasValue ? ActionItemSubItemRules.ToStoredDay(dto.StartDate.Value) : (DateTime?)null;
        var end   = ActionItemSubItemRules.ToStoredDay(dto.EndDate);

        // The range rule runs when a date changes, on both dates; a sub-item
        // left outside by a later change of the parent's dates still saves
        // other edits (and can still be ticked) until its dates are edited.
        var datesChanged = !ActionItemSubItemRules.SameDay(start, subItem.StartDate)
                           || !ActionItemSubItemRules.SameDay(end, subItem.EndDate);
        if (datesChanged)
        {
            var rangeProblem = ActionItemSubItemRules.DateRangeProblem(start, end, parent.StartDate, parent.DueDate);
            if (rangeProblem != null)
                throw new ArgumentException(rangeProblem);
        }

        // Working-day rule — only the dates whose day changes (Q8).
        await _calendars.EnsureWorkingDaysAsync(parent.WorkspaceId, new[]
        {
            WorkingDateCheck.Changed(ActionItemSubItemRules.StartField, start, subItem.StartDate),
            WorkingDateCheck.Changed(ActionItemSubItemRules.EndField,   end,   subItem.EndDate),
        }, ct);

        // Assignees: the list replaces the current one. Only users being added
        // must be in the workspace's pool — someone already assigned who has
        // since left it may stay (as on the parent's edit forms).
        var wanted  = DistinctIds(dto.AssigneeIds);
        var current = subItem.Assignees.Select(a => a.UserId).ToList();
        var added   = wanted.Where(id => !current.Contains(id, StringComparer.Ordinal)).ToList();
        await EnsureAssignableAsync(parent.WorkspaceId, added, ct);

        subItem.Title           = dto.Title.Trim();
        subItem.Description     = NormalizeDescription(dto.Description);
        subItem.StartDate       = start;
        subItem.EndDate         = end;
        subItem.UpdatedAt       = DateTime.UtcNow;
        subItem.UpdatedByUserId = userId;

        foreach (var removed in subItem.Assignees.Where(a => !wanted.Contains(a.UserId, StringComparer.Ordinal)).ToList())
        {
            subItem.Assignees.Remove(removed);
            _db.ActionItemSubItemAssignees.Remove(removed);
        }
        foreach (var id in added)
            subItem.Assignees.Add(new ActionItemSubItemAssignee { SubItemId = subItem.Id, UserId = id });

        await _db.SaveChangesAsync(ct);

        QueueAssignedNotification(subItem.Id, added, userId);
        return await LoadDtoAsync(parent, subItem.Id, ct);
    }

    public async Task DeleteAsync(Guid actionItemId, Guid subItemId, string userId, CancellationToken ct = default)
    {
        var parent = await LoadParentAsync(actionItemId, ct);
        EnsureParentAcceptsChanges(parent);
        var subItem = await LoadTrackedSubItemAsync(actionItemId, subItemId, ct);

        // Deleted on its own: DeletedWithParent stays false, so restoring the
        // parent later does not bring it back (D2).
        subItem.IsDeleted         = true;
        subItem.DeletedWithParent = false;
        subItem.UpdatedAt         = DateTime.UtcNow;
        subItem.UpdatedByUserId   = userId;

        // Keep the remaining order dense (1..n).
        var remaining = await _db.ActionItemSubItems
            .Where(s => s.ActionItemId == actionItemId && s.Id != subItemId)
            .OrderBy(s => s.SortOrder).ThenBy(s => s.CreatedAt)
            .ToListAsync(ct);
        for (var i = 0; i < remaining.Count; i++)
            remaining[i].SortOrder = i + 1;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Sub-item {SubItemId} of ActionItem {ActionItemId} soft-deleted by {UserId}",
            subItemId, actionItemId, userId);
    }

    public async Task<ActionItemSubItemDto> SetDoneAsync(
        Guid actionItemId, Guid subItemId, bool isDone, string userId, bool canEdit, CancellationToken ct = default)
    {
        var parent  = await LoadParentAsync(actionItemId, ct);
        var subItem = await LoadTrackedSubItemAsync(actionItemId, subItemId, ct);

        if (!canEdit && !subItem.Assignees.Any(a => a.UserId == userId))
            throw new UnauthorizedAccessException(
                "Only a user assigned to this sub-item, or who may edit action items, can mark it done or not done.");

        EnsureParentAcceptsChanges(parent);

        if (subItem.IsDone != isDone)
        {
            subItem.IsDone          = isDone;
            subItem.DoneAt          = isDone ? DateTime.UtcNow : null;
            subItem.DoneByUserId    = isDone ? userId : null;
            subItem.UpdatedAt       = DateTime.UtcNow;
            subItem.UpdatedByUserId = userId;
            await _db.SaveChangesAsync(ct);

            if (isDone)
                QueueCompletedNotification(subItem.Id, userId);
        }

        return await LoadDtoAsync(parent, subItem.Id, ct);
    }

    public async Task<ActionItemSubItemListDto> ReorderAsync(
        Guid actionItemId, IReadOnlyList<Guid> subItemIds, string userId, CancellationToken ct = default)
    {
        var parent = await LoadParentAsync(actionItemId, ct);
        EnsureParentAcceptsChanges(parent);

        var subItems = await _db.ActionItemSubItems
            .Where(s => s.ActionItemId == actionItemId)
            .ToListAsync(ct);

        var liveIds = subItems.Select(s => s.Id).ToHashSet();
        if (subItemIds.Count != liveIds.Count || subItemIds.Distinct().Count() != subItemIds.Count
            || !subItemIds.All(liveIds.Contains))
            throw new ArgumentException("The new order must list every sub-item of the action item exactly once.");

        var byId = subItems.ToDictionary(s => s.Id);
        for (var i = 0; i < subItemIds.Count; i++)
        {
            var s = byId[subItemIds[i]];
            if (s.SortOrder == i + 1) continue;
            s.SortOrder       = i + 1;
            s.UpdatedAt       = DateTime.UtcNow;
            s.UpdatedByUserId = userId;
        }

        await _db.SaveChangesAsync(ct);
        return await BuildListAsync(parent, ct);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private sealed record ParentInfo(Guid Id, Guid WorkspaceId, DateTime? StartDate, DateTime DueDate, ActionStatus Status);

    /// <summary>
    /// The parent through the action-item query filters: a deleted parent, or
    /// one in a private workspace the caller is not part of, is "not found".
    /// </summary>
    private async Task<ParentInfo> LoadParentAsync(Guid actionItemId, CancellationToken ct) =>
        await _db.ActionItems
            .AsNoTracking()
            .Where(a => a.Id == actionItemId)
            .Select(a => new ParentInfo(a.Id, a.WorkspaceId, a.StartDate, a.DueDate, a.Status))
            .FirstOrDefaultAsync(ct)
        ?? throw new KeyNotFoundException($"ActionItem {actionItemId} not found.");

    private static void EnsureParentAcceptsChanges(ParentInfo parent)
    {
        if (!ActionItemSubItemRules.ParentAcceptsChanges(parent.Status))
            throw new SubItemParentLockedException(ActionItemSubItemRules.ParentLockedMessage(parent.Status));
    }

    private async Task<ActionItemSubItem> LoadTrackedSubItemAsync(Guid actionItemId, Guid subItemId, CancellationToken ct) =>
        await _db.ActionItemSubItems
            .Include(s => s.Assignees)
            .FirstOrDefaultAsync(s => s.Id == subItemId && s.ActionItemId == actionItemId, ct)
        ?? throw new KeyNotFoundException($"Sub-item {subItemId} not found.");

    private static List<string> DistinctIds(IEnumerable<string>? ids) =>
        (ids ?? Enumerable.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    /// <summary>
    /// The same pool as the parent's assignee pickers:
    /// <c>GET /api/workspaces/{id}/assignable-users</c> (active admins, members
    /// and project managers of the workspace).
    /// </summary>
    private async Task EnsureAssignableAsync(Guid workspaceId, IReadOnlyCollection<string> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0) return;

        var pool = (await _workspaces.GetAssignableUsersAsync(workspaceId))
            .Select(u => u.UserId)
            .ToHashSet(StringComparer.Ordinal);

        var refused = userIds.Where(id => !pool.Contains(id)).ToList();
        if (refused.Count == 0) return;

        var names = await _db.Users
            .Where(u => refused.Contains(u.Id))
            .Select(u => new { u.Id, Name = (u.FirstName + " " + u.LastName).Trim() })
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        var labels = refused.Select(id => names.TryGetValue(id, out var n) && n.Length > 0 ? n : $"unknown user {id}");

        throw new ArgumentException(
            "Sub-item assignees must be active admins, members or project managers of the action item's workspace. "
            + "Not allowed: " + string.Join(", ", labels) + ".");
    }

    private async Task<ActionItemSubItemListDto> BuildListAsync(ParentInfo parent, CancellationToken ct)
    {
        var subItems = await _db.ActionItemSubItems
            .AsNoTracking()
            .Where(s => s.ActionItemId == parent.Id)
            .Include(s => s.Assignees).ThenInclude(a => a.User)
            .Include(s => s.DoneByUser)
            .OrderBy(s => s.SortOrder).ThenBy(s => s.CreatedAt)
            .ToListAsync(ct);

        var items = subItems.Select(s => ToDto(s, parent)).ToList();
        return new ActionItemSubItemListDto
        {
            ActionItemId         = parent.Id,
            ParentStartDate      = parent.StartDate,
            ParentDueDate        = parent.DueDate,
            ParentAcceptsChanges = ActionItemSubItemRules.ParentAcceptsChanges(parent.Status),
            TotalCount           = items.Count,
            DoneCount            = items.Count(i => i.IsDone),
            Items                = items,
        };
    }

    private async Task<ActionItemSubItemDto> LoadDtoAsync(ParentInfo parent, Guid subItemId, CancellationToken ct)
    {
        var subItem = await _db.ActionItemSubItems
            .AsNoTracking()
            .Include(s => s.Assignees).ThenInclude(a => a.User)
            .Include(s => s.DoneByUser)
            .FirstAsync(s => s.Id == subItemId, ct);
        return ToDto(subItem, parent);
    }

    private static ActionItemSubItemDto ToDto(ActionItemSubItem s, ParentInfo parent) => new()
    {
        Id           = s.Id,
        ActionItemId = s.ActionItemId,
        Title        = s.Title,
        Description  = s.Description,
        StartDate    = s.StartDate,
        EndDate      = s.EndDate,
        SortOrder    = s.SortOrder,
        IsDone       = s.IsDone,
        DoneAt       = s.DoneAt,
        DoneByUserId = s.DoneByUserId,
        DoneByName   = s.DoneByUser is null ? null : (s.DoneByUser.FirstName + " " + s.DoneByUser.LastName).Trim(),
        Assignees    = s.Assignees
            .Select(a => new SubItemAssigneeDto
            {
                UserId   = a.UserId,
                FullName = a.User is null ? string.Empty : (a.User.FirstName + " " + a.User.LastName).Trim(),
                Email    = a.User?.Email ?? string.Empty,
            })
            .OrderBy(a => a.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList(),
        IsOutsideParentDates = ActionItemSubItemRules.IsOutsideParentDates(s.StartDate, s.EndDate, parent.StartDate, parent.DueDate),
        CreatedByUserId = s.CreatedByUserId,
        CreatedAt       = s.CreatedAt,
        UpdatedAt       = s.UpdatedAt,
    };

    // ── Notifications (in-app + SignalR + e-mail, through IWorkflowNotificationHelper) ──

    private void QueueAssignedNotification(Guid subItemId, IReadOnlyCollection<string> newAssigneeIds, string actorUserId)
    {
        // The helper skips the actor; nothing to send when only the actor was added.
        if (!newAssigneeIds.Any(id => !string.Equals(id, actorUserId, StringComparison.Ordinal)))
            return;
        var ids = newAssigneeIds.ToList();
        _ = RunNotificationAsync(h => h.NotifySubItemAssignedAsync(subItemId, ids, actorUserId), subItemId);
    }

    private void QueueCompletedNotification(Guid subItemId, string actorUserId) =>
        _ = RunNotificationAsync(h => h.NotifySubItemCompletedAsync(subItemId, actorUserId), subItemId);

    /// <summary>Fire-and-forget in a fresh scope (the request's DbContext is gone by then); never fails the save.</summary>
    private Task RunNotificationAsync(Func<IWorkflowNotificationHelper, Task> send, Guid subItemId) =>
        Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var helper = scope.ServiceProvider.GetRequiredService<IWorkflowNotificationHelper>();
                await send(helper);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notifications for sub-item {SubItemId}", subItemId);
            }
        });
}
