using ActionTracker.Application.Features.Calendars.Interfaces;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;

namespace ActionTracker.Application.Features.ActionItems;

/// <summary>
/// Actual start and actual end dates on action items (WO-2026-11 A-1, Q9).
///
/// <list type="bullet">
/// <item>Actual start is set to today on a move into In Progress when it is empty. It is never cleared automatically.</item>
/// <item>Actual end is set to today on a move into Done, unless the same save supplies it, and cleared when the item leaves Done.</item>
/// <item>Both can be edited by anyone who may edit the item. Neither may be after today, and actual end may not be before actual start.</item>
/// <item>Actual end exists only while the item is Done.</item>
/// </list>
///
/// Actual dates are recorded facts: the planned-date freeze, the date-change
/// approval workflow and the working-day rule (C-3) do not apply to them.
/// Values are stored as a calendar day at midnight, like the planned dates.
///
/// Every path that changes an action item's status calls <see cref="Resolve"/>
/// so the rule is the same everywhere.
/// </summary>
public static class ActionItemActualDates
{
    public const string ActualStartField = "Actual start";
    public const string ActualEndField   = "Actual end";

    /// <summary>
    /// Today as a calendar day in <paramref name="timeZoneId"/>, at midnight.
    /// Falls back to the UTC day when the time zone is missing or unknown.
    /// </summary>
    public static DateTime Today(string? timeZoneId, DateTime utcNow)
    {
        var utc = utcNow.Kind == DateTimeKind.Utc ? utcNow : DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        var local = !string.IsNullOrWhiteSpace(timeZoneId)
                    && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId.Trim(), out var zone)
            ? TimeZoneInfo.ConvertTimeFromUtc(utc, zone)
            : utc;
        return DateTime.SpecifyKind(local.Date, DateTimeKind.Utc);
    }

    /// <summary>
    /// Today in the time zone of the workspace's working calendar (the default
    /// calendar when the workspace has none), or the UTC day when no calendar
    /// is configured at all.
    /// </summary>
    public static async Task<DateTime> TodayForWorkspaceAsync(
        IWorkCalendarService calendars, Guid workspaceId, CancellationToken ct = default)
    {
        string? timeZoneId = null;
        try
        {
            timeZoneId = (await calendars.GetForWorkspaceAsync(workspaceId, ct))?.TimeZoneId;
        }
        catch (InvalidOperationException)
        {
            // No calendar is configured: use the UTC day.
        }
        return Today(timeZoneId, DateTime.UtcNow);
    }

    /// <summary>
    /// Works out the actual dates an action item keeps after a save.
    /// </summary>
    /// <param name="previousStatus">The stored status before the save; null on create.</param>
    /// <param name="requestedStatus">The status the save asked for. It differs from
    /// <paramref name="resultingStatus"/> when the past-due rule turns In Progress into Overdue.</param>
    /// <param name="resultingStatus">The status the item has after the save.</param>
    /// <param name="currentStart">The stored actual start.</param>
    /// <param name="currentEnd">The stored actual end.</param>
    /// <param name="suppliedStart">An actual start sent with the save; null = leave it.</param>
    /// <param name="clearStart">True when the save clears the actual start.</param>
    /// <param name="suppliedEnd">An actual end sent with the save; null = leave it.</param>
    /// <param name="today">Today as a calendar day (see <see cref="Today"/>).</param>
    /// <exception cref="ArgumentException">A supplied value breaks a rule. The message names the field.</exception>
    public static (DateTime? Start, DateTime? End) Resolve(
        ActionStatus? previousStatus,
        ActionStatus requestedStatus,
        ActionStatus resultingStatus,
        DateTime? currentStart,
        DateTime? currentEnd,
        DateTime? suppliedStart,
        bool clearStart,
        DateTime? suppliedEnd,
        DateTime today)
    {
        today = ToDay(today);
        var start = suppliedStart.HasValue ? ToDay(suppliedStart.Value) : (DateTime?)null;
        var end   = suppliedEnd.HasValue   ? ToDay(suppliedEnd.Value)   : (DateTime?)null;

        // ── Supplied values ──────────────────────────────────────────────────
        if (clearStart && start.HasValue)
            throw new ArgumentException($"{ActualStartField} was both set and cleared. Send one or the other.");
        if (start > today)
            throw new ArgumentException($"{ActualStartField} ({start:yyyy-MM-dd}) cannot be after today ({today:yyyy-MM-dd}).");
        if (end > today)
            throw new ArgumentException($"{ActualEndField} ({end:yyyy-MM-dd}) cannot be after today ({today:yyyy-MM-dd}).");
        if (end.HasValue && resultingStatus != ActionStatus.Done)
            throw new ArgumentException($"{ActualEndField} can only be set when the action item is Done.");

        // ── Actual start ─────────────────────────────────────────────────────
        var movedIntoInProgress = requestedStatus == ActionStatus.InProgress
                                  && previousStatus != ActionStatus.InProgress;

        var resultStart = clearStart ? null : start ?? currentStart;
        if (resultStart is null && movedIntoInProgress && !clearStart)
            resultStart = today;

        // ── Actual end ───────────────────────────────────────────────────────
        DateTime? resultEnd;
        if (resultingStatus != ActionStatus.Done)
            resultEnd = null;                               // not Done, or leaving Done
        else if (end.HasValue)
            resultEnd = end;                                // supplied with this save
        else if (previousStatus != ActionStatus.Done)
            resultEnd = today;                              // moving into Done
        else
            resultEnd = currentEnd;                         // staying Done

        // ── Order ────────────────────────────────────────────────────────────
        if (resultStart.HasValue && resultEnd.HasValue && resultEnd < resultStart)
        {
            if (start.HasValue || end.HasValue)
                throw new ArgumentException(
                    $"{ActualEndField} ({resultEnd:yyyy-MM-dd}) cannot be before {ActualStartField.ToLowerInvariant()} ({resultStart:yyyy-MM-dd}).");

            // Only automatic values are involved (e.g. the workspace moved to a
            // calendar in an earlier time zone): never block a status change on that.
            resultEnd = resultStart;
        }

        return (resultStart, resultEnd);
    }

    /// <summary>Applies <see cref="Resolve"/> for a status change that carries no supplied actual dates.</summary>
    public static void ApplyStatusChange(
        ActionItem item, ActionStatus previousStatus, ActionStatus requestedStatus, DateTime today)
    {
        var (start, end) = Resolve(
            previousStatus, requestedStatus, item.Status,
            item.ActualStartDate, item.ActualEndDate,
            suppliedStart: null, clearStart: false, suppliedEnd: null,
            today);
        item.ActualStartDate = start;
        item.ActualEndDate   = end;
    }

    /// <summary>The calendar day of <paramref name="value"/>, at midnight.</summary>
    public static DateTime ToDay(DateTime value) => DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
}
