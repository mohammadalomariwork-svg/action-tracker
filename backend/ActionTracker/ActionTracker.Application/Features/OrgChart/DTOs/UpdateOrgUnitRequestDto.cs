using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Features.OrgChart.DTOs;

public class UpdateOrgUnitRequestDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string? Code { get; set; }

    public Guid? ParentId { get; set; }
}
