using ActionTracker.Application.Features.Calendars.DTOs;
using ActionTracker.Application.Features.Calendars.Services;

namespace ActionTracker.Application.Features.Calendars.Interfaces;

/// <summary>
/// Working calendars: admin CRUD plus the calculation API other features use
/// for date rules.
///
/// Error contract (mapped by <c>CalendarsController</c>):
/// <see cref="KeyNotFoundException"/> = not found,
/// <see cref="ArgumentException"/> = invalid input,
/// <see cref="InvalidOperationException"/> = a rule forbids the change
/// (duplicate name, calendar is the default, calendar is in use).
/// </summary>
public interface IWorkCalendarService
{
    // ── Admin CRUD ────────────────────────────────────────────────────────────

    /// <summary>All calendars, default first then by name.</summary>
    Task<List<WorkCalendarDto>> GetAllAsync(bool includeDeleted = false, CancellationToken ct = default);

    Task<WorkCalendarDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<WorkCalendarDto> CreateAsync(CreateWorkCalendarDto dto, string userId, CancellationToken ct = default);

    Task<WorkCalendarDto> UpdateAsync(Guid id, UpdateWorkCalendarDto dto, string userId, CancellationToken ct = default);

    /// <summary>Soft-deletes a calendar. Refused for the default calendar and for one a workspace uses.</summary>
    Task SoftDeleteAsync(Guid id, string userId, CancellationToken ct = default);

    Task RestoreAsync(Guid id, string userId, CancellationToken ct = default);

    /// <summary>Makes this calendar the single default; the previous default loses the flag in the same save.</summary>
    Task<WorkCalendarDto> SetDefaultAsync(Guid id, string userId, CancellationToken ct = default);

    /// <summary>
    /// Workspaces using the calendar. Names only the workspaces
    /// <paramref name="userId"/> may see; the rest are reported as a count.
    /// </summary>
    Task<WorkCalendarUsageDto> GetUsageAsync(Guid id, string userId, CancellationToken ct = default);

    // ── Calculation API ───────────────────────────────────────────────────────

    /// <summary>The default calendar, resolved for calculations (cached).</summary>
    Task<ResolvedWorkCalendar> GetDefaultAsync(CancellationToken ct = default);

    /// <summary>
    /// The calendar that applies to a workspace, resolved for calculations
    /// (cached): the workspace's own calendar, or the default when it has none.
    /// Use the result's <c>IsWorkingDay</c>, <c>NextWorkingDay</c>,
    /// <c>AddWorkingDays</c> and <c>WorkingDaysBetween</c>.
    /// </summary>
    Task<ResolvedWorkCalendar> GetForWorkspaceAsync(Guid workspaceId, CancellationToken ct = default);
}
