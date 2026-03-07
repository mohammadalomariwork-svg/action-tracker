using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Features.Kpis.DTOs;

public class UpsertKpiTargetRequestDto
{
    [Required]
    public Guid KpiId { get; set; }

    [Required]
    [Range(2020, 2099)]
    public int Year { get; set; }

    [Required]
    [Range(1, 12)]
    public int Month { get; set; }

    public decimal? Target { get; set; }
    public decimal? Actual { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
