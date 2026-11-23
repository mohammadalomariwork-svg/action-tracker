namespace ActionTracker.Application.Features.Calendars.DTOs;

/// <summary>
/// The working calendar that applies to a workspace (WO-2026-11 C-3), for the
/// date fields of its action items, milestones and projects and for the
/// Working Calendar section of Workspace Settings.
/// </summary>
public class WorkspaceCalendarDto
{
    public Guid WorkspaceId { get; set; }

    /// <summary>The workspace's own setting. Null = "use the default calendar".</summary>
    public Guid? WorkCalendarId { get; set; }

    /// <summary>The calendar that actually applies: the workspace's own, or the default.</summary>
    public Guid CalendarId { get; set; }
    public string CalendarName { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = string.Empty;

    /// <summary>True when the calendar that applies is the default calendar.</summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// True when the workspace has no calendar of its own (or its calendar is
    /// no longer active) and the default applies instead.
    /// </summary>
    public bool IsInherited { get; set; }

    /// <summary>First and last day of the window <see cref="Exceptions"/> covers (inclusive).</summary>
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }

    /// <summary>Seven rows, Sunday to Saturday.</summary>
    public List<WorkCalendarDayDto> Days { get; set; } = new();

    /// <summary>Exceptions that overlap <see cref="From"/>..<see cref="To"/>, ordered by start date.</summary>
    public List<WorkCalendarExceptionDto> Exceptions { get; set; } = new();
}

/// <summary>Body of <c>PUT /api/workspaces/{id}/calendar</c>.</summary>
public class SetWorkspaceCalendarDto
{
    /// <summary>The calendar to use. Null = go back to the default calendar.</summary>
    public Guid? WorkCalendarId { get; set; }
}

/// <summary>An active calendar offered in the Workspace Settings dropdown.</summary>
public class WorkspaceCalendarOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TimeZoneId { get; set; } = string.Empty;
    public bool IsDefault { get; set; }

    /// <summary>Seven rows, Sunday to Saturday — lets the page summarise the week before saving.</summary>
    public List<WorkCalendarDayDto> Days { get; set; } = new();
}
