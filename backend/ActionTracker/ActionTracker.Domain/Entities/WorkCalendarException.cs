namespace ActionTracker.Domain.Entities;

/// <summary>
/// A dated override of a calendar's weekly pattern, e.g. a public holiday
/// (non-working), a special working day, or a reduced-hours period such as
/// Ramadan (working, with its own hours). The date range is inclusive.
/// </summary>
public class WorkCalendarException
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WorkCalendarId { get; set; }
    public WorkCalendar WorkCalendar { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    /// <summary>Last day covered by the exception (inclusive).</summary>
    public DateOnly EndDate { get; set; }

    public bool IsWorkingDay { get; set; }

    /// <summary>Working hours for the period; null keeps the weekly pattern's hours.</summary>
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
}
