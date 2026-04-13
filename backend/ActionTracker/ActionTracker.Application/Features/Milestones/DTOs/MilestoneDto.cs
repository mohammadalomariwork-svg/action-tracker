using System.ComponentModel.DataAnnotations;
using ActionTracker.Application.Common.Extensions;
using ActionTracker.Domain.Enums;

namespace ActionTracker.Application.Features.Milestones.DTOs;

public class MilestoneResponseDto
{
    public Guid Id { get; set; }
    public string MilestoneCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid ProjectId { get; set; }
    public int SequenceOrder { get; set; }

    public DateTime PlannedStartDate { get; set; }
    public DateTime PlannedDueDate { get; set; }
    public DateTime? ActualCompletionDate { get; set; }

    public bool IsDeadlineFixed { get; set; }

    public ProjectPhase Phase { get; set; }
    public string PhaseLabel => Phase.GetDescription();

    public MilestoneStatus Status { get; set; }
    public decimal CompletionPercentage { get; set; }

    public string? ApproverUserId { get; set; }
    public string? ApproverName { get; set; }

    public DateTime? BaselinePlannedStartDate { get; set; }
    public DateTime? BaselinePlannedDueDate { get; set; }

    /// <summary>Positive = behind schedule, negative = ahead of schedule, null = no baseline.</summary>
    public int? ScheduleVarianceDays { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Human-readable label
    public string StatusLabel => Status.GetDescription();
}

public class MilestoneCreateDto
{
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(5000)]
    public string? Description { get; set; }

    public int SequenceOrder { get; set; }

    [Required]
    public ProjectPhase Phase { get; set; }

    [Required]
    public DateTime PlannedStartDate { get; set; }

    [Required]
    public DateTime PlannedDueDate { get; set; }

    public bool IsDeadlineFixed { get; set; }

    [Range(0, 100)]
    public decimal CompletionPercentage { get; set; }

    public string? ApproverUserId { get; set; }
}

public class MilestoneUpdateDto
{
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(5000)]
    public string? Description { get; set; }

    public int SequenceOrder { get; set; }

    [Required]
    public ProjectPhase Phase { get; set; }

    public DateTime PlannedStartDate { get; set; }
    public DateTime PlannedDueDate { get; set; }
    public DateTime? ActualCompletionDate { get; set; }

    public bool IsDeadlineFixed { get; set; }

    public MilestoneStatus Status { get; set; }

    [Range(0, 100)]
    public decimal CompletionPercentage { get; set; }

    public string? ApproverUserId { get; set; }
}
