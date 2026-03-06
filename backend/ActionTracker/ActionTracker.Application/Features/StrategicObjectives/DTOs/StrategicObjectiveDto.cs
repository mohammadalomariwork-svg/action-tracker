namespace ActionTracker.Application.Features.StrategicObjectives.DTOs;

public class StrategicObjectiveDto
{
    public Guid Id { get; set; }
    public string ObjectiveCode { get; set; } = string.Empty;
    public string Statement { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid OrgUnitId { get; set; }
    public string OrgUnitName { get; set; } = string.Empty;
    public string? OrgUnitCode { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public string? DeletedBy { get; set; }
    public string? CreatedByName { get; set; }
    public string? UpdatedByName { get; set; }
    public string? DeletedByName { get; set; }
    public int KpiCount { get; set; }
}
