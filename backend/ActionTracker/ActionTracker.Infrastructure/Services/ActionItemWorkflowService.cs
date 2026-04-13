using ActionTracker.Application.Common;
using ActionTracker.Application.Common.Extensions;
using ActionTracker.Application.Common.Interfaces;
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

namespace ActionTracker.Infrastructure.Services;

public class ActionItemWorkflowService : IActionItemWorkflowService
{
    private readonly IAppDbContext _db;
    private readonly INotificationService _notificationService;
    private readonly IEmailSender _emailSender;
    private readonly AppSettings _appSettings;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ActionItemWorkflowService> _logger;

    // Statuses that block new workflow requests
    private static readonly HashSet<ActionStatus> TerminalStatuses = new()
    {
        ActionStatus.Done,
        ActionStatus.Deferred,
        ActionStatus.Cancelled
    };

    // Valid source statuses for status change requests
    private static readonly HashSet<ActionStatus> AllowedSourceStatuses = new()
    {
        ActionStatus.ToDo,
        ActionStatus.InProgress,
        ActionStatus.InReview,
        ActionStatus.Overdue
    };

    // Valid target statuses for status change requests
    private static readonly HashSet<ActionStatus> AllowedTargetStatuses = new()
    {
        ActionStatus.Done,
        ActionStatus.InReview,
        ActionStatus.Deferred,
        ActionStatus.Cancelled
    };

    public ActionItemWorkflowService(
        IAppDbContext db,
        INotificationService notificationService,
        IEmailSender emailSender,
        IOptions<AppSettings> appSettings,
        IServiceScopeFactory scopeFactory,
        ILogger<ActionItemWorkflowService> logger)
    {
        _db                  = db;
        _notificationService = notificationService;
        _emailSender         = emailSender;
        _appSettings         = appSettings.Value;
        _scopeFactory        = scopeFactory;
        _logger              = logger;
    }

    // -------------------------------------------------------------------------
    // Create requests
    // -------------------------------------------------------------------------

    public async Task<WorkflowRequestResponseDto> CreateDateChangeRequestAsync(
        CreateDateChangeRequestDto dto, string requestedByUserId)
    {
        var actionItem = await _db.ActionItems
            .IgnoreQueryFilters()
            .Include(a => a.Assignees)
            .FirstOrDefaultAsync(a => a.Id == dto.ActionItemId)
            ?? throw new KeyNotFoundException($"ActionItem {dto.ActionItemId} not found.");

        if (TerminalStatuses.Contains(actionItem.Status))
            throw new InvalidOperationException(
                $"Cannot request date change for an action item with status '{actionItem.Status}'.");

        var hasPending = await _db.ActionItemWorkflowRequests
            .AnyAsync(r => r.ActionItemId == dto.ActionItemId
                        && r.RequestType == WorkflowRequestType.DateChangeRequest
                        && r.Status == WorkflowRequestStatus.Pending);
        if (hasPending)
            throw new InvalidOperationException("A pending date change request already exists for this action item.");

        var requesterName = await GetUserDisplayNameAsync(requestedByUserId) ?? "Unknown User";

        var request = new ActionItemWorkflowRequest
        {
            Id                      = Guid.NewGuid(),
            ActionItemId            = actionItem.Id,
            RequestType             = WorkflowRequestType.DateChangeRequest,
            Status                  = WorkflowRequestStatus.Pending,
            RequestedByUserId       = requestedByUserId,
            RequestedByDisplayName  = requesterName,
            RequestedNewStartDate   = dto.NewStartDate,
            RequestedNewDueDate     = dto.NewDueDate,
            CurrentStartDate        = actionItem.StartDate,
            CurrentDueDate          = actionItem.DueDate,
            CurrentStatus           = actionItem.Status,
            Reason                  = dto.Reason,
            CreatedAt               = DateTime.UtcNow,
        };

        _db.ActionItemWorkflowRequests.Add(request);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Date change request {RequestId} created for ActionItem {ActionItemId} by {UserId}",
            request.Id, actionItem.Id, requestedByUserId);

        // Fire-and-forget: notify creator + managers
        var capturedRequest = request;
        var capturedItem = actionItem;
        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var emailSender  = scope.ServiceProvider.GetRequiredService<IEmailSender>();
            var notifService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var db           = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

            try
            {
                await SendWorkflowRequestNotificationsAsync(
                    db, emailSender, notifService,
                    capturedItem, capturedRequest,
                    "Workflow.DateChangeRequested",
                    "Date Change Requested",
                    $"{capturedItem.ActionId}: A date change has been requested by {capturedRequest.RequestedByDisplayName}",
                    "DateChangeRequested",
                    requestedByUserId,
                    capturedRequest.RequestedByDisplayName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error sending notifications for date change request {RequestId}", capturedRequest.Id);
            }
        });

        // Attach ActionItem navigation for mapping
        request.ActionItem = actionItem;
        return MapToDto(request);
    }

    public async Task<WorkflowRequestResponseDto> CreateStatusChangeRequestAsync(
        CreateStatusChangeRequestDto dto, string requestedByUserId)
    {
        var actionItem = await _db.ActionItems
            .IgnoreQueryFilters()
            .Include(a => a.Assignees)
            .FirstOrDefaultAsync(a => a.Id == dto.ActionItemId)
            ?? throw new KeyNotFoundException($"ActionItem {dto.ActionItemId} not found.");

        if (!AllowedSourceStatuses.Contains(actionItem.Status))
            throw new InvalidOperationException(
                $"Cannot request status change from '{actionItem.Status}'. " +
                $"Only items with status ToDo, InProgress, InReview, or Overdue can be changed.");

        if (!AllowedTargetStatuses.Contains(dto.NewStatus))
            throw new InvalidOperationException(
                $"Cannot request status change to '{dto.NewStatus}'. " +
                $"Allowed target statuses: Done, InReview, Deferred, Cancelled.");

        var hasPending = await _db.ActionItemWorkflowRequests
            .AnyAsync(r => r.ActionItemId == dto.ActionItemId
                        && r.RequestType == WorkflowRequestType.StatusChangeRequest
                        && r.Status == WorkflowRequestStatus.Pending);
        if (hasPending)
            throw new InvalidOperationException("A pending status change request already exists for this action item.");

        var requesterName = await GetUserDisplayNameAsync(requestedByUserId) ?? "Unknown User";

        var request = new ActionItemWorkflowRequest
        {
            Id                      = Guid.NewGuid(),
            ActionItemId            = actionItem.Id,
            RequestType             = WorkflowRequestType.StatusChangeRequest,
            Status                  = WorkflowRequestStatus.Pending,
            RequestedByUserId       = requestedByUserId,
            RequestedByDisplayName  = requesterName,
            RequestedNewStatus      = dto.NewStatus,
            CurrentStartDate        = actionItem.StartDate,
            CurrentDueDate          = actionItem.DueDate,
            CurrentStatus           = actionItem.Status,
            Reason                  = dto.Reason,
            CreatedAt               = DateTime.UtcNow,
        };

        _db.ActionItemWorkflowRequests.Add(request);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Status change request {RequestId} created for ActionItem {ActionItemId} by {UserId}",
            request.Id, actionItem.Id, requestedByUserId);

        // Fire-and-forget: notify creator + managers
        var capturedRequest = request;
        var capturedItem = actionItem;
        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var emailSender  = scope.ServiceProvider.GetRequiredService<IEmailSender>();
            var notifService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var db           = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

            try
            {
                await SendWorkflowRequestNotificationsAsync(
                    db, emailSender, notifService,
                    capturedItem, capturedRequest,
                    "Workflow.StatusChangeRequested",
                    "Status Change Requested",
                    $"{capturedItem.ActionId}: A status change to {dto.NewStatus} has been requested by {capturedRequest.RequestedByDisplayName}",
                    "StatusChangeRequested",
                    requestedByUserId,
                    capturedRequest.RequestedByDisplayName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error sending notifications for status change request {RequestId}", capturedRequest.Id);
            }
        });

        request.ActionItem = actionItem;
        return MapToDto(request);
    }

    // -------------------------------------------------------------------------
    // Review
    // -------------------------------------------------------------------------

    public async Task<WorkflowRequestResponseDto> ReviewRequestAsync(
        Guid requestId, ReviewWorkflowRequestDto dto, string reviewerUserId)
    {
        var request = await _db.ActionItemWorkflowRequests
            .Include(r => r.ActionItem)
                .ThenInclude(a => a.Assignees)
            .FirstOrDefaultAsync(r => r.Id == requestId)
            ?? throw new KeyNotFoundException($"Workflow request {requestId} not found.");

        if (request.Status != WorkflowRequestStatus.Pending)
            throw new InvalidOperationException("This request has already been reviewed.");

        var canReview = await CanUserReviewAsync(request.ActionItemId, reviewerUserId);
        if (!canReview)
            throw new UnauthorizedAccessException("You are not authorized to review this request.");

        var reviewerName = await GetUserDisplayNameAsync(reviewerUserId) ?? "Unknown User";

        request.Status                 = dto.IsApproved ? WorkflowRequestStatus.Approved : WorkflowRequestStatus.Rejected;
        request.ReviewedByUserId       = reviewerUserId;
        request.ReviewedByDisplayName  = reviewerName;
        request.ReviewComment          = dto.ReviewComment;
        request.ReviewedAt             = DateTime.UtcNow;

        // Apply changes if approved
        if (dto.IsApproved)
        {
            var actionItem = request.ActionItem;

            if (request.RequestType == WorkflowRequestType.DateChangeRequest)
            {
                if (request.RequestedNewStartDate.HasValue)
                    actionItem.StartDate = request.RequestedNewStartDate.Value;
                if (request.RequestedNewDueDate.HasValue)
                    actionItem.DueDate = request.RequestedNewDueDate.Value;

                actionItem.UpdatedAt = DateTime.UtcNow;
            }
            else if (request.RequestType == WorkflowRequestType.StatusChangeRequest
                     && request.RequestedNewStatus.HasValue)
            {
                actionItem.Status = request.RequestedNewStatus.Value;

                if (request.RequestedNewStatus.Value == ActionStatus.Done)
                    actionItem.Progress = 100;

                actionItem.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Workflow request {RequestId} {Status} by {ReviewerId}",
            requestId, request.Status, reviewerUserId);

        // Fire-and-forget: notify the requester
        var capturedRequest = request;
        var capturedActionItem = request.ActionItem;
        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var emailSender  = scope.ServiceProvider.GetRequiredService<IEmailSender>();
            var notifService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var db           = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

            try
            {
                var statusLabel = capturedRequest.Status == WorkflowRequestStatus.Approved ? "Approved" : "Rejected";
                var typeLabel = capturedRequest.RequestType == WorkflowRequestType.DateChangeRequest
                    ? "Date Change" : "Status Change";

                var templateKey = capturedRequest.RequestType == WorkflowRequestType.DateChangeRequest
                    ? "Workflow.DateChangeReviewed"
                    : "Workflow.StatusChangeReviewed";

                var requesterEmail = await GetUserEmailFromDbAsync(db, capturedRequest.RequestedByUserId);
                if (requesterEmail is not null)
                {
                    var placeholders = BuildWorkflowPlaceholders(capturedActionItem, capturedRequest);
                    await emailSender.SendEmailAsync(
                        templateKey, placeholders,
                        new List<string> { requesterEmail },
                        "ActionItem", capturedActionItem.Id,
                        reviewerUserId);
                }

                // In-app notification to requester
                var url = $"{_appSettings.FrontendBaseUrl}/actions/{capturedActionItem.Id}/view";
                var notification = new CreateNotificationDto
                {
                    UserId               = capturedRequest.RequestedByUserId,
                    Title                = $"{typeLabel} Request {statusLabel}",
                    Message              = $"Your {typeLabel.ToLower()} request for {capturedActionItem.ActionId} has been {statusLabel.ToLower()} by {reviewerName}",
                    Type                 = "Workflow",
                    ActionType           = $"{capturedRequest.RequestType}{statusLabel}",
                    RelatedEntityType    = "ActionItem",
                    RelatedEntityId      = capturedActionItem.Id,
                    RelatedEntityCode    = capturedActionItem.ActionId,
                    Url                  = url,
                    CreatedByUserId      = reviewerUserId,
                    CreatedByDisplayName = reviewerName,
                };
                await notifService.CreateAsync(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error sending review notifications for workflow request {RequestId}", capturedRequest.Id);
            }
        });

        return MapToDto(request);
    }

    // -------------------------------------------------------------------------
    // Queries
    // -------------------------------------------------------------------------

    public async Task<PagedResult<WorkflowRequestResponseDto>> GetPendingRequestsForReviewerAsync(
        string reviewerUserId, int page, int pageSize)
    {
        // Get action item IDs where the reviewer is the creator
        var createdItemIds = await _db.ActionItems
            .IgnoreQueryFilters()
            .Where(a => a.CreatedByUserId == reviewerUserId && !a.IsDeleted)
            .Select(a => a.Id)
            .ToListAsync();

        // Get action item IDs where the reviewer is a manager of an assignee
        var managedItemIds = await GetManagedActionItemIdsAsync(reviewerUserId);

        var allReviewableIds = createdItemIds
            .Union(managedItemIds)
            .Distinct()
            .ToHashSet();

        var query = _db.ActionItemWorkflowRequests
            .Include(r => r.ActionItem)
            .Where(r => r.Status == WorkflowRequestStatus.Pending
                      && allReviewableIds.Contains(r.ActionItemId))
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => MapToDto(r));

        return await PagedResult<WorkflowRequestResponseDto>.CreateAsync(query, page, pageSize);
    }

    public async Task<PagedResult<WorkflowRequestResponseDto>> GetMyRequestsAsync(
        string userId, int page, int pageSize)
    {
        var query = _db.ActionItemWorkflowRequests
            .Include(r => r.ActionItem)
            .Where(r => r.RequestedByUserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => MapToDto(r));

        return await PagedResult<WorkflowRequestResponseDto>.CreateAsync(query, page, pageSize);
    }

    public async Task<List<WorkflowRequestResponseDto>> GetRequestsForActionItemAsync(Guid actionItemId)
    {
        return await _db.ActionItemWorkflowRequests
            .Include(r => r.ActionItem)
            .Where(r => r.ActionItemId == actionItemId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => MapToDto(r))
            .ToListAsync();
    }

    public async Task<WorkflowRequestSummaryDto> GetPendingSummaryAsync(string userId)
    {
        // Get all action item IDs this user can review
        var createdItemIds = await _db.ActionItems
            .IgnoreQueryFilters()
            .Where(a => a.CreatedByUserId == userId && !a.IsDeleted)
            .Select(a => a.Id)
            .ToListAsync();

        var managedItemIds = await GetManagedActionItemIdsAsync(userId);

        var allReviewableIds = createdItemIds
            .Union(managedItemIds)
            .Distinct()
            .ToHashSet();

        var pendingRequests = await _db.ActionItemWorkflowRequests
            .Where(r => r.Status == WorkflowRequestStatus.Pending
                      && allReviewableIds.Contains(r.ActionItemId))
            .GroupBy(r => r.RequestType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync();

        var dateChanges = pendingRequests
            .FirstOrDefault(x => x.Type == WorkflowRequestType.DateChangeRequest)?.Count ?? 0;
        var statusChanges = pendingRequests
            .FirstOrDefault(x => x.Type == WorkflowRequestType.StatusChangeRequest)?.Count ?? 0;

        return new WorkflowRequestSummaryDto
        {
            PendingDateChanges   = dateChanges,
            PendingStatusChanges = statusChanges,
            TotalPending         = dateChanges + statusChanges,
        };
    }

    // -------------------------------------------------------------------------
    // Escalation & Direction
    // -------------------------------------------------------------------------

    public async Task HandleEscalationAsync(Guid actionItemId, string escalatedByUserId, string reason)
    {
        var actionItem = await _db.ActionItems
            .Include(a => a.Assignees).ThenInclude(a => a.User)
            .Include(a => a.Workspace)
            .Include(a => a.Project)
            .FirstOrDefaultAsync(a => a.Id == actionItemId)
            ?? throw new KeyNotFoundException($"ActionItem {actionItemId} not found.");

        actionItem.IsEscalated = true;
        actionItem.UpdatedAt = DateTime.UtcNow;

        _db.ActionItemEscalations.Add(new ActionItemEscalation
        {
            Id                = Guid.NewGuid(),
            ActionItemId      = actionItemId,
            Explanation       = reason.Trim(),
            EscalatedByUserId = escalatedByUserId,
            CreatedAt         = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "ActionItem {ActionItemId} escalated by {UserId}", actionItemId, escalatedByUserId);

        // Fire-and-forget: notify managers
        var capturedItem = actionItem;
        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var emailSender  = scope.ServiceProvider.GetRequiredService<IEmailSender>();
            var notifService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var db           = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

            try
            {
                var escalatorName = await GetUserDisplayNameFromDbAsync(db, escalatedByUserId) ?? "Unknown User";

                // Notify assignees + creator + managers
                var managers = await GetDirectManagersFromDbAsync(db, capturedItem);
                var recipientEmails = new List<string>();

                // Add assignee emails
                foreach (var assignee in capturedItem.Assignees)
                {
                    var assigneeEmail = await GetUserEmailFromDbAsync(db, assignee.UserId);
                    if (assigneeEmail is not null) recipientEmails.Add(assigneeEmail);
                }

                // Add creator email
                var creatorEmail = await GetUserEmailFromDbAsync(db, capturedItem.CreatedByUserId);
                if (creatorEmail is not null) recipientEmails.Add(creatorEmail);

                // Add manager emails
                recipientEmails.AddRange(managers.Select(m => m.Email));
                recipientEmails = recipientEmails.Distinct().ToList();

                if (recipientEmails.Count > 0)
                {
                    var assigneeNames = capturedItem.Assignees.Any()
                        ? string.Join(", ", capturedItem.Assignees.Select(a => a.User?.FullName ?? a.UserId))
                        : "—";
                    var creatorName = await GetUserDisplayNameFromDbAsync(db, capturedItem.CreatedByUserId) ?? "—";

                    var placeholders = new Dictionary<string, string>
                    {
                        ["ActionId"]      = capturedItem.ActionId,
                        ["Title"]         = capturedItem.Title,
                        ["Description"]   = capturedItem.Description ?? "—",
                        ["Status"]        = capturedItem.Status.GetDescription(),
                        ["Priority"]      = capturedItem.Priority.GetDescription(),
                        ["DueDate"]       = capturedItem.DueDate.ToString("MMM d, yyyy"),
                        ["Progress"]      = capturedItem.Progress.ToString(),
                        ["AssignedTo"]    = assigneeNames,
                        ["CreatedBy"]     = creatorName,
                        ["WorkspaceName"] = capturedItem.Workspace?.Title ?? "—",
                        ["ProjectName"]   = capturedItem.Project?.Name ?? "—",
                        ["Reason"]        = reason,
                        ["EscalatedBy"]   = escalatorName,
                        ["ItemUrl"]       = $"{_appSettings.FrontendBaseUrl}/actions/{capturedItem.Id}/view",
                    };

                    await emailSender.SendEmailAsync(
                        "ActionItem.Escalated", placeholders,
                        recipientEmails, "ActionItem", capturedItem.Id, escalatedByUserId);
                }

                // In-app notifications
                var recipientUserIds = new HashSet<string>();
                // Add assignee user IDs
                foreach (var assignee in capturedItem.Assignees)
                    recipientUserIds.Add(assignee.UserId);
                // Add creator
                if (!string.IsNullOrWhiteSpace(capturedItem.CreatedByUserId))
                    recipientUserIds.Add(capturedItem.CreatedByUserId);
                // Add managers
                foreach (var m in managers.Where(m => m.UserId is not null))
                    recipientUserIds.Add(m.UserId!);
                recipientUserIds.Remove(escalatedByUserId);

                var url = $"{_appSettings.FrontendBaseUrl}/actions/{capturedItem.Id}/view";
                var notifications = recipientUserIds.Select(uid => new CreateNotificationDto
                {
                    UserId               = uid,
                    Title                = "Action Item Escalated",
                    Message              = $"{capturedItem.ActionId} has been escalated: {reason}",
                    Type                 = "Workflow",
                    ActionType           = "Escalated",
                    RelatedEntityType    = "ActionItem",
                    RelatedEntityId      = capturedItem.Id,
                    RelatedEntityCode    = capturedItem.ActionId,
                    Url                  = url,
                    CreatedByUserId      = escalatedByUserId,
                    CreatedByDisplayName = escalatorName,
                }).ToList();

                if (notifications.Count > 0)
                    await notifService.CreateBulkAsync(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error sending escalation notifications for ActionItem {ActionItemId}", capturedItem.Id);
            }
        });
    }

    public async Task GiveDirectionAsync(WorkflowDirectionDto dto, string directorUserId)
    {
        var actionItem = await _db.ActionItems
            .Include(a => a.Assignees)
            .FirstOrDefaultAsync(a => a.Id == dto.ActionItemId)
            ?? throw new KeyNotFoundException($"ActionItem {dto.ActionItemId} not found.");

        _logger.LogInformation(
            "Direction given for ActionItem {ActionItemId} by {UserId}: {Direction}",
            dto.ActionItemId, directorUserId, dto.DirectionText);

        // Add direction as a comment
        _db.ActionItemComments.Add(new ActionItemComment
        {
            Id               = Guid.NewGuid(),
            ActionItemId     = actionItem.Id,
            Content          = $"[Direction] {dto.DirectionText.Trim()}",
            AuthorUserId     = directorUserId,
            IsHighImportance = true,
            CreatedAt        = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();

        // Fire-and-forget: notify assignees
        var capturedItem = actionItem;
        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var emailSender  = scope.ServiceProvider.GetRequiredService<IEmailSender>();
            var notifService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var db           = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

            try
            {
                var directorName = await GetUserDisplayNameFromDbAsync(db, directorUserId) ?? "Unknown User";

                // Notify all assignees + creator
                var recipientUserIds = new HashSet<string>();
                if (!string.IsNullOrWhiteSpace(capturedItem.CreatedByUserId))
                    recipientUserIds.Add(capturedItem.CreatedByUserId);
                foreach (var a in capturedItem.Assignees)
                    recipientUserIds.Add(a.UserId);
                recipientUserIds.Remove(directorUserId);

                var recipientEmails = await GetActiveUserEmailsFromDbAsync(db, recipientUserIds);
                if (recipientEmails.Count > 0)
                {
                    var placeholders = new Dictionary<string, string>
                    {
                        ["ActionId"]      = capturedItem.ActionId,
                        ["Title"]         = capturedItem.Title,
                        ["DirectionText"] = dto.DirectionText,
                        ["DirectedBy"]    = directorName,
                        ["ItemUrl"]       = $"{_appSettings.FrontendBaseUrl}/actions/{capturedItem.Id}/view",
                    };

                    await emailSender.SendEmailAsync(
                        "Workflow.DirectionGiven", placeholders,
                        recipientEmails, "ActionItem", capturedItem.Id, directorUserId);
                }

                // In-app notifications
                var url = $"{_appSettings.FrontendBaseUrl}/actions/{capturedItem.Id}/view";
                var notifications = recipientUserIds.Select(uid => new CreateNotificationDto
                {
                    UserId               = uid,
                    Title                = "Direction Given",
                    Message              = $"New direction for {capturedItem.ActionId}: {dto.DirectionText}",
                    Type                 = "Workflow",
                    ActionType           = "DirectionGiven",
                    RelatedEntityType    = "ActionItem",
                    RelatedEntityId      = capturedItem.Id,
                    RelatedEntityCode    = capturedItem.ActionId,
                    Url                  = url,
                    CreatedByUserId      = directorUserId,
                    CreatedByDisplayName = directorName,
                }).ToList();

                if (notifications.Count > 0)
                    await notifService.CreateBulkAsync(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error sending direction notifications for ActionItem {ActionItemId}", capturedItem.Id);
            }
        });
    }

    // -------------------------------------------------------------------------
    // Authorization helpers
    // -------------------------------------------------------------------------

    public async Task<bool> CanUserReviewAsync(Guid actionItemId, string userId)
    {
        var actionItem = await _db.ActionItems
            .IgnoreQueryFilters()
            .Include(a => a.Assignees)
            .FirstOrDefaultAsync(a => a.Id == actionItemId);

        if (actionItem is null) return false;

        // Creator can review
        if (actionItem.CreatedByUserId == userId)
            return true;

        // Check if the user is a direct manager of any assignee
        var userEmail = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(userEmail)) return false;

        foreach (var assignee in actionItem.Assignees)
        {
            var assigneeEmail = await _db.Users
                .Where(u => u.Id == assignee.UserId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(assigneeEmail)) continue;

            // Look up the assignee's record in KuEmployeeInfo to find their supervisor
            var empInfo = await _db.KuEmployeeInfo
                .FirstOrDefaultAsync(e => e.EmailAddress != null
                    && e.EmailAddress.ToLower() == assigneeEmail.ToLower());

            if (empInfo is null) continue;

            // Check supervisor name/number — we need the supervisor's email
            // The supervisor is identified by SupervisorNumber; look up supervisor email
            if (!string.IsNullOrWhiteSpace(empInfo.SupervisorNumber))
            {
                var supervisorEmail = await _db.KuEmployeeInfo
                    .Where(e => e.EmpNo == empInfo.SupervisorNumber)
                    .Select(e => e.EmailAddress)
                    .FirstOrDefaultAsync();

                if (!string.IsNullOrWhiteSpace(supervisorEmail)
                    && supervisorEmail.Equals(userEmail, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    public async Task<bool> HasPendingRequestsAsync(Guid actionItemId)
    {
        return await _db.ActionItemWorkflowRequests
            .AnyAsync(r => r.ActionItemId == actionItemId
                        && r.Status == WorkflowRequestStatus.Pending);
    }

    // -------------------------------------------------------------------------
    // Manager lookup helpers
    // -------------------------------------------------------------------------

    private async Task<List<(string? UserId, string Email, string Name)>> GetDirectManagersAsync(
        ActionItem actionItem)
    {
        var managers = new List<(string? UserId, string Email, string Name)>();
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var assignee in actionItem.Assignees)
        {
            var assigneeEmail = await _db.Users
                .Where(u => u.Id == assignee.UserId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(assigneeEmail)) continue;

            var empInfo = await _db.KuEmployeeInfo
                .FirstOrDefaultAsync(e => e.EmailAddress != null
                    && e.EmailAddress.ToLower() == assigneeEmail.ToLower());

            if (empInfo is null || string.IsNullOrWhiteSpace(empInfo.SupervisorNumber)) continue;

            // Look up supervisor's email from their employee record
            var supervisor = await _db.KuEmployeeInfo
                .FirstOrDefaultAsync(e => e.EmpNo == empInfo.SupervisorNumber);

            if (supervisor is null || string.IsNullOrWhiteSpace(supervisor.EmailAddress)) continue;

            if (!seenEmails.Add(supervisor.EmailAddress)) continue;

            // Try to find this manager in ApplicationUser
            var managerUser = await _db.Users
                .FirstOrDefaultAsync(u => u.Email != null
                    && u.Email.ToLower() == supervisor.EmailAddress.ToLower());

            managers.Add((
                managerUser?.Id,
                supervisor.EmailAddress,
                supervisor.EmployeeName ?? supervisor.SupervisorName ?? "Manager"
            ));
        }

        return managers;
    }

    /// <summary>
    /// Scoped variant of GetDirectManagersAsync for use inside Task.Run (fresh DbContext).
    /// </summary>
    private static async Task<List<(string? UserId, string Email, string Name)>> GetDirectManagersFromDbAsync(
        IAppDbContext db, ActionItem actionItem)
    {
        var managers = new List<(string? UserId, string Email, string Name)>();
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var assignee in actionItem.Assignees)
        {
            var assigneeEmail = await db.Users
                .Where(u => u.Id == assignee.UserId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(assigneeEmail)) continue;

            var empInfo = await db.KuEmployeeInfo
                .FirstOrDefaultAsync(e => e.EmailAddress != null
                    && e.EmailAddress.ToLower() == assigneeEmail.ToLower());

            if (empInfo is null || string.IsNullOrWhiteSpace(empInfo.SupervisorNumber)) continue;

            var supervisor = await db.KuEmployeeInfo
                .FirstOrDefaultAsync(e => e.EmpNo == empInfo.SupervisorNumber);

            if (supervisor is null || string.IsNullOrWhiteSpace(supervisor.EmailAddress)) continue;

            if (!seenEmails.Add(supervisor.EmailAddress)) continue;

            var managerUser = await db.Users
                .FirstOrDefaultAsync(u => u.Email != null
                    && u.Email.ToLower() == supervisor.EmailAddress.ToLower());

            managers.Add((
                managerUser?.Id,
                supervisor.EmailAddress,
                supervisor.EmployeeName ?? supervisor.SupervisorName ?? "Manager"
            ));
        }

        return managers;
    }

    /// <summary>
    /// Gets action item IDs where the given user is a manager of at least one assignee.
    /// </summary>
    private async Task<List<Guid>> GetManagedActionItemIdsAsync(string userId)
    {
        var userEmail = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(userEmail)) return new List<Guid>();

        // Find employee numbers where this user is the supervisor
        var supervisorEmpNo = await _db.KuEmployeeInfo
            .Where(e => e.EmailAddress != null
                && e.EmailAddress.ToLower() == userEmail.ToLower())
            .Select(e => e.EmpNo)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(supervisorEmpNo)) return new List<Guid>();

        // Find all employees supervised by this user
        var subordinateEmails = await _db.KuEmployeeInfo
            .Where(e => e.SupervisorNumber == supervisorEmpNo
                     && e.EmailAddress != null)
            .Select(e => e.EmailAddress!.ToLower())
            .ToListAsync();

        if (subordinateEmails.Count == 0) return new List<Guid>();

        // Find ApplicationUser IDs for those subordinates
        var subordinateUserIds = await _db.Users
            .Where(u => u.Email != null && subordinateEmails.Contains(u.Email.ToLower()))
            .Select(u => u.Id)
            .ToListAsync();

        if (subordinateUserIds.Count == 0) return new List<Guid>();

        // Find action items that have any of those users as assignees
        return await _db.ActionItemAssignees
            .Where(aa => subordinateUserIds.Contains(aa.UserId))
            .Select(aa => aa.ActionItemId)
            .Distinct()
            .ToListAsync();
    }

    // -------------------------------------------------------------------------
    // Mapping helper
    // -------------------------------------------------------------------------

    private static WorkflowRequestResponseDto MapToDto(ActionItemWorkflowRequest request)
    {
        return new WorkflowRequestResponseDto
        {
            Id                      = request.Id,
            ActionItemId            = request.ActionItemId,
            ActionItemCode          = request.ActionItem?.ActionId ?? string.Empty,
            ActionItemTitle         = request.ActionItem?.Title ?? string.Empty,
            RequestType             = request.RequestType.ToString(),
            Status                  = request.Status.ToString(),
            RequestedByUserId       = request.RequestedByUserId,
            RequestedByDisplayName  = request.RequestedByDisplayName,
            RequestedNewStartDate   = request.RequestedNewStartDate,
            RequestedNewDueDate     = request.RequestedNewDueDate,
            RequestedNewStatus      = request.RequestedNewStatus?.ToString(),
            CurrentStartDate        = request.CurrentStartDate,
            CurrentDueDate          = request.CurrentDueDate,
            CurrentStatus           = request.CurrentStatus?.ToString(),
            Reason                  = request.Reason,
            ReviewedByUserId        = request.ReviewedByUserId,
            ReviewedByDisplayName   = request.ReviewedByDisplayName,
            ReviewComment           = request.ReviewComment,
            ReviewedAt              = request.ReviewedAt,
            CreatedAt               = request.CreatedAt,
        };
    }

    // -------------------------------------------------------------------------
    // Notification helpers
    // -------------------------------------------------------------------------

    private async Task SendWorkflowRequestNotificationsAsync(
        IAppDbContext db,
        IEmailSender emailSender,
        INotificationService notifService,
        ActionItem actionItem,
        ActionItemWorkflowRequest request,
        string emailTemplateKey,
        string notificationTitle,
        string notificationMessage,
        string actionType,
        string actorUserId,
        string actorDisplayName)
    {
        // Build email recipient list: creator + direct managers
        var recipientEmails = new List<string>();

        var creatorEmail = await GetUserEmailFromDbAsync(db, actionItem.CreatedByUserId);
        if (creatorEmail is not null) recipientEmails.Add(creatorEmail);

        var managers = await GetDirectManagersFromDbAsync(db, actionItem);
        recipientEmails.AddRange(managers.Select(m => m.Email));
        recipientEmails = recipientEmails.Distinct().ToList();

        if (recipientEmails.Count > 0)
        {
            var placeholders = BuildWorkflowPlaceholders(actionItem, request);
            await emailSender.SendEmailAsync(
                emailTemplateKey, placeholders,
                recipientEmails, "ActionItem", actionItem.Id, actorUserId);
        }

        // In-app notifications: creator + managers (exclude actor)
        var recipientUserIds = new HashSet<string>();
        if (!string.IsNullOrWhiteSpace(actionItem.CreatedByUserId))
            recipientUserIds.Add(actionItem.CreatedByUserId);
        foreach (var m in managers.Where(m => m.UserId is not null))
            recipientUserIds.Add(m.UserId!);
        recipientUserIds.Remove(actorUserId);

        var url = $"{_appSettings.FrontendBaseUrl}/actions/{actionItem.Id}/view";
        var notifications = recipientUserIds.Select(uid => new CreateNotificationDto
        {
            UserId               = uid,
            Title                = notificationTitle,
            Message              = notificationMessage,
            Type                 = "Workflow",
            ActionType           = actionType,
            RelatedEntityType    = "ActionItem",
            RelatedEntityId      = actionItem.Id,
            RelatedEntityCode    = actionItem.ActionId,
            Url                  = url,
            CreatedByUserId      = actorUserId,
            CreatedByDisplayName = actorDisplayName,
        }).ToList();

        if (notifications.Count > 0)
            await notifService.CreateBulkAsync(notifications);
    }

    private static Dictionary<string, string> BuildWorkflowPlaceholders(
        ActionItem actionItem, ActionItemWorkflowRequest request)
    {
        return new Dictionary<string, string>
        {
            ["ActionId"]             = actionItem.ActionId,
            ["Title"]                = actionItem.Title,
            ["RequestType"]          = request.RequestType.ToString(),
            ["Status"]               = request.Status.ToString(),
            ["RequestedBy"]          = request.RequestedByDisplayName,
            ["Reason"]               = request.Reason,
            ["CurrentStartDate"]     = request.CurrentStartDate?.ToString("yyyy-MM-dd") ?? "N/A",
            ["CurrentDueDate"]       = request.CurrentDueDate?.ToString("yyyy-MM-dd") ?? "N/A",
            ["CurrentStatus"]        = request.CurrentStatus?.ToString() ?? "N/A",
            ["RequestedNewStartDate"] = request.RequestedNewStartDate?.ToString("yyyy-MM-dd") ?? "N/A",
            ["RequestedNewDueDate"]  = request.RequestedNewDueDate?.ToString("yyyy-MM-dd") ?? "N/A",
            ["RequestedNewStatus"]   = request.RequestedNewStatus?.ToString() ?? "N/A",
            ["ReviewedBy"]           = request.ReviewedByDisplayName ?? "N/A",
            ["ReviewComment"]        = request.ReviewComment ?? "N/A",
            ["ItemUrl"]              = $"actions/{actionItem.Id}/view",
        };
    }

    // ── Scoped DB helpers for use inside Task.Run ────────────────────────────

    private async Task<string?> GetUserDisplayNameAsync(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;
        return await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.FirstName + " " + u.LastName)
            .FirstOrDefaultAsync();
    }

    private static async Task<string?> GetUserEmailFromDbAsync(IAppDbContext db, string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;
        return await db.Users
            .Where(u => u.Id == userId && u.IsActive)
            .Select(u => u.Email)
            .FirstOrDefaultAsync();
    }

    private static async Task<string?> GetUserDisplayNameFromDbAsync(IAppDbContext db, string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;
        return await db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.FirstName + " " + u.LastName)
            .FirstOrDefaultAsync();
    }

    private static async Task<List<string>> GetActiveUserEmailsFromDbAsync(
        IAppDbContext db, IEnumerable<string> userIds)
    {
        var ids = userIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (ids.Count == 0) return new List<string>();
        return await db.Users
            .Where(u => ids.Contains(u.Id) && u.IsActive && u.Email != null)
            .Select(u => u.Email!)
            .Distinct()
            .ToListAsync();
    }
}
