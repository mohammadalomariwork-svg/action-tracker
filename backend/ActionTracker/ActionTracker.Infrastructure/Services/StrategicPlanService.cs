using ActionTracker.Application.Common;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.Notifications;
using ActionTracker.Application.Features.Notifications.DTOs;
using ActionTracker.Application.Features.StrategicPlans.DTOs;
using ActionTracker.Application.Features.StrategicPlans.Interfaces;
using ActionTracker.Application.Features.StrategicPlans.Workflow;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Constants;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using ActionTracker.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActionTracker.Infrastructure.Services;

public class StrategicPlanService : IStrategicPlanService
{
    private readonly AppDbContext _context;
    private readonly IUserLookupService _userLookup;
    private readonly ILogger<StrategicPlanService> _logger;
    private readonly IStrategicScopeService _scopeService;
    private readonly IOrgUnitScopeResolver _orgUnitScopeResolver;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AppSettings _appSettings;
    private readonly IStrategicPlanLockGuard _lockGuard;

    public StrategicPlanService(
        AppDbContext context,
        IUserLookupService userLookup,
        ILogger<StrategicPlanService> logger,
        IStrategicScopeService scopeService,
        IOrgUnitScopeResolver orgUnitScopeResolver,
        UserManager<ApplicationUser> userManager,
        IServiceScopeFactory scopeFactory,
        IOptions<AppSettings> appSettings,
        IStrategicPlanLockGuard lockGuard)
    {
        _context = context;
        _userLookup = userLookup;
        _logger = logger;
        _scopeService = scopeService;
        _orgUnitScopeResolver = orgUnitScopeResolver;
        _userManager = userManager;
        _scopeFactory = scopeFactory;
        _appSettings = appSettings.Value;
        _lockGuard = lockGuard;
    }

    public async Task<StrategicPlanListResponseDto> GetAllAsync(
        int page,
        int pageSize,
        Guid? orgUnitId = null,
        int? status = null,
        int? year = null,
        bool includeDeleted = false,
        string? currentUserId = null,
        CancellationToken ct = default)
    {
        var query = includeDeleted
            ? _context.StrategicPlans.IgnoreQueryFilters()
            : _context.StrategicPlans.AsQueryable();

        query = query
            .Include(p => p.OrgUnit)
            .Include(p => p.Objectives);

        if (orgUnitId.HasValue)
            query = query.Where(p => p.OrgUnitId == orgUnitId.Value);

        if (status.HasValue)
            query = query.Where(p => (int)p.Status == status.Value);

        if (year.HasValue)
            query = query.Where(p => p.StartYear <= year.Value && p.StartYear + p.DurationYears - 1 >= year.Value);

        if (currentUserId is not null)
        {
            var visible = await _scopeService.GetVisibleOrgUnitIdsAsync(currentUserId, ct);
            if (visible is not null)
            {
                if (visible.Count == 0)
                    return new StrategicPlanListResponseDto { Plans = new(), TotalCount = 0, Page = page, PageSize = pageSize };
                query = query.Where(p => visible.Contains(p.OrgUnitId));
            }
        }

        query = query.OrderByDescending(p => p.StartYear).ThenBy(p => p.Name);

        var totalCount = await query.CountAsync(ct);
        var plans = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var names = await ResolveNamesAsync(plans, ct);

        return new StrategicPlanListResponseDto
        {
            Plans = plans.Select(p => MapToDto(p, names)).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<StrategicPlanDto?> GetByIdAsync(Guid id, string? currentUserId = null, CancellationToken ct = default)
    {
        var plan = await _context.StrategicPlans
            .IgnoreQueryFilters()
            .Include(p => p.OrgUnit)
            .Include(p => p.Objectives)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (plan is null) return null;

        if (currentUserId is not null)
        {
            var visible = await _scopeService.GetVisibleOrgUnitIdsAsync(currentUserId, ct);
            if (visible is not null && !visible.Contains(plan.OrgUnitId))
                return null;
        }

        var names = await ResolveNamesAsync(new[] { plan }, ct);
        return MapToDto(plan, names);
    }

    public async Task<List<StrategicPlanDto>> GetByOrgUnitAsync(Guid orgUnitId, string? currentUserId = null, CancellationToken ct = default)
    {
        if (currentUserId is not null)
        {
            var visible = await _scopeService.GetVisibleOrgUnitIdsAsync(currentUserId, ct);
            if (visible is not null && !visible.Contains(orgUnitId))
                return new List<StrategicPlanDto>();
        }

        var plans = await _context.StrategicPlans
            .Include(p => p.OrgUnit)
            .Include(p => p.Objectives)
            .Where(p => p.OrgUnitId == orgUnitId)
            .OrderByDescending(p => p.StartYear)
            .ToListAsync(ct);

        var names = await ResolveNamesAsync(plans, ct);
        return plans.Select(p => MapToDto(p, names)).ToList();
    }

    public async Task<StrategicPlanDto?> GetActiveForOrgUnitAsync(Guid orgUnitId, string? currentUserId = null, CancellationToken ct = default)
    {
        if (currentUserId is not null)
        {
            var visible = await _scopeService.GetVisibleOrgUnitIdsAsync(currentUserId, ct);
            if (visible is not null && !visible.Contains(orgUnitId))
                return null;
        }

        var plan = await _context.StrategicPlans
            .Include(p => p.OrgUnit)
            .Include(p => p.Objectives)
            .Where(p => p.OrgUnitId == orgUnitId &&
                        (p.Status == StrategicPlanStatus.Approved || p.Status == StrategicPlanStatus.PendingApproval))
            .FirstOrDefaultAsync(ct);

        if (plan is null) return null;

        var names = await ResolveNamesAsync(new[] { plan }, ct);
        return MapToDto(plan, names);
    }

    public async Task<StrategicPlanDto> CreateAsync(CreateStrategicPlanRequestDto request, string userId, CancellationToken ct = default)
    {
        await _scopeService.EnsureCanWriteAsync(userId, request.OrgUnitId, ct);

        var orgUnit = await _context.OrgUnits
            .FirstOrDefaultAsync(o => o.Id == request.OrgUnitId, ct)
            ?? throw new ArgumentException(
                $"Org unit '{request.OrgUnitId}' does not exist or has been deleted.",
                nameof(request.OrgUnitId));

        // Uniqueness: only one active plan per org unit (Approved or PendingApproval).
        var hasActive = await _context.StrategicPlans
            .AnyAsync(p => p.OrgUnitId == request.OrgUnitId &&
                           (p.Status == StrategicPlanStatus.Approved || p.Status == StrategicPlanStatus.PendingApproval), ct);

        if (hasActive)
            throw new InvalidOperationException(
                "An active strategic plan (Approved or PendingApproval) already exists for this org unit.");

        var plan = new StrategicPlan
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            OrgUnitId = request.OrgUnitId,
            StartYear = request.StartYear,
            DurationYears = request.DurationYears,
            Status = StrategicPlanStatus.Draft,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId,
        };

        _context.StrategicPlans.Add(plan);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Created StrategicPlan {Id} '{Name}' for OrgUnit {OrgUnitId}",
            plan.Id, plan.Name, plan.OrgUnitId);

        plan.OrgUnit = orgUnit;

        // Fire-and-forget notification fan-out (CR-001-C): notify every Strategy
        // Editor whose OrgUnit falls within the plan's org-unit subtree, plus
        // record a self-notification for the creator.
        var capturedPlan = plan;
        var capturedOrgUnit = orgUnit;
        var capturedUserId = userId;
        _ = Task.Run(async () => await FanOutPlanCreatedNotificationsAsync(
            capturedPlan, capturedOrgUnit, capturedUserId));

        var names = await ResolveNamesAsync(new[] { plan }, ct);
        return MapToDto(plan, names);
    }

    private async Task FanOutPlanCreatedNotificationsAsync(
        StrategicPlan plan,
        OrgUnit orgUnit,
        string creatorUserId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;
            var notif = sp.GetRequiredService<INotificationService>();
            var emailSender = sp.GetRequiredService<IEmailSender>();
            var db = sp.GetRequiredService<IAppDbContext>();
            var orgResolver = sp.GetRequiredService<IOrgUnitScopeResolver>();
            var userMgr = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var lookup = sp.GetRequiredService<IUserLookupService>();

            // Resolve subtree: plan's own OrgUnit + descendants.
            var descendants = await orgResolver.GetDescendantOrgUnitIdsAsync(plan.OrgUnitId);
            var scopeIds = new HashSet<Guid>(descendants) { plan.OrgUnitId };

            // Find Strategy Editors whose OrgUnit is within the subtree.
            var editors = await userMgr.GetUsersInRoleAsync(AppRoles.StrategyEditor);
            var inScope = editors
                .Where(u => u.IsActive
                            && u.OrgUnitId.HasValue
                            && scopeIds.Contains(u.OrgUnitId.Value))
                .Distinct()
                .ToList();

            if (inScope.Count == 0)
            {
                _logger.LogInformation(
                    "Plan created but no Strategy Editors to notify in subtree of OrgUnit {OrgUnitId}",
                    plan.OrgUnitId);
            }

            var creatorName = await lookup.GetDisplayNameAsync(creatorUserId, CancellationToken.None);
            var planUrl = $"{_appSettings.FrontendBaseUrl}/strategy/org-units/{plan.OrgUnitId}";
            var endYear = plan.StartYear + plan.DurationYears - 1;

            var placeholders = new Dictionary<string, string>
            {
                ["PlanName"] = plan.Name,
                ["StartYear"] = plan.StartYear.ToString(),
                ["EndYear"] = endYear.ToString(),
                ["DurationYears"] = plan.DurationYears.ToString(),
                ["OrgUnitName"] = orgUnit.Name,
                ["CreatedByDisplayName"] = creatorName ?? creatorUserId,
                ["PlanUrl"] = planUrl,
            };

            var emails = inScope
                .Select(u => u.Email)
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Cast<string>()
                .Distinct()
                .ToList();

            if (emails.Count > 0)
            {
                try
                {
                    await emailSender.SendEmailAsync(
                        "StrategicPlan.Initiated",
                        placeholders,
                        emails,
                        "StrategicPlan",
                        plan.Id,
                        creatorUserId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending StrategicPlan.Initiated email for plan {Id}", plan.Id);
                }
            }

            // In-app notifications, one per recipient.
            foreach (var editor in inScope)
            {
                try
                {
                    await notif.CreateAsync(new CreateNotificationDto
                    {
                        UserId = editor.Id,
                        Title = "New Strategic Plan",
                        Message = $"{plan.Name} ({plan.StartYear}–{endYear}) — {orgUnit.Name}",
                        Type = "StrategicPlan",
                        ActionType = "StrategicPlanCreated",
                        RelatedEntityType = "StrategicPlan",
                        RelatedEntityId = plan.Id,
                        Url = planUrl,
                        CreatedByUserId = creatorUserId,
                        CreatedByDisplayName = placeholders["CreatedByDisplayName"],
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating in-app notification for plan {Id}, user {UserId}", plan.Id, editor.Id);
                }
            }

            // Self-notification for the creator (record-keeping).
            try
            {
                await notif.CreateAsync(new CreateNotificationDto
                {
                    UserId = creatorUserId,
                    Title = "Strategic Plan Created",
                    Message = $"{plan.Name} ({plan.StartYear}–{endYear}) — {orgUnit.Name}",
                    Type = "StrategicPlan",
                    ActionType = "StrategicPlanCreated",
                    RelatedEntityType = "StrategicPlan",
                    RelatedEntityId = plan.Id,
                    Url = planUrl,
                    CreatedByUserId = creatorUserId,
                    CreatedByDisplayName = placeholders["CreatedByDisplayName"],
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating self-notification for plan {Id}", plan.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fanning out notifications for plan {Id}", plan.Id);
        }
    }

    public async Task<StrategicPlanDto> UpdateAsync(Guid id, UpdateStrategicPlanRequestDto request, string userId, CancellationToken ct = default)
    {
        var plan = await _context.StrategicPlans
            .IgnoreQueryFilters()
            .Include(p => p.Objectives)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException($"Strategic plan '{id}' not found.");

        await _scopeService.EnsureCanWriteAsync(userId, plan.OrgUnitId, ct);
        await _lockGuard.EnsureNotLockedAsync(plan.Id, ct);

        // Optional org-unit reassignment. Gated to Draft plans, requires write
        // scope on the destination, and rejects targets that already host
        // another non-archived plan. Linked objectives' OrgUnitId is cascaded
        // so plan + objective references stay aligned.
        if (request.OrgUnitId.HasValue && request.OrgUnitId.Value != plan.OrgUnitId)
        {
            if (plan.Status != StrategicPlanStatus.Draft)
                throw new InvalidOperationException(
                    "The plan's organisation unit can only be changed while the plan is in Draft status.");

            var newOrgUnitId = request.OrgUnitId.Value;
            await _scopeService.EnsureCanWriteAsync(userId, newOrgUnitId, ct);

            var newUnitExists = await _context.OrgUnits
                .AnyAsync(o => o.Id == newOrgUnitId && !o.IsDeleted, ct);
            if (!newUnitExists)
                throw new ArgumentException(
                    $"Organisation unit '{newOrgUnitId}' does not exist or has been deleted.",
                    nameof(request.OrgUnitId));

            var conflict = await _context.StrategicPlans
                .AnyAsync(p => p.OrgUnitId == newOrgUnitId
                            && p.Status != StrategicPlanStatus.Archived
                            && p.Id != plan.Id, ct);
            if (conflict)
                throw new InvalidOperationException(
                    "The selected organisation unit already has an in-flight strategic plan.");

            plan.OrgUnitId = newOrgUnitId;

            var now = DateTime.UtcNow;
            foreach (var obj in plan.Objectives.Where(o => !o.IsDeleted))
            {
                obj.OrgUnitId = newOrgUnitId;
                obj.UpdatedAt = now;
                obj.UpdatedBy = userId;
            }
        }

        plan.Name = request.Name;
        plan.StartYear = request.StartYear;
        plan.DurationYears = request.DurationYears;
        plan.UpdatedAt = DateTime.UtcNow;
        plan.UpdatedBy = userId;

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Updated StrategicPlan {Id}", id);

        var orgUnit = await _context.OrgUnits.FirstOrDefaultAsync(o => o.Id == plan.OrgUnitId, ct);
        plan.OrgUnit = orgUnit!;

        var names = await ResolveNamesAsync(new[] { plan }, ct);
        return MapToDto(plan, names);
    }

    public async Task SoftDeleteAsync(Guid id, string userId, CancellationToken ct = default)
    {
        var plan = await _context.StrategicPlans
            .Include(p => p.Objectives)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException($"Strategic plan '{id}' not found.");

        await _scopeService.EnsureCanWriteAsync(userId, plan.OrgUnitId, ct);
        await _lockGuard.EnsureNotLockedAsync(plan.Id, ct);

        // Q2 default: block delete if any non-deleted objective references this plan.
        var hasLiveObjectives = plan.Objectives.Any(o => !o.IsDeleted);
        if (hasLiveObjectives)
            throw new InvalidOperationException(
                "Cannot delete a strategic plan that still has active objectives.");

        var now = DateTime.UtcNow;
        plan.IsDeleted = true;
        plan.DeletedAt = now;
        plan.UpdatedAt = now;
        plan.DeletedBy = userId;

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Soft-deleted StrategicPlan {Id}", id);
    }

    public async Task RestoreAsync(Guid id, string userId, CancellationToken ct = default)
    {
        var plan = await _context.StrategicPlans
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException($"Strategic plan '{id}' not found.");

        await _scopeService.EnsureCanWriteAsync(userId, plan.OrgUnitId, ct);

        plan.IsDeleted = false;
        plan.DeletedAt = null;
        plan.DeletedBy = null;
        plan.UpdatedAt = DateTime.UtcNow;
        plan.UpdatedBy = userId;

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Restored StrategicPlan {Id}", id);
    }

    // -------------------------------------------------------------------------

    private async Task<Dictionary<string, string>> ResolveNamesAsync(
        IEnumerable<StrategicPlan> plans,
        CancellationToken ct)
    {
        var ids = plans
            .SelectMany(p => new[] { p.CreatedBy, p.UpdatedBy, p.DeletedBy, p.ApprovedByUserId })
            .Where(id => id != null)
            .Cast<string>()
            .Distinct();
        return await _userLookup.GetDisplayNamesAsync(ids, ct);
    }

    private static string? Resolve(string? userId, Dictionary<string, string> names)
        => userId != null && names.TryGetValue(userId, out var n) ? n : null;

    private static StrategicPlanDto MapToDto(StrategicPlan p, Dictionary<string, string> names) =>
        new()
        {
            Id = p.Id,
            Name = p.Name,
            OrgUnitId = p.OrgUnitId,
            OrgUnitName = p.OrgUnit?.Name ?? string.Empty,
            OrgUnitCode = p.OrgUnit?.Code,
            StartYear = p.StartYear,
            DurationYears = p.DurationYears,
            EndYear = p.StartYear + p.DurationYears - 1,
            Status = p.Status,
            ApprovedAtUtc = p.ApprovedAtUtc,
            ApprovedByUserId = p.ApprovedByUserId,
            ApprovedByName = Resolve(p.ApprovedByUserId, names),
            ObjectiveCount = p.Objectives?.Count(o => !o.IsDeleted) ?? 0,
            IsDeleted = p.IsDeleted,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
            DeletedAt = p.DeletedAt,
            CreatedBy = p.CreatedBy,
            UpdatedBy = p.UpdatedBy,
            DeletedBy = p.DeletedBy,
            CreatedByName = Resolve(p.CreatedBy, names),
            UpdatedByName = Resolve(p.UpdatedBy, names),
            DeletedByName = Resolve(p.DeletedBy, names),
            IsDraft = p.IsDraft,
            OriginalPlanId = p.OriginalPlanId,
        };
}
