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

    /// <summary>
    /// The working-day rule for saved dates (WO-2026-11 C-3, Q7 / Q8). Checks
    /// every entry of <paramref name="checks"/> whose date is created or
    /// changed (<see cref="WorkingDateCheck.IsChanged"/>) against the calendar
    /// of <paramref name="workspaceId"/> (the default calendar when null) and
    /// throws <see cref="NonWorkingDateException"/> listing every one that is
    /// not a working day. Unchanged dates are never checked, so existing
    /// records keep saving. Does nothing when no calendar is configured at all.
    /// </summary>
    Task EnsureWorkingDaysAsync(
        Guid? workspaceId, IReadOnlyCollection<WorkingDateCheck> checks, CancellationToken ct = default);

    // ── Workspace setting (WO-2026-11 C-3) ────────────────────────────────────

    /// <summary>
    /// The calendar that applies to a workspace, with its weekly pattern and
    /// the exceptions overlapping <paramref name="from"/>..<paramref name="to"/>
    /// (default: one year back to three years ahead).
    /// <see cref="KeyNotFoundException"/> when the workspace does not exist or
    /// is a private workspace the caller is not part of;
    /// <see cref="ArgumentException"/> for a reversed or over-long window.
    /// </summary>
    Task<WorkspaceCalendarDto> GetWorkspaceCalendarAsync(
        Guid workspaceId, DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default);

    /// <summary>
    /// Points a workspace at a calendar, or back at the default (null).
    /// <see cref="KeyNotFoundException"/> when <paramref name="userId"/> cannot
    /// see the workspace; <see cref="ArgumentException"/> when the calendar does
    /// not exist, is deleted or is inactive. Only dates saved afterwards are
    /// checked against the new calendar; existing records are left alone.
    /// </summary>
    Task<WorkspaceCalendarDto> SetWorkspaceCalendarAsync(
        Guid workspaceId, SetWorkspaceCalendarDto dto, string userId, CancellationToken ct = default);

    /// <summary>Active calendars a workspace can be pointed at, default first then by name.</summary>
    Task<List<WorkspaceCalendarOptionDto>> GetWorkspaceCalendarOptionsAsync(CancellationToken ct = default);
}
