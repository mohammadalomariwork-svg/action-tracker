using ActionTracker.Domain.Enums;

namespace ActionTracker.Domain.Entities;

public class ProjectApprovalRequest
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public string RequestedByUserId { get; set; } = string.Empty;
    public string RequestedByDisplayName { get; set; } = string.Empty;

    public string? ReviewedByUserId { get; set; }
    public string? ReviewedByDisplayName { get; set; }

    public ProjectApprovalStatus Status { get; set; } = ProjectApprovalStatus.Pending;

    public string Reason { get; set; } = string.Empty;
    public string? ReviewComment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }

    public bool IsDeleted { get; set; } = false;

    // Navigation
    public Project Project { get; set; } = null!;
}
