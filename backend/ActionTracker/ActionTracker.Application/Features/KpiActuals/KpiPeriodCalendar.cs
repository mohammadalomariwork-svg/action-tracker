using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;

namespace ActionTracker.Application.Features.KpiActuals;

/// <summary>
/// CR-001-F §4.3 — calendar helpers for KPI actual reporting periods.
/// Submission of an actual is rejected when the period has not yet ended.
/// </summary>
public static class KpiPeriodCalendar
{
    /// <summary>
    /// UTC end-of-day for the last day of the given period. The submit guard
    /// compares <see cref="DateTime.UtcNow"/> against this value.
    /// </summary>
    public static DateTime GetPeriodEndUtc(KpiActualPeriodType type, int index, int year)
    {
        ValidateIndex(type, index);

        return type switch
        {
            KpiActualPeriodType.Quarter => index switch
            {
                1 => EndOfDay(year, 3, 31),
                2 => EndOfDay(year, 6, 30),
                3 => EndOfDay(year, 9, 30),
                4 => EndOfDay(year, 12, 31),
                _ => throw new ArgumentOutOfRangeException(nameof(index)),
            },
            KpiActualPeriodType.SemiAnnual => index switch
            {
                1 => EndOfDay(year, 6, 30),
                2 => EndOfDay(year, 12, 31),
                _ => throw new ArgumentOutOfRangeException(nameof(index)),
            },
            KpiActualPeriodType.Annual => EndOfDay(year, 12, 31),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
    }

    public static string GetPeriodLabel(KpiActualPeriodType type, int index)
    {
        ValidateIndex(type, index);
        return type switch
        {
            KpiActualPeriodType.Quarter    => $"Q{index}",
            KpiActualPeriodType.SemiAnnual => $"H{index}",
            KpiActualPeriodType.Annual     => "Annual",
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
    }

    /// <summary>
    /// Maps a <c>Kpi.Period</c> (Monthly/Quarterly/SemiAnnual/Yearly) to the
    /// matching actuals period type. Monthly KPIs are rolled up to Quarterly
    /// for actuals reporting (CR-001-F §2: actuals are Quarter/Semi/Annual).
    /// </summary>
    public static KpiActualPeriodType FromKpiPeriod(MeasurementPeriod period) => period switch
    {
        MeasurementPeriod.Monthly    => KpiActualPeriodType.Quarter,
        MeasurementPeriod.Quarterly  => KpiActualPeriodType.Quarter,
        MeasurementPeriod.SemiAnnual => KpiActualPeriodType.SemiAnnual,
        MeasurementPeriod.Yearly     => KpiActualPeriodType.Annual,
        _ => throw new ArgumentOutOfRangeException(nameof(period)),
    };

    public static int SlotCount(KpiActualPeriodType type) => type switch
    {
        KpiActualPeriodType.Quarter    => 4,
        KpiActualPeriodType.SemiAnnual => 2,
        KpiActualPeriodType.Annual     => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    private static void ValidateIndex(KpiActualPeriodType type, int index)
    {
        var max = SlotCount(type);
        if (index < 1 || index > max)
            throw new ArgumentOutOfRangeException(
                nameof(index),
                $"Index {index} is out of range for {type} (expected 1..{max}).");
    }

    private static DateTime EndOfDay(int year, int month, int day) =>
        new(year, month, day, 23, 59, 59, DateTimeKind.Utc);
}
