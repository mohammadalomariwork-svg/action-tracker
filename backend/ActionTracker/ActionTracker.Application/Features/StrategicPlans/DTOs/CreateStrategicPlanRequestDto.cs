using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Features.StrategicPlans.DTOs;

public class CreateStrategicPlanRequestDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public Guid OrgUnitId { get; set; }

    [Required]
    [Range(2000, 2100)]
    public int StartYear { get; set; }

    [Required]
    [Range(1, 10)]
    public int DurationYears { get; set; }
}
