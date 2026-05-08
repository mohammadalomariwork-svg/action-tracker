using System.ComponentModel.DataAnnotations;
using ActionTracker.Domain.Enums;

namespace ActionTracker.Application.Features.Kpis.DTOs;

public class BulkUpsertKpiTargetsRequestDto
{
    [Required]
    public Guid KpiId { get; set; }

    [Required]
    [Range(2020, 2099)]
    public int Year { get; set; }

    [Required]
    [MinLength(1)]
    public List<MonthTargetDto> Targets { get; set; } = new();
}

public class MonthTargetDto
{
    [Required]
    [Range(1, 12)]
    public int Month { get; set; }

    public decimal? Target { get; set; }
    public decimal? Actual { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public decimal? Baseline { get; set; }

    [MaxLength(500)]
    public string? DataSource { get; set; }

    [MaxLength(2000)]
    public string? CalculationNotes { get; set; }

    /// <summary>If null, the existing direction is preserved (no-change semantics).</summary>
    public KpiDirection? Direction { get; set; }
}
