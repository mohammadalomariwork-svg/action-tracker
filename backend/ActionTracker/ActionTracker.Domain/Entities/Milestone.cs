using ActionTracker.Domain.Enums;

namespace ActionTracker.Domain.Entities;

public class Milestone
{
    public Guid Id { get; set; }

    /// <summary>Auto-generated code in format MS-YYYY-001.</summary>
    public string MilestoneCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid ProjectId { get; set; }

    /// <summary>Controls display order within the project.</summary>
    public int SequenceOrder { get; set; }

    public DateTime PlannedStartDate { get; set; }
    public DateTime PlannedDueDate { get; set; }
    public DateTime? ActualCompletionDate { get; set; }

    /// <summary>Whether the due date is a hard deadline.</summary>
    public bool IsDeadlineFixed { get; set; }

    public MilestoneStatus Status { get; set; } = MilestoneStatus.NotStarted;

    /// <summary>0–100 completion percentage. Auto-calculated from action items or set manually.</summary>
    public decimal CompletionPercentage { get; set; }

    /// <summary>Weight towards overall project progress. All weights in a project should sum to 100.</summary>
    public decimal Weight { get; set; }

    /// <summary>User responsible for formally signing off on completion.</summary>
    public string? ApproverUserId { get; set; }

    // ── Baseline fields ─────────────────────────────────────────────────────
    /// <summary>Original planned start date at time of baseline.</summary>
    public DateTime? BaselinePlannedStartDate { get; set; }

    /// <summary>Original planned due date at time of baseline.</summary>
    public DateTime? BaselinePlannedDueDate { get; set; }

    public bool IsDeleted { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public Project Project { get; set; } = null!;
    public ApplicationUser? Approver { get; set; }
}
