using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Features.Kpis.DTOs;

public class UpdateKpiRequestDto
{
    [Required]
    [MaxLength(300)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string CalculationMethod { get; set; } = string.Empty;

    [Required]
    [Range(1, 4)]
    public int Period { get; set; }

    [MaxLength(50)]
    public string? Unit { get; set; }
}
