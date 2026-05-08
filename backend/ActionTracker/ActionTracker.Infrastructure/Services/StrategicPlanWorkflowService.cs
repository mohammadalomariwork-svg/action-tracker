using ActionTracker.Application.Common;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.Notifications;
using ActionTracker.Application.Features.Notifications.DTOs;
using ActionTracker.Application.Features.StrategicPlans.Workflow;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Constants;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActionTracker.Infrastructure.Services;

/// <summary>
/// Strategic plan approval workflow (CR-001-D + CR-001-E). Mirrors
/// <see cref="ProjectWorkflowService"/>: submit transitions Draft to
/// PendingApproval, review approves/rejects, revision opens a re-approval
/// loop without changing the plan's status.
/// </summary>
public class StrategicPlanWorkflowService : IStrategicPlanWorkflowService
{
    private readonly IAppDbContext _db;
    private readonly IApprovalAuthorityResolver _approvalAuthority;
    private readonly IStrategicScopeService _scopeService;
    private readonly INotificationService _notificationService;
    private readonly IEmailSender _emailSender;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppSettings _appSettings;
    private readonly ILogger<StrategicPlanWorkflowService> _logger;

    public StrategicPlanWorkflowService(
        IAppDbContext db,
        IApprovalAuthorityResolver approvalAuthority,
        IStrategicScopeService scopeService,
        INotificationService notificationService,
        IEmailSender emailSender,
        UserManager<ApplicationUser> userManager,
        IOptions<AppSettings> appSettings,
        ILogger<StrategicPlanWorkflowService> logger)
    {
        _db = db;
        _approvalAuthority = approvalAuthority;
        _scopeService = scopeService;
        _notificationService = notificationService;
        _emailSender = emailSender;
        _userManager = userManager;
        _appSettings = appSettings.Value;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Submit / Request Revision / Review
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<StrategicPlanApprovalRequestDto> SubmitAsync(
        SubmitStrategicPlanApprovalRequestDto dto, string currentUserId, CancellationToken ct = default)
    {
        var plan = await _db.StrategicPlans
            .Include(p => p.OrgUnit)
            .Include(p => p.Objectives)
            .FirstOrDefaultAsync(p => p.Id == dto.PlanId, ct)
            ?? throw new KeyNotFoundException($"Strategic plan '{dto.PlanId}' not found.");

        if (plan.Status != StrategicPlanStatus.Draft)
            throw new ArgumentException("Plan can only be submitted for approval from Draft status.");

        await _scopeService.EnsureCanWriteAsync(currentUserId, plan.OrgUnitId, ct);

        var hasPending = await _db.StrategicPlanApprovalRequests
            .AnyAsync(r => r.StrategicPlanId == dto.PlanId
                           && r.Status == StrategicPlanApprovalStatus.Pending, ct);
        if (hasPending)
            throw new ArgumentException("A pending approval request already exists for this plan.");

        // Submission rule: the plan must have at least one strategic objective,
        // and every objective linked to the plan (including those created
        // against descendant org units via the subtree picker) must have at
        // least one *non-deleted* KPI. Filtering KPIs explicitly defends the
        // rule even if the global query filter on Kpi gets disabled by a
        // future caller (e.g. an `IgnoreQueryFilters()` mistake).
        var objectiveIds = plan.Objectives.Where(o => !o.IsDeleted).Select(o => o.Id).ToList();
        if (objectiveIds.Count == 0)
            throw new ArgumentException("The plan must have at least one strategic objective before submitting for approval.");

        var objectivesWithKpis = await _db.Kpis
            .Where(k => objectiveIds.Contains(k.StrategicObjectiveId) && !k.IsDeleted)
            .Select(k => k.StrategicObjectiveId)
            .Distinct()
            .ToListAsync(ct);

        var emptyObjectiveIds = objectiveIds.Except(objectivesWithKpis).ToList();
        if (emptyObjectiveIds.Count > 0)
        {
            var emptyNames = plan.Objectives
                .Where(o => emptyObjectiveIds.Contains(o.Id))
                .Select(o => o.ObjectiveCode ?? o.Statement)
                .ToList();
            throw new ArgumentException(
                $"Every strategic objective (including sub-unit ones) must have at least one KPI before submitting. Missing: {string.Join(", ", emptyNames)}.");
        }

        // Each KPI must have a complete set of targets for the plan's first
        // year, sized by its measurement period (Yearly=1, Semi-Annual=2,
        // Quarterly=4, Monthly=12). We check the *months* match the slot
        // schedule the UI uses, not just the count, so 4 January-only rows
        // can't satisfy a Quarterly KPI. Subsequent years aren't required at
        // submit time — multi-year plans typically build out year N+1 later.
        var kpiList = await _db.Kpis
            .Where(k => objectiveIds.Contains(k.StrategicObjectiveId) && !k.IsDeleted)
            .Select(k => new { k.Id, k.KpiNumber, k.Period, k.StrategicObjectiveId })
            .ToListAsync(ct);

        if (kpiList.Count > 0)
        {
            var kpiIds = kpiList.Select(k => k.Id).ToList();
            var monthsByKpi = await _db.KpiTargets
                .Where(t => kpiIds.Contains(t.KpiId)
                         && t.Year == plan.StartYear
                         && t.Target != null)
                .GroupBy(t => t.KpiId)
                .Select(g => new { KpiId = g.Key, Months = g.Select(t => t.Month).ToList() })
                .ToListAsync(ct);
            var monthsLookup = monthsByKpi.ToDictionary(x => x.KpiId, x => x.Months);

            // Code-by-objective for nicer error labels — ObjectiveCode is on
            // the StrategicObjective entity, not the Kpi row.
            var codesByObjId = plan.Objectives.ToDictionary(
                o => o.Id, o => o.ObjectiveCode ?? o.Statement);

            var incompleteLabels = new List<string>();
            foreach (var k in kpiList)
            {
                var requiredMonths = RequiredMonthsForPeriod(k.Period);
                monthsLookup.TryGetValue(k.Id, out var have);
                have ??= new List<int>();
                var missing = requiredMonths.Except(have).ToList();
                if (missing.Count > 0)
                {
                    var code = codesByObjId.TryGetValue(k.StrategicObjectiveId, out var c) ? c : "?";
                    incompleteLabels.Add(
                        $"{code}-K{k.KpiNumber} ({have.Count}/{requiredMonths.Length} for {plan.StartYear})");
                }
            }

            if (incompleteLabels.Count > 0)
                throw new ArgumentException(
                    $"Every KPI must have its full set of targets entered for {plan.StartYear} (Yearly = 1, Semi-Annual = 2, Quarterly = 4, Monthly = 12 — slots must match the period schedule). Incomplete: {string.Join(", ", incompleteLabels)}.");
        }

        // Resolve the chain. If the plan has configured approvers, we use those
        // (multi-level sequential — only the level-1 reviewer gets a Pending row;
        // the workflow advances on each Approve). Otherwise we fall back to the
        // legacy single-level resolver (CR-001-H level-3 head).
        var chain = await _db.StrategicPlanApprovers
            .AsNoTracking()
            .Where(a => a.StrategicPlanId == plan.Id)
            .OrderBy(a => a.Order)
            .ToListAsync(ct);

        ApprovalAuthorityDto approver;
        int totalLevels;

        if (chain.Count > 0)
        {
            var firstUserId = chain[0].UserId;
            var firstUser = await _userManager.FindByIdAsync(firstUserId)
                ?? throw new InvalidOperationException(
                    "The first approver in the chain is no longer a valid user.");
            if (!firstUser.IsActive)
                throw new InvalidOperationException(
                    "The first approver in the chain is inactive.");

            approver = new ApprovalAuthorityDto(
                UserId: firstUser.Id,
                DisplayName: firstUser.FullName ?? firstUser.UserName ?? firstUserId,
                Email: firstUser.Email ?? string.Empty,
                SourceOrgUnitId: plan.OrgUnitId,
                SourceOrgUnitLevel: 0,
                SourceOrgUnitName: plan.OrgUnit?.Name ?? string.Empty);
            totalLevels = chain.Count;
        }
        else
        {
            approver = await _approvalAuthority.ResolvePlanApproverAsync(plan.OrgUnitId, ct)
                ?? throw new InvalidOperationException(
                    "No plan approver is configured. Either define an approval chain on the plan, or assign a head to the level-3 ancestor of this plan's org unit before submitting.");
            totalLevels = 1;
        }

        var requester = await _userManager.FindByIdAsync(currentUserId);
        var requesterName = requester?.FullName ?? "Unknown";

        var request = new StrategicPlanApprovalRequest
        {
            Id = Guid.NewGuid(),
            StrategicPlanId = plan.Id,
            RequestType = StrategicPlanApprovalRequestType.Initial,
            RequestedByUserId = currentUserId,
            RequestedByDisplayName = requesterName,
            AssignedReviewerUserId = approver.UserId,
            AssignedReviewerDisplayName = approver.DisplayName,
            Status = StrategicPlanApprovalStatus.Pending,
            Justification = (dto.Justification ?? string.Empty).Trim(),
            CreatedAt = DateTime.UtcNow,
            Level = 1,
            TotalLevels = totalLevels,
        };

        _db.StrategicPlanApprovalRequests.Add(request);
        plan.Status = StrategicPlanStatus.PendingApproval;
        plan.UpdatedAt = DateTime.UtcNow;
        plan.UpdatedBy = currentUserId;

        await _db.SaveChangesAsync(ct);

        await NotifySubmitAsync(plan, request, approver, "StrategicPlanApprovalRequested",
            "Strategic Plan Approval Requested",
            $"{requesterName} submitted '{plan.Name}' for approval");

        return MapToDto(request, plan);
    }

    public async Task<StrategicPlanApprovalRequestDto> RequestRevisionAsync(
        RequestStrategicPlanRevisionDto dto, string currentUserId, CancellationToken ct = default)
    {
        var plan = await _db.StrategicPlans
            .Include(p => p.OrgUnit)
            .FirstOrDefaultAsync(p => p.Id == dto.PlanId, ct)
            ?? throw new KeyNotFoundException($"Strategic plan '{dto.PlanId}' not found.");

        if (plan.Status != StrategicPlanStatus.Approved)
            throw new ArgumentException("Revision can only be requested for an Approved plan.");

        await _scopeService.EnsureCanWriteAsync(currentUserId, plan.OrgUnitId, ct);

        var hasPendingRevision = await _db.StrategicPlanApprovalRequests
            .AnyAsync(r => r.StrategicPlanId == plan.Id
                           && r.RequestType == StrategicPlanApprovalRequestType.Revision
                           && r.Status == StrategicPlanApprovalStatus.Pending, ct);
        if (hasPendingRevision)
            throw new ArgumentException("A revision request is already pending for this plan.");

        // Same multi-level routing rule as Submit — use the configured chain
        // when present, fall back to the legacy single-level resolver otherwise.
        var chain = await _db.StrategicPlanApprovers
            .AsNoTracking()
            .Where(a => a.StrategicPlanId == plan.Id)
            .OrderBy(a => a.Order)
            .ToListAsync(ct);

        ApprovalAuthorityDto approver;
        int totalLevels;
        if (chain.Count > 0)
        {
            var firstUser = await _userManager.FindByIdAsync(chain[0].UserId)
                ?? throw new InvalidOperationException(
                    "The first approver in the chain is no longer a valid user.");
            if (!firstUser.IsActive)
                throw new InvalidOperationException("The first approver in the chain is inactive.");
            approver = new ApprovalAuthorityDto(
                UserId: firstUser.Id,
                DisplayName: firstUser.FullName ?? firstUser.UserName ?? chain[0].UserId,
                Email: firstUser.Email ?? string.Empty,
                SourceOrgUnitId: plan.OrgUnitId,
                SourceOrgUnitLevel: 0,
                SourceOrgUnitName: plan.OrgUnit?.Name ?? string.Empty);
            totalLevels = chain.Count;
        }
        else
        {
            approver = await _approvalAuthority.ResolvePlanApproverAsync(plan.OrgUnitId, ct)
                ?? throw new InvalidOperationException(
                    "No plan approver is configured. Cannot route the revision request.");
            totalLevels = 1;
        }

        var requester = await _userManager.FindByIdAsync(currentUserId);
        var requesterName = requester?.FullName ?? "Unknown";

        var request = new StrategicPlanApprovalRequest
        {
            Id = Guid.NewGuid(),
            StrategicPlanId = plan.Id,
            RequestType = StrategicPlanApprovalRequestType.Revision,
            RequestedByUserId = currentUserId,
            RequestedByDisplayName = requesterName,
            AssignedReviewerUserId = approver.UserId,
            AssignedReviewerDisplayName = approver.DisplayName,
            Status = StrategicPlanApprovalStatus.Pending,
            Justification = (dto.Justification ?? string.Empty).Trim(),
            CreatedAt = DateTime.UtcNow,
            Level = 1,
            TotalLevels = totalLevels,
        };

        _db.StrategicPlanApprovalRequests.Add(request);
        await _db.SaveChangesAsync(ct);

        await NotifySubmitAsync(plan, request, approver, "StrategicPlanRevisionRequested",
            "Strategic Plan Revision Requested",
            $"{requesterName} requested a revision of '{plan.Name}'");

        return MapToDto(request, plan);
    }

    public async Task<StrategicPlanApprovalRequestDto> ReviewAsync(
        ReviewStrategicPlanApprovalRequestDto dto, string reviewerUserId, CancellationToken ct = default)
    {
        var request = await _db.StrategicPlanApprovalRequests
            .Include(r => r.StrategicPlan).ThenInclude(p => p.OrgUnit)
            .FirstOrDefaultAsync(r => r.Id == dto.RequestId, ct)
            ?? throw new KeyNotFoundException($"Approval request '{dto.RequestId}' not found.");

        if (request.Status != StrategicPlanApprovalStatus.Pending)
            throw new ArgumentException("This approval request has already been reviewed.");

        var plan = request.StrategicPlan;

        // Race protection — for Initial requests, the plan must still be PendingApproval.
        if (request.RequestType == StrategicPlanApprovalRequestType.Initial
            && plan.Status != StrategicPlanStatus.PendingApproval)
            throw new InvalidOperationException("Plan status no longer matches an in-flight initial approval request.");

        if (!await CanReviewInternalAsync(request, reviewerUserId))
            throw new UnauthorizedAccessException("You are not authorised to review this request.");

        if (!dto.IsApproved && string.IsNullOrWhiteSpace(dto.ReviewComment))
            throw new ArgumentException("A reviewer comment is required when rejecting a request.");

        var reviewer = await _userManager.FindByIdAsync(reviewerUserId);
        var reviewerName = reviewer?.FullName ?? "Reviewer";

        request.Status = dto.IsApproved
            ? StrategicPlanApprovalStatus.Approved
            : StrategicPlanApprovalStatus.Rejected;
        request.ReviewedByUserId = reviewerUserId;
        request.ReviewedByDisplayName = reviewerName;
        request.ReviewerComment = dto.ReviewComment?.Trim();
        request.ReviewedAt = DateTime.UtcNow;

        // Multi-level routing: when an *approval* is not the final level,
        // create a Pending row for the next approver in the chain and leave
        // plan-level state alone. Plan-level transitions only fire on the
        // last-level approval (Initial → Approved) or on any rejection.
        StrategicPlanApprovalRequest? advanceRequest = null;
        ApprovalAuthorityDto? nextApprover = null;
        var isFinalLevel = request.Level >= request.TotalLevels;

        if (dto.IsApproved && !isFinalLevel)
        {
            var nextLevel = request.Level + 1;
            var chain = await _db.StrategicPlanApprovers
                .AsNoTracking()
                .Where(a => a.StrategicPlanId == plan.Id && a.Order == nextLevel)
                .FirstOrDefaultAsync(ct);

            if (chain is null)
                throw new InvalidOperationException(
                    $"Approval chain is broken: no approver configured for level {nextLevel}.");

            var nextUser = await _userManager.FindByIdAsync(chain.UserId)
                ?? throw new InvalidOperationException(
                    $"Approver at level {nextLevel} is no longer a valid user.");
            if (!nextUser.IsActive)
                throw new InvalidOperationException(
                    $"Approver at level {nextLevel} is inactive.");

            advanceRequest = new StrategicPlanApprovalRequest
            {
                Id = Guid.NewGuid(),
                StrategicPlanId = plan.Id,
                RequestType = request.RequestType,
                RequestedByUserId = request.RequestedByUserId,
                RequestedByDisplayName = request.RequestedByDisplayName,
                AssignedReviewerUserId = nextUser.Id,
                AssignedReviewerDisplayName = nextUser.FullName ?? nextUser.UserName ?? nextUser.Id,
                Status = StrategicPlanApprovalStatus.Pending,
                Justification = request.Justification,
                CreatedAt = DateTime.UtcNow,
                Level = nextLevel,
                TotalLevels = request.TotalLevels,
            };
            _db.StrategicPlanApprovalRequests.Add(advanceRequest);

            nextApprover = new ApprovalAuthorityDto(
                UserId: nextUser.Id,
                DisplayName: advanceRequest.AssignedReviewerDisplayName!,
                Email: nextUser.Email ?? string.Empty,
                SourceOrgUnitId: plan.OrgUnitId,
                SourceOrgUnitLevel: 0,
                SourceOrgUnitName: plan.OrgUnit?.Name ?? string.Empty);
        }

        // Plan-level transitions only happen on Initial requests at the *final*
        // level (full approval) or any rejection. Revision approval/rejection
        // re-locks the plan via the lock guard but doesn't change Status.
        if (request.RequestType == StrategicPlanApprovalRequestType.Initial)
        {
            if (!dto.IsApproved)
            {
                plan.Status = StrategicPlanStatus.Draft;
                plan.UpdatedAt = DateTime.UtcNow;
                plan.UpdatedBy = reviewerUserId;
            }
            else if (isFinalLevel)
            {
                plan.Status = StrategicPlanStatus.Approved;
                plan.ApprovedAtUtc = DateTime.UtcNow;
                plan.ApprovedByUserId = reviewerUserId;
                plan.UpdatedAt = DateTime.UtcNow;
                plan.UpdatedBy = reviewerUserId;
            }
            // Intermediate-level approvals: plan stays PendingApproval.
        }

        await _db.SaveChangesAsync(ct);

        // Notification: this review's outcome (existing template), plus a
        // submit-style notification for the *next* level when we advanced.
        await NotifyReviewAsync(plan, request, reviewerName, dto.IsApproved);

        if (advanceRequest is not null && nextApprover is not null)
        {
            await NotifySubmitAsync(plan, advanceRequest, nextApprover,
                "StrategicPlanApprovalRequested",
                "Strategic Plan Approval Requested",
                $"{request.RequestedByDisplayName}'s plan '{plan.Name}' is awaiting your approval (level {advanceRequest.Level} of {advanceRequest.TotalLevels}).");
        }

        return MapToDto(request, plan);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Queries
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<List<StrategicPlanApprovalRequestDto>> GetForPlanAsync(Guid planId, CancellationToken ct = default)
    {
        var rows = await _db.StrategicPlanApprovalRequests
            .Include(r => r.StrategicPlan).ThenInclude(p => p.OrgUnit)
            .Where(r => r.StrategicPlanId == planId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return rows.Select(r => MapToDto(r, r.StrategicPlan)).ToList();
    }

    public async Task<List<StrategicPlanApprovalRequestDto>> GetPendingReviewsAsync(string userId, CancellationToken ct = default)
    {
        var isAdmin = await IsAdminAsync(userId);

        var query = _db.StrategicPlanApprovalRequests
            .Include(r => r.StrategicPlan).ThenInclude(p => p.OrgUnit)
            .Where(r => r.Status == StrategicPlanApprovalStatus.Pending);

        if (!isAdmin)
            query = query.Where(r => r.AssignedReviewerUserId == userId);

        var rows = await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return rows.Select(r => MapToDto(r, r.StrategicPlan)).ToList();
    }

    public async Task<List<StrategicPlanApprovalRequestDto>> GetMyRequestsAsync(string userId, CancellationToken ct = default)
    {
        var rows = await _db.StrategicPlanApprovalRequests
            .Include(r => r.StrategicPlan).ThenInclude(p => p.OrgUnit)
            .Where(r => r.RequestedByUserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return rows.Select(r => MapToDto(r, r.StrategicPlan)).ToList();
    }

    public async Task<StrategicPlanApprovalSummaryDto> GetPendingSummaryAsync(string userId, CancellationToken ct = default)
    {
        var isAdmin = await IsAdminAsync(userId);

        var query = _db.StrategicPlanApprovalRequests
            .Where(r => r.Status == StrategicPlanApprovalStatus.Pending);
        if (!isAdmin)
            query = query.Where(r => r.AssignedReviewerUserId == userId);

        var count = await query.CountAsync(ct);
        return new StrategicPlanApprovalSummaryDto { PendingPlanApprovals = count };
    }

    public async Task<bool> CanReviewAsync(Guid planId, string userId, CancellationToken ct = default)
    {
        var pending = await _db.StrategicPlanApprovalRequests
            .AsNoTracking()
            .Where(r => r.StrategicPlanId == planId && r.Status == StrategicPlanApprovalStatus.Pending)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (pending is null) return false;

        return await CanReviewInternalAsync(pending, userId);
    }

    public async Task<StrategicPlanLockStateDto> GetLockStateAsync(Guid planId, CancellationToken ct = default)
    {
        var plan = await _db.StrategicPlans
            .AsNoTracking()
            .Where(p => p.Id == planId)
            .Select(p => new { p.Id, p.Status })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException($"Strategic plan '{planId}' not found.");

        var pendingRevision = await _db.StrategicPlanApprovalRequests
            .AsNoTracking()
            .Where(r => r.StrategicPlanId == planId
                        && r.RequestType == StrategicPlanApprovalRequestType.Revision
                        && r.Status == StrategicPlanApprovalStatus.Pending)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(ct);

        var isLocked =
            plan.Status == StrategicPlanStatus.Archived
            || (plan.Status == StrategicPlanStatus.Approved && pendingRevision is null);

        return new StrategicPlanLockStateDto
        {
            PlanId = plan.Id,
            Status = plan.Status,
            IsLocked = isLocked,
            PendingRevisionId = pendingRevision,
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<bool> CanReviewInternalAsync(StrategicPlanApprovalRequest request, string userId)
    {
        if (string.IsNullOrEmpty(userId)) return false;
        if (request.AssignedReviewerUserId == userId) return true;
        return await IsAdminAsync(userId);
    }

    private async Task<bool> IsAdminAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return false;
        return await _userManager.IsInRoleAsync(user, AppRoles.Admin);
    }

    private async Task NotifySubmitAsync(
        StrategicPlan plan,
        StrategicPlanApprovalRequest request,
        ApprovalAuthorityDto approver,
        string actionType,
        string title,
        string message)
    {
        // PMO Heads (cc; in-app only)
        var pmoUsers = await _userManager.GetUsersInRoleAsync(AppRoles.PmoHead);
        var pmoIds = pmoUsers
            .Where(u => u.IsActive)
            .Select(u => u.Id)
            .ToHashSet();

        var planUrl = $"{_appSettings.FrontendBaseUrl}/strategy/org-units/{plan.OrgUnitId}";

        // In-app notification to assigned reviewer
        try
        {
            await _notificationService.CreateAsync(new CreateNotificationDto
            {
                UserId = approver.UserId,
                Title = title,
                Message = message,
                Type = "StrategicPlan",
                ActionType = actionType,
                RelatedEntityType = "StrategicPlan",
                RelatedEntityId = plan.Id,
                Url = planUrl,
                CreatedByUserId = request.RequestedByUserId,
                CreatedByDisplayName = request.RequestedByDisplayName,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending strategic plan submit notification to reviewer {UserId}", approver.UserId);
        }

        // Email to assigned reviewer
        if (!string.IsNullOrWhiteSpace(approver.Email))
        {
            try
            {
                await _emailSender.SendEmailAsync(
                    "StrategicPlan.Submitted",
                    new Dictionary<string, string>
                    {
                        ["PlanName"] = plan.Name,
                        ["OrgUnitName"] = plan.OrgUnit?.Name ?? string.Empty,
                        ["RequestedByName"] = request.RequestedByDisplayName,
                        ["Justification"] = request.Justification,
                        ["RequestType"] = request.RequestType.ToString(),
                        ["PlanUrl"] = planUrl,
                        ["ApprovalUrl"] = $"{_appSettings.FrontendBaseUrl}/approvals",
                    },
                    new List<string> { approver.Email },
                    "StrategicPlan",
                    plan.Id,
                    request.RequestedByUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending strategic plan submit email to {Email}", approver.Email);
            }
        }

        // PMO Heads — in-app only (Q12: cc, no email duplication)
        foreach (var pmoUserId in pmoIds.Where(id => id != approver.UserId && id != request.RequestedByUserId))
        {
            try
            {
                await _notificationService.CreateAsync(new CreateNotificationDto
                {
                    UserId = pmoUserId,
                    Title = title,
                    Message = message,
                    Type = "StrategicPlan",
                    ActionType = actionType,
                    RelatedEntityType = "StrategicPlan",
                    RelatedEntityId = plan.Id,
                    Url = planUrl,
                    CreatedByUserId = request.RequestedByUserId,
                    CreatedByDisplayName = request.RequestedByDisplayName,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending PMO cc notification for plan {Id}", plan.Id);
            }
        }

        // Self-notification (confirmation)
        try
        {
            await _notificationService.CreateAsync(new CreateNotificationDto
            {
                UserId = request.RequestedByUserId,
                Title = "Submitted for Approval",
                Message = $"You submitted '{plan.Name}' for approval.",
                Type = "StrategicPlan",
                ActionType = actionType,
                RelatedEntityType = "StrategicPlan",
                RelatedEntityId = plan.Id,
                Url = planUrl,
                CreatedByUserId = request.RequestedByUserId,
                CreatedByDisplayName = request.RequestedByDisplayName,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending self-confirmation notification for plan {Id}", plan.Id);
        }
    }

    private async Task NotifyReviewAsync(
        StrategicPlan plan,
        StrategicPlanApprovalRequest request,
        string reviewerName,
        bool isApproved)
    {
        var decision = isApproved ? "Approved" : "Rejected";
        var actionType = request.RequestType == StrategicPlanApprovalRequestType.Revision
            ? (isApproved ? "StrategicPlanRevisionApproved" : "StrategicPlanRevisionRejected")
            : (isApproved ? "StrategicPlanApprovalApproved" : "StrategicPlanApprovalRejected");
        var planUrl = $"{_appSettings.FrontendBaseUrl}/strategy/org-units/{plan.OrgUnitId}";

        // Notify submitter
        try
        {
            await _notificationService.CreateAsync(new CreateNotificationDto
            {
                UserId = request.RequestedByUserId,
                Title = $"Strategic Plan {decision}",
                Message = $"{reviewerName} has {decision.ToLower()} '{plan.Name}'.",
                Type = "StrategicPlan",
                ActionType = actionType,
                RelatedEntityType = "StrategicPlan",
                RelatedEntityId = plan.Id,
                Url = planUrl,
                CreatedByUserId = request.ReviewedByUserId,
                CreatedByDisplayName = reviewerName,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying submitter of plan review {Id}", plan.Id);
        }

        // Email submitter
        var submitter = await _userManager.FindByIdAsync(request.RequestedByUserId);
        if (submitter?.Email is { Length: > 0 } email)
        {
            try
            {
                await _emailSender.SendEmailAsync(
                    "StrategicPlan.Reviewed",
                    new Dictionary<string, string>
                    {
                        ["PlanName"] = plan.Name,
                        ["OrgUnitName"] = plan.OrgUnit?.Name ?? string.Empty,
                        ["Decision"] = decision,
                        ["RequestType"] = request.RequestType.ToString(),
                        ["ReviewedByName"] = reviewerName,
                        ["ReviewerComment"] = request.ReviewerComment ?? string.Empty,
                        ["PlanUrl"] = planUrl,
                    },
                    new List<string> { email },
                    "StrategicPlan",
                    plan.Id,
                    request.ReviewedByUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error emailing submitter of plan review {Id}", plan.Id);
            }
        }

        // PMO Heads in-app cc
        var pmoUsers = await _userManager.GetUsersInRoleAsync(AppRoles.PmoHead);
        foreach (var pmo in pmoUsers.Where(u => u.IsActive && u.Id != request.RequestedByUserId))
        {
            try
            {
                await _notificationService.CreateAsync(new CreateNotificationDto
                {
                    UserId = pmo.Id,
                    Title = $"Strategic Plan {decision}",
                    Message = $"{reviewerName} has {decision.ToLower()} '{plan.Name}'.",
                    Type = "StrategicPlan",
                    ActionType = actionType,
                    RelatedEntityType = "StrategicPlan",
                    RelatedEntityId = plan.Id,
                    Url = planUrl,
                    CreatedByUserId = request.ReviewedByUserId,
                    CreatedByDisplayName = reviewerName,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying PMO of plan review {Id}", plan.Id);
            }
        }
    }

    /// <summary>
    /// Months that must have non-null `KpiTarget.Target` rows for a KPI of the
    /// given measurement period. Mirrors the slot schedule the
    /// `KpiTargetsComponent` UI uses so that "complete on the screen" matches
    /// "complete on submit".
    /// </summary>
    private static int[] RequiredMonthsForPeriod(MeasurementPeriod period) => period switch
    {
        MeasurementPeriod.Yearly     => new[] { 12 },
        MeasurementPeriod.SemiAnnual => new[] { 6, 12 },
        MeasurementPeriod.Quarterly  => new[] { 3, 6, 9, 12 },
        MeasurementPeriod.Monthly    => new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 },
        _                            => Array.Empty<int>(),
    };

    private static StrategicPlanApprovalRequestDto MapToDto(StrategicPlanApprovalRequest r, StrategicPlan plan) =>
        new()
        {
            Id = r.Id,
            StrategicPlanId = r.StrategicPlanId,
            PlanName = plan.Name,
            OrgUnitId = plan.OrgUnitId,
            OrgUnitName = plan.OrgUnit?.Name ?? string.Empty,
            RequestType = r.RequestType,
            Status = r.Status,
            RequestedByUserId = r.RequestedByUserId,
            RequestedByDisplayName = r.RequestedByDisplayName,
            AssignedReviewerUserId = r.AssignedReviewerUserId,
            AssignedReviewerDisplayName = r.AssignedReviewerDisplayName,
            ReviewedByUserId = r.ReviewedByUserId,
            ReviewedByDisplayName = r.ReviewedByDisplayName,
            Justification = r.Justification,
            ReviewerComment = r.ReviewerComment,
            CreatedAt = r.CreatedAt,
            ReviewedAt = r.ReviewedAt,
            Level = r.Level,
            TotalLevels = r.TotalLevels,
        };
}
