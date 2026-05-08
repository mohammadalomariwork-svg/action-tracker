using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Features.StrategicObjectives.DTOs;

public class CreateStrategicObjectiveRequestDto
{
    [Required]
    [MaxLength(300)]
    public string Statement { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public Guid OrgUnitId { get; set; }

    /// <summary>
    /// Optional explicit attachment to a strategic plan. When omitted, the
    /// service auto-resolves the active plan for <see cref="OrgUnitId"/>.
    /// When supplied, the service validates that <see cref="OrgUnitId"/> is
    /// inside the plan's subtree (the plan's own unit or a descendant) and
    /// uses the supplied id directly — needed when the caller is creating
    /// against a sub-unit that may not have its own plan.
    /// </summary>
    public Guid? StrategicPlanId { get; set; }
}
