using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.StrategicPlans.Workflow;
using ActionTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ActionTracker.Infrastructure.Services;

/// <summary>
/// Concrete <see cref="IStrategicPlanLockGuard"/> (CR-001-E + CR-001-G).
///
/// Lock semantics (post CR-001-G):
///   • Draft plan → not locked. Editable by submitter / strategy editors.
///   • Draft plan that has been submitted (its own Status = PendingApproval)
///     → locked while a reviewer holds it; the workflow controls editability.
///   • Published plan, Status = PendingApproval → locked (initial approval).
///   • Published plan, Status = Approved → **always locked** under CR-001-G.
///     Edits live on a separate draft row (see <c>IStrategicPlanDraftService</c>),
///     no longer on the published row.
///   • Published plan, Status = Archived → always locked.
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
        // IgnoreQueryFilters so we can also answer the question for a draft row
        // (draft rows are hidden from the default StrategicPlans query).
        var plan = await _db.StrategicPlans
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(p => p.Id == strategicPlanId && !p.IsDeleted)
            .Select(p => new { p.Id, p.Status, p.IsDraft })
            .FirstOrDefaultAsync(ct);

        if (plan is null) return false;

        // Drafts: editable while in Draft status, locked once submitted.
        if (plan.IsDraft) return plan.Status != StrategicPlanStatus.Draft;

        // Published rows: Approved + Archived are now permanently locked.
        // PendingApproval (initial submission) is governed by the workflow.
        return plan.Status is StrategicPlanStatus.Approved
                            or StrategicPlanStatus.Archived
                            or StrategicPlanStatus.PendingApproval;
    }

    public async Task EnsureNotLockedAsync(Guid strategicPlanId, CancellationToken ct = default)
    {
        if (await IsLockedAsync(strategicPlanId, ct))
            throw new InvalidOperationException(
                "Plan is approved and locked. Open a revision draft to make changes.");
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
    /// Finds the org unit's *published* plan whose state contributes to the
    /// lockdown. Published rows in Approved / PendingApproval / Archived all
    /// qualify (drafts are filtered out by the global IsDraft filter).
    /// Published Draft plans don't lock anything.
    /// </summary>
    private Task<Guid?> ResolvePotentiallyLockedPlanIdAsync(Guid orgUnitId, CancellationToken ct)
        => _db.StrategicPlans
            .AsNoTracking()
            .Where(p => p.OrgUnitId == orgUnitId
                        && (p.Status == StrategicPlanStatus.Approved
                            || p.Status == StrategicPlanStatus.Archived
                            || p.Status == StrategicPlanStatus.PendingApproval))
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);
}
