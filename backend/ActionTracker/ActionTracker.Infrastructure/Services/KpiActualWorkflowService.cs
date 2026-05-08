using ActionTracker.Application.Common;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.KpiActuals;
using ActionTracker.Application.Features.KpiActuals.DTOs;
using ActionTracker.Application.Features.KpiActuals.Interfaces;
using ActionTracker.Application.Features.Notifications;
using ActionTracker.Application.Features.Notifications.DTOs;
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
/// CR-001-F — Submit/Review workflow for KPI actuals. Mirrors the Strategic
/// Plan workflow (CR-001-D): the level-3 head from
/// <c>IApprovalAuthorityResolver</c> is the only valid reviewer (plus Admin).
/// </summary>
public class KpiActualWorkflowService : IKpiActualWorkflowService
{
    private readonly IAppDbContext _db;
    private readonly IApprovalAuthorityResolver _approvalAuthority;
    private readonly IStrategicScopeService _scopeService;
    private readonly INotificationService _notificationService;
    private readonly IEmailSender _emailSender;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly KpiActualService _kpiActualService;
    private readonly AppSettings _appSettings;
    private readonly ILogger<KpiActualWorkflowService> _logger;

    public KpiActualWorkflowService(
        IAppDbContext db,
        IApprovalAuthorityResolver approvalAuthority,
        IStrategicScopeService scopeService,
        INotificationService notificationService,
        IEmailSender emailSender,
        UserManager<ApplicationUser> userManager,
        KpiActualService kpiActualService,
        IOptions<AppSettings> appSettings,
        ILogger<KpiActualWorkflowService> logger)
    {
        _db = db;
        _approvalAuthority = approvalAuthority;
        _scopeService = scopeService;
        _notificationService = notificationService;
        _emailSender = emailSender;
        _userManager = userManager;
        _kpiActualService = kpiActualService;
        _appSettings = appSettings.Value;
        _logger = logger;
    }

    public async Task<KpiActualDto> SubmitAsync(
        SubmitKpiActualRequestDto dto, string currentUserId, CancellationToken ct = default)
    {
        var entity = await _db.KpiActuals
            .Include(a => a.KpiTarget)
                .ThenInclude(t => t.Kpi)
                    .ThenInclude(k => k.StrategicObjective)
                        .ThenInclude(o => o.OrgUnit)
            .FirstOrDefaultAsync(a => a.Id == dto.ActualId, ct)
            ?? throw new KeyNotFoundException($"KPI actual '{dto.ActualId}' not found.");

        if (entity.Status != KpiActualStatus.Draft)
            throw new ArgumentException("Only drafts can be submitted for approval.");

        var orgUnitId = entity.KpiTarget.Kpi.StrategicObjective.OrgUnitId;
        await _scopeService.EnsureCanWriteAsync(currentUserId, orgUnitId, ct);

        // Q7 — period must have ended.
        var endUtc = KpiPeriodCalendar.GetPeriodEndUtc(entity.PeriodType, entity.PeriodIndex, entity.Year);
        if (DateTime.UtcNow < endUtc)
            throw new ArgumentException(
                $"Cannot submit — the period {KpiPeriodCalendar.GetPeriodLabel(entity.PeriodType, entity.PeriodIndex)} {entity.Year} ends on {endUtc:yyyy-MM-dd}.");

        var approver = await _approvalAuthority.ResolvePlanApproverAsync(orgUnitId, ct)
            ?? throw new InvalidOperationException(
                "No reviewer is configured. The level-3 ancestor of this KPI's org unit must have a head assigned before actuals can be submitted.");

        var requester = await _userManager.FindByIdAsync(currentUserId);
        var requesterName = requester?.FullName ?? "Unknown";

        entity.Status = KpiActualStatus.PendingApproval;
        entity.SubmittedByUserId = currentUserId;
        entity.SubmittedByDisplayName = requesterName;
        entity.SubmittedAtUtc = DateTime.UtcNow;
        entity.AssignedReviewerUserId = approver.UserId;
        entity.AssignedReviewerDisplayName = approver.DisplayName;
        entity.ModifiedAtUtc = DateTime.UtcNow;
        entity.ModifiedByUserId = currentUserId;

        await _db.SaveChangesAsync(ct);

        await NotifySubmitAsync(entity, approver, requesterName);

        return await _kpiActualService.BuildDtoAsync(entity.Id, ct)
               ?? throw new InvalidOperationException("Failed to reload actual after submit.");
    }

    public async Task<KpiActualDto> ReviewAsync(
        ReviewKpiActualRequestDto dto, string reviewerUserId, CancellationToken ct = default)
    {
        var entity = await _db.KpiActuals
            .Include(a => a.KpiTarget)
                .ThenInclude(t => t.Kpi)
                    .ThenInclude(k => k.StrategicObjective)
                        .ThenInclude(o => o.OrgUnit)
            .FirstOrDefaultAsync(a => a.Id == dto.ActualId, ct)
            ?? throw new KeyNotFoundException($"KPI actual '{dto.ActualId}' not found.");

        if (entity.Status != KpiActualStatus.PendingApproval)
            throw new ArgumentException("This actual is not pending approval.");

        if (!await CanReviewInternalAsync(entity, reviewerUserId))
            throw new UnauthorizedAccessException("You are not authorised to review this actual.");

        if (!dto.IsApproved && string.IsNullOrWhiteSpace(dto.ReviewComment))
            throw new ArgumentException("A reviewer comment is required when rejecting an actual.");

        var reviewer = await _userManager.FindByIdAsync(reviewerUserId);
        var reviewerName = reviewer?.FullName ?? "Reviewer";

        entity.Status = dto.IsApproved ? KpiActualStatus.Approved : KpiActualStatus.Rejected;
        entity.ReviewedByUserId = reviewerUserId;
        entity.ReviewedByDisplayName = reviewerName;
        entity.ReviewerComment = dto.ReviewComment?.Trim();
        entity.ReviewedAtUtc = DateTime.UtcNow;
        entity.ModifiedAtUtc = DateTime.UtcNow;
        entity.ModifiedByUserId = reviewerUserId;

        await _db.SaveChangesAsync(ct);

        await NotifyReviewAsync(entity, reviewerName, dto.IsApproved);

        return await _kpiActualService.BuildDtoAsync(entity.Id, ct)
               ?? throw new InvalidOperationException("Failed to reload actual after review.");
    }

    public async Task<List<KpiActualDto>> GetPendingReviewsAsync(string userId, CancellationToken ct = default)
    {
        var isAdmin = await IsAdminAsync(userId);
        var query = _db.KpiActuals
            .AsNoTracking()
            .Include(a => a.KpiTarget)
                .ThenInclude(t => t.Kpi)
                    .ThenInclude(k => k.StrategicObjective)
                        .ThenInclude(o => o.OrgUnit)
            .Where(a => a.Status == KpiActualStatus.PendingApproval);

        if (!isAdmin)
            query = query.Where(a => a.AssignedReviewerUserId == userId);

        var rows = await query.OrderByDescending(a => a.SubmittedAtUtc).ToListAsync(ct);
        return rows.Select(MapWithLoadedNav).ToList();
    }

    public async Task<List<KpiActualDto>> GetMyRequestsAsync(string userId, CancellationToken ct = default)
    {
        var rows = await _db.KpiActuals
            .AsNoTracking()
            .Include(a => a.KpiTarget)
                .ThenInclude(t => t.Kpi)
                    .ThenInclude(k => k.StrategicObjective)
                        .ThenInclude(o => o.OrgUnit)
            .Where(a => a.SubmittedByUserId == userId || a.CreatedByUserId == userId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(ct);

        return rows.Select(MapWithLoadedNav).ToList();
    }

    public async Task<KpiActualSummaryDto> GetPendingSummaryAsync(string userId, CancellationToken ct = default)
    {
        var isAdmin = await IsAdminAsync(userId);
        var q = _db.KpiActuals.AsNoTracking()
            .Where(a => a.Status == KpiActualStatus.PendingApproval);
        if (!isAdmin)
            q = q.Where(a => a.AssignedReviewerUserId == userId);

        var count = await q.CountAsync(ct);
        return new KpiActualSummaryDto { PendingKpiActuals = count };
    }

    public async Task<bool> CanReviewAsync(Guid actualId, string userId, CancellationToken ct = default)
    {
        var entity = await _db.KpiActuals.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == actualId && a.Status == KpiActualStatus.PendingApproval, ct);
        if (entity is null) return false;
        return await CanReviewInternalAsync(entity, userId);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────────

    private async Task<bool> CanReviewInternalAsync(KpiActual entity, string userId)
    {
        if (string.IsNullOrEmpty(userId)) return false;
        if (entity.AssignedReviewerUserId == userId) return true;
        return await IsAdminAsync(userId);
    }

    private async Task<bool> IsAdminAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return false;
        return await _userManager.IsInRoleAsync(user, AppRoles.Admin);
    }

    private static KpiActualDto MapWithLoadedNav(KpiActual a)
    {
        var kpi = a.KpiTarget.Kpi;
        var orgUnitId = kpi.StrategicObjective.OrgUnitId;
        var orgUnitName = kpi.StrategicObjective.OrgUnit?.Name ?? string.Empty;
        return KpiActualService.MapToDto(a, a.KpiTarget, kpi, orgUnitId, orgUnitName, 0);
    }

    private async Task NotifySubmitAsync(KpiActual entity, ApprovalAuthorityDto approver, string requesterName)
    {
        var orgUnitName = entity.KpiTarget.Kpi.StrategicObjective.OrgUnit?.Name ?? string.Empty;
        var orgUnitId = entity.KpiTarget.Kpi.StrategicObjective.OrgUnitId;
        var label = KpiPeriodCalendar.GetPeriodLabel(entity.PeriodType, entity.PeriodIndex);
        var url = $"{_appSettings.FrontendBaseUrl}/strategy/org-units/{orgUnitId}";

        var title = "KPI Actual Awaiting Approval";
        var message = $"{requesterName} submitted a KPI actual for {entity.KpiTarget.Kpi.Name} ({label} {entity.Year}).";

        // Reviewer in-app
        try
        {
            await _notificationService.CreateAsync(new CreateNotificationDto
            {
                UserId = approver.UserId,
                Title = title,
                Message = message,
                Type = "KpiActual",
                ActionType = "KpiActualSubmitted",
                RelatedEntityType = "KpiActual",
                RelatedEntityId = entity.Id,
                Url = url,
                CreatedByUserId = entity.SubmittedByUserId,
                CreatedByDisplayName = requesterName,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending KPI actual submit notification to reviewer {UserId}", approver.UserId);
        }

        // Reviewer email
        if (!string.IsNullOrWhiteSpace(approver.Email))
        {
            try
            {
                await _emailSender.SendEmailAsync(
                    "KpiActual.Submitted",
                    new Dictionary<string, string>
                    {
                        ["KpiName"] = entity.KpiTarget.Kpi.Name,
                        ["OrgUnitName"] = orgUnitName,
                        ["PeriodLabel"] = label,
                        ["Year"] = entity.Year.ToString(),
                        ["RequestedByName"] = requesterName,
                        ["ActualValue"] = entity.ActualValue.ToString("F2"),
                        ["PlanUrl"] = url,
                        ["ApprovalUrl"] = $"{_appSettings.FrontendBaseUrl}/approvals",
                    },
                    new List<string> { approver.Email },
                    "KpiActual",
                    entity.Id,
                    entity.SubmittedByUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending KPI actual submit email to {Email}", approver.Email);
            }
        }

        // PMO Heads in-app cc
        var pmoUsers = await _userManager.GetUsersInRoleAsync(AppRoles.PmoHead);
        foreach (var pmo in pmoUsers
            .Where(u => u.IsActive && u.Id != approver.UserId && u.Id != entity.SubmittedByUserId))
        {
            try
            {
                await _notificationService.CreateAsync(new CreateNotificationDto
                {
                    UserId = pmo.Id,
                    Title = title,
                    Message = message,
                    Type = "KpiActual",
                    ActionType = "KpiActualSubmitted",
                    RelatedEntityType = "KpiActual",
                    RelatedEntityId = entity.Id,
                    Url = url,
                    CreatedByUserId = entity.SubmittedByUserId,
                    CreatedByDisplayName = requesterName,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending PMO cc notification for KPI actual {Id}", entity.Id);
            }
        }

        // Self confirmation
        if (!string.IsNullOrEmpty(entity.SubmittedByUserId))
        {
            try
            {
                await _notificationService.CreateAsync(new CreateNotificationDto
                {
                    UserId = entity.SubmittedByUserId,
                    Title = "Submitted for Approval",
                    Message = $"You submitted a KPI actual for {entity.KpiTarget.Kpi.Name} ({label} {entity.Year}).",
                    Type = "KpiActual",
                    ActionType = "KpiActualSubmitted",
                    RelatedEntityType = "KpiActual",
                    RelatedEntityId = entity.Id,
                    Url = url,
                    CreatedByUserId = entity.SubmittedByUserId,
                    CreatedByDisplayName = requesterName,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending self-confirmation for KPI actual {Id}", entity.Id);
            }
        }
    }

    private async Task NotifyReviewAsync(KpiActual entity, string reviewerName, bool isApproved)
    {
        var decision = isApproved ? "Approved" : "Rejected";
        var actionType = isApproved ? "KpiActualApproved" : "KpiActualRejected";
        var orgUnitId = entity.KpiTarget.Kpi.StrategicObjective.OrgUnitId;
        var orgUnitName = entity.KpiTarget.Kpi.StrategicObjective.OrgUnit?.Name ?? string.Empty;
        var label = KpiPeriodCalendar.GetPeriodLabel(entity.PeriodType, entity.PeriodIndex);
        var url = $"{_appSettings.FrontendBaseUrl}/strategy/org-units/{orgUnitId}";

        if (!string.IsNullOrEmpty(entity.SubmittedByUserId))
        {
            try
            {
                await _notificationService.CreateAsync(new CreateNotificationDto
                {
                    UserId = entity.SubmittedByUserId,
                    Title = $"KPI Actual {decision}",
                    Message = $"{reviewerName} has {decision.ToLower()} your actual for {entity.KpiTarget.Kpi.Name} ({label} {entity.Year}).",
                    Type = "KpiActual",
                    ActionType = actionType,
                    RelatedEntityType = "KpiActual",
                    RelatedEntityId = entity.Id,
                    Url = url,
                    CreatedByUserId = entity.ReviewedByUserId,
                    CreatedByDisplayName = reviewerName,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying submitter of KPI actual review {Id}", entity.Id);
            }

            var submitter = await _userManager.FindByIdAsync(entity.SubmittedByUserId);
            if (submitter?.Email is { Length: > 0 } email)
            {
                try
                {
                    await _emailSender.SendEmailAsync(
                        "KpiActual.Reviewed",
                        new Dictionary<string, string>
                        {
                            ["KpiName"] = entity.KpiTarget.Kpi.Name,
                            ["OrgUnitName"] = orgUnitName,
                            ["PeriodLabel"] = label,
                            ["Year"] = entity.Year.ToString(),
                            ["Decision"] = decision,
                            ["ReviewedByName"] = reviewerName,
                            ["ReviewerComment"] = entity.ReviewerComment ?? string.Empty,
                            ["PlanUrl"] = url,
                        },
                        new List<string> { email },
                        "KpiActual",
                        entity.Id,
                        entity.ReviewedByUserId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error emailing submitter of KPI actual review {Id}", entity.Id);
                }
            }
        }

        // PMO Heads cc
        var pmoUsers = await _userManager.GetUsersInRoleAsync(AppRoles.PmoHead);
        foreach (var pmo in pmoUsers.Where(u => u.IsActive && u.Id != entity.SubmittedByUserId))
        {
            try
            {
                await _notificationService.CreateAsync(new CreateNotificationDto
                {
                    UserId = pmo.Id,
                    Title = $"KPI Actual {decision}",
                    Message = $"{reviewerName} has {decision.ToLower()} the actual for {entity.KpiTarget.Kpi.Name} ({label} {entity.Year}).",
                    Type = "KpiActual",
                    ActionType = actionType,
                    RelatedEntityType = "KpiActual",
                    RelatedEntityId = entity.Id,
                    Url = url,
                    CreatedByUserId = entity.ReviewedByUserId,
                    CreatedByDisplayName = reviewerName,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying PMO of KPI actual review {Id}", entity.Id);
            }
        }
    }
}
