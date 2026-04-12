namespace ActionTracker.Application.Features.ProjectRisks.DTOs;

public class ProjectRiskSummaryDto
{
    public Guid Id { get; set; }
    public string RiskCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int RiskScore { get; set; }
    public string RiskRating { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? RiskOwnerDisplayName { get; set; }
    public DateTime IdentifiedDate { get; set; }
    public DateTime? DueDate { get; set; }
}
