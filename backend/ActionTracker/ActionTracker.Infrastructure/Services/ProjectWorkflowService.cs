using ActionTracker.Application.Common;
using ActionTracker.Application.Common.Extensions;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.Notifications;
using ActionTracker.Application.Features.Notifications.DTOs;
using ActionTracker.Application.Features.Projects.DTOs;
using ActionTracker.Application.Features.Projects.Interfaces;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActionTracker.Infrastructure.Services;

public class ProjectWorkflowService : IProjectWorkflowService
{
    private readonly IAppDbContext _db;
    private readonly INotificationService _notificationService;
    private readonly IEmailSender _emailSender;
    private readonly AppSettings _appSettings;
    private readonly ILogger<ProjectWorkflowService> _logger;

    public ProjectWorkflowService(
        IAppDbContext db,
        INotificationService notificationService,
        IEmailSender emailSender,
        IOptions<AppSettings> appSettings,
        ILogger<ProjectWorkflowService> logger)
    {
        _db = db;
        _notificationService = notificationService;
        _emailSender = emailSender;
        _appSettings = appSettings.Value;
        _logger = logger;
    }

    // ── Send notifications on project creation ───────────────────────────────

    public async Task SendProjectCreatedNotificationsAsync(Guid projectId, string createdByUserId)
    {
        var project = await _db.Projects
            .Include(p => p.Sponsors)
            .Include(p => p.ProjectManager)
            .Include(p => p.Workspace)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project is null) return;

        var creatorName = project.ProjectManager?.FullName ?? "System";
        var recipients = await ResolveReviewerInfosAsync(project);

        foreach (var (userId, email, displayName) in recipients)
        {
            if (userId != null && userId != createdByUserId)
            {
                await CreateNotificationAsync(
                    userId,
                    "New Project Created",
                    $"{creatorName} created project {project.ProjectCode} — {project.Name}",
                    "ProjectCreated",
                    project,
                    createdByUserId,
                    creatorName);
            }

            if (!string.IsNullOrEmpty(email))
            {
                await SendEmailSafeAsync("Project.Created", new Dictionary<string, string>
                {
                    ["ProjectCode"] = project.ProjectCode,
                    ["ProjectName"] = project.Name,
                    ["ProjectType"] = project.ProjectType.ToString(),
                    ["CreatedByName"] = creatorName,
                    ["WorkspaceName"] = project.Workspace?.Title ?? "",
                    ["PlannedStartDate"] = project.PlannedStartDate.ToString("yyyy-MM-dd"),
                    ["PlannedEndDate"] = project.PlannedEndDate.ToString("yyyy-MM-dd"),
                    ["ProjectManager"] = creatorName,
                    ["Description"] = project.Description ?? "",
                    ["Status"] = project.Status.ToString(),
                    ["Priority"] = project.Priority.ToString(),
                    ["Budget"] = project.ApprovedBudget?.ToString("N2") ?? "",
                    ["ItemUrl"] = $"{_appSettings.FrontendBaseUrl}/projects/{project.Id}",
                }, email, project.Id, createdByUserId);
            }
        }
    }

    // ── Submit for approval ──────────────────────────────────────────────────

    public async Task<ProjectApprovalRequestDto> SubmitForApprovalAsync(
        SubmitProjectApprovalRequestDto dto, string requestedByUserId)
    {
        var project = await _db.Projects
            .Include(p => p.Sponsors)
            .Include(p => p.ProjectManager)
            .FirstOrDefaultAsync(p => p.Id == dto.ProjectId)
            ?? throw new KeyNotFoundException($"Project {dto.ProjectId} not found.");

        if (project.Status != ProjectStatus.Draft)
            throw new ArgumentException("Project can only be submitted for approval when in Draft status.");

        if (project.ProjectManagerUserId != requestedByUserId)
            throw new ArgumentException("Only the project manager can submit a project for approval.");

        var hasPending = await _db.ProjectApprovalRequests
            .AnyAsync(r => r.ProjectId == dto.ProjectId && r.Status == ProjectApprovalStatus.Pending);
        if (hasPending)
            throw new ArgumentException("A pending approval request already exists for this project.");

        // Validate: milestones must cover all 5 project phases
        var milestones = await _db.Milestones
            .Where(m => m.ProjectId == dto.ProjectId)
            .Select(m => new { m.Id, m.Phase })
            .ToListAsync();

        var allPhases = Enum.GetValues<ProjectPhase>();
        var coveredPhases = milestones.Select(m => m.Phase).Distinct().ToHashSet();
        var missingPhases = allPhases.Where(p => !coveredPhases.Contains(p)).ToList();
        if (missingPhases.Count > 0)
        {
            var missing = string.Join(", ", missingPhases.Select(p => p.GetDescription()));
            throw new ArgumentException($"The project must have at least one milestone in each phase before submitting for approval. Missing phases: {missing}.");
        }

        // Validate: every milestone must have at least one action item
        var milestoneIds = milestones.Select(m => m.Id).ToList();
        var milestonesWithActions = await _db.ActionItems
            .Where(a => a.MilestoneId != null && milestoneIds.Contains(a.MilestoneId.Value))
            .Select(a => a.MilestoneId!.Value)
            .Distinct()
            .ToListAsync();

        var milestonesWithoutActions = milestoneIds.Except(milestonesWithActions).ToList();
        if (milestonesWithoutActions.Count > 0)
        {
            var emptyNames = await _db.Milestones
                .Where(m => milestonesWithoutActions.Contains(m.Id))
                .Select(m => m.Name)
                .ToListAsync();
            throw new ArgumentException($"Every milestone must have at least one action item before submitting for approval. Milestones without action items: {string.Join(", ", emptyNames)}.");
        }

        var requesterName = project.ProjectManager?.FullName ?? "Unknown";

        var request = new ProjectApprovalRequest
        {
            Id = Guid.NewGuid(),
            ProjectId = dto.ProjectId,
            RequestedByUserId = requestedByUserId,
            RequestedByDisplayName = requesterName,
            Status = ProjectApprovalStatus.Pending,
            Reason = dto.Reason.Trim(),
            CreatedAt = DateTime.UtcNow,
        };

        _db.ProjectApprovalRequests.Add(request);
        project.Status = ProjectStatus.PendingApproval;
        await _db.SaveChangesAsync();

        // Notify reviewers
        var reviewers = await ResolveReviewerInfosAsync(project);
        foreach (var (userId, email, displayName) in reviewers)
        {
            if (userId != null)
            {
                await CreateNotificationAsync(
                    userId,
                    "Project Approval Requested",
                    $"{requesterName} submitted {project.ProjectCode} — {project.Name} for approval",
                    "ProjectApprovalRequested",
                    project,
                    requestedByUserId,
                    requesterName);
            }

            if (!string.IsNullOrEmpty(email))
            {
                await SendEmailSafeAsync("ProjectApproval.Requested", new Dictionary<string, string>
                {
                    ["ProjectCode"] = project.ProjectCode,
                    ["ProjectName"] = project.Name,
                    ["RequestedByName"] = requesterName,
                    ["Reason"] = request.Reason,
                    ["ProjectUrl"] = $"{_appSettings.FrontendBaseUrl}/projects/{project.Id}",
                    ["ApprovalUrl"] = $"{_appSettings.FrontendBaseUrl}/approvals",
                }, email, project.Id, requestedByUserId);
            }
        }

        return MapToDto(request, project);
    }

    // ── Review approval request ──────────────────────────────────────────────

    public async Task<ProjectApprovalRequestDto> ReviewApprovalRequestAsync(
        ReviewProjectApprovalRequestDto dto, string reviewerUserId)
    {
        var request = await _db.ProjectApprovalRequests
            .Include(r => r.Project)
                .ThenInclude(p => p.Sponsors)
            .Include(r => r.Project)
                .ThenInclude(p => p.ProjectManager)
            .FirstOrDefaultAsync(r => r.Id == dto.RequestId)
            ?? throw new KeyNotFoundException($"Approval request {dto.RequestId} not found.");

        if (request.Status != ProjectApprovalStatus.Pending)
            throw new ArgumentException("This approval request has already been reviewed.");

        var canReview = await CanReviewProjectAsync(request.ProjectId, reviewerUserId);
        if (!canReview)
            throw new UnauthorizedAccessException("You are not authorized to review this project.");

        var reviewer = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == reviewerUserId);
        var reviewerName = reviewer?.FullName ?? "Reviewer";

        request.Status = dto.IsApproved ? ProjectApprovalStatus.Approved : ProjectApprovalStatus.Rejected;
        request.ReviewedByUserId = reviewerUserId;
        request.ReviewedByDisplayName = reviewerName;
        request.ReviewComment = dto.ReviewComment?.Trim();
        request.ReviewedAt = DateTime.UtcNow;

        var project = request.Project;

        if (dto.IsApproved)
        {
            project.Status = ProjectStatus.Active;
            project.ActualStartDate = DateTime.UtcNow;
            project.IsBaselined = true;

            // Baseline milestones
            var milestones = await _db.Milestones
                .Where(m => m.ProjectId == project.Id)
                .ToListAsync();
            foreach (var m in milestones)
            {
                m.BaselinePlannedStartDate ??= m.PlannedStartDate;
                m.BaselinePlannedDueDate ??= m.PlannedDueDate;
            }

            // Auto-close other pending requests
            var otherPending = await _db.ProjectApprovalRequests
                .Where(r => r.ProjectId == project.Id && r.Id != request.Id && r.Status == ProjectApprovalStatus.Pending)
                .ToListAsync();
            foreach (var other in otherPending)
            {
                other.Status = ProjectApprovalStatus.Rejected;
                other.ReviewedByUserId = "system";
                other.ReviewedByDisplayName = "System";
                other.ReviewComment = "Auto-closed: project approved by another reviewer";
                other.ReviewedAt = DateTime.UtcNow;
            }
        }
        else
        {
            project.Status = ProjectStatus.Draft;
        }

        await _db.SaveChangesAsync();

        // Notify the project manager
        var decision = dto.IsApproved ? "Approved" : "Rejected";
        var actionType = dto.IsApproved ? "ProjectApprovalApproved" : "ProjectApprovalRejected";

        await CreateNotificationAsync(
            project.ProjectManagerUserId,
            $"Project {decision}",
            $"{reviewerName} has {decision.ToLower()} project {project.ProjectCode} — {project.Name}",
            actionType,
            project,
            reviewerUserId,
            reviewerName);

        var pmEmail = await GetUserEmailAsync(project.ProjectManagerUserId);
        if (pmEmail != null)
        {
            await SendEmailSafeAsync("ProjectApproval.Reviewed", new Dictionary<string, string>
            {
                ["ProjectCode"] = project.ProjectCode,
                ["ProjectName"] = project.Name,
                ["Decision"] = decision,
                ["ReviewedByName"] = reviewerName,
                ["ReviewComment"] = request.ReviewComment ?? "",
                ["ProjectUrl"] = $"{_appSettings.FrontendBaseUrl}/projects/{project.Id}",
            }, pmEmail, project.Id, reviewerUserId);
        }

        return MapToDto(request, project);
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    public async Task<List<ProjectApprovalRequestDto>> GetApprovalRequestsForProjectAsync(Guid projectId)
    {
        var requests = await _db.ProjectApprovalRequests
            .Include(r => r.Project)
            .Where(r => r.ProjectId == projectId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return requests.Select(r => MapToDto(r, r.Project)).ToList();
    }

    public async Task<List<ProjectApprovalRequestDto>> GetPendingReviewsAsync(string userId)
    {
        var reviewableProjectIds = await GetReviewableProjectIdsAsync(userId);

        var requests = await _db.ProjectApprovalRequests
            .Include(r => r.Project)
            .Where(r => r.Status == ProjectApprovalStatus.Pending && reviewableProjectIds.Contains(r.ProjectId))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return requests.Select(r => MapToDto(r, r.Project)).ToList();
    }

    public async Task<List<ProjectApprovalRequestDto>> GetMyRequestsAsync(string userId)
    {
        var requests = await _db.ProjectApprovalRequests
            .Include(r => r.Project)
            .Where(r => r.RequestedByUserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return requests.Select(r => MapToDto(r, r.Project)).ToList();
    }

    public async Task<ProjectApprovalSummaryDto> GetPendingSummaryAsync(string userId)
    {
        var reviewableProjectIds = await GetReviewableProjectIdsAsync(userId);

        var count = await _db.ProjectApprovalRequests
            .CountAsync(r => r.Status == ProjectApprovalStatus.Pending && reviewableProjectIds.Contains(r.ProjectId));

        return new ProjectApprovalSummaryDto { PendingProjectApprovals = count };
    }

    public async Task<bool> CanReviewProjectAsync(Guid projectId, string userId)
    {
        // Check if user is a sponsor
        var isSponsor = await _db.ProjectSponsors
            .AnyAsync(s => s.ProjectId == projectId && s.UserId == userId);
        if (isSponsor) return true;

        // Check if user is the direct line manager of the project manager
        var project = await _db.Projects
            .Where(p => p.Id == projectId)
            .Select(p => new { p.ProjectManagerUserId })
            .FirstOrDefaultAsync();
        if (project is null) return false;

        return await IsDirectLineManagerAsync(project.ProjectManagerUserId, userId);
    }

    // ── Validate before submit ─────────────────────────────────────────────

    public async Task<SubmitValidationResultDto> ValidateSubmitForApprovalAsync(Guid projectId, string userId)
    {
        var result = new SubmitValidationResultDto { IsValid = true };

        var project = await _db.Projects
            .Where(p => p.Id == projectId)
            .Select(p => new { p.Status, p.ProjectManagerUserId })
            .FirstOrDefaultAsync();

        if (project is null)
        {
            result.IsValid = false;
            result.Errors.Add("Project not found.");
            return result;
        }

        if (project.Status != ProjectStatus.Draft)
            result.Errors.Add("Project can only be submitted for approval when in Draft status.");

        if (project.ProjectManagerUserId != userId)
            result.Errors.Add("Only the project manager can submit a project for approval.");

        var hasPending = await _db.ProjectApprovalRequests
            .AnyAsync(r => r.ProjectId == projectId && r.Status == ProjectApprovalStatus.Pending);
        if (hasPending)
            result.Errors.Add("A pending approval request already exists for this project.");

        // Phase coverage
        var milestones = await _db.Milestones
            .Where(m => m.ProjectId == projectId)
            .Select(m => new { m.Id, m.Phase, m.Name })
            .ToListAsync();

        var allPhases = Enum.GetValues<ProjectPhase>();
        var coveredPhases = milestones.Select(m => m.Phase).Distinct().ToHashSet();
        var missingPhases = allPhases.Where(p => !coveredPhases.Contains(p)).ToList();
        if (missingPhases.Count > 0)
        {
            var missing = string.Join(", ", missingPhases.Select(p => p.GetDescription()));
            result.Errors.Add($"The project must have at least one milestone in each phase. Missing phases: {missing}.");
        }

        // Each milestone must have action items
        if (milestones.Count > 0)
        {
            var milestoneIds = milestones.Select(m => m.Id).ToList();
            var milestonesWithActions = await _db.ActionItems
                .Where(a => a.MilestoneId != null && milestoneIds.Contains(a.MilestoneId.Value))
                .Select(a => a.MilestoneId!.Value)
                .Distinct()
                .ToListAsync();

            var empty = milestones.Where(m => !milestonesWithActions.Contains(m.Id)).ToList();
            if (empty.Count > 0)
            {
                var names = string.Join(", ", empty.Select(m => m.Name));
                result.Errors.Add($"Every milestone must have at least one action item. Milestones without action items: {names}.");
            }
        }

        result.IsValid = result.Errors.Count == 0;
        return result;
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task<List<Guid>> GetReviewableProjectIdsAsync(string userId)
    {
        // Projects where user is a sponsor
        var sponsoredProjectIds = await _db.ProjectSponsors
            .Where(s => s.UserId == userId)
            .Select(s => s.ProjectId)
            .ToListAsync();

        // Projects where user is the direct line manager of the PM
        var userEmail = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync();

        var managerOfProjectIds = new List<Guid>();
        if (!string.IsNullOrEmpty(userEmail))
        {
            // Find the employee number for this user
            var empInfo = await _db.KuEmployeeInfo.AsNoTracking()
                .Where(e => e.EmailAddress != null && e.EmailAddress.ToLower() == userEmail.ToLower())
                .Select(e => e.EmpNo)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrEmpty(empInfo))
            {
                // Find all employees who have this user as supervisor
                var subordinateEmails = await _db.KuEmployeeInfo.AsNoTracking()
                    .Where(e => e.SupervisorNumber == empInfo && e.EmailAddress != null)
                    .Select(e => e.EmailAddress!.ToLower())
                    .ToListAsync();

                // Find the user IDs of those subordinates
                var subordinateUserIds = await _db.Users.AsNoTracking()
                    .Where(u => u.Email != null && subordinateEmails.Contains(u.Email.ToLower()))
                    .Select(u => u.Id)
                    .ToListAsync();

                // Find projects where those subordinates are PM
                var projectIds = await _db.Projects
                    .Where(p => subordinateUserIds.Contains(p.ProjectManagerUserId))
                    .Select(p => p.Id)
                    .ToListAsync();

                managerOfProjectIds.AddRange(projectIds);
            }
        }

        return sponsoredProjectIds.Union(managerOfProjectIds).Distinct().ToList();
    }

    private async Task<bool> IsDirectLineManagerAsync(string projectManagerUserId, string candidateManagerUserId)
    {
        var pmEmail = await _db.Users.AsNoTracking()
            .Where(u => u.Id == projectManagerUserId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync();

        if (string.IsNullOrEmpty(pmEmail)) return false;

        var pmEmpInfo = await _db.KuEmployeeInfo.AsNoTracking()
            .Where(e => e.EmailAddress != null && e.EmailAddress.ToLower() == pmEmail.ToLower())
            .FirstOrDefaultAsync();

        if (pmEmpInfo is null || string.IsNullOrEmpty(pmEmpInfo.SupervisorNumber)) return false;

        var supervisorInfo = await _db.KuEmployeeInfo.AsNoTracking()
            .Where(e => e.EmpNo == pmEmpInfo.SupervisorNumber)
            .Select(e => e.EmailAddress)
            .FirstOrDefaultAsync();

        if (string.IsNullOrEmpty(supervisorInfo)) return false;

        var managerUser = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == supervisorInfo.ToLower());

        return managerUser?.Id == candidateManagerUserId;
    }

    private async Task<List<(string? UserId, string Email, string DisplayName)>> ResolveReviewerInfosAsync(Project project)
    {
        var result = new List<(string? UserId, string Email, string DisplayName)>();
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Add sponsors
        var sponsorUserIds = project.Sponsors.Select(s => s.UserId).ToList();
        if (sponsorUserIds.Count > 0)
        {
            var sponsors = await _db.Users.AsNoTracking()
                .Where(u => sponsorUserIds.Contains(u.Id) && u.IsActive)
                .ToListAsync();

            foreach (var s in sponsors)
            {
                if (!string.IsNullOrEmpty(s.Email) && seenEmails.Add(s.Email))
                    result.Add((s.Id, s.Email, s.FullName ?? "Sponsor"));
            }
        }

        // Add PM's direct line manager
        var pmEmail = await _db.Users.AsNoTracking()
            .Where(u => u.Id == project.ProjectManagerUserId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync();

        if (!string.IsNullOrEmpty(pmEmail))
        {
            var pmEmpInfo = await _db.KuEmployeeInfo.AsNoTracking()
                .Where(e => e.EmailAddress != null && e.EmailAddress.ToLower() == pmEmail.ToLower())
                .FirstOrDefaultAsync();

            if (pmEmpInfo != null && !string.IsNullOrEmpty(pmEmpInfo.SupervisorNumber))
            {
                var supervisorInfo = await _db.KuEmployeeInfo.AsNoTracking()
                    .Where(e => e.EmpNo == pmEmpInfo.SupervisorNumber)
                    .FirstOrDefaultAsync();

                if (supervisorInfo != null && !string.IsNullOrEmpty(supervisorInfo.EmailAddress)
                    && seenEmails.Add(supervisorInfo.EmailAddress))
                {
                    var managerUser = await _db.Users.AsNoTracking()
                        .FirstOrDefaultAsync(u => u.Email != null
                            && u.Email.ToLower() == supervisorInfo.EmailAddress.ToLower());

                    var name = supervisorInfo.EmployeeName ?? supervisorInfo.SupervisorName ?? "Manager";
                    result.Add((managerUser?.Id, supervisorInfo.EmailAddress, name));
                }
            }
        }

        return result;
    }

    private async Task CreateNotificationAsync(
        string userId, string title, string message, string actionType,
        Project project, string? createdByUserId, string? createdByDisplayName)
    {
        await _notificationService.CreateAsync(new CreateNotificationDto
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = "Project",
            ActionType = actionType,
            RelatedEntityType = "Project",
            RelatedEntityId = project.Id,
            RelatedEntityCode = project.ProjectCode,
            Url = $"/projects/{project.Id}",
            CreatedByUserId = createdByUserId,
            CreatedByDisplayName = createdByDisplayName,
        });
    }

    private async Task SendEmailSafeAsync(
        string templateKey, Dictionary<string, string> placeholders,
        string email, Guid projectId, string? triggeredByUserId)
    {
        try
        {
            await _emailSender.SendEmailAsync(
                templateKey, placeholders,
                new List<string> { email },
                "Project", projectId, triggeredByUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email {TemplateKey} to {Email}", templateKey, email);
        }
    }

    private async Task<string?> GetUserEmailAsync(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;
        return await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId && u.IsActive)
            .Select(u => u.Email)
            .FirstOrDefaultAsync();
    }

    private static ProjectApprovalRequestDto MapToDto(ProjectApprovalRequest r, Project p)
    {
        return new ProjectApprovalRequestDto
        {
            Id = r.Id,
            ProjectId = r.ProjectId,
            ProjectCode = p.ProjectCode,
            ProjectName = p.Name,
            RequestedByUserId = r.RequestedByUserId,
            RequestedByDisplayName = r.RequestedByDisplayName,
            ReviewedByUserId = r.ReviewedByUserId,
            ReviewedByDisplayName = r.ReviewedByDisplayName,
            Status = r.Status.ToString(),
            Reason = r.Reason,
            ReviewComment = r.ReviewComment,
            CreatedAt = r.CreatedAt,
            ReviewedAt = r.ReviewedAt,
        };
    }
}
