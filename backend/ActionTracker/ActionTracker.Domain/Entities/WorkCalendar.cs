namespace ActionTracker.Domain.Entities;

/// <summary>
/// A working calendar: a weekly pattern of working days and hours plus dated
/// exceptions (holidays, special working days, reduced-hours periods).
/// Exactly one non-deleted calendar is the default; a workspace with no
/// calendar of its own uses the default.
/// </summary>
public class WorkCalendar
{
    /// <summary>Time zone used when a calendar does not name one.</summary>
    public const string DefaultTimeZoneId = "Asia/Dubai";

    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Display name, unique among non-deleted calendars.</summary>
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>IANA or Windows time-zone id the working hours are expressed in.</summary>
    public string TimeZoneId { get; set; } = DefaultTimeZoneId;

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    /// <summary>The weekly pattern — always exactly seven rows, one per day of week.</summary>
    public ICollection<WorkCalendarDay> Days { get; set; } = new List<WorkCalendarDay>();

    /// <summary>Dated overrides of the weekly pattern.</summary>
    public ICollection<WorkCalendarException> Exceptions { get; set; } = new List<WorkCalendarException>();
}
