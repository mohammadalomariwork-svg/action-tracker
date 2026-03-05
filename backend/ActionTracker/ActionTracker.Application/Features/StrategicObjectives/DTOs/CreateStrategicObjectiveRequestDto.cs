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
}
