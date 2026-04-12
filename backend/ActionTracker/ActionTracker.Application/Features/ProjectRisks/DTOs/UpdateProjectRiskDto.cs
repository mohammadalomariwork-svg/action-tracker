namespace ActionTracker.Application.Features.ProjectRisks.DTOs;

public class UpdateProjectRiskDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int ProbabilityScore { get; set; }
    public int ImpactScore { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? MitigationPlan { get; set; }
    public string? ContingencyPlan { get; set; }
    public string? RiskOwnerUserId { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? ClosedDate { get; set; }
    public string? Notes { get; set; }
}
