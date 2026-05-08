using ActionTracker.Domain.Enums;

namespace ActionTracker.Application.Features.StrategicPlans.DTOs;

public class StrategicPlanDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid OrgUnitId { get; set; }
    public string OrgUnitName { get; set; } = string.Empty;
    public string? OrgUnitCode { get; set; }
    public int StartYear { get; set; }
    public int DurationYears { get; set; }
    public int EndYear { get; set; }
    public StrategicPlanStatus Status { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? ApprovedByUserId { get; set; }
    public string? ApprovedByName { get; set; }
    public int ObjectiveCount { get; set; }
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
}
