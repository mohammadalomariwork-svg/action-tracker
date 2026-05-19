namespace ActionTracker.Application.Features.StrategicPlans.Drafts;

/// <summary>
/// Draft / working-copy service for the strategic-plan revision flow (CR-001-G).
///
/// After a plan is Approved its published row is read-only to every consumer.
/// To make changes, the submitter calls <see cref="OpenDraftAsync"/> which
/// clones the published plan's graph (plan + objectives + KPIs + targets) into
/// new rows tagged <c>IsDraft = true</c> and pointing back to their published
/// counterparts via the new <c>Original*Id</c> FKs. Edits are confined to that
/// draft graph; the rest of the system continues to read the published plan.
///
/// On <see cref="PromoteDraftAsync"/> (final-level approval of a Revision
/// request) the draft is merged back into the published row by:
///   • updating fields in place on each published entity that has a draft
///     counterpart (same Id stays, projects / KPI actuals keep their FKs);
///   • promoting brand-new draft rows (no Original*Id) into published by
///     clearing <c>IsDraft</c> and re-parenting their FKs to the published
///     plan / objective / kpi;
///   • soft-deleting published entities that are no longer represented in the
///     draft graph.
/// The draft graph itself is then hard-deleted. <see cref="CancelDraftAsync"/>
/// just hard-deletes the draft graph without touching the published plan.
/// </summary>
public interface IStrategicPlanDraftService
{
    /// <summary>Create a new draft of <paramref name="publishedPlanId"/>. Fails
    /// if the plan is not Approved or if a draft already exists for it.</summary>
    Task<Guid> OpenDraftAsync(Guid publishedPlanId, string userId, CancellationToken ct = default);

    /// <summary>Returns the open draft's id for the published plan, or null
    /// when none exists.</summary>
    Task<Guid?> GetCurrentDraftIdAsync(Guid publishedPlanId, CancellationToken ct = default);

    /// <summary>Hard-deletes the draft graph (plan + objectives + KPIs + targets).
    /// Cancels any in-flight Revision approval requests pointing at the draft.
    /// The published plan is untouched.</summary>
    Task CancelDraftAsync(Guid draftId, string userId, CancellationToken ct = default);

    /// <summary>Merges the draft graph into its published parent, then deletes
    /// the draft graph. Called internally by the workflow service on final-level
    /// approval of a Revision request.</summary>
    Task PromoteDraftAsync(Guid draftId, string reviewerUserId, CancellationToken ct = default);
}
