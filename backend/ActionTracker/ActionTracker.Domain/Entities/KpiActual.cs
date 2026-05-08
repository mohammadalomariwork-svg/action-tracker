using ActionTracker.Domain.Enums;

namespace ActionTracker.Domain.Entities;

/// <summary>
/// CR-001-F — Measured value of a <see cref="KpiTarget"/> for a single
/// reporting period (Quarter / SemiAnnual / Annual). The actual is the
/// workflow record itself: submit/review state lives directly on the entity
/// rather than a separate "approval request" table.
///
/// Editable even when the parent <see cref="StrategicPlan"/> is locked —
/// actuals are the ongoing reporting layer, not the plan baseline.
/// </summary>
public class KpiActual
{
    public Guid Id { get; set; }

    public Guid KpiTargetId { get; set; }

    /// <summary>Period bucket. Must align with the parent KPI's <c>Period</c>.</summary>
    public KpiActualPeriodType PeriodType { get; set; }

    /// <summary>1-based index within the period type (Q1=1..Q4=4; H1=1, H2=2; Annual=1).</summary>
    public int PeriodIndex { get; set; }

    public int Year { get; set; }

    public decimal ActualValue { get; set; }

    public string? Notes { get; set; }

    public KpiActualStatus Status { get; set; } = KpiActualStatus.Draft;

    public string? SubmittedByUserId { get; set; }
    public string? SubmittedByDisplayName { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }

    /// <summary>
    /// Reviewer resolved at submit time via <c>IApprovalAuthorityResolver</c>
    /// (level-3 head of the parent org-unit chain).
    /// </summary>
    public string? AssignedReviewerUserId { get; set; }
    public string? AssignedReviewerDisplayName { get; set; }

    public string? ReviewedByUserId { get; set; }
    public string? ReviewedByDisplayName { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewerComment { get; set; }

    public bool IsDeleted { get; set; } = false;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedAtUtc { get; set; }
    public string? CreatedByUserId { get; set; }
    public string? ModifiedByUserId { get; set; }

    // Navigation
    public KpiTarget KpiTarget { get; set; } = null!;
}
