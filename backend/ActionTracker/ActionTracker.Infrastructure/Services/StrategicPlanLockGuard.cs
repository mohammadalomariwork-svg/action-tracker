using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.StrategicPlans.Workflow;
using ActionTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ActionTracker.Infrastructure.Services;

/// <summary>
/// Concrete <see cref="IStrategicPlanLockGuard"/> (CR-001-E).
///
/// Lock semantics:
///   • Draft / PendingApproval → not locked here (PendingApproval is
///     governed by the workflow, not the lockdown rules).
///   • Approved → locked unless an in-flight Revision request exists.
///   • Archived → always locked, no escape hatch.
/// </summary>
public class StrategicPlanLockGuard : IStrategicPlanLockGuard
{
    private readonly IAppDbContext _db;

    public StrategicPlanLockGuard(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> IsLockedAsync(Guid strategicPlanId, CancellationToken ct = default)
    {
        var plan = await _db.StrategicPlans
            .AsNoTracking()
            .Where(p => p.Id == strategicPlanId)
            .Select(p => new { p.Id, p.Status })
            .FirstOrDefaultAsync(ct);

        if (plan is null) return false;

        if (plan.Status == StrategicPlanStatus.Archived) return true;
        if (plan.Status != StrategicPlanStatus.Approved) return false;

        // Approved + pending revision = temporarily editable.
        var hasPendingRevision = await _db.StrategicPlanApprovalRequests
            .AsNoTracking()
            .AnyAsync(r =>
                r.StrategicPlanId == strategicPlanId
                && r.RequestType == StrategicPlanApprovalRequestType.Revision
                && r.Status == StrategicPlanApprovalStatus.Pending, ct);

        return !hasPendingRevision;
    }

    public async Task EnsureNotLockedAsync(Guid strategicPlanId, CancellationToken ct = default)
    {
        if (await IsLockedAsync(strategicPlanId, ct))
            throw new InvalidOperationException(
                "Plan is approved and locked. Submit a revision request to make changes.");
    }

    public async Task EnsureNotLockedForOrgUnitAsync(Guid orgUnitId, CancellationToken ct = default)
    {
        var planId = await ResolvePotentiallyLockedPlanIdAsync(orgUnitId, ct);
        if (planId is null) return; // No active plan ⇒ nothing to lock.

        await EnsureNotLockedAsync(planId.Value, ct);
    }

    public async Task<bool> IsLockedForOrgUnitAsync(Guid orgUnitId, CancellationToken ct = default)
    {
        var planId = await ResolvePotentiallyLockedPlanIdAsync(orgUnitId, ct);
        if (planId is null) return false;
        return await IsLockedAsync(planId.Value, ct);
    }

    /// <summary>
    /// Finds the org unit's plan that *might* be locked — only Approved or
    /// Archived plans qualify. Draft / PendingApproval plans never lock,
    /// so we skip them at the query layer.
    /// </summary>
    private Task<Guid?> ResolvePotentiallyLockedPlanIdAsync(Guid orgUnitId, CancellationToken ct)
        => _db.StrategicPlans
            .AsNoTracking()
            .Where(p => p.OrgUnitId == orgUnitId
                        && (p.Status == StrategicPlanStatus.Approved
                            || p.Status == StrategicPlanStatus.Archived))
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);
}
