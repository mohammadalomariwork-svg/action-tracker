namespace ActionTracker.Domain.Entities;

/// <summary>
/// One day of a calendar's weekly pattern. A working day carries its working
/// hours; a non-working day has no hours.
/// </summary>
public class WorkCalendarDay
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WorkCalendarId { get; set; }
    public WorkCalendar WorkCalendar { get; set; } = null!;

    public DayOfWeek DayOfWeek { get; set; }

    public bool IsWorkingDay { get; set; }

    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
}
