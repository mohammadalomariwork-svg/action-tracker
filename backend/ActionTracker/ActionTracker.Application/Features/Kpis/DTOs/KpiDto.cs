namespace ActionTracker.Application.Features.Kpis.DTOs;

public class KpiDto
{
    public Guid Id { get; set; }
    public int KpiNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CalculationMethod { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public int PeriodValue { get; set; }
    public string? Unit { get; set; }
    public Guid StrategicObjectiveId { get; set; }
    public string ObjectiveCode { get; set; } = string.Empty;
    public string ObjectiveStatement { get; set; } = string.Empty;
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
    public int TargetCount { get; set; }
}
