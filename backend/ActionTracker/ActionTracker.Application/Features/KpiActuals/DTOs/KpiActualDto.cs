using ActionTracker.Domain.Enums;

namespace ActionTracker.Application.Features.KpiActuals.DTOs;

public class KpiActualDto
{
    public Guid Id { get; set; }
    public Guid KpiTargetId { get; set; }
    public Guid KpiId { get; set; }
    public string KpiName { get; set; } = string.Empty;
    public Guid StrategicObjectiveId { get; set; }
    public Guid OrgUnitId { get; set; }
    public string OrgUnitName { get; set; } = string.Empty;

    public KpiActualPeriodType PeriodType { get; set; }
    public string PeriodTypeName => PeriodType.ToString();
    public int PeriodIndex { get; set; }
    public int Year { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;

    public decimal ActualValue { get; set; }
    public decimal? TargetValue { get; set; }
    public KpiDirection Direction { get; set; }
    public string DirectionName => Direction.ToString();
    public decimal? AchievementPercent { get; set; }

    public string? Notes { get; set; }

    public KpiActualStatus Status { get; set; }
    public string StatusName => Status.ToString();

    public string? SubmittedByUserId { get; set; }
    public string? SubmittedByDisplayName { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public string? AssignedReviewerUserId { get; set; }
    public string? AssignedReviewerDisplayName { get; set; }
    public string? ReviewedByUserId { get; set; }
    public string? ReviewedByDisplayName { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewerComment { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }

    public int DocumentCount { get; set; }
}

public class CreateKpiActualRequestDto
{
    public Guid KpiTargetId { get; set; }
    public KpiActualPeriodType PeriodType { get; set; }
    public int PeriodIndex { get; set; }
    public int Year { get; set; }
    public decimal ActualValue { get; set; }
    public string? Notes { get; set; }
}

public class UpdateKpiActualRequestDto
{
    public decimal ActualValue { get; set; }
    public string? Notes { get; set; }
}

public class SubmitKpiActualRequestDto
{
    public Guid ActualId { get; set; }
}

public class ReviewKpiActualRequestDto
{
    public Guid ActualId { get; set; }
    public bool IsApproved { get; set; }
    public string? ReviewComment { get; set; }
}

public class KpiActualGridRowDto
{
    public KpiActualPeriodType PeriodType { get; set; }
    public int PeriodIndex { get; set; }
    public int Year { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;

    public decimal? TargetValue { get; set; }
    public KpiDirection Direction { get; set; }

    public KpiActualDto? Current { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public bool PeriodHasEnded { get; set; }
}

public class KpiActualGridResponseDto
{
    public Guid KpiId { get; set; }
    public Guid KpiTargetId { get; set; }
    public string KpiName { get; set; } = string.Empty;
    public KpiActualPeriodType PeriodType { get; set; }
    public string PeriodTypeName => PeriodType.ToString();
    public int Year { get; set; }
    public List<KpiActualGridRowDto> Rows { get; set; } = new();
}

public class KpiActualSummaryDto
{
    public int PendingKpiActuals { get; set; }
}
