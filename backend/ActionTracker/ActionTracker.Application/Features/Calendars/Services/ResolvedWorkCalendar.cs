using ActionTracker.Domain.Entities;

namespace ActionTracker.Application.Features.Calendars.Services;

/// <summary>
/// An immutable, in-memory snapshot of a <see cref="WorkCalendar"/> that
/// answers working-day questions without touching the database. Safe to share
/// between requests, which is what makes it cacheable.
///
/// Rules:
/// <list type="bullet">
/// <item>An exception covering a date overrides the weekly pattern for that date.</item>
/// <item>When several exceptions cover the same date, the shortest one wins
/// (a three-day holiday inside a month of reduced hours beats the month);
/// on a tie a non-working exception wins.</item>
/// </list>
/// </summary>
public sealed class ResolvedWorkCalendar
{
    /// <summary>
    /// Upper bound on how far a search for a working day walks. Stops a
    /// calendar with no reachable working day from looping forever.
    /// </summary>
    private const int MaxSearchDays = 3660;

    private readonly DayRule[] _week = new DayRule[7];
    private readonly ExceptionRule[] _exceptions;

    public Guid Id { get; }
    public string Name { get; }
    public string TimeZoneId { get; }
    public bool IsDefault { get; }

    public ResolvedWorkCalendar(WorkCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        Id         = calendar.Id;
        Name       = calendar.Name;
        TimeZoneId = calendar.TimeZoneId;
        IsDefault  = calendar.IsDefault;

        // A day of week with no row is treated as non-working.
        foreach (var day in calendar.Days)
            _week[(int)day.DayOfWeek] = new DayRule(day.IsWorkingDay, day.StartTime, day.EndTime);

        _exceptions = calendar.Exceptions
            .Select(e => new ExceptionRule(e.StartDate, e.EndDate, e.IsWorkingDay, e.StartTime, e.EndTime))
            // Most specific first: shortest range, then non-working before working.
            .OrderBy(e => e.EndDate.DayNumber - e.StartDate.DayNumber)
            .ThenBy(e => e.IsWorkingDay)
            .ToArray();
    }

    /// <summary>True when <paramref name="date"/> is a working day in this calendar.</summary>
    public bool IsWorkingDay(DateOnly date)
    {
        var exception = FindException(date);
        return exception?.IsWorkingDay ?? _week[(int)date.DayOfWeek].IsWorkingDay;
    }

    /// <summary>
    /// Working hours for <paramref name="date"/> in the calendar's time zone,
    /// or null when it is not a working day (or the calendar holds no hours for it).
    /// </summary>
    public (TimeOnly Start, TimeOnly End)? GetWorkingHours(DateOnly date)
    {
        var weekly    = _week[(int)date.DayOfWeek];
        var exception = FindException(date);

        if (exception is null)
            return weekly.IsWorkingDay ? weekly.Hours : null;

        if (!exception.Value.IsWorkingDay)
            return null;

        // A working exception without hours of its own keeps the weekly hours.
        return exception.Value.Hours ?? weekly.Hours;
    }

    /// <summary>
    /// The first working day on or after <paramref name="date"/> —
    /// <paramref name="date"/> itself when it is already a working day.
    /// </summary>
    public DateOnly NextWorkingDay(DateOnly date) => Seek(date, step: 1);

    /// <summary>
    /// The last working day on or before <paramref name="date"/> —
    /// <paramref name="date"/> itself when it is already a working day.
    /// </summary>
    public DateOnly PreviousWorkingDay(DateOnly date) => Seek(date, step: -1);

    /// <summary>
    /// Moves <paramref name="workingDays"/> working days from
    /// <paramref name="date"/>; the start date itself is not counted. A
    /// negative number moves backwards. Zero returns the date unchanged.
    /// </summary>
    public DateOnly AddWorkingDays(DateOnly date, int workingDays)
    {
        if (workingDays == 0) return date;

        var step      = workingDays > 0 ? 1 : -1;
        var remaining = Math.Abs(workingDays);
        var current   = date;

        while (remaining > 0)
        {
            current = Seek(current.AddDays(step), step);
            remaining--;
        }

        return current;
    }

    /// <summary>
    /// Number of working days from <paramref name="start"/> to
    /// <paramref name="end"/>, counting both ends. Returns 0 when
    /// <paramref name="end"/> is before <paramref name="start"/>.
    /// </summary>
    public int WorkingDaysBetween(DateOnly start, DateOnly end)
    {
        var count = 0;
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            if (IsWorkingDay(day)) count++;
            if (day == DateOnly.MaxValue) break;
        }

        return count;
    }

    private DateOnly Seek(DateOnly from, int step)
    {
        var current = from;
        for (var i = 0; i < MaxSearchDays; i++)
        {
            if (IsWorkingDay(current)) return current;
            current = current.AddDays(step);
        }

        throw new InvalidOperationException(
            $"Calendar '{Name}' has no working day within {MaxSearchDays} days of {from:yyyy-MM-dd}.");
    }

    private ExceptionRule? FindException(DateOnly date)
    {
        // Already ordered most-specific first, so the first hit is the winner.
        foreach (var exception in _exceptions)
        {
            if (date >= exception.StartDate && date <= exception.EndDate)
                return exception;
        }

        return null;
    }

    private readonly record struct DayRule(bool IsWorkingDay, TimeOnly? StartTime, TimeOnly? EndTime)
    {
        public (TimeOnly Start, TimeOnly End)? Hours =>
            StartTime.HasValue && EndTime.HasValue ? (StartTime.Value, EndTime.Value) : null;
    }

    private readonly record struct ExceptionRule(
        DateOnly StartDate, DateOnly EndDate, bool IsWorkingDay, TimeOnly? StartTime, TimeOnly? EndTime)
    {
        public (TimeOnly Start, TimeOnly End)? Hours =>
            StartTime.HasValue && EndTime.HasValue ? (StartTime.Value, EndTime.Value) : null;
    }
}
