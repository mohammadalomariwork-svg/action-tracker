using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.StrategicPlans.Workflow;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Infrastructure.Services;

/// <inheritdoc cref="IStrategicPlanApproverService"/>
public class StrategicPlanApproverService : IStrategicPlanApproverService
{
    private readonly IAppDbContext _db;
    private readonly IStrategicScopeService _scopeService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<StrategicPlanApproverService> _logger;

    public StrategicPlanApproverService(
        IAppDbContext db,
        IStrategicScopeService scopeService,
        UserManager<ApplicationUser> userManager,
        ILogger<StrategicPlanApproverService> logger)
    {
        _db = db;
        _scopeService = scopeService;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<IReadOnlyList<StrategicPlanApproverDto>> GetForPlanAsync(
        Guid planId, string currentUserId, CancellationToken ct = default)
    {
        var plan = await _db.StrategicPlans
            .AsNoTracking()
            .Where(p => p.Id == planId)
            .Select(p => new { p.Id, p.OrgUnitId })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException($"Strategic plan '{planId}' not found.");

        // Read uses the same scope rules as the rest of the strategy reads —
        // anyone who can see the plan can see its approvers.
        await _scopeService.EnsureCanWriteAsync(currentUserId, plan.OrgUnitId, ct);

        var rows = await _db.StrategicPlanApprovers
            .AsNoTracking()
            .Where(a => a.StrategicPlanId == planId)
            .OrderBy(a => a.Order)
            .ToListAsync(ct);

        return await ResolveDisplayAsync(rows);
    }

    public async Task<IReadOnlyList<StrategicPlanApproverDto>> UpsertForPlanAsync(
        Guid planId,
        UpsertStrategicPlanApproversRequestDto request,
        string currentUserId,
        CancellationToken ct = default)
    {
        var plan = await _db.StrategicPlans
            .FirstOrDefaultAsync(p => p.Id == planId, ct)
            ?? throw new KeyNotFoundException($"Strategic plan '{planId}' not found.");

        await _scopeService.EnsureCanWriteAsync(currentUserId, plan.OrgUnitId, ct);

        // Chain is frozen once the plan leaves Draft. Editing it mid-flight
        // would let a user retarget the in-flight workflow, so we reject — they
        // can change approvers only after the plan is back in Draft (e.g. after
        // a rejection or never-submitted plans).
        if (plan.Status != StrategicPlanStatus.Draft)
            throw new InvalidOperationException(
                "The approval chain can only be changed while the plan is in Draft status.");

        var items = request.Approvers ?? new List<UpsertStrategicPlanApproverItemDto>();

        // Normalise: order by the supplied Order asc (stable on equal values),
        // dedup by UserId keeping the first occurrence, and re-number to a
        // dense 1..N. The client may have sent gaps or duplicates after
        // reorder/remove operations — the server is the source of truth.
        var normalized = items
            .OrderBy(a => a.Order)
            .ThenBy(a => a.UserId, StringComparer.Ordinal)
            .GroupBy(a => a.UserId)
            .Select(g => g.First())
            .Where(a => !string.IsNullOrWhiteSpace(a.UserId))
            .Select((a, idx) => new { a.UserId, Order = idx + 1 })
            .ToList();

        if (normalized.Count > 0)
        {
            // Validate user ids exist + are active. A single missing/disabled
            // approver fails the whole upsert — chain is rejected as invalid.
            var ids = normalized.Select(n => n.UserId).ToList();
            var validIds = await _db.Users
                .AsNoTracking()
                .Where(u => ids.Contains(u.Id) && u.IsActive)
                .Select(u => u.Id)
                .ToListAsync(ct);

            var missing = ids.Except(validIds).ToList();
            if (missing.Count > 0)
                throw new ArgumentException(
                    $"One or more approvers are not valid active users: {string.Join(", ", missing)}.");
        }

        // Replace the chain in one transaction. The new rows aren't created
        // until SaveChangesAsync, so the unique (PlanId, UserId) and
        // (PlanId, Order) indexes don't fight us during the swap.
        var existing = await _db.StrategicPlanApprovers
            .Where(a => a.StrategicPlanId == planId)
            .ToListAsync(ct);
        if (existing.Count > 0)
            _db.StrategicPlanApprovers.RemoveRange(existing);

        foreach (var item in normalized)
        {
            _db.StrategicPlanApprovers.Add(new StrategicPlanApprover
            {
                Id = Guid.NewGuid(),
                StrategicPlanId = planId,
                UserId = item.UserId,
                Order = item.Order,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = currentUserId,
            });
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Upserted {Count} approvers for StrategicPlan {Id}", normalized.Count, planId);

        var rows = await _db.StrategicPlanApprovers
            .AsNoTracking()
            .Where(a => a.StrategicPlanId == planId)
            .OrderBy(a => a.Order)
            .ToListAsync(ct);

        return await ResolveDisplayAsync(rows);
    }

    private async Task<List<StrategicPlanApproverDto>> ResolveDisplayAsync(List<StrategicPlanApprover> rows)
    {
        if (rows.Count == 0)
            return new List<StrategicPlanApproverDto>();

        var ids = rows.Select(r => r.UserId).Distinct().ToList();
        var users = await _db.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.Email, u.OrgUnitId })
            .ToListAsync();

        var unitIds = users.Where(u => u.OrgUnitId.HasValue).Select(u => u.OrgUnitId!.Value).Distinct().ToList();
        var unitNames = unitIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.OrgUnits
                .AsNoTracking()
                .Where(o => unitIds.Contains(o.Id))
                .ToDictionaryAsync(o => o.Id, o => o.Name);

        var byId = users.ToDictionary(u => u.Id);

        return rows.Select(r =>
        {
            byId.TryGetValue(r.UserId, out var u);
            string? orgUnitName = null;
            if (u?.OrgUnitId is Guid uid && unitNames.TryGetValue(uid, out var name))
                orgUnitName = name;

            return new StrategicPlanApproverDto
            {
                Id = r.Id,
                StrategicPlanId = r.StrategicPlanId,
                UserId = r.UserId,
                DisplayName = u?.FullName ?? r.UserId,
                Email = u?.Email,
                OrgUnitName = orgUnitName,
                Order = r.Order,
            };
        }).ToList();
    }
}
