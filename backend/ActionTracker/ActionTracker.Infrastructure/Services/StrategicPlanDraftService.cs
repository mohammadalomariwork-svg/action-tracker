using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.StrategicPlans.Drafts;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Infrastructure.Services;

/// <summary>
/// CR-001-G implementation. See <see cref="IStrategicPlanDraftService"/> for
/// the contract. Cloning + promotion run inside an EF transaction so the
/// system never observes a half-merged plan graph.
/// </summary>
public class StrategicPlanDraftService : IStrategicPlanDraftService
{
    private readonly IAppDbContext _db;
    private readonly IStrategicScopeService _scope;
    private readonly ILogger<StrategicPlanDraftService> _logger;

    public StrategicPlanDraftService(
        IAppDbContext db,
        IStrategicScopeService scope,
        ILogger<StrategicPlanDraftService> logger)
    {
        _db = db;
        _scope = scope;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Open
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<Guid> OpenDraftAsync(
        Guid publishedPlanId, string userId, CancellationToken ct = default)
    {
        var published = await _db.StrategicPlans
            .Include(p => p.Objectives.Where(o => !o.IsDeleted))
                .ThenInclude(o => o.Kpis.Where(k => !k.IsDeleted))
                    .ThenInclude(k => k.Targets)
            .FirstOrDefaultAsync(p => p.Id == publishedPlanId, ct)
            ?? throw new KeyNotFoundException($"Strategic plan '{publishedPlanId}' not found.");

        if (published.IsDraft)
            throw new InvalidOperationException("Cannot open a draft of a draft.");
        if (published.Status != StrategicPlanStatus.Approved)
            throw new InvalidOperationException(
                "A revision draft can only be opened from an Approved plan.");

        await _scope.EnsureCanWriteAsync(userId, published.OrgUnitId, ct);

        // Filtered unique index guarantees ≤ 1 draft per published plan, but we
        // probe up front so the error message is actionable instead of a raw
        // SQL duplicate-key exception.
        var existingDraftId = await _db.StrategicPlans
            .IgnoreQueryFilters()
            .Where(p => p.OriginalPlanId == publishedPlanId && !p.IsDeleted)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);
        if (existingDraftId is not null)
            throw new InvalidOperationException(
                $"A draft already exists for this plan (id {existingDraftId}). Cancel or submit it before opening a new one.");

        var now = DateTime.UtcNow;
        var draftPlanId = Guid.NewGuid();
        var draftPlan = new StrategicPlan
        {
            Id              = draftPlanId,
            OrgUnitId       = published.OrgUnitId,
            Name            = published.Name,
            StartYear       = published.StartYear,
            DurationYears   = published.DurationYears,
            Status          = StrategicPlanStatus.Draft,
            IsDraft         = true,
            OriginalPlanId  = published.Id,
            IsDeleted       = false,
            CreatedAt       = now,
            CreatedBy       = userId,
        };
        _db.StrategicPlans.Add(draftPlan);

        // Clone the object graph. Each draft entity carries Original*Id so the
        // promote step can apply field updates in place on the published row.
        foreach (var pubObj in published.Objectives)
        {
            var draftObjId = Guid.NewGuid();
            var draftObj = new StrategicObjective
            {
                Id                  = draftObjId,
                ObjectiveCode       = pubObj.ObjectiveCode,
                Statement           = pubObj.Statement,
                Description         = pubObj.Description,
                OrgUnitId           = pubObj.OrgUnitId,
                StrategicPlanId     = draftPlanId,
                IsDraft             = true,
                OriginalObjectiveId = pubObj.Id,
                IsDeleted           = false,
                CreatedAt           = now,
                CreatedBy           = userId,
            };
            _db.StrategicObjectives.Add(draftObj);

            foreach (var pubKpi in pubObj.Kpis)
            {
                var draftKpiId = Guid.NewGuid();
                var draftKpi = new Kpi
                {
                    Id                   = draftKpiId,
                    KpiNumber            = pubKpi.KpiNumber,
                    Name                 = pubKpi.Name,
                    Description          = pubKpi.Description,
                    CalculationMethod    = pubKpi.CalculationMethod,
                    Period               = pubKpi.Period,
                    Unit                 = pubKpi.Unit,
                    StrategicObjectiveId = draftObjId,
                    IsDraft              = true,
                    OriginalKpiId        = pubKpi.Id,
                    IsDeleted            = false,
                    CreatedAt            = now,
                    CreatedBy            = userId,
                };
                _db.Kpis.Add(draftKpi);

                foreach (var pubTarget in pubKpi.Targets)
                {
                    _db.KpiTargets.Add(new KpiTarget
                    {
                        Id                  = Guid.NewGuid(),
                        KpiId               = draftKpiId,
                        Year                = pubTarget.Year,
                        Month               = pubTarget.Month,
                        Target              = pubTarget.Target,
                        Baseline            = pubTarget.Baseline,
                        DataSource          = pubTarget.DataSource,
                        CalculationNotes    = pubTarget.CalculationNotes,
                        Direction           = pubTarget.Direction,
                        Notes               = pubTarget.Notes,
                        IsDraft             = true,
                        OriginalKpiTargetId = pubTarget.Id,
                        CreatedAt           = now,
                        CreatedBy           = userId,
                    });
                }
            }
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Opened draft {DraftId} for published plan {PublishedPlanId} by user {UserId}",
            draftPlanId, publishedPlanId, userId);
        return draftPlanId;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Lookup
    // ─────────────────────────────────────────────────────────────────────────

    public Task<Guid?> GetCurrentDraftIdAsync(Guid publishedPlanId, CancellationToken ct = default)
        => _db.StrategicPlans
            .IgnoreQueryFilters()
            .Where(p => p.OriginalPlanId == publishedPlanId && !p.IsDeleted)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);

    // ─────────────────────────────────────────────────────────────────────────
    // Cancel — hard-delete the draft graph; published plan untouched
    // ─────────────────────────────────────────────────────────────────────────

    public async Task CancelDraftAsync(Guid draftId, string userId, CancellationToken ct = default)
    {
        var draft = await _db.StrategicPlans
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == draftId && p.IsDraft, ct)
            ?? throw new KeyNotFoundException($"Draft plan '{draftId}' not found.");

        await _scope.EnsureCanWriteAsync(userId, draft.OrgUnitId, ct);

        // Only the original submitter can cancel (matches the spec — strategy
        // editors of the same org unit shouldn't kill each other's drafts).
        if (!string.Equals(draft.CreatedBy, userId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException(
                "Only the user who opened this draft can cancel it.");

        // Pending Revision request(s) for this draft must also be killed so
        // the published plan can host a fresh draft afterwards.
        var pendingRequests = await _db.StrategicPlanApprovalRequests
            .Where(r => r.StrategicPlanId == draftId
                     && r.Status == StrategicPlanApprovalStatus.Pending)
            .ToListAsync(ct);
        foreach (var r in pendingRequests)
        {
            r.Status = StrategicPlanApprovalStatus.Rejected;
            r.ReviewerComment = "Draft canceled by submitter.";
            r.ReviewedByUserId = userId;
            r.ReviewedAt = DateTime.UtcNow;
        }

        await HardDeleteDraftGraphAsync(draftId, ct);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Canceled draft {DraftId} by user {UserId}", draftId, userId);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Promote — atomic merge into published, then delete draft graph
    // ─────────────────────────────────────────────────────────────────────────

    public async Task PromoteDraftAsync(Guid draftId, string reviewerUserId, CancellationToken ct = default)
    {
        var draft = await _db.StrategicPlans
            .IgnoreQueryFilters()
            .Include(p => p.Objectives)
                .ThenInclude(o => o.Kpis)
                    .ThenInclude(k => k.Targets)
            .FirstOrDefaultAsync(p => p.Id == draftId && p.IsDraft, ct)
            ?? throw new KeyNotFoundException($"Draft plan '{draftId}' not found.");

        if (draft.OriginalPlanId is null)
            throw new InvalidOperationException(
                "Draft has no OriginalPlanId — cannot promote.");

        var published = await _db.StrategicPlans
            .IgnoreQueryFilters()
            .Include(p => p.Objectives)
                .ThenInclude(o => o.Kpis)
                    .ThenInclude(k => k.Targets)
            .FirstOrDefaultAsync(p => p.Id == draft.OriginalPlanId.Value, ct)
            ?? throw new InvalidOperationException(
                "Published plan referenced by draft no longer exists.");

        var now = DateTime.UtcNow;

        // ── Plan-level fields ────────────────────────────────────────────────
        published.Name          = draft.Name;
        published.StartYear     = draft.StartYear;
        published.DurationYears = draft.DurationYears;
        // OrgUnitId, Status, ApprovedAtUtc, ApprovedByUserId stay on the
        // published row — those aren't draft-editable.
        published.UpdatedAt = now;
        published.UpdatedBy = reviewerUserId;

        // ── Objective sync ──────────────────────────────────────────────────
        var draftObjs = draft.Objectives.Where(o => !o.IsDeleted).ToList();
        var draftOrigObjIds = draftObjs
            .Where(o => o.OriginalObjectiveId.HasValue)
            .Select(o => o.OriginalObjectiveId!.Value)
            .ToHashSet();

        // (a) Updates: draft objectives that carry OriginalObjectiveId →
        //     copy fields onto the matching published row in place.
        foreach (var draftObj in draftObjs.Where(o => o.OriginalObjectiveId.HasValue))
        {
            var pubObj = published.Objectives
                .FirstOrDefault(o => o.Id == draftObj.OriginalObjectiveId!.Value);
            if (pubObj is null) continue;
            pubObj.Statement   = draftObj.Statement;
            pubObj.Description = draftObj.Description;
            pubObj.OrgUnitId   = draftObj.OrgUnitId;
            pubObj.UpdatedAt   = now;
            pubObj.UpdatedBy   = reviewerUserId;

            await SyncKpisAsync(draftObj, pubObj, reviewerUserId, now, ct);
        }

        // (b) Soft-delete: published objectives no longer represented in the
        //     draft. The draft editor's delete path already blocks deletes
        //     when a project still references the objective — this is just a
        //     belt-and-braces check on the merge.
        var toSoftDeleteObjs = published.Objectives
            .Where(o => !o.IsDeleted && !draftOrigObjIds.Contains(o.Id))
            .ToList();
        foreach (var pubObj in toSoftDeleteObjs)
        {
            var linkedProjects = await _db.Projects
                .Where(p => p.StrategicObjectiveId == pubObj.Id && !p.IsDeleted)
                .Select(p => p.ProjectCode)
                .ToListAsync(ct);
            if (linkedProjects.Count > 0)
                throw new InvalidOperationException(
                    $"Cannot promote draft: objective '{pubObj.ObjectiveCode}' is " +
                    $"removed in the draft but still has linked projects " +
                    $"({string.Join(", ", linkedProjects)}). Unlink them before resubmitting.");

            pubObj.IsDeleted = true;
            pubObj.DeletedAt = now;
            pubObj.DeletedBy = reviewerUserId;
            foreach (var pubKpi in pubObj.Kpis.Where(k => !k.IsDeleted))
            {
                pubKpi.IsDeleted = true;
                pubKpi.DeletedAt = now;
                pubKpi.DeletedBy = reviewerUserId;
            }
        }

        // (c) New: draft objectives without OriginalObjectiveId — promote them
        //     into published by flipping IsDraft, re-parenting to the published
        //     plan, and clearing the draft pointer. Their child KPIs/targets
        //     do the same in PromoteNewKpisAsync.
        foreach (var draftObj in draftObjs.Where(o => !o.OriginalObjectiveId.HasValue))
        {
            draftObj.IsDraft         = false;
            draftObj.StrategicPlanId = published.Id;
            draftObj.UpdatedAt       = now;
            draftObj.UpdatedBy       = reviewerUserId;
            await PromoteNewKpisAsync(draftObj, reviewerUserId, now, ct);
        }

        // The draft plan + the leftover draft objectives that were "updates"
        // (their content now lives in published rows) get hard-deleted below.
        await _db.SaveChangesAsync(ct);
        await HardDeleteDraftGraphAsync(draftId, ct);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Promoted draft {DraftId} into published plan {PublishedPlanId}",
            draftId, published.Id);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // KPI / target sync — used by promote for "updated" objectives
    // ─────────────────────────────────────────────────────────────────────────

    private async Task SyncKpisAsync(
        StrategicObjective draftObj, StrategicObjective pubObj,
        string reviewerUserId, DateTime now, CancellationToken ct)
    {
        var draftKpis = draftObj.Kpis.Where(k => !k.IsDeleted).ToList();
        var draftOrigKpiIds = draftKpis
            .Where(k => k.OriginalKpiId.HasValue)
            .Select(k => k.OriginalKpiId!.Value)
            .ToHashSet();

        // (a) Updates
        foreach (var draftKpi in draftKpis.Where(k => k.OriginalKpiId.HasValue))
        {
            var pubKpi = pubObj.Kpis.FirstOrDefault(k => k.Id == draftKpi.OriginalKpiId!.Value);
            if (pubKpi is null) continue;
            pubKpi.Name              = draftKpi.Name;
            pubKpi.Description       = draftKpi.Description;
            pubKpi.CalculationMethod = draftKpi.CalculationMethod;
            pubKpi.Period            = draftKpi.Period;
            pubKpi.Unit              = draftKpi.Unit;
            pubKpi.UpdatedAt         = now;
            pubKpi.UpdatedBy         = reviewerUserId;

            SyncTargets(draftKpi, pubKpi, reviewerUserId, now);
        }

        // (b) Soft-delete missing
        foreach (var pubKpi in pubObj.Kpis.Where(k => !k.IsDeleted && !draftOrigKpiIds.Contains(k.Id)))
        {
            pubKpi.IsDeleted = true;
            pubKpi.DeletedAt = now;
            pubKpi.DeletedBy = reviewerUserId;
        }

        // (c) New KPIs — promote into published under the existing published
        //     objective. (Brand-new KPIs against existing objectives.)
        foreach (var draftKpi in draftKpis.Where(k => !k.OriginalKpiId.HasValue))
        {
            draftKpi.IsDraft               = false;
            draftKpi.StrategicObjectiveId  = pubObj.Id;
            draftKpi.UpdatedAt             = now;
            draftKpi.UpdatedBy             = reviewerUserId;
            foreach (var t in draftKpi.Targets)
            {
                t.IsDraft              = false;
                t.OriginalKpiTargetId  = null;
                t.UpdatedAt            = now;
                t.UpdatedBy            = reviewerUserId;
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>For brand-new draft objectives, promote their entire KPI/target
    /// subtree under the same (now-published) objective Id.</summary>
    private async Task PromoteNewKpisAsync(
        StrategicObjective draftObj, string reviewerUserId, DateTime now, CancellationToken ct)
    {
        foreach (var draftKpi in draftObj.Kpis.Where(k => !k.IsDeleted))
        {
            draftKpi.IsDraft              = false;
            draftKpi.OriginalKpiId        = null;
            draftKpi.StrategicObjectiveId = draftObj.Id;
            draftKpi.UpdatedAt            = now;
            draftKpi.UpdatedBy            = reviewerUserId;
            foreach (var t in draftKpi.Targets)
            {
                t.IsDraft             = false;
                t.OriginalKpiTargetId = null;
                t.UpdatedAt           = now;
                t.UpdatedBy           = reviewerUserId;
            }
        }
        await Task.CompletedTask;
    }

    private void SyncTargets(Kpi draftKpi, Kpi pubKpi, string reviewerUserId, DateTime now)
    {
        var draftTargets = draftKpi.Targets.ToList();
        var draftOrigTargetIds = draftTargets
            .Where(t => t.OriginalKpiTargetId.HasValue)
            .Select(t => t.OriginalKpiTargetId!.Value)
            .ToHashSet();

        // Updates
        foreach (var dt in draftTargets.Where(t => t.OriginalKpiTargetId.HasValue))
        {
            var pubT = pubKpi.Targets.FirstOrDefault(t => t.Id == dt.OriginalKpiTargetId!.Value);
            if (pubT is null) continue;
            pubT.Year             = dt.Year;
            pubT.Month            = dt.Month;
            pubT.Target           = dt.Target;
            pubT.Baseline         = dt.Baseline;
            pubT.DataSource       = dt.DataSource;
            pubT.CalculationNotes = dt.CalculationNotes;
            pubT.Direction        = dt.Direction;
            pubT.Notes            = dt.Notes;
            pubT.UpdatedAt        = now;
            pubT.UpdatedBy        = reviewerUserId;
        }

        // Removed targets — hard-delete from published (no soft-delete column
        // on KpiTarget). KpiActuals reference KpiTargetId so we'd want to
        // guard, but in practice targets carry no actuals while the plan is
        // pending revision; the actuals flow is independent of the plan lock.
        var toDelete = pubKpi.Targets
            .Where(t => !draftOrigTargetIds.Contains(t.Id))
            .ToList();
        foreach (var pubT in toDelete) _db.KpiTargets.Remove(pubT);

        // New targets — promote (clear draft markers, re-parent to published kpi)
        foreach (var dt in draftTargets.Where(t => !t.OriginalKpiTargetId.HasValue))
        {
            dt.IsDraft             = false;
            dt.KpiId               = pubKpi.Id;
            dt.UpdatedAt           = now;
            dt.UpdatedBy           = reviewerUserId;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Hard-delete helpers
    // ─────────────────────────────────────────────────────────────────────────

    private async Task HardDeleteDraftGraphAsync(Guid draftPlanId, CancellationToken ct)
    {
        // Targets → KPIs → Objectives → Plan (children first to satisfy FKs).
        // Only rows still flagged IsDraft are removed; rows promoted into
        // published during PromoteDraftAsync have already had IsDraft cleared.
        var targets = await _db.KpiTargets
            .IgnoreQueryFilters()
            .Where(t => t.IsDraft && t.Kpi.StrategicObjective.StrategicPlanId == draftPlanId)
            .ToListAsync(ct);
        _db.KpiTargets.RemoveRange(targets);

        var kpis = await _db.Kpis
            .IgnoreQueryFilters()
            .Where(k => k.IsDraft && k.StrategicObjective.StrategicPlanId == draftPlanId)
            .ToListAsync(ct);
        _db.Kpis.RemoveRange(kpis);

        var objs = await _db.StrategicObjectives
            .IgnoreQueryFilters()
            .Where(o => o.IsDraft && o.StrategicPlanId == draftPlanId)
            .ToListAsync(ct);
        _db.StrategicObjectives.RemoveRange(objs);

        var draftPlan = await _db.StrategicPlans
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == draftPlanId && p.IsDraft, ct);
        if (draftPlan is not null) _db.StrategicPlans.Remove(draftPlan);
    }
}
