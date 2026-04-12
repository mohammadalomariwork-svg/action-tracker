namespace ActionTracker.Application.Features.ProjectRisks.DTOs;

public class ProjectRiskDto
{
    public Guid Id { get; set; }
    public string RiskCode { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int ProbabilityScore { get; set; }
    public int ImpactScore { get; set; }
    public int RiskScore { get; set; }
    public string RiskRating { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? MitigationPlan { get; set; }
    public string? ContingencyPlan { get; set; }
    public string? RiskOwnerUserId { get; set; }
    public string? RiskOwnerDisplayName { get; set; }
    public DateTime IdentifiedDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? ClosedDate { get; set; }
    public string? Notes { get; set; }
    public string? CreatedByUserId { get; set; }
    public string? CreatedByDisplayName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
