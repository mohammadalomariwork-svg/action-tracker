using ActionTracker.Domain.Enums;

namespace ActionTracker.Domain.Entities;

public class ProjectRisk
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Auto-generated per project: RISK-001, RISK-002, etc.</summary>
    public string RiskCode { get; set; } = string.Empty;

    public Guid ProjectId { get; set; }

    /// <summary>Risk title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Detailed description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Risk category (e.g. Technical, Schedule, Resource, Budget, External, Quality).</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>1–5 scale.</summary>
    public int ProbabilityScore { get; set; }

    /// <summary>1–5 scale.</summary>
    public int ImpactScore { get; set; }

    /// <summary>Computed: ProbabilityScore × ImpactScore (1–25).</summary>
    public int RiskScore { get; set; }

    /// <summary>Derived from RiskScore: 20–25 = Critical, 12–19 = High, 5–11 = Medium, 1–4 = Low.</summary>
    public RiskRating RiskRating { get; set; }

    public RiskStatus Status { get; set; } = RiskStatus.Open;

    /// <summary>Mitigation strategy.</summary>
    public string? MitigationPlan { get; set; }

    /// <summary>Fallback plan if risk occurs.</summary>
    public string? ContingencyPlan { get; set; }

    /// <summary>User responsible for monitoring (no FK to AspNetUsers).</summary>
    public string? RiskOwnerUserId { get; set; }

    /// <summary>Denormalized display name.</summary>
    public string? RiskOwnerDisplayName { get; set; }

    /// <summary>Date risk was identified.</summary>
    public DateTime IdentifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date by which mitigation must be applied.</summary>
    public DateTime? DueDate { get; set; }

    /// <summary>Date risk was closed.</summary>
    public DateTime? ClosedDate { get; set; }

    /// <summary>Additional notes.</summary>
    public string? Notes { get; set; }

    /// <summary>Creator user ID (no FK).</summary>
    public string? CreatedByUserId { get; set; }

    /// <summary>Denormalized creator display name.</summary>
    public string? CreatedByDisplayName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; } = false;

    // Navigation
    public virtual Project Project { get; set; } = null!;
}
