namespace ActionTracker.Application.Features.Projects.DTOs;

public class SubmitProjectApprovalRequestDto
{
    public Guid ProjectId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class ReviewProjectApprovalRequestDto
{
    public Guid RequestId { get; set; }
    public bool IsApproved { get; set; }
    public string? ReviewComment { get; set; }
}

public class ProjectApprovalRequestDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string RequestedByUserId { get; set; } = string.Empty;
    public string RequestedByDisplayName { get; set; } = string.Empty;
    public string? ReviewedByUserId { get; set; }
    public string? ReviewedByDisplayName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? ReviewComment { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

public class ProjectApprovalSummaryDto
{
    public int PendingProjectApprovals { get; set; }
}

public class SubmitValidationResultDto
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}
