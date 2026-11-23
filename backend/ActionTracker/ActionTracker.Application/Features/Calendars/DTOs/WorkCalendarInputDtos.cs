using ActionTracker.Domain.Entities;

namespace ActionTracker.Application.Features.Calendars.DTOs;

/// <summary>Fields shared by calendar create and update requests.</summary>
public abstract class WorkCalendarInputDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>IANA or Windows time-zone id. Defaults to Asia/Dubai.</summary>
    public string TimeZoneId { get; set; } = WorkCalendar.DefaultTimeZoneId;

    public bool IsActive { get; set; } = true;

    /// <summary>The full weekly pattern — exactly seven rows, one per day of week.</summary>
    public List<WorkCalendarDayDto> Days { get; set; } = new();

    /// <summary>
    /// The full list of exceptions. On update, rows missing from the list are
    /// removed, rows with an <c>Id</c> are updated, rows without one are added.
    /// </summary>
    public List<WorkCalendarExceptionDto> Exceptions { get; set; } = new();
}

public class CreateWorkCalendarDto : WorkCalendarInputDto
{
}

public class UpdateWorkCalendarDto : WorkCalendarInputDto
{
}
