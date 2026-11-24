using ActionTracker.Application.Common;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.ActionItems.DTOs;
using ActionTracker.Application.Features.ActionItemSubItems;
using ActionTracker.Application.Features.Dashboard;
using ActionTracker.Application.Features.ActionItems.Interfaces;
using ActionTracker.Application.Features.ActionItems.Mappers;
using ActionTracker.Application.Features.Calendars;
using ActionTracker.Application.Features.Calendars.Interfaces;
using ActionTracker.Application.Features.Notifications;
using ActionTracker.Application.Features.Notifications.DTOs;
using ActionTracker.Application.Features.Workflow.DTOs;
using ActionTracker.Application.Features.Workflow.Interfaces;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActionTracker.Application.Features.ActionItems.Services;

public class ActionItemService : IActionItemService
{
    private readonly IAppDbContext _dbContext;
    private readonly ILogger<ActionItemService> _logger;
    private readonly IEmailSender _emailSender;
    private readonly INotificationService _notificationService;
    private readonly AppSettings _appSettings;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWorkCalendarService _calendars;

    public ActionItemService(
        IAppDbContext dbContext,
        ILogger<ActionItemService> logger,
        IEmailSender emailSender,
        INotificationService notificationService,
        IOptions<AppSettings> appSettings,
        IServiceScopeFactory scopeFactory,
        IWorkCalendarService calendars)
    {
        _dbContext            = dbContext;
        _logger               = logger;
        _emailSender          = emailSender;
        _notificationService  = notificationService;
        _appSettings          = appSettings.Value;
        _scopeFactory         = scopeFactory;
        _calendars            = calendars;
    }

    // -------------------------------------------------------------------------
    // Query
    // -------------------------------------------------------------------------

    public Task<PagedResult<ActionItemResponseDto>> GetAllAsync(
        ActionItemFilterDto filter, CancellationToken ct)
        => QueryAsync(filter, subItemOnlyAssigneeId: null, ct);

    /// <inheritdoc />
    public Task<PagedResult<ActionItemResponseDto>> GetAssignedViaSubItemsAsync(
        string userId, ActionItemFilterDto filter, CancellationToken ct)
    {
        // Fixed server-side: the caller's own sub-item assignments, live items
        // only, in every workspace (like /my-actions; the private-workspace
        // query filters still apply).
        filter.AssigneeId          = string.Empty;
        filter.CreatedById         = string.Empty;
        filter.IncludeDeleted      = false;
        filter.VisibleOrgUnitIds   = null;
        filter.VisibleWorkspaceIds = null;
        return QueryAsync(filter, subItemOnlyAssigneeId: userId, ct);
    }

    /// <summary>
    /// The action-item list query. <paramref name="subItemOnlyAssigneeId"/>
    /// (WO-2026-11 A-2, set only by <see cref="GetAssignedViaSubItemsAsync"/>)
    /// keeps the items where that user is assigned to a live sub-item and is
    /// not an assignee of the item itself.
    /// </summary>
    private async Task<PagedResult<ActionItemResponseDto>> QueryAsync(
        ActionItemFilterDto filter, string? subItemOnlyAssigneeId, CancellationToken ct)
    {
        var query = _dbContext.ActionItems
            .Include(a => a.Workspace)
            .Include(a => a.Project)
            .Include(a => a.Milestone)
            .Include(a => a.Assignees).ThenInclude(aa => aa.User)
            .Include(a => a.Escalations).ThenInclude(e => e.EscalatedByUser)
            .AsQueryable();

        if (filter.IncludeDeleted)
            query = query.IgnoreQueryFilters()
                .WhereWorkspaceVisibleTo(_dbContext.CurrentUserId);

        // Enum filters
        if (filter.Status.HasValue)
            query = query.Where(a => a.Status == filter.Status.Value);

        if (filter.Priority.HasValue)
            query = query.Where(a => a.Priority == filter.Priority.Value);

        if (filter.WorkspaceId.HasValue)
            query = query.Where(a => a.WorkspaceId == filter.WorkspaceId.Value);

        if (filter.ProjectId.HasValue)
            query = query.Where(a => a.ProjectId == filter.ProjectId.Value);

        if (filter.MilestoneId.HasValue)
            query = query.Where(a => a.MilestoneId == filter.MilestoneId.Value);

        if (filter.IsStandalone.HasValue)
            query = query.Where(a => a.IsStandalone == filter.IsStandalone.Value);

        if (!string.IsNullOrWhiteSpace(filter.AssigneeId))
            query = query.Where(a => a.Assignees.Any(aa => aa.UserId == filter.AssigneeId));

        if (!string.IsNullOrWhiteSpace(filter.CreatedById))
            query = query.Where(a => a.CreatedByUserId == filter.CreatedById);

        if (!string.IsNullOrWhiteSpace(subItemOnlyAssigneeId))
        {
            var uid = subItemOnlyAssigneeId;
            query = query.Where(a =>
                a.SubItems.Any(s => !s.IsDeleted && s.Assignees.Any(x => x.UserId == uid))
                && !a.Assignees.Any(x => x.UserId == uid));
        }

        // Dashboard drill-down filters — same rules the dashboards count by
        var today = DateTime.UtcNow.Date;

        if (filter.OpenOnly == true)
            query = query.Where(ActionItemRules.OpenPredicate());

        if (filter.OverdueOnly == true)
            query = query.Where(ActionItemRules.OverduePredicate(today));

        if (filter.IsEscalated.HasValue)
            query = query.Where(a => a.IsEscalated == filter.IsEscalated.Value);

        var duePredicate = ActionItemRules.DueBucketPredicate(filter.DueBucket, today);
        if (duePredicate != null)
            query = query.Where(duePredicate);

        var agePredicate = ActionItemRules.AgeBucketPredicate(filter.AgeBucket, today);
        if (agePredicate != null)
            query = query.Where(agePredicate);

        // Workspace-membership scoping: regular users only see action items in
        // workspaces they're a member/admin of. Privileged roles pass null.
        if (filter.VisibleWorkspaceIds != null)
        {
            var wsIds = filter.VisibleWorkspaceIds;
            if (wsIds.Count == 0)
                query = query.Where(a => false);
            else
                query = query.Where(a => wsIds.Contains(a.WorkspaceId));
        }

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

        var items = paged.Items.Select(ActionItemMapper.ToDto).ToList();
        await FillSubItemCountsAsync(items, subItemOnlyAssigneeId, ct);

        return new PagedResult<ActionItemResponseDto>
        {
            Items      = items,
            TotalCount = paged.TotalCount,
            PageNumber = paged.PageNumber,
            PageSize   = paged.PageSize,
        };
    }

    public async Task<ActionItemResponseDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var item = await _dbContext.ActionItems
            .Include(a => a.Workspace)
            .Include(a => a.Project)
            .Include(a => a.Milestone)
            .Include(a => a.Assignees).ThenInclude(aa => aa.User)
            .Include(a => a.Escalations).ThenInclude(e => e.EscalatedByUser)
            .Include(a => a.Comments).ThenInclude(c => c.Author)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (item is null) return null;
        var dto = ActionItemMapper.ToDto(item);
        await FillSubItemCountsAsync(new[] { dto }, null, ct);
        return dto;
    }

    /// <summary>
    /// "x of y done" (WO-2026-11 A-2): one grouped query for the given items,
    /// through the sub-item query filters (live, visible sub-items only). With
    /// <paramref name="myUserId"/> also counts that user's sub-items.
    /// </summary>
    private async Task FillSubItemCountsAsync(
        IReadOnlyCollection<ActionItemResponseDto> items, string? myUserId, CancellationToken ct)
    {
        if (items.Count == 0) return;
        var ids = items.Select(i => i.Id).ToList();

        var counts = await _dbContext.ActionItemSubItems
            .Where(s => ids.Contains(s.ActionItemId))
            .GroupBy(s => s.ActionItemId)
            .Select(g => new
            {
                ActionItemId = g.Key,
                Total        = g.Count(),
                Done         = g.Count(s => s.IsDone),
            })
            .ToDictionaryAsync(c => c.ActionItemId, ct);

        Dictionary<Guid, int>? mine = null;
        if (!string.IsNullOrWhiteSpace(myUserId))
        {
            mine = await _dbContext.ActionItemSubItemAssignees
                .Where(a => a.UserId == myUserId && ids.Contains(a.SubItem.ActionItemId))
                .GroupBy(a => a.SubItem.ActionItemId)
                .Select(g => new { ActionItemId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(c => c.ActionItemId, c => c.Count, ct);
        }

        foreach (var dto in items)
        {
            if (counts.TryGetValue(dto.Id, out var c))
            {
                dto.SubItemCount     = c.Total;
                dto.SubItemDoneCount = c.Done;
            }
            if (mine != null)
                dto.MySubItemCount = mine.TryGetValue(dto.Id, out var m) ? m : 0;
        }
    }

    // -------------------------------------------------------------------------
    // Write
    // -------------------------------------------------------------------------

    public async Task<ActionItemResponseDto> CreateAsync(
        ActionItemCreateDto dto, string createdByUserId, CancellationToken ct)
    {
        // Block creation if linked to a non-Draft project
        if (dto.ProjectId.HasValue && dto.ProjectId.Value != Guid.Empty)
        {
            var parentProject = await _dbContext.Projects
                .Where(p => p.Id == dto.ProjectId.Value && !p.IsDeleted)
                .Select(p => new { p.Status })
                .FirstOrDefaultAsync(ct);
            if (parentProject != null && parentProject.Status != ProjectStatus.Draft)
                throw new ArgumentException("New action items cannot be added to a project after it has been submitted for approval or activated.");
        }

        // ── Working-day rule (WO-2026-11 C-3) ─────────────────────────────────
        await _calendars.EnsureWorkingDaysAsync(dto.WorkspaceId, new[]
        {
            WorkingDateCheck.New("Planned start", dto.StartDate),
            WorkingDateCheck.New("Planned end",   dto.DueDate),
        }, ct);

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
            WorkspaceId  = dto.WorkspaceId,
            ProjectId    = dto.ProjectId,
            MilestoneId  = dto.MilestoneId,
            // Follows the project link; dto.IsStandalone is ignored (WO-2026-11 A-1b).
            IsStandalone = ActionItemStandalone.ForProject(dto.ProjectId),
            Priority     = dto.Priority,
            Status      = dto.Status,
            StartDate   = dto.StartDate,
            DueDate     = dto.DueDate,
            Progress    = dto.Progress,
            IsEscalated      = dto.IsEscalated,
            CreatedByUserId  = createdByUserId,
            CreatedAt        = DateTime.UtcNow,
        };

        // Auto-set progress to 100 when status is Done
        if (dto.Status == ActionStatus.Done)
            item.Progress = 100;

        // ── Actual dates (WO-2026-11 A-1) ─────────────────────────────────────
        // Created as In Progress = started today; created as Done = finished today.
        if (dto.Status is ActionStatus.InProgress or ActionStatus.Done)
        {
            var today = await ActionItemActualDates.TodayForWorkspaceAsync(_calendars, dto.WorkspaceId, ct);
            (item.ActualStartDate, item.ActualEndDate) = ActionItemActualDates.Resolve(
                previousStatus: null, dto.Status, dto.Status,
                currentStart: null, currentEnd: null,
                suppliedStart: null, clearStart: false, suppliedEnd: null,
                today);
        }

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
            .Include(a => a.Project)
            .Include(a => a.Milestone)
            .Include(a => a.Assignees).ThenInclude(aa => aa.User)
            .Include(a => a.Escalations).ThenInclude(e => e.EscalatedByUser)
            .Include(a => a.Comments).ThenInclude(c => c.Author)
            .FirstAsync(a => a.Id == item.Id, ct);

        _logger.LogInformation(
            "ActionItem {ActionId} created by user {UserId}", created.ActionId, createdByUserId);

        // Fire-and-forget email notifications (new scope to avoid disposed DbContext)
        var capturedCreated = created;
        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
            var notifService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

            try
            {
                var placeholders = BuildActionItemPlaceholders(capturedCreated);
                var creatorEmail = await GetUserEmailFromDbAsync(db, createdByUserId);

                // Send Created notification to creator
                if (creatorEmail is not null)
                {
                    await emailSender.SendEmailAsync("ActionItem.Created", placeholders,
                        [creatorEmail], "ActionItem", capturedCreated.Id, createdByUserId);
                }

                // Send Assigned notification to each assignee
                var assigneeEmails = await GetActiveUserEmailsFromDbAsync(db,
                    capturedCreated.Assignees.Select(a => a.UserId));
                if (assigneeEmails.Count > 0)
                {
                    await emailSender.SendEmailAsync("ActionItem.Assigned", placeholders,
                        assigneeEmails, "ActionItem", capturedCreated.Id, createdByUserId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email for ActionItem.Created {ActionId}", capturedCreated.ActionId);
            }

            // In-app notifications — notify assignees (exclude actor)
            try
            {
                var actorName = await GetUserDisplayNameFromDbAsync(db, createdByUserId);
                var url = $"{_appSettings.FrontendBaseUrl}/actions/{capturedCreated.Id}/view";
                var notifications = capturedCreated.Assignees
                    .Where(a => a.UserId != createdByUserId)
                    .Select(a => a.UserId)
                    .Distinct()
                    .Select(uid => new CreateNotificationDto
                    {
                        UserId              = uid,
                        Title               = "New Action Item Assigned",
                        Message             = $"You've been assigned to {capturedCreated.Title} ({capturedCreated.ActionId})",
                        Type                = "ActionItem",
                        ActionType          = "Assigned",
                        RelatedEntityType   = "ActionItem",
                        RelatedEntityId     = capturedCreated.Id,
                        RelatedEntityCode   = capturedCreated.ActionId,
                        Url                 = url,
                        CreatedByUserId     = createdByUserId,
                        CreatedByDisplayName = actorName,
                    }).ToList();

                if (notifications.Count > 0)
                    await notifService.CreateBulkAsync(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating notifications for ActionItem.Created {ActionId}", capturedCreated.ActionId);
            }
        });

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

        // ── Block edits on completed items ────────────────────────────────────
        // The one exception (WO-2026-11 A-1): a payload that only sets the
        // actual dates, so the recorded start / completion day can be corrected.
        if (item.Status == ActionStatus.Done && !(dto.ChangesOnlyActualDates() && dto.HasActualDates()))
            throw new InvalidOperationException("Completed action items cannot be edited.");

        // ── Date freeze for project-linked items ──────────────────────────────
        if (item.ProjectId.HasValue)
        {
            var parentProject = await _dbContext.Projects
                .Where(p => p.Id == item.ProjectId.Value && !p.IsDeleted)
                .Select(p => new { p.Status })
                .FirstOrDefaultAsync(ct);

            if (parentProject != null && parentProject.Status != ProjectStatus.Draft)
            {
                var dateChanged = (dto.StartDate is not null && dto.StartDate.Value != item.StartDate) ||
                                  (dto.DueDate   is not null && dto.DueDate.Value   != item.DueDate);
                if (dateChanged)
                    throw new ArgumentException("Action item dates cannot be changed after the parent project has been submitted for approval or activated.");
            }
        }

        // ── Working-day rule (WO-2026-11 C-3) ─────────────────────────────────
        // Runs before the standalone branch below. That branch turns a date
        // change into an approval request and only logs a failure, so a refused
        // date has to stop the save here or it would be dropped silently.
        // Only dates whose day changes are checked (Q8). A standalone item is
        // checked against its current workspace, as its request will be.
        var calendarWorkspaceId = item.IsStandalone ? item.WorkspaceId : dto.WorkspaceId ?? item.WorkspaceId;
        await _calendars.EnsureWorkingDaysAsync(calendarWorkspaceId, new[]
        {
            WorkingDateCheck.Changed("Planned start", dto.StartDate, item.StartDate),
            WorkingDateCheck.Changed("Planned end",   dto.DueDate,   item.DueDate),
        }, ct);

        // ── Actual dates (WO-2026-11 A-1) ─────────────────────────────────────
        // Worked out (and validated) before the standalone branch, so a refused
        // value stops the save before any approval request is created. A status
        // the standalone branch sends for approval does not change in this save.
        // The planned-date freeze, the date-change workflow and the working-day
        // rule do not apply to actual dates.
        var statusSentForApproval = item.IsStandalone
                                    && dto.Status is not null
                                    && RequiresStatusApproval(item.Status, dto.Status.Value);
        var targetStatus = statusSentForApproval ? item.Status : dto.Status ?? item.Status;
        var actualStart  = item.ActualStartDate;
        var actualEnd    = item.ActualEndDate;
        if (dto.HasActualDates() || targetStatus != item.Status)
        {
            var today = await ActionItemActualDates.TodayForWorkspaceAsync(_calendars, calendarWorkspaceId, ct);
            (actualStart, actualEnd) = ActionItemActualDates.Resolve(
                item.Status, targetStatus, targetStatus,
                item.ActualStartDate, item.ActualEndDate,
                dto.ActualStartDate, dto.ClearActualStartDate == true, dto.ActualEndDate,
                today);
        }

        // ── Workflow auto-request for standalone items ─────────────────────────
        if (item.IsStandalone)
        {
            var workflowService = _scopeFactory.CreateScope().ServiceProvider
                .GetRequiredService<IActionItemWorkflowService>();

            // Date freeze: auto-create a date change request instead of applying directly
            var dateChanged = (dto.StartDate is not null && dto.StartDate.Value != item.StartDate) ||
                              (dto.DueDate   is not null && dto.DueDate.Value   != item.DueDate);
            if (dateChanged)
            {
                try
                {
                    await workflowService.CreateDateChangeRequestAsync(
                        new CreateDateChangeRequestDto
                        {
                            ActionItemId = item.Id,
                            NewStartDate = dto.StartDate ?? item.StartDate,
                            NewDueDate   = dto.DueDate   ?? item.DueDate,
                            Reason       = "Date change requested via action item edit."
                        }, updatedByUserId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not auto-create date change request for {Id}", item.Id);
                }

                // Strip the date fields so the update proceeds without changing dates
                dto.StartDate = null;
                dto.DueDate   = null;
            }

            // Status approval: auto-create a status change request for terminal transitions
            if (dto.Status is not null && dto.Status.Value != item.Status)
            {
                if (statusSentForApproval)
                {
                    try
                    {
                        await workflowService.CreateStatusChangeRequestAsync(
                            new CreateStatusChangeRequestDto
                            {
                                ActionItemId = item.Id,
                                NewStatus    = dto.Status.Value,
                                Reason       = "Status change requested via action item edit."
                            }, updatedByUserId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not auto-create status change request for {Id}", item.Id);
                    }

                    // Strip the status field so the update proceeds without changing status
                    dto.Status = null;
                }
            }
        }

        // Patch only the fields that were supplied
        var previousProjectId = item.ProjectId;
        if (dto.Title       is not null) item.Title       = dto.Title;
        if (dto.Description is not null) item.Description = dto.Description;
        if (dto.WorkspaceId is not null) item.WorkspaceId = dto.WorkspaceId.Value;
        if (dto.ProjectId   is not null) item.ProjectId   = dto.ProjectId.Value == Guid.Empty ? null : dto.ProjectId.Value;
        if (dto.MilestoneId is not null) item.MilestoneId = dto.MilestoneId.Value == Guid.Empty ? null : dto.MilestoneId.Value;
        // The standalone flag follows the project link and changes only when this
        // save changes the link; dto.IsStandalone is ignored (WO-2026-11 A-1b).
        item.IsStandalone = ActionItemStandalone.AfterUpdate(item.IsStandalone, previousProjectId, item.ProjectId);
        if (dto.Priority    is not null) item.Priority    = dto.Priority.Value;
        if (dto.Status      is not null) item.Status      = dto.Status.Value;
        if (dto.StartDate   is not null) item.StartDate   = dto.StartDate.Value;
        if (dto.DueDate     is not null) item.DueDate     = dto.DueDate.Value;
        if (dto.Progress    is not null) item.Progress    = dto.Progress.Value;
        if (dto.IsEscalated is not null) item.IsEscalated = dto.IsEscalated.Value;
        item.ActualStartDate = actualStart;
        item.ActualEndDate   = actualEnd;

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
            .Include(a => a.Project)
            .Include(a => a.Milestone)
            .Include(a => a.Assignees).ThenInclude(aa => aa.User)
            .Include(a => a.Escalations).ThenInclude(e => e.EscalatedByUser)
            .Include(a => a.Comments).ThenInclude(c => c.Author)
            .FirstAsync(a => a.Id == id, ct);

        _logger.LogInformation("ActionItem {Id} updated", id);

        // Fire-and-forget email for escalation
        if (dto.IsEscalated == true)
        {
            var capturedUpdated = updated;
            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                var notifService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

                try
                {
                    var placeholders = BuildActionItemPlaceholders(capturedUpdated);
                    var recipients = await GetActionItemRecipientEmailsFromDbAsync(db, capturedUpdated);
                    await emailSender.SendEmailAsync("ActionItem.Escalated", placeholders,
                        recipients, "ActionItem", capturedUpdated.Id, updatedByUserId);

                    // In-app notifications
                    var actorName = await GetUserDisplayNameFromDbAsync(db, updatedByUserId);
                    var recipientIds = GetActionItemRecipientUserIds(capturedUpdated, updatedByUserId);
                    var notifications = recipientIds.Select(uid => new CreateNotificationDto
                    {
                        UserId               = uid,
                        Title                = "Action Item Escalated",
                        Message              = $"{capturedUpdated.ActionId} has been escalated",
                        Type                 = "ActionItem",
                        ActionType           = "Escalated",
                        RelatedEntityType    = "ActionItem",
                        RelatedEntityId      = capturedUpdated.Id,
                        RelatedEntityCode    = capturedUpdated.ActionId,
                        Url                  = $"{_appSettings.FrontendBaseUrl}/actions/{capturedUpdated.Id}/view",
                        CreatedByUserId      = updatedByUserId,
                        CreatedByDisplayName = actorName,
                    }).ToList();
                    if (notifications.Count > 0)
                        await notifService.CreateBulkAsync(notifications);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending escalation notifications for ActionItem {Id}", capturedUpdated.Id);
                }
            });
        }

        var updatedDto = ActionItemMapper.ToDto(updated);
        await FillSubItemCountsAsync(new[] { updatedDto }, null, ct);
        return updatedDto;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var item = await _dbContext.ActionItems
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new KeyNotFoundException($"ActionItem {id} not found.");

        if (item.ProjectId.HasValue)
        {
            var parentProject = await _dbContext.Projects
                .Where(p => p.Id == item.ProjectId.Value && !p.IsDeleted)
                .Select(p => new { p.Status })
                .FirstOrDefaultAsync(ct);
            if (parentProject != null && parentProject.Status != ProjectStatus.Draft)
                throw new ArgumentException("Action items cannot be removed from a project after it has been submitted for approval or activated.");
        }

        item.IsDeleted = true;
        // Sub-items go with their parent and come back with it (WO-2026-11 A-2, D2).
        var subItemCount = await ActionItemSubItemCascade.DeleteWithParentsAsync(_dbContext, new[] { id }, ct);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("ActionItem {Id} soft-deleted with {SubItemCount} sub-items", id, subItemCount);
    }

    public async Task RestoreAsync(Guid id, CancellationToken ct)
    {
        var item = await _dbContext.ActionItems
            .IgnoreQueryFilters()
            .WhereWorkspaceVisibleTo(_dbContext.CurrentUserId)
            .Include(a => a.Project)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new KeyNotFoundException($"ActionItem {id} not found.");

        if (!item.IsDeleted)
            throw new InvalidOperationException("Action item is not deleted.");

        if (item.ProjectId != null && item.Project != null && item.Project.IsDeleted)
            throw new InvalidOperationException("Cannot restore this action item because its parent project is deleted. Restore the project first.");

        item.IsDeleted = false;
        // Only the sub-items deleted together with this item (WO-2026-11 A-2, D2).
        var subItemCount = await ActionItemSubItemCascade.RestoreWithParentsAsync(_dbContext, new[] { id }, ct);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("ActionItem {Id} restored with {SubItemCount} sub-items", id, subItemCount);
    }

    public async Task UpdateStatusAsync(Guid id, ActionStatus newStatus, CancellationToken ct)
    {
        var item = await _dbContext.ActionItems
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new KeyNotFoundException($"ActionItem {id} not found.");

        // ── Workflow auto-request for standalone items ─────────────────────────
        if (item.IsStandalone && newStatus != item.Status)
        {
            if (RequiresStatusApproval(item.Status, newStatus))
            {
                // Auto-create a workflow request instead of blocking
                var workflowService = _scopeFactory.CreateScope().ServiceProvider
                    .GetRequiredService<IActionItemWorkflowService>();
                await workflowService.CreateStatusChangeRequestAsync(
                    new CreateStatusChangeRequestDto
                    {
                        ActionItemId = item.Id,
                        NewStatus    = newStatus,
                        Reason       = "Status change requested."
                    }, "system");
                return; // Don't apply the status change — wait for approval
            }
        }

        var previousStatus = item.Status;
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

        // ── Actual dates (WO-2026-11 A-1) ─────────────────────────────────────
        // The asked-for status counts for the actual start: In Progress on a
        // past-due item is stored as Overdue, but the work did start.
        if (newStatus != previousStatus || item.Status != previousStatus)
        {
            var today = await ActionItemActualDates.TodayForWorkspaceAsync(_calendars, item.WorkspaceId, ct);
            ActionItemActualDates.ApplyStatusChange(item, previousStatus, newStatus, today);
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("ActionItem {Id} status set to {Status}", id, item.Status);

        // Fire-and-forget email notifications for status change
        var capturedItem = item;
        var capturedStatus = item.Status;
        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
            var notifService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

            try
            {
                // Re-fetch with navigations for placeholder resolution
                var full = await db.ActionItems
                    .IgnoreQueryFilters()
                    .Include(a => a.Workspace)
                    .Include(a => a.Project)
                    .Include(a => a.Assignees).ThenInclude(aa => aa.User)
                    .FirstOrDefaultAsync(a => a.Id == capturedItem.Id);
                if (full is null) return;

                var placeholders = BuildActionItemPlaceholders(full);
                var recipients = await GetActionItemRecipientEmailsFromDbAsync(db, full);

                await emailSender.SendEmailAsync("ActionItem.StatusChanged", placeholders,
                    recipients, "ActionItem", full.Id, full.CreatedByUserId);

                if (capturedStatus == ActionStatus.Done)
                {
                    var allRecipients = new List<string>(recipients);
                    if (full.Project != null)
                    {
                        var pmEmail = await GetUserEmailFromDbAsync(db, full.Project.ProjectManagerUserId);
                        if (pmEmail is not null) allRecipients.Add(pmEmail);
                    }
                    await emailSender.SendEmailAsync("ActionItem.Completed", placeholders,
                        allRecipients.Distinct().ToList(), "ActionItem", full.Id, full.CreatedByUserId);
                }
                else if (capturedStatus == ActionStatus.Overdue)
                {
                    await emailSender.SendEmailAsync("ActionItem.Overdue", placeholders,
                        recipients, "ActionItem", full.Id, full.CreatedByUserId);
                }

                // In-app notifications for status change
                var actorName = await GetUserDisplayNameFromDbAsync(db, full.CreatedByUserId);
                var recipientIds = GetActionItemRecipientUserIds(full, null);
                var url = $"{_appSettings.FrontendBaseUrl}/actions/{full.Id}/view";

                string title, actionType;
                if (capturedStatus == ActionStatus.Done)
                {
                    title = "Action Item Completed";
                    actionType = "Completed";
                    if (full.Project != null && !string.IsNullOrWhiteSpace(full.Project.ProjectManagerUserId))
                        recipientIds.Add(full.Project.ProjectManagerUserId);
                    recipientIds = recipientIds.Distinct().ToList();
                }
                else if (capturedStatus == ActionStatus.Overdue)
                {
                    title = "Action Item Overdue";
                    actionType = "Overdue";
                }
                else
                {
                    title = "Status Updated";
                    actionType = "StatusChanged";
                }

                var notifications = recipientIds.Select(uid => new CreateNotificationDto
                {
                    UserId               = uid,
                    Title                = title,
                    Message              = $"{full.ActionId} status changed to {capturedStatus}",
                    Type                 = "ActionItem",
                    ActionType           = actionType,
                    RelatedEntityType    = "ActionItem",
                    RelatedEntityId      = full.Id,
                    RelatedEntityCode    = full.ActionId,
                    Url                  = url,
                    CreatedByUserId      = full.CreatedByUserId,
                    CreatedByDisplayName = actorName,
                }).ToList();
                if (notifications.Count > 0)
                    await notifService.CreateBulkAsync(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending status change notifications for ActionItem {Id}", capturedItem.Id);
            }
        });
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

    // -------------------------------------------------------------------------
    // Workflow bypass (called after approval — skips standalone guards)
    // -------------------------------------------------------------------------

    public async Task ApplyApprovedDateChangeAsync(Guid actionItemId, DateTime? newStartDate, DateTime? newDueDate)
    {
        var item = await _dbContext.ActionItems
            .FirstOrDefaultAsync(a => a.Id == actionItemId)
            ?? throw new KeyNotFoundException($"ActionItem {actionItemId} not found.");

        if (newStartDate.HasValue)
            item.StartDate = newStartDate.Value;
        if (newDueDate.HasValue)
            item.DueDate = newDueDate.Value;

        item.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(default);

        _logger.LogInformation(
            "Approved date change applied to ActionItem {Id}: StartDate={StartDate}, DueDate={DueDate}",
            actionItemId, newStartDate, newDueDate);
    }

    public async Task ApplyApprovedStatusChangeAsync(Guid actionItemId, ActionStatus newStatus)
    {
        var item = await _dbContext.ActionItems
            .FirstOrDefaultAsync(a => a.Id == actionItemId)
            ?? throw new KeyNotFoundException($"ActionItem {actionItemId} not found.");

        var previousStatus = item.Status;
        item.Status = newStatus;

        if (newStatus == ActionStatus.Done)
            item.Progress = 100;

        // Actual dates (WO-2026-11 A-1): same rule as every other status path.
        if (newStatus != previousStatus)
        {
            var today = await ActionItemActualDates.TodayForWorkspaceAsync(_calendars, item.WorkspaceId);
            ActionItemActualDates.ApplyStatusChange(item, previousStatus, newStatus, today);
        }

        item.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(default);

        _logger.LogInformation(
            "Approved status change applied to ActionItem {Id}: Status={Status}",
            actionItemId, newStatus);
    }

    // -------------------------------------------------------------------------
    // My Stats
    // -------------------------------------------------------------------------

    public async Task<ActionItemMyStatsDto> GetMyStatsAsync(string userId, CancellationToken ct)
    {
        var baseQuery = _dbContext.ActionItems
            .Where(a => !a.IsDeleted && a.Assignees.Any(aa => aa.UserId == userId));

        var total      = await baseQuery.CountAsync(ct);
        var critical   = await baseQuery.CountAsync(a => a.Priority == ActionPriority.Critical, ct);
        var inProgress = await baseQuery.CountAsync(a => a.Status == ActionStatus.InProgress, ct);
        var completed  = await baseQuery.CountAsync(a => a.Status == ActionStatus.Done, ct);
        var overdue    = await baseQuery.CountAsync(a => a.Status == ActionStatus.Overdue, ct);

        decimal completionRate = total > 0
            ? Math.Round((decimal)completed / total * 100, 1) : 0;

        var completedOnTime = await baseQuery
            .CountAsync(a => a.Status == ActionStatus.Done && a.UpdatedAt <= a.DueDate, ct);
        decimal onTimeRate = completed > 0
            ? Math.Round((decimal)completedOnTime / completed * 100, 1) : 0;

        return new ActionItemMyStatsDto
        {
            TotalCount          = total,
            CriticalCount       = critical,
            InProgressCount     = inProgress,
            CompletedCount      = completed,
            OverdueCount        = overdue,
            CompletionRate      = completionRate,
            OnTimeCompletionRate = onTimeRate,
        };
    }

    // -------------------------------------------------------------------------
    // Comments
    // -------------------------------------------------------------------------

    public async Task<List<ActionItemCommentResponseDto>> GetCommentsAsync(Guid actionItemId, CancellationToken ct)
    {
        return await _dbContext.ActionItemComments
            .Where(c => c.ActionItemId == actionItemId)
            .Include(c => c.Author)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new ActionItemCommentResponseDto
            {
                Id               = c.Id,
                ActionItemId     = c.ActionItemId,
                Content          = c.Content,
                AuthorUserId     = c.AuthorUserId,
                AuthorName       = c.Author.FirstName + " " + c.Author.LastName,
                IsHighImportance = c.IsHighImportance,
                CreatedAt        = c.CreatedAt,
                UpdatedAt        = c.UpdatedAt,
            })
            .ToListAsync(ct);
    }

    public async Task<ActionItemCommentResponseDto> AddCommentAsync(
        Guid actionItemId, CreateCommentDto dto, string userId, CancellationToken ct)
    {
        var exists = await _dbContext.ActionItems.AnyAsync(a => a.Id == actionItemId, ct);
        if (!exists) throw new KeyNotFoundException($"ActionItem {actionItemId} not found.");

        var comment = new ActionItemComment
        {
            Id               = Guid.NewGuid(),
            ActionItemId     = actionItemId,
            Content          = dto.Content.Trim(),
            AuthorUserId     = userId,
            IsHighImportance = dto.IsHighImportance,
            CreatedAt        = DateTime.UtcNow,
        };

        _dbContext.ActionItemComments.Add(comment);
        await _dbContext.SaveChangesAsync(ct);

        // Fetch with author name
        var saved = await _dbContext.ActionItemComments
            .Include(c => c.Author)
            .FirstAsync(c => c.Id == comment.Id, ct);

        _logger.LogInformation("Comment {CommentId} added to ActionItem {ActionItemId}", comment.Id, actionItemId);

        return new ActionItemCommentResponseDto
        {
            Id               = saved.Id,
            ActionItemId     = saved.ActionItemId,
            Content          = saved.Content,
            AuthorUserId     = saved.AuthorUserId,
            AuthorName       = saved.Author?.FullName ?? string.Empty,
            IsHighImportance = saved.IsHighImportance,
            CreatedAt        = saved.CreatedAt,
            UpdatedAt        = saved.UpdatedAt,
        };
    }

    public async Task<ActionItemCommentResponseDto> UpdateCommentAsync(
        Guid actionItemId, Guid commentId, UpdateCommentDto dto, string userId, CancellationToken ct)
    {
        var comment = await _dbContext.ActionItemComments
            .FirstOrDefaultAsync(c => c.Id == commentId && c.ActionItemId == actionItemId, ct)
            ?? throw new KeyNotFoundException($"Comment {commentId} not found.");

        if (comment.AuthorUserId != userId)
            throw new UnauthorizedAccessException("You can only edit your own comments.");

        comment.Content          = dto.Content.Trim();
        comment.IsHighImportance = dto.IsHighImportance;
        comment.UpdatedAt        = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        var saved = await _dbContext.ActionItemComments
            .Include(c => c.Author)
            .FirstAsync(c => c.Id == commentId, ct);

        return new ActionItemCommentResponseDto
        {
            Id               = saved.Id,
            ActionItemId     = saved.ActionItemId,
            Content          = saved.Content,
            AuthorUserId     = saved.AuthorUserId,
            AuthorName       = saved.Author?.FullName ?? string.Empty,
            IsHighImportance = saved.IsHighImportance,
            CreatedAt        = saved.CreatedAt,
            UpdatedAt        = saved.UpdatedAt,
        };
    }

    public async Task DeleteCommentAsync(
        Guid actionItemId, Guid commentId, string userId, CancellationToken ct)
    {
        var comment = await _dbContext.ActionItemComments
            .FirstOrDefaultAsync(c => c.Id == commentId && c.ActionItemId == actionItemId, ct)
            ?? throw new KeyNotFoundException($"Comment {commentId} not found.");

        if (comment.AuthorUserId != userId)
            throw new UnauthorizedAccessException("You can only delete your own comments.");

        _dbContext.ActionItemComments.Remove(comment);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Comment {CommentId} deleted from ActionItem {ActionItemId}", commentId, actionItemId);
    }

    // -------------------------------------------------------------------------
    // Status approval
    // -------------------------------------------------------------------------

    /// <summary>
    /// True when moving a standalone item from <paramref name="current"/> to
    /// <paramref name="target"/> needs an approved status-change request:
    /// a terminal target (Done / Deferred / Cancelled) from an open status.
    /// Used by both PUT and PATCH /status so the two cannot drift.
    /// </summary>
    private static bool RequiresStatusApproval(ActionStatus current, ActionStatus target) =>
        target != current
        && target is ActionStatus.Done or ActionStatus.Deferred or ActionStatus.Cancelled
        && current is ActionStatus.ToDo or ActionStatus.InProgress or ActionStatus.InReview or ActionStatus.Overdue;

    // -------------------------------------------------------------------------
    // Email helpers
    // -------------------------------------------------------------------------

    private Dictionary<string, string> BuildActionItemPlaceholders(ActionItem item)
    {
        var assigneeNames = item.Assignees?
            .Where(a => a.User != null)
            .Select(a => $"{a.User.FirstName} {a.User.LastName}".Trim())
            .ToList() ?? [];

        return new Dictionary<string, string>
        {
            ["ActionId"]      = item.ActionId,
            ["Title"]         = item.Title,
            ["Description"]   = item.Description,
            ["Status"]        = item.Status.ToString(),
            ["Priority"]      = item.Priority.ToString(),
            ["DueDate"]       = item.DueDate.ToString("yyyy-MM-dd"),
            ["Progress"]      = item.Progress.ToString(),
            ["AssignedTo"]    = assigneeNames.Count > 0 ? string.Join(", ", assigneeNames) : "Unassigned",
            ["CreatedBy"]     = item.CreatedByUserId ?? "System",
            ["WorkspaceName"] = item.Workspace?.Title ?? string.Empty,
            ["ProjectName"]   = item.Project?.Name ?? string.Empty,
            ["ItemUrl"]       = $"{_appSettings.FrontendBaseUrl}/actions/{item.Id}/view",
        };
    }

    private async Task<List<string>> GetActionItemRecipientEmailsAsync(ActionItem item)
    {
        var userIds = new HashSet<string>();
        if (!string.IsNullOrWhiteSpace(item.CreatedByUserId))
            userIds.Add(item.CreatedByUserId);
        foreach (var a in item.Assignees)
            userIds.Add(a.UserId);

        return await GetActiveUserEmailsAsync(userIds);
    }

    private async Task<string?> GetUserEmailAsync(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;
        return await _dbContext.Users
            .Where(u => u.Id == userId && u.IsActive)
            .Select(u => u.Email)
            .FirstOrDefaultAsync();
    }

    private async Task<List<string>> GetActiveUserEmailsAsync(IEnumerable<string> userIds)
    {
        var ids = userIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (ids.Count == 0) return [];

        return await _dbContext.Users
            .Where(u => ids.Contains(u.Id) && u.IsActive && u.Email != null)
            .Select(u => u.Email!)
            .Distinct()
            .ToListAsync();
    }

    private static List<string> GetActionItemRecipientUserIds(ActionItem item, string? excludeUserId)
    {
        var userIds = new HashSet<string>();
        if (!string.IsNullOrWhiteSpace(item.CreatedByUserId))
            userIds.Add(item.CreatedByUserId);
        foreach (var a in item.Assignees)
            userIds.Add(a.UserId);
        if (!string.IsNullOrWhiteSpace(excludeUserId))
            userIds.Remove(excludeUserId);
        return userIds.ToList();
    }

    private async Task<string?> GetUserDisplayNameAsync(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;
        return await _dbContext.Users
            .Where(u => u.Id == userId)
            .Select(u => u.FirstName + " " + u.LastName)
            .FirstOrDefaultAsync();
    }

    // ── Scoped variants for use inside Task.Run (fresh DbContext) ────────────

    private static async Task<string?> GetUserEmailFromDbAsync(IAppDbContext db, string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;
        return await db.Users
            .Where(u => u.Id == userId && u.IsActive)
            .Select(u => u.Email)
            .FirstOrDefaultAsync();
    }

    private static async Task<List<string>> GetActiveUserEmailsFromDbAsync(IAppDbContext db, IEnumerable<string> userIds)
    {
        var ids = userIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (ids.Count == 0) return [];
        return await db.Users
            .Where(u => ids.Contains(u.Id) && u.IsActive && u.Email != null)
            .Select(u => u.Email!)
            .Distinct()
            .ToListAsync();
    }

    private static async Task<string?> GetUserDisplayNameFromDbAsync(IAppDbContext db, string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;
        return await db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.FirstName + " " + u.LastName)
            .FirstOrDefaultAsync();
    }

    private static async Task<List<string>> GetActionItemRecipientEmailsFromDbAsync(IAppDbContext db, ActionItem item)
    {
        var userIds = new HashSet<string>();
        if (!string.IsNullOrWhiteSpace(item.CreatedByUserId))
            userIds.Add(item.CreatedByUserId);
        foreach (var a in item.Assignees)
            userIds.Add(a.UserId);
        return await GetActiveUserEmailsFromDbAsync(db, userIds);
    }
}
