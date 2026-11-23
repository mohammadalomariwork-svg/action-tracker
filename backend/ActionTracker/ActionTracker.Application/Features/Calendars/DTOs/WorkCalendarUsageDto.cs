namespace ActionTracker.Application.Features.Calendars.DTOs;

/// <summary>Which workspaces use a calendar.</summary>
public class WorkCalendarUsageDto
{
    public Guid CalendarId { get; set; }
    public bool IsDefault { get; set; }

    /// <summary>
    /// Every workspace that names this calendar explicitly, including ones the
    /// caller cannot see. This is the number the delete / deactivate guard uses.
    /// </summary>
    public int TotalWorkspaceCount { get; set; }

    /// <summary>Workspaces naming this calendar that the caller is allowed to see.</summary>
    public List<WorkCalendarUsageWorkspaceDto> Workspaces { get; set; } = new();

    /// <summary>Workspaces naming this calendar that the caller cannot see (private workspaces).</summary>
    public int HiddenWorkspaceCount { get; set; }

    /// <summary>
    /// For the default calendar: visible workspaces with no calendar of their
    /// own, which therefore fall back to this one. Always 0 for other calendars.
    /// </summary>
    public int InheritingWorkspaceCount { get; set; }
}

public class WorkCalendarUsageWorkspaceDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
