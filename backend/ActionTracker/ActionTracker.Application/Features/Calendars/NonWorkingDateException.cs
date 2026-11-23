using System.Globalization;

namespace ActionTracker.Application.Features.Calendars;

/// <summary>
/// A save was refused because a date it creates or changes is not a working
/// day in the workspace's calendar (WO-2026-11 C-3, rule Q7).
///
/// Derives from <see cref="ArgumentException"/> so it keeps the existing
/// "invalid input = 400" contract. Controllers whose endpoints do not already
/// catch <see cref="ArgumentException"/> catch this type and return its
/// <see cref="Exception.Message"/> in an <c>ApiResponse</c>, because the
/// global exception middleware hides the message in Production.
/// </summary>
public sealed class NonWorkingDateException : ArgumentException
{
    public NonWorkingDateException(string calendarName, IReadOnlyList<NonWorkingDate> dates)
        : base(BuildMessage(calendarName, dates))
    {
        CalendarName = calendarName;
        Dates        = dates;
    }

    /// <summary>Name of the calendar the dates were checked against.</summary>
    public string CalendarName { get; }

    /// <summary>Every refused date, in the order the fields were checked.</summary>
    public IReadOnlyList<NonWorkingDate> Dates { get; }

    private static string BuildMessage(string calendarName, IReadOnlyList<NonWorkingDate> dates) =>
        string.Join(" ", dates.Select(d =>
        {
            var sentence =
                $"{d.Field} {Format(d.Date)} is not a working day in the workspace calendar \"{calendarName}\".";
            return d.NextWorkingDay is { } next ? $"{sentence} Next working day: {Format(next)}." : sentence;
        }));

    private static string Format(DateOnly date) =>
        date.ToString("yyyy-MM-dd '('dddd')'", CultureInfo.InvariantCulture);
}

/// <summary>One refused date and the first working day on or after it (null when none is reachable).</summary>
public sealed record NonWorkingDate(string Field, DateOnly Date, DateOnly? NextWorkingDay);
