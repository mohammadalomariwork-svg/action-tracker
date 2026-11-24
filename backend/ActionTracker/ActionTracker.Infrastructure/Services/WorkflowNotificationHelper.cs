using ActionTracker.Application.Common;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.Notifications;
using ActionTracker.Application.Features.Notifications.DTOs;
using ActionTracker.Application.Features.Workflow.Interfaces;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActionTracker.Infrastructure.Services;

public class WorkflowNotificationHelper : IWorkflowNotificationHelper
{
    private readonly IAppDbContext _db;
    private readonly INotificationService _notificationService;
    private readonly IEmailSender _emailSender;
    private readonly AppSettings _appSettings;
    private readonly ILogger<WorkflowNotificationHelper> _logger;

    public WorkflowNotificationHelper(
        IAppDbContext db,
        INotificationService notificationService,
        IEmailSender emailSender,
        IOptions<AppSettings> appSettings,
        ILogger<WorkflowNotificationHelper> logger)
    {
        _db = db;
        _notificationService = notificationService;
        _emailSender = emailSender;
        _appSettings = appSettings.Value;
        _logger = logger;
    }

    // ── Date Change ──────────────────────────────────────────────────────────

    public async Task NotifyDateChangeRequestedAsync(ActionItem actionItem, ActionItemWorkflowRequest request)
    {
        var actionType = "DateChangeRequested";
        var title = "Date Change Requested";
        var message = $"{request.RequestedByDisplayName} requested a date change on {actionItem.ActionId}: {actionItem.Title}";
        var url = "/approvals";

        // Notify managers of assignees
        var managers = await GetManagersOfAssigneesAsync(actionItem);
        var notifiedUserIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var notifiedEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (userId, email, displayName) in managers)
        {
            if (userId != null)
            {
                if (!notifiedUserIds.Add(userId)) continue;

                await CreateInAppNotificationAsync(
                    userId, title, message, actionType, actionItem, request.RequestedByUserId, request.RequestedByDisplayName, url);
            }

            if (!notifiedEmails.Add(email)) continue;

            await SendEmailSafeAsync(
                "WorkflowDateChangeRequested",
                BuildDateChangePlaceholders(actionItem, request),
                email, actionItem, request.RequestedByUserId);
        }
    }

    public async Task NotifyDateChangeReviewedAsync(ActionItem actionItem, ActionItemWorkflowRequest request)
    {
        var isApproved = request.Status == WorkflowRequestStatus.Approved;
        var actionType = isApproved ? "DateChangeApproved" : "DateChangeRejected";
        var title = isApproved ? "Date Change Approved" : "Date Change Rejected";
        var message = isApproved
            ? $"Your date change request for {actionItem.ActionId} has been approved by {request.ReviewedByDisplayName}"
            : $"Your date change request for {actionItem.ActionId} has been rejected by {request.ReviewedByDisplayName}";
        var url = $"/actions/{actionItem.Id}/view";

        var notifiedUserIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var notifiedEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Notify the requester
        await NotifyRecipientAsync(
            request.RequestedByUserId, title, message, actionType, actionItem,
            request.ReviewedByUserId, request.ReviewedByDisplayName, url,
            isApproved ? "WorkflowDateChangeApproved" : "WorkflowDateChangeRejected",
            BuildDateChangeReviewPlaceholders(actionItem, request),
            notifiedUserIds, notifiedEmails);

        // Notify assignees
        await NotifyAssigneesAsync(
            actionItem, title, message, actionType,
            request.ReviewedByUserId, request.ReviewedByDisplayName, url,
            isApproved ? "WorkflowDateChangeApproved" : "WorkflowDateChangeRejected",
            BuildDateChangeReviewPlaceholders(actionItem, request),
            notifiedUserIds, notifiedEmails);
    }

    // ── Status Change ────────────────────────────────────────────────────────

    public async Task NotifyStatusChangeRequestedAsync(ActionItem actionItem, ActionItemWorkflowRequest request)
    {
        var actionType = "StatusChangeRequested";
        var title = "Status Change Requested";
        var newStatus = request.RequestedNewStatus?.ToString() ?? "Unknown";
        var message = $"{request.RequestedByDisplayName} requested to change {actionItem.ActionId} status to {newStatus}";
        var url = "/approvals";

        var managers = await GetManagersOfAssigneesAsync(actionItem);
        var notifiedUserIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var notifiedEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (userId, email, displayName) in managers)
        {
            if (userId != null)
            {
                if (!notifiedUserIds.Add(userId)) continue;

                await CreateInAppNotificationAsync(
                    userId, title, message, actionType, actionItem, request.RequestedByUserId, request.RequestedByDisplayName, url);
            }

            if (!notifiedEmails.Add(email)) continue;

            await SendEmailSafeAsync(
                "WorkflowStatusChangeRequested",
                BuildStatusChangePlaceholders(actionItem, request),
                email, actionItem, request.RequestedByUserId);
        }
    }

    public async Task NotifyStatusChangeReviewedAsync(ActionItem actionItem, ActionItemWorkflowRequest request)
    {
        var isApproved = request.Status == WorkflowRequestStatus.Approved;
        var actionType = isApproved ? "StatusChangeApproved" : "StatusChangeRejected";
        var title = isApproved ? "Status Change Approved" : "Status Change Rejected";
        var message = isApproved
            ? $"Your status change request for {actionItem.ActionId} has been approved by {request.ReviewedByDisplayName}"
            : $"Your status change request for {actionItem.ActionId} has been rejected by {request.ReviewedByDisplayName}";
        var url = $"/actions/{actionItem.Id}/view";

        var notifiedUserIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var notifiedEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Notify the requester
        await NotifyRecipientAsync(
            request.RequestedByUserId, title, message, actionType, actionItem,
            request.ReviewedByUserId, request.ReviewedByDisplayName, url,
            isApproved ? "WorkflowStatusChangeApproved" : "WorkflowStatusChangeRejected",
            BuildStatusChangeReviewPlaceholders(actionItem, request),
            notifiedUserIds, notifiedEmails);

        // Notify assignees
        await NotifyAssigneesAsync(
            actionItem, title, message, actionType,
            request.ReviewedByUserId, request.ReviewedByDisplayName, url,
            isApproved ? "WorkflowStatusChangeApproved" : "WorkflowStatusChangeRejected",
            BuildStatusChangeReviewPlaceholders(actionItem, request),
            notifiedUserIds, notifiedEmails);
    }

    // ── Escalation ───────────────────────────────────────────────────────────

    public async Task NotifyEscalationAsync(ActionItem actionItem, string escalatedByUserId, string reason)
    {
        var actionType = "ActionItemEscalated";
        var title = "Action Item Escalated";
        var message = $"{actionItem.ActionId}: {actionItem.Title} has been escalated. Reason: {reason}";
        var url = $"/actions/{actionItem.Id}/view";

        var escalatedByUser = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == escalatedByUserId);
        var escalatedByName = escalatedByUser?.DisplayName ?? escalatedByUser?.FullName ?? "System";

        var notifiedUserIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var notifiedEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var placeholders = new Dictionary<string, string>
        {
            { "Code", actionItem.ActionId },
            { "Title", actionItem.Title },
            { "Reason", reason },
            { "EscalatedByName", escalatedByName }
        };

        // Notify managers of assignees
        var managers = await GetManagersOfAssigneesAsync(actionItem);
        foreach (var (userId, email, displayName) in managers)
        {
            if (userId != null)
            {
                if (!notifiedUserIds.Add(userId)) continue;

                await CreateInAppNotificationAsync(
                    userId, title, message, actionType, actionItem, escalatedByUserId, escalatedByName, url);
            }

            if (!notifiedEmails.Add(email)) continue;

            await SendEmailSafeAsync("WorkflowEscalation", placeholders, email, actionItem, escalatedByUserId);
        }

        // Notify assignees
        await NotifyAssigneesAsync(
            actionItem, title, message, actionType,
            escalatedByUserId, escalatedByName, url,
            "WorkflowEscalation", placeholders,
            notifiedUserIds, notifiedEmails);

        // Notify creator
        if (!string.IsNullOrEmpty(actionItem.CreatedByUserId))
        {
            await NotifyRecipientAsync(
                actionItem.CreatedByUserId, title, message, actionType, actionItem,
                escalatedByUserId, escalatedByName, url,
                "WorkflowEscalation", placeholders,
                notifiedUserIds, notifiedEmails);
        }
    }

    public async Task NotifyDirectionGivenAsync(ActionItem actionItem, string directorUserId, string directionText)
    {
        var actionType = "EscalationDirectionGiven";
        var title = "Direction Received";
        var url = $"/actions/{actionItem.Id}/view";

        var directorUser = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == directorUserId);
        var directorName = directorUser?.DisplayName ?? directorUser?.FullName ?? "System";

        var message = $"{directorName} has given direction on escalated item {actionItem.ActionId}";

        var notifiedUserIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var notifiedEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var placeholders = new Dictionary<string, string>
        {
            { "Code", actionItem.ActionId },
            { "Title", actionItem.Title },
            { "DirectorName", directorName },
            { "DirectionText", directionText }
        };

        // Notify assignees
        await NotifyAssigneesAsync(
            actionItem, title, message, actionType,
            directorUserId, directorName, url,
            "WorkflowDirectionGiven", placeholders,
            notifiedUserIds, notifiedEmails);

        // Notify creator
        if (!string.IsNullOrEmpty(actionItem.CreatedByUserId))
        {
            await NotifyRecipientAsync(
                actionItem.CreatedByUserId, title, message, actionType, actionItem,
                directorUserId, directorName, url,
                "WorkflowDirectionGiven", placeholders,
                notifiedUserIds, notifiedEmails);
        }
    }

    // ── Sub-items (WO-2026-11 A-2) ───────────────────────────────────────────

    public const string SubItemAssignedTemplateKey  = "ActionItem.SubItemAssigned";
    public const string SubItemCompletedTemplateKey = "ActionItem.SubItemCompleted";

    public async Task NotifySubItemAssignedAsync(Guid subItemId, IReadOnlyCollection<string> newAssigneeIds, string actorUserId)
    {
        var subItem = await LoadSubItemAsync(subItemId);
        if (subItem is null) return;

        var recipientIds = newAssigneeIds
            .Where(id => !string.IsNullOrWhiteSpace(id) && !string.Equals(id, actorUserId, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (recipientIds.Count == 0) return;

        var actorName = await GetDisplayNameAsync(actorUserId);
        var parent    = subItem.ActionItem;
        var message   = $"{actorName} assigned you to the sub-item \"{subItem.Title}\" of {parent.ActionId}: {parent.Title}";

        await SendSubItemNotificationsAsync(
            subItem, recipientIds, actorUserId, actorName,
            "Sub-item Assigned", message, "SubItemAssigned", SubItemAssignedTemplateKey);
    }

    public async Task NotifySubItemCompletedAsync(Guid subItemId, string actorUserId)
    {
        var subItem = await LoadSubItemAsync(subItemId);
        if (subItem is null || !subItem.IsDone) return;

        var parent = subItem.ActionItem;
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(parent.CreatedByUserId)) candidates.Add(parent.CreatedByUserId);
        candidates.AddRange(parent.Assignees.Select(a => a.UserId));
        candidates.AddRange(subItem.Assignees.Select(a => a.UserId));

        var recipientIds = candidates
            .Where(id => !string.IsNullOrWhiteSpace(id) && !string.Equals(id, actorUserId, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (recipientIds.Count == 0) return;

        var counts = await _db.ActionItemSubItems
            .IgnoreQueryFilters()
            .Where(s => s.ActionItemId == parent.Id && !s.IsDeleted)
            .GroupBy(s => s.ActionItemId)
            .Select(g => new { Total = g.Count(), Done = g.Count(s => s.IsDone) })
            .FirstOrDefaultAsync();

        var actorName = await GetDisplayNameAsync(actorUserId);
        var progress  = counts is null ? string.Empty : $" ({counts.Done} of {counts.Total} done)";
        var message   = $"{actorName} completed the sub-item \"{subItem.Title}\" of {parent.ActionId}: {parent.Title}{progress}";

        await SendSubItemNotificationsAsync(
            subItem, recipientIds, actorUserId, actorName,
            "Sub-item Completed", message, "SubItemCompleted", SubItemCompletedTemplateKey,
            new Dictionary<string, string>
            {
                ["DoneCount"]  = counts?.Done.ToString()  ?? "0",
                ["TotalCount"] = counts?.Total.ToString() ?? "0",
            });
    }

    /// <summary>
    /// The sub-item with its parent and both assignee lists. Query filters are
    /// bypassed because this runs after the request (no user context); it reads
    /// only this sub-item's own parent, and a deleted one sends nothing.
    /// </summary>
    private async Task<ActionItemSubItem?> LoadSubItemAsync(Guid subItemId)
    {
        var subItem = await _db.ActionItemSubItems
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(s => s.Assignees)
            .Include(s => s.ActionItem).ThenInclude(a => a.Assignees)
            .Include(s => s.ActionItem).ThenInclude(a => a.Workspace)
            .FirstOrDefaultAsync(s => s.Id == subItemId);

        return subItem is null || subItem.IsDeleted || subItem.ActionItem.IsDeleted ? null : subItem;
    }

    private async Task<string> GetDisplayNameAsync(string userId)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return "Someone";
        return !string.IsNullOrWhiteSpace(user.DisplayName) ? user.DisplayName! : user.FullName;
    }

    private async Task SendSubItemNotificationsAsync(
        ActionItemSubItem subItem,
        IReadOnlyCollection<string> recipientIds,
        string actorUserId,
        string actorName,
        string title,
        string message,
        string actionType,
        string templateKey,
        Dictionary<string, string>? extraPlaceholders = null)
    {
        var parent = subItem.ActionItem;
        var url    = $"/actions/{parent.Id}/view";

        var recipients = await _db.Users.AsNoTracking()
            .Where(u => recipientIds.Contains(u.Id) && u.IsActive)
            .ToListAsync();

        var notifiedEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var user in recipients)
        {
            await CreateInAppNotificationAsync(
                user.Id, title, message, actionType, parent, actorUserId, actorName, url, type: "ActionItem");

            if (string.IsNullOrEmpty(user.Email) || !notifiedEmails.Add(user.Email)) continue;

            var placeholders = new Dictionary<string, string>
            {
                ["ActionId"]         = parent.ActionId,
                ["Title"]            = parent.Title,
                ["SubItemTitle"]     = subItem.Title,
                ["SubItemStartDate"] = subItem.StartDate?.ToString("yyyy-MM-dd") ?? "—",
                ["SubItemEndDate"]   = subItem.EndDate.ToString("yyyy-MM-dd"),
                ["ActorName"]        = actorName,
                ["RecipientName"]    = !string.IsNullOrWhiteSpace(user.DisplayName) ? user.DisplayName! : user.FullName,
                ["WorkspaceName"]    = parent.Workspace?.Title ?? string.Empty,
                ["ItemUrl"]          = $"{_appSettings.FrontendBaseUrl}{url}",
            };
            if (extraPlaceholders != null)
                foreach (var (key, value) in extraPlaceholders)
                    placeholders[key] = value;

            await SendEmailSafeAsync(templateKey, placeholders, user.Email, parent, actorUserId);
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task<List<(string? UserId, string Email, string DisplayName)>> GetManagersOfAssigneesAsync(ActionItem actionItem)
    {
        var managers = new List<(string? UserId, string Email, string DisplayName)>();

        var assigneeUserIds = actionItem.Assignees.Select(a => a.UserId).ToList();
        if (assigneeUserIds.Count == 0) return managers;

        var assigneeUsers = await _db.Users.AsNoTracking()
            .Where(u => assigneeUserIds.Contains(u.Id))
            .ToListAsync();

        var assigneeEmails = assigneeUsers
            .Where(u => !string.IsNullOrEmpty(u.Email))
            .Select(u => u.Email!.ToLowerInvariant())
            .ToList();

        if (assigneeEmails.Count == 0) return managers;

        // Find KuEmployeeInfo records for assignees to get their supervisor info
        var employeeInfos = await _db.KuEmployeeInfo.AsNoTracking()
            .Where(e => e.EmailAddress != null && assigneeEmails.Contains(e.EmailAddress.ToLower()))
            .ToListAsync();

        // Get unique supervisor numbers
        var supervisorNumbers = employeeInfos
            .Where(e => !string.IsNullOrEmpty(e.SupervisorNumber))
            .Select(e => e.SupervisorNumber!)
            .Distinct()
            .ToList();

        if (supervisorNumbers.Count == 0) return managers;

        // Resolve supervisor details from KuEmployeeInfo
        var supervisorInfos = await _db.KuEmployeeInfo.AsNoTracking()
            .Where(e => e.EmpNo != null && supervisorNumbers.Contains(e.EmpNo))
            .ToListAsync();

        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var supervisor in supervisorInfos)
        {
            if (string.IsNullOrEmpty(supervisor.EmailAddress)) continue;
            if (!seenEmails.Add(supervisor.EmailAddress)) continue;

            var displayName = supervisor.EmployeeName ?? supervisor.SupervisorName ?? "Manager";

            // Check if the supervisor has a system account
            var systemUser = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email != null
                    && u.Email.ToLower() == supervisor.EmailAddress.ToLower());

            managers.Add((systemUser?.Id, supervisor.EmailAddress, displayName));
        }

        return managers;
    }

    private async Task NotifyAssigneesAsync(
        ActionItem actionItem,
        string title,
        string message,
        string actionType,
        string? triggeredByUserId,
        string? triggeredByDisplayName,
        string url,
        string emailTemplateKey,
        Dictionary<string, string> placeholders,
        HashSet<string> notifiedUserIds,
        HashSet<string> notifiedEmails)
    {
        var assigneeUserIds = actionItem.Assignees.Select(a => a.UserId).ToList();
        if (assigneeUserIds.Count == 0) return;

        var assignees = await _db.Users.AsNoTracking()
            .Where(u => assigneeUserIds.Contains(u.Id))
            .ToListAsync();

        foreach (var assignee in assignees)
        {
            if (!notifiedUserIds.Add(assignee.Id)) continue;

            await CreateInAppNotificationAsync(
                assignee.Id, title, message, actionType, actionItem,
                triggeredByUserId, triggeredByDisplayName, url);

            if (!string.IsNullOrEmpty(assignee.Email) && notifiedEmails.Add(assignee.Email))
            {
                await SendEmailSafeAsync(emailTemplateKey, placeholders, assignee.Email, actionItem, triggeredByUserId);
            }
        }
    }

    private async Task NotifyRecipientAsync(
        string recipientUserId,
        string title,
        string message,
        string actionType,
        ActionItem actionItem,
        string? triggeredByUserId,
        string? triggeredByDisplayName,
        string url,
        string emailTemplateKey,
        Dictionary<string, string> placeholders,
        HashSet<string> notifiedUserIds,
        HashSet<string> notifiedEmails)
    {
        if (notifiedUserIds.Add(recipientUserId))
        {
            await CreateInAppNotificationAsync(
                recipientUserId, title, message, actionType, actionItem,
                triggeredByUserId, triggeredByDisplayName, url);
        }

        // Also send email to the recipient
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == recipientUserId);

        if (user != null && !string.IsNullOrEmpty(user.Email) && notifiedEmails.Add(user.Email))
        {
            await SendEmailSafeAsync(emailTemplateKey, placeholders, user.Email, actionItem, triggeredByUserId);
        }
    }

    private async Task CreateInAppNotificationAsync(
        string userId,
        string title,
        string message,
        string actionType,
        ActionItem actionItem,
        string? createdByUserId,
        string? createdByDisplayName,
        string url,
        string type = "Workflow")
    {
        var dto = new CreateNotificationDto
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            ActionType = actionType,
            RelatedEntityType = "ActionItem",
            RelatedEntityId = actionItem.Id,
            RelatedEntityCode = actionItem.ActionId,
            Url = url,
            CreatedByUserId = createdByUserId,
            CreatedByDisplayName = createdByDisplayName
        };

        await _notificationService.CreateAsync(dto);
    }

    private async Task SendEmailSafeAsync(
        string templateKey,
        Dictionary<string, string> placeholders,
        string email,
        ActionItem actionItem,
        string? triggeredByUserId)
    {
        try
        {
            await _emailSender.SendEmailAsync(
                templateKey,
                placeholders,
                new List<string> { email },
                "ActionItem",
                actionItem.Id,
                triggeredByUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send workflow email {TemplateKey} to {Email}", templateKey, email);
        }
    }

    // ── Placeholder builders ─────────────────────────────────────────────────

    private static Dictionary<string, string> BuildDateChangePlaceholders(
        ActionItem actionItem, ActionItemWorkflowRequest request)
    {
        return new Dictionary<string, string>
        {
            { "Code", actionItem.ActionId },
            { "Title", actionItem.Title },
            { "RequesterName", request.RequestedByDisplayName },
            { "CurrentStartDate", request.CurrentStartDate?.ToString("yyyy-MM-dd") ?? "N/A" },
            { "CurrentDueDate", request.CurrentDueDate?.ToString("yyyy-MM-dd") ?? "N/A" },
            { "RequestedNewStartDate", request.RequestedNewStartDate?.ToString("yyyy-MM-dd") ?? "N/A" },
            { "RequestedNewDueDate", request.RequestedNewDueDate?.ToString("yyyy-MM-dd") ?? "N/A" },
            { "Reason", request.Reason }
        };
    }

    private static Dictionary<string, string> BuildDateChangeReviewPlaceholders(
        ActionItem actionItem, ActionItemWorkflowRequest request)
    {
        var placeholders = BuildDateChangePlaceholders(actionItem, request);
        placeholders["ReviewerName"] = request.ReviewedByDisplayName ?? "Reviewer";
        placeholders["ReviewComment"] = request.ReviewComment ?? string.Empty;
        placeholders["Status"] = request.Status.ToString();
        return placeholders;
    }

    private static Dictionary<string, string> BuildStatusChangePlaceholders(
        ActionItem actionItem, ActionItemWorkflowRequest request)
    {
        return new Dictionary<string, string>
        {
            { "Code", actionItem.ActionId },
            { "Title", actionItem.Title },
            { "RequesterName", request.RequestedByDisplayName },
            { "CurrentStatus", request.CurrentStatus?.ToString() ?? "N/A" },
            { "RequestedNewStatus", request.RequestedNewStatus?.ToString() ?? "N/A" },
            { "Reason", request.Reason }
        };
    }

    private static Dictionary<string, string> BuildStatusChangeReviewPlaceholders(
        ActionItem actionItem, ActionItemWorkflowRequest request)
    {
        var placeholders = BuildStatusChangePlaceholders(actionItem, request);
        placeholders["ReviewerName"] = request.ReviewedByDisplayName ?? "Reviewer";
        placeholders["ReviewComment"] = request.ReviewComment ?? string.Empty;
        placeholders["Status"] = request.Status.ToString();
        return placeholders;
    }
}
