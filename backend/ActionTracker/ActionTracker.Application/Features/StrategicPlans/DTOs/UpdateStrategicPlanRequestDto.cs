using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Features.StrategicPlans.DTOs;

public class UpdateStrategicPlanRequestDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Range(2000, 2100)]
    public int StartYear { get; set; }

    [Required]
    [Range(1, 10)]
    public int DurationYears { get; set; }

    /// <summary>
    /// Optional re-assignment to a different org unit. Only honoured when the
    /// plan is in Draft. The service rejects targets that already host another
    /// non-archived plan, and cascades the change to linked StrategicObjectives
    /// so plan + objective org-unit references stay aligned.
    /// </summary>
    public Guid? OrgUnitId { get; set; }
}
