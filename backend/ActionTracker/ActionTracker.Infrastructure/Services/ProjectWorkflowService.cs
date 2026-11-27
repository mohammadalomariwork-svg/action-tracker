using ActionTracker.Application.Common;
using ActionTracker.Application.Common.Extensions;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.Notifications;
using ActionTracker.Application.Features.Notifications.DTOs;
using ActionTracker.Application.Features.ProjectPeople.Interfaces;
using ActionTracker.Application.Features.ProjectPeople.Services;
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
        // Strategic projects: notify the workspace's strategic-project approver chain.
        // Operational projects: PM has full authority, no reviewers to notify.
        var recipients = project.ProjectType == ProjectType.Strategic
            ? await ResolveStrategicApproverInfosAsync(project)
            : new List<(string? UserId, string Email, string DisplayName)>();

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
                await SendEmailSafeAsync("Project.Created", EmailPlaceholders.Encode(new Dictionary<string, string>
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
                }), email, project.Id, createdByUserId);
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

        // WO-2026-11 P-2 (decision D3): the lead PM or an additional PM may submit;
        // P-2b (B5): so may the Admin role. Routing, reviewers and notifications stay
        // with the lead PM and the workspace's chain (unchanged below).
        if (!await CanSubmitAsync(project.Id, project.ProjectManagerUserId, requestedByUserId))
            throw new ArgumentException(SubmitNotAllowedMessage);

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

        // Operational projects bypass approval entirely: PM has full authority,
        // so the project goes Draft → Active immediately and no ApprovalRequest
        // is created. A historical "pseudo-approved" request is returned so the
        // existing UI flow can show that the submit happened.
        if (project.ProjectType == ProjectType.Operational)
        {
            project.Status = ProjectStatus.Active;
            project.ActualStartDate = DateTime.UtcNow;
            project.IsBaselined = true;

            var operationalMilestones = await _db.Milestones
                .Where(m => m.ProjectId == project.Id)
                .ToListAsync();
            foreach (var m in operationalMilestones)
            {
                m.BaselinePlannedStartDate ??= m.PlannedStartDate;
                m.BaselinePlannedDueDate ??= m.PlannedDueDate;
            }

            var autoRequest = new ProjectApprovalRequest
            {
                Id                       = Guid.NewGuid(),
                ProjectId                = dto.ProjectId,
                RequestedByUserId        = requestedByUserId,
                RequestedByDisplayName   = requesterName,
                AssignedReviewerUserId   = requestedByUserId,
                AssignedReviewerDisplayName = requesterName,
                Status                   = ProjectApprovalStatus.Approved,
                Reason                   = dto.Reason.Trim(),
                CreatedAt                = DateTime.UtcNow,
                ReviewedByUserId         = requestedByUserId,
                ReviewedByDisplayName    = requesterName,
                ReviewComment            = "Auto-approved: operational project (PM authority).",
                ReviewedAt               = DateTime.UtcNow,
                Level                    = 1,
                TotalLevels              = 1,
            };
            _db.ProjectApprovalRequests.Add(autoRequest);
            await _db.SaveChangesAsync();

            return MapToDto(autoRequest, project);
        }

        // Strategic projects: build the sequential chain from the workspace's
        // configured approvers. Mirrors the strategic-plan workflow exactly —
        // one Pending row is created for level 1, and each Approve advances
        // the chain.
        var chain = await _db.WorkspaceStrategicProjectApprovers
            .AsNoTracking()
            .Where(a => a.WorkspaceId == project.WorkspaceId)
            .OrderBy(a => a.Order)
            .ToListAsync();

        if (chain.Count == 0)
            throw new InvalidOperationException(
                "This workspace has no Strategic Project Approvers configured. Configure the approval chain on the workspace settings page before submitting strategic projects for approval.");

        var firstApproverUserId = chain[0].UserId;
        var firstApprover = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == firstApproverUserId)
            ?? throw new InvalidOperationException(
                "The first approver in the chain is no longer a valid user.");
        if (!firstApprover.IsActive)
            throw new InvalidOperationException(
                "The first approver in the chain is inactive.");

        var firstApproverName = firstApprover.FullName ?? firstApprover.UserName ?? firstApproverUserId;

        var request = new ProjectApprovalRequest
        {
            Id                          = Guid.NewGuid(),
            ProjectId                   = dto.ProjectId,
            RequestedByUserId           = requestedByUserId,
            RequestedByDisplayName      = requesterName,
            AssignedReviewerUserId      = firstApprover.Id,
            AssignedReviewerDisplayName = firstApproverName,
            Status                      = ProjectApprovalStatus.Pending,
            Reason                      = dto.Reason.Trim(),
            CreatedAt                   = DateTime.UtcNow,
            Level                       = 1,
            TotalLevels                 = chain.Count,
        };

        _db.ProjectApprovalRequests.Add(request);
        project.Status = ProjectStatus.PendingApproval;
        await _db.SaveChangesAsync();

        // Notify the level-1 approver only (the rest get pinged when the chain advances).
        if (!string.IsNullOrEmpty(firstApprover.Id))
        {
            await CreateNotificationAsync(
                firstApprover.Id,
                "Project Approval Requested",
                $"{requesterName} submitted {project.ProjectCode} — {project.Name} for approval " +
                $"(level {request.Level} of {request.TotalLevels})",
                "ProjectApprovalRequested",
                project,
                requestedByUserId,
                requesterName);
        }

        if (!string.IsNullOrEmpty(firstApprover.Email))
        {
            await SendEmailSafeAsync("ProjectApproval.Requested", EmailPlaceholders.Encode(new Dictionary<string, string>
            {
                ["ProjectCode"] = project.ProjectCode,
                ["ProjectName"] = project.Name,
                ["RequestedByName"] = requesterName,
                ["Reason"] = request.Reason,
                ["ProjectUrl"] = $"{_appSettings.FrontendBaseUrl}/projects/{project.Id}",
                ["ApprovalUrl"] = $"{_appSettings.FrontendBaseUrl}/approvals",
            }), firstApprover.Email, project.Id, requestedByUserId);
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

        // Multi-level routing: when an *approval* is not the final level, create
        // a Pending row for the next approver in the chain and leave project-
        // level state alone. Project-level transitions only fire on the final-
        // level approval or any rejection.
        ProjectApprovalRequest? advanceRequest = null;
        ApplicationUser? nextApprover = null;
        var isFinalLevel = request.Level >= request.TotalLevels;

        if (dto.IsApproved && !isFinalLevel)
        {
            var nextLevel = request.Level + 1;
            var chainEntry = await _db.WorkspaceStrategicProjectApprovers
                .AsNoTracking()
                .Where(a => a.WorkspaceId == project.WorkspaceId && a.Order == nextLevel)
                .FirstOrDefaultAsync();

            if (chainEntry is null)
                throw new InvalidOperationException(
                    $"Approval chain is broken: no approver configured at level {nextLevel} for this workspace.");

            nextApprover = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == chainEntry.UserId)
                ?? throw new InvalidOperationException(
                    $"Approver at level {nextLevel} is no longer a valid user.");
            if (!nextApprover.IsActive)
                throw new InvalidOperationException(
                    $"Approver at level {nextLevel} is inactive.");

            var nextName = nextApprover.FullName ?? nextApprover.UserName ?? nextApprover.Id;
            advanceRequest = new ProjectApprovalRequest
            {
                Id                          = Guid.NewGuid(),
                ProjectId                   = project.Id,
                RequestedByUserId           = request.RequestedByUserId,
                RequestedByDisplayName      = request.RequestedByDisplayName,
                AssignedReviewerUserId      = nextApprover.Id,
                AssignedReviewerDisplayName = nextName,
                Status                      = ProjectApprovalStatus.Pending,
                Reason                      = request.Reason,
                CreatedAt                   = DateTime.UtcNow,
                Level                       = nextLevel,
                TotalLevels                 = request.TotalLevels,
            };
            _db.ProjectApprovalRequests.Add(advanceRequest);
        }

        if (dto.IsApproved && isFinalLevel)
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
        else if (!dto.IsApproved)
        {
            // Any rejection sends the project back to Draft.
            project.Status = ProjectStatus.Draft;
        }
        // Intermediate-level approvals: project stays PendingApproval.

        await _db.SaveChangesAsync();

        // Notify the project manager of this level's outcome.
        var decision = dto.IsApproved ? "Approved" : "Rejected";
        var actionType = dto.IsApproved ? "ProjectApprovalApproved" : "ProjectApprovalRejected";
        var levelTag = request.TotalLevels > 1 ? $" (level {request.Level} of {request.TotalLevels})" : "";

        await CreateNotificationAsync(
            project.ProjectManagerUserId,
            $"Project {decision}{levelTag}",
            $"{reviewerName} has {decision.ToLower()} project {project.ProjectCode} — {project.Name}{levelTag}",
            actionType,
            project,
            reviewerUserId,
            reviewerName);

        var pmEmail = await GetUserEmailAsync(project.ProjectManagerUserId);
        if (pmEmail != null)
        {
            await SendEmailSafeAsync("ProjectApproval.Reviewed", EmailPlaceholders.Encode(new Dictionary<string, string>
            {
                ["ProjectCode"] = project.ProjectCode,
                ["ProjectName"] = project.Name,
                ["Decision"] = decision,
                ["ReviewedByName"] = reviewerName,
                ["ReviewComment"] = request.ReviewComment ?? "",
                ["ProjectUrl"] = $"{_appSettings.FrontendBaseUrl}/projects/{project.Id}",
            }), pmEmail, project.Id, reviewerUserId);
        }

        // If we advanced the chain, ping the next approver.
        if (advanceRequest is not null && nextApprover is not null)
        {
            await CreateNotificationAsync(
                nextApprover.Id,
                "Project Approval Requested",
                $"{request.RequestedByDisplayName}'s project {project.ProjectCode} — {project.Name} " +
                $"is awaiting your approval (level {advanceRequest.Level} of {advanceRequest.TotalLevels})",
                "ProjectApprovalRequested",
                project,
                reviewerUserId,
                reviewerName);

            if (!string.IsNullOrEmpty(nextApprover.Email))
            {
                await SendEmailSafeAsync("ProjectApproval.Requested", EmailPlaceholders.Encode(new Dictionary<string, string>
                {
                    ["ProjectCode"] = project.ProjectCode,
                    ["ProjectName"] = project.Name,
                    ["RequestedByName"] = request.RequestedByDisplayName,
                    ["Reason"] = advanceRequest.Reason,
                    ["ProjectUrl"] = $"{_appSettings.FrontendBaseUrl}/projects/{project.Id}",
                    ["ApprovalUrl"] = $"{_appSettings.FrontendBaseUrl}/approvals",
                }), nextApprover.Email, project.Id, reviewerUserId);
            }
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
        // For chain-aware rows, show only requests where the *current* assigned
        // reviewer is the user. For legacy rows (no AssignedReviewerUserId),
        // fall back to "user is a sponsor or PM's line manager".
        var legacyReviewableProjectIds = await GetReviewableProjectIdsAsync(userId);

        var requests = await _db.ProjectApprovalRequests
            .Include(r => r.Project)
            .Where(r => r.Status == ProjectApprovalStatus.Pending
                && (r.AssignedReviewerUserId == userId
                    || (r.AssignedReviewerUserId == null
                        && legacyReviewableProjectIds.Contains(r.ProjectId))))
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
        var legacyReviewableProjectIds = await GetReviewableProjectIdsAsync(userId);

        // WO-2026-11 P-2b (fix 8): only requests on projects the caller can see —
        // the same rows pending-reviews lists (its Include drops hidden projects).
        var count = await _db.ProjectApprovalRequests
            .CountAsync(r => r.Status == ProjectApprovalStatus.Pending
                && (r.AssignedReviewerUserId == userId
                    || (r.AssignedReviewerUserId == null
                        && legacyReviewableProjectIds.Contains(r.ProjectId)))
                && _db.Projects.Any(p => p.Id == r.ProjectId));

        return new ProjectApprovalSummaryDto { PendingProjectApprovals = count };
    }

    public async Task<bool> CanReviewProjectAsync(Guid projectId, string userId)
    {
        // New workspace-chain model: user must be the assigned reviewer on the
        // currently-pending request for this project. Falls through to the
        // legacy resolver (sponsors + PM's line manager) for older rows whose
        // AssignedReviewerUserId is null.
        var pending = await _db.ProjectApprovalRequests
            .AsNoTracking()
            .Where(r => r.ProjectId == projectId && r.Status == ProjectApprovalStatus.Pending)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync();

        if (pending is not null && !string.IsNullOrEmpty(pending.AssignedReviewerUserId))
            return pending.AssignedReviewerUserId == userId;

        // Legacy fallback
        var isSponsor = await _db.ProjectSponsors
            .AnyAsync(s => s.ProjectId == projectId && s.UserId == userId);
        if (isSponsor) return true;

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
            .Select(p => new { p.Status, p.ProjectManagerUserId, p.ProjectType, p.WorkspaceId })
            .FirstOrDefaultAsync();

        if (project is null)
        {
            result.IsValid = false;
            result.Errors.Add("Project not found.");
            return result;
        }

        if (project.Status != ProjectStatus.Draft)
            result.Errors.Add("Project can only be submitted for approval when in Draft status.");

        if (!await CanSubmitAsync(projectId, project.ProjectManagerUserId, userId))   // WO-2026-11 P-2 (D3), P-2b (B5)
            result.Errors.Add(SubmitNotAllowedMessage);

        var hasPending = await _db.ProjectApprovalRequests
            .AnyAsync(r => r.ProjectId == projectId && r.Status == ProjectApprovalStatus.Pending);
        if (hasPending)
            result.Errors.Add("A pending approval request already exists for this project.");

        if (project.ProjectType == ProjectType.Strategic)
        {
            // The submit routes to the chain's first approver (unchanged); WO-2026-11 P-2b:
            // the validation now also names a first approver who is no longer an active
            // user, which the submit refuses (422) — before, only the submit said so.
            var firstApprover = await _db.WorkspaceStrategicProjectApprovers
                .AsNoTracking()
                .Where(a => a.WorkspaceId == project.WorkspaceId)
                .OrderBy(a => a.Order)
                .Select(a => new { a.UserId, a.UserName })
                .FirstOrDefaultAsync();
            if (firstApprover is null)
                result.Errors.Add(
                    "This workspace has no Strategic Project Approvers configured. " +
                    "Add at least one approver on the workspace settings page before submitting a strategic project for approval.");
            else
            {
                var approverUser = await _db.Users.AsNoTracking()
                    .Where(u => u.Id == firstApprover.UserId)
                    .Select(u => new { u.IsActive, u.FirstName, u.LastName })
                    .FirstOrDefaultAsync();
                if (approverUser is null || !approverUser.IsActive)
                {
                    var name = approverUser is null
                        ? firstApprover.UserName
                        : $"{approverUser.FirstName} {approverUser.LastName}".Trim();
                    result.Errors.Add(FirstApproverInactiveMessage(string.IsNullOrWhiteSpace(name) ? firstApprover.UserId : name));
                }
            }
        }

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

    /// <summary>WO-2026-11 P-2b: validate-submit's reason for an inactive (or removed) first approver.</summary>
    public static string FirstApproverInactiveMessage(string approverName) =>
        $"The first approver in this workspace's Strategic Project Approvers chain ({approverName}) is not an active user. " +
        "Change the chain on the workspace settings page before submitting a strategic project for approval.";

    public const string SubmitNotAllowedMessage =
        "Only the project's project managers or an Admin can submit it for approval.";

    /// <summary>
    /// WO-2026-11 P-2b (B5): who may press Submit — <see cref="ProjectAccess.CanSubmit"/>
    /// (a PM of the project, or the Admin role where the project is seen at workspace
    /// level). Only the request user's own roles are known; for anyone else only the
    /// PM rule applies.
    /// </summary>
    private async Task<bool> CanSubmitAsync(Guid projectId, string leadProjectManagerUserId, string userId)
    {
        if (string.Equals(_db.CurrentUserId, userId, StringComparison.OrdinalIgnoreCase))
            return (await new ProjectAccessService(_db).GetAsync(projectId))?.CanSubmit == true;
        return await IsProjectManagerAsync(projectId, leadProjectManagerUserId, userId);
    }

    /// <summary>
    /// WO-2026-11 P-2 (decision D3): the lead PM (<c>ProjectManagerUserId</c>) or an
    /// additional project manager (<c>ProjectMembers</c>, role ProjectManager) may
    /// submit. Only who may press Submit changes — the request is routed exactly as
    /// before, from the lead PM.
    /// </summary>
    private async Task<bool> IsProjectManagerAsync(Guid projectId, string leadProjectManagerUserId, string userId)
        => string.Equals(leadProjectManagerUserId, userId, StringComparison.OrdinalIgnoreCase)
           || await _db.ProjectMembers.AnyAsync(m =>
                  m.ProjectId == projectId
                  && m.UserId == userId
                  && m.Role == ProjectMemberRole.ProjectManager);

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

    /// <summary>
    /// Returns the strategic approver chain (in order) for a project's
    /// workspace, as (userId, email, displayName) tuples — used to seed
    /// informational notifications when a strategic project is created or
    /// submitted. Empty when the workspace has no chain configured.
    /// </summary>
    private async Task<List<(string? UserId, string Email, string DisplayName)>> ResolveStrategicApproverInfosAsync(Project project)
    {
        var result = new List<(string? UserId, string Email, string DisplayName)>();
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var chainUserIds = await _db.WorkspaceStrategicProjectApprovers
            .AsNoTracking()
            .Where(a => a.WorkspaceId == project.WorkspaceId)
            .OrderBy(a => a.Order)
            .Select(a => a.UserId)
            .ToListAsync();

        if (chainUserIds.Count == 0) return result;

        var users = await _db.Users.AsNoTracking()
            .Where(u => chainUserIds.Contains(u.Id) && u.IsActive)
            .ToListAsync();

        foreach (var id in chainUserIds)
        {
            var u = users.FirstOrDefault(x => x.Id == id);
            if (u is null) continue;
            if (!string.IsNullOrEmpty(u.Email) && seenEmails.Add(u.Email))
                result.Add((u.Id, u.Email, u.FullName ?? "Approver"));
        }
        return result;
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
            AssignedReviewerUserId = r.AssignedReviewerUserId,
            AssignedReviewerDisplayName = r.AssignedReviewerDisplayName,
            Status = r.Status.ToString(),
            Reason = r.Reason,
            ReviewComment = r.ReviewComment,
            CreatedAt = r.CreatedAt,
            ReviewedAt = r.ReviewedAt,
            Level = r.Level,
            TotalLevels = r.TotalLevels,
        };
    }
}
