using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.ActionItemUpdates.DTOs;
using ActionTracker.Application.Features.ActionItemUpdates.Interfaces;
using ActionTracker.Application.Features.Workflow.Interfaces;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Application.Features.ActionItemUpdates.Services;

/// <inheritdoc />
/// <remarks>
/// Visibility comes from the query filters on <see cref="ActionItem"/>,
/// <see cref="ActionItemSubItem"/> and <see cref="ActionItemUpdate"/>: a
/// deleted or private parent is not found, and a draft is returned to its
/// author only. Nothing here uses <c>IgnoreQueryFilters()</c>.
/// </remarks>
public class ActionItemUpdateService : IActionItemUpdateService
{
    private readonly IAppDbContext _db;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ActionItemUpdateService> _logger;

    public ActionItemUpdateService(
        IAppDbContext db,
        IServiceScopeFactory scopeFactory,
        ILogger<ActionItemUpdateService> logger)
    {
        _db           = db;
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    // ── Read ─────────────────────────────────────────────────────────────────

    public async Task<ActionUpdateListDto> GetAsync(Guid actionItemId, Guid? subItemId, CancellationToken ct = default)
    {
        await EnsureTargetVisibleAsync(actionItemId, subItemId, ct);

        var rows = await _db.ActionItemUpdates
            .AsNoTracking()
            .Where(u => u.ActionItemId == actionItemId && u.SubItemId == subItemId)
            .Include(u => u.Author)
            .ToListAsync(ct);

        var me = _db.CurrentUserId;
        var items = rows
            .OrderBy(u => u.Status == ActionItemUpdateStatus.Draft ? 0 : 1)
            .ThenByDescending(u => u.Status == ActionItemUpdateStatus.Draft ? u.UpdatedAt ?? u.CreatedAt : u.SubmittedAt ?? u.CreatedAt)
            .ThenByDescending(u => u.CreatedAt)
            .Select(u => ToDto(u, me))
            .ToList();

        return new ActionUpdateListDto
        {
            ActionItemId   = actionItemId,
            SubItemId      = subItemId,
            SubmittedCount = items.Count(i => i.Status == ActionItemUpdateStatus.Submitted),
            DraftCount     = items.Count(i => i.Status == ActionItemUpdateStatus.Draft),
            Items          = items,
        };
    }

    public async Task<ActionUpdateCountsDto> GetCountsAsync(Guid actionItemId, CancellationToken ct = default)
    {
        await EnsureTargetVisibleAsync(actionItemId, null, ct);

        var groups = await _db.ActionItemUpdates
            .AsNoTracking()
            .Where(u => u.ActionItemId == actionItemId)
            .GroupBy(u => u.SubItemId)
            .Select(g => new
            {
                SubItemId = g.Key,
                Submitted = g.Count(u => u.Status == ActionItemUpdateStatus.Submitted),
                Drafts    = g.Count(u => u.Status == ActionItemUpdateStatus.Draft),
            })
            .ToListAsync(ct);

        var item = groups.FirstOrDefault(g => g.SubItemId == null);
        return new ActionUpdateCountsDto
        {
            ActionItemId = actionItemId,
            Item = new ActionUpdateCountDto
            {
                SubmittedCount = item?.Submitted ?? 0,
                DraftCount     = item?.Drafts ?? 0,
            },
            SubItems = groups
                .Where(g => g.SubItemId != null)
                .Select(g => new SubItemActionUpdateCountDto
                {
                    SubItemId      = g.SubItemId!.Value,
                    SubmittedCount = g.Submitted,
                    DraftCount     = g.Drafts,
                })
                .OrderBy(g => g.SubItemId)
                .ToList(),
        };
    }

    // ── Write ────────────────────────────────────────────────────────────────

    public async Task<ActionUpdateDto> CreateAsync(
        Guid actionItemId, Guid? subItemId, CreateActionUpdateDto dto, string userId, CancellationToken ct = default)
    {
        await EnsureTargetVisibleAsync(actionItemId, subItemId, ct);

        var now = DateTime.UtcNow;
        var update = new ActionItemUpdate
        {
            Id               = Guid.NewGuid(),
            ActionItemId     = actionItemId,
            SubItemId        = subItemId,
            Content          = dto.Content.Trim(),
            AuthorUserId     = userId,
            IsHighImportance = dto.IsHighImportance,
            Status           = dto.Submit ? ActionItemUpdateStatus.Submitted : ActionItemUpdateStatus.Draft,
            SubmittedAt      = dto.Submit ? now : null,
            CreatedAt        = now,
        };

        _db.ActionItemUpdates.Add(update);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Action update {UpdateId} ({Status}) added to ActionItem {ActionItemId}{SubItem} by {UserId}",
            update.Id, update.Status, actionItemId, subItemId is null ? string.Empty : $" / sub-item {subItemId}", userId);

        if (update.Status == ActionItemUpdateStatus.Submitted)
            QueueSubmittedNotification(update.Id, userId);

        return await LoadDtoAsync(update.Id, userId, ct);
    }

    public async Task<ActionUpdateDto> EditAsync(
        Guid actionItemId, Guid? subItemId, Guid updateId, EditActionUpdateDto dto, string userId, CancellationToken ct = default)
    {
        var update = await LoadOwnDraftAsync(actionItemId, subItemId, updateId, userId, ct);

        update.Content          = dto.Content.Trim();
        update.IsHighImportance = dto.IsHighImportance;
        update.UpdatedAt        = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await LoadDtoAsync(update.Id, userId, ct);
    }

    public async Task<ActionUpdateDto> SubmitAsync(
        Guid actionItemId, Guid? subItemId, Guid updateId, string userId, CancellationToken ct = default)
    {
        var update = await LoadOwnDraftAsync(actionItemId, subItemId, updateId, userId, ct);

        var now = DateTime.UtcNow;
        update.Status      = ActionItemUpdateStatus.Submitted;
        update.SubmittedAt = now;
        update.UpdatedAt   = now;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Action update {UpdateId} of ActionItem {ActionItemId} submitted by {UserId}",
            updateId, actionItemId, userId);

        QueueSubmittedNotification(update.Id, userId);
        return await LoadDtoAsync(update.Id, userId, ct);
    }

    public async Task DeleteAsync(
        Guid actionItemId, Guid? subItemId, Guid updateId, string userId, CancellationToken ct = default)
    {
        var update = await LoadOwnDraftAsync(actionItemId, subItemId, updateId, userId, ct);

        update.IsDeleted = true;
        update.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Draft action update {UpdateId} of ActionItem {ActionItemId} deleted by {UserId}",
            updateId, actionItemId, userId);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// The action item (and the sub-item, when given) through the query
    /// filters: deleted, or in a private workspace the caller is not part of,
    /// is "not found". A sub-item of another action item is not found either.
    /// </summary>
    private async Task EnsureTargetVisibleAsync(Guid actionItemId, Guid? subItemId, CancellationToken ct)
    {
        if (!await _db.ActionItems.AnyAsync(a => a.Id == actionItemId, ct))
            throw new KeyNotFoundException($"ActionItem {actionItemId} not found.");

        if (subItemId is { } sid
            && !await _db.ActionItemSubItems.AnyAsync(s => s.Id == sid && s.ActionItemId == actionItemId, ct))
            throw new KeyNotFoundException($"Sub-item {sid} not found.");
    }

    /// <summary>
    /// The caller's own draft, tracked. A submitted update is 409 for everyone
    /// (checked first, so the answer is the same for every role); an update
    /// the caller cannot see — someone else's draft — is 404.
    /// </summary>
    private async Task<ActionItemUpdate> LoadOwnDraftAsync(
        Guid actionItemId, Guid? subItemId, Guid updateId, string userId, CancellationToken ct)
    {
        await EnsureTargetVisibleAsync(actionItemId, subItemId, ct);

        var update = await _db.ActionItemUpdates
            .FirstOrDefaultAsync(u => u.Id == updateId && u.ActionItemId == actionItemId && u.SubItemId == subItemId, ct)
            ?? throw new KeyNotFoundException($"Action update {updateId} not found.");

        if (update.Status == ActionItemUpdateStatus.Submitted)
            throw new ActionItemUpdateLockedException();

        // The query filter already hides other users' drafts in a request;
        // this also covers a caller without a user context.
        if (!string.Equals(update.AuthorUserId, userId, StringComparison.Ordinal))
            throw new KeyNotFoundException($"Action update {updateId} not found.");

        return update;
    }

    private async Task<ActionUpdateDto> LoadDtoAsync(Guid updateId, string userId, CancellationToken ct)
    {
        var update = await _db.ActionItemUpdates
            .AsNoTracking()
            .Include(u => u.Author)
            .FirstAsync(u => u.Id == updateId, ct);
        return ToDto(update, userId);
    }

    private static ActionUpdateDto ToDto(ActionItemUpdate u, string? currentUserId) => new()
    {
        Id               = u.Id,
        ActionItemId     = u.ActionItemId,
        SubItemId        = u.SubItemId,
        Content          = u.Content,
        AuthorUserId     = u.AuthorUserId,
        AuthorName       = u.Author is null ? string.Empty : (u.Author.FirstName + " " + u.Author.LastName).Trim(),
        IsHighImportance = u.IsHighImportance,
        Status           = u.Status,
        SubmittedAt      = u.SubmittedAt,
        CreatedAt        = u.CreatedAt,
        UpdatedAt        = u.UpdatedAt,
        CanEdit          = u.Status == ActionItemUpdateStatus.Draft
                           && currentUserId is not null
                           && string.Equals(u.AuthorUserId, currentUserId, StringComparison.Ordinal),
    };

    // ── Notifications (submit only; never for a draft, never to the submitter) ──

    private void QueueSubmittedNotification(Guid updateId, string actorUserId) =>
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var helper = scope.ServiceProvider.GetRequiredService<IWorkflowNotificationHelper>();
                await helper.NotifyActionUpdateSubmittedAsync(updateId, actorUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notifications for action update {UpdateId}", updateId);
            }
        });
}
