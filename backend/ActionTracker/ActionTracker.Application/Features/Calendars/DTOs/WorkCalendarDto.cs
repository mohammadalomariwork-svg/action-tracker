namespace ActionTracker.Application.Features.Calendars.DTOs;

/// <summary>A working calendar with its weekly pattern and exceptions.</summary>
public class WorkCalendarDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TimeZoneId { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    /// <summary>Seven rows, Sunday to Saturday.</summary>
    public List<WorkCalendarDayDto> Days { get; set; } = new();

    /// <summary>Ordered by start date.</summary>
    public List<WorkCalendarExceptionDto> Exceptions { get; set; } = new();
}

/// <summary>One day of the weekly pattern. Times are <c>HH:mm:ss</c> in the calendar's time zone.</summary>
public class WorkCalendarDayDto
{
    public DayOfWeek DayOfWeek { get; set; }
    public bool IsWorkingDay { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
}

/// <summary>A dated override of the weekly pattern; the date range is inclusive.</summary>
public class WorkCalendarExceptionDto
{
    /// <summary>Null on input = a new exception. Set = update that existing row.</summary>
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsWorkingDay { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
}
