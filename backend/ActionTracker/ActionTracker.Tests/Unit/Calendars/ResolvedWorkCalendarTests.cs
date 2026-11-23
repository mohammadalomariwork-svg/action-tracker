using ActionTracker.Application.Features.Calendars.Services;
using ActionTracker.Domain.Entities;
using FluentAssertions;

namespace ActionTracker.Tests.Unit.Calendars;

/// <summary>
/// Calculation API of a resolved calendar: weekends, holiday ranges, special
/// working days, year boundaries and overlapping exceptions.
/// Reference week: Monday 23 Nov 2026 … Sunday 29 Nov 2026.
/// </summary>
public class ResolvedWorkCalendarTests
{
    private static readonly TimeOnly Eight     = new(8, 0);
    private static readonly TimeOnly Seventeen = new(17, 0);

    private static DateOnly D(int year, int month, int day) => new(year, month, day);

    /// <summary>Monday–Friday 08:00–17:00, Saturday–Sunday off, plus the given exceptions.</summary>
    private static ResolvedWorkCalendar Standard(params WorkCalendarException[] exceptions)
    {
        var calendar = new WorkCalendar { Name = "Test", IsDefault = true };

        foreach (var dayOfWeek in Enum.GetValues<DayOfWeek>())
        {
            var working = dayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
            calendar.Days.Add(new WorkCalendarDay
            {
                DayOfWeek    = dayOfWeek,
                IsWorkingDay = working,
                StartTime    = working ? Eight : null,
                EndTime      = working ? Seventeen : null,
            });
        }

        foreach (var exception in exceptions)
            calendar.Exceptions.Add(exception);

        return new ResolvedWorkCalendar(calendar);
    }

    private static WorkCalendarException Holiday(string name, DateOnly start, DateOnly end) =>
        new() { Name = name, StartDate = start, EndDate = end, IsWorkingDay = false };

    // ── Weekend ───────────────────────────────────────────────────────────────

    [Fact]
    public void IsWorkingDay_FollowsTheWeeklyPattern()
    {
        var calendar = Standard();

        D(2026, 11, 27).DayOfWeek.Should().Be(DayOfWeek.Friday);
        calendar.IsWorkingDay(D(2026, 11, 27)).Should().BeTrue();
        calendar.IsWorkingDay(D(2026, 11, 28)).Should().BeFalse("Saturday is off");
        calendar.IsWorkingDay(D(2026, 11, 29)).Should().BeFalse("Sunday is off");
        calendar.IsWorkingDay(D(2026, 11, 30)).Should().BeTrue();
    }

    [Fact]
    public void NextWorkingDay_SkipsTheWeekend_AndKeepsAWorkingDay()
    {
        var calendar = Standard();

        calendar.NextWorkingDay(D(2026, 11, 28)).Should().Be(D(2026, 11, 30)); // Sat → Mon
        calendar.NextWorkingDay(D(2026, 11, 29)).Should().Be(D(2026, 11, 30)); // Sun → Mon
        calendar.NextWorkingDay(D(2026, 11, 25)).Should().Be(D(2026, 11, 25)); // Wed stays
    }

    [Fact]
    public void PreviousWorkingDay_WalksBackOverTheWeekend()
    {
        var calendar = Standard();

        calendar.PreviousWorkingDay(D(2026, 11, 29)).Should().Be(D(2026, 11, 27)); // Sun → Fri
        calendar.PreviousWorkingDay(D(2026, 11, 27)).Should().Be(D(2026, 11, 27));
    }

    [Fact]
    public void AddWorkingDays_SkipsTheWeekend()
    {
        var calendar = Standard();

        calendar.AddWorkingDays(D(2026, 11, 27), 1).Should().Be(D(2026, 11, 30));  // Fri + 1 → Mon
        calendar.AddWorkingDays(D(2026, 11, 23), 5).Should().Be(D(2026, 11, 30));  // Mon + 5 → next Mon
        calendar.AddWorkingDays(D(2026, 11, 28), 1).Should().Be(D(2026, 11, 30));  // Sat + 1 → Mon
        calendar.AddWorkingDays(D(2026, 11, 30), -1).Should().Be(D(2026, 11, 27)); // Mon − 1 → Fri
        calendar.AddWorkingDays(D(2026, 11, 28), 0).Should().Be(D(2026, 11, 28));  // zero = unchanged
    }

    [Fact]
    public void WorkingDaysBetween_CountsBothEnds_AndIgnoresTheWeekend()
    {
        var calendar = Standard();

        calendar.WorkingDaysBetween(D(2026, 11, 23), D(2026, 11, 29)).Should().Be(5); // Mon..Sun
        calendar.WorkingDaysBetween(D(2026, 11, 23), D(2026, 11, 23)).Should().Be(1); // same working day
        calendar.WorkingDaysBetween(D(2026, 11, 28), D(2026, 11, 29)).Should().Be(0); // weekend only
        calendar.WorkingDaysBetween(D(2026, 11, 27), D(2026, 11, 23)).Should().Be(0); // end before start
    }

    // ── Holiday range ─────────────────────────────────────────────────────────

    [Fact]
    public void HolidayRange_OverridesTheWeeklyPattern_OnEveryDayItCovers()
    {
        // National Day: Wednesday 2 and Thursday 3 December 2026.
        var calendar = Standard(Holiday("National Day", D(2026, 12, 2), D(2026, 12, 3)));

        calendar.IsWorkingDay(D(2026, 12, 1)).Should().BeTrue();
        calendar.IsWorkingDay(D(2026, 12, 2)).Should().BeFalse();
        calendar.IsWorkingDay(D(2026, 12, 3)).Should().BeFalse();
        calendar.IsWorkingDay(D(2026, 12, 4)).Should().BeTrue();

        calendar.NextWorkingDay(D(2026, 12, 2)).Should().Be(D(2026, 12, 4));
        calendar.AddWorkingDays(D(2026, 12, 1), 1).Should().Be(D(2026, 12, 4));
        calendar.WorkingDaysBetween(D(2026, 11, 30), D(2026, 12, 4)).Should().Be(3); // Mon, Tue, Fri
        calendar.GetWorkingHours(D(2026, 12, 2)).Should().BeNull();
    }

    // ── Special working day ───────────────────────────────────────────────────

    [Fact]
    public void SpecialWorkingDay_TurnsAWeekendDayIntoAWorkingDay()
    {
        D(2026, 12, 5).DayOfWeek.Should().Be(DayOfWeek.Saturday);

        var calendar = Standard(new WorkCalendarException
        {
            Name         = "Make-up Saturday",
            StartDate    = D(2026, 12, 5),
            EndDate      = D(2026, 12, 5),
            IsWorkingDay = true,
            StartTime    = new TimeOnly(9, 0),
            EndTime      = new TimeOnly(13, 0),
        });

        calendar.IsWorkingDay(D(2026, 12, 5)).Should().BeTrue();
        calendar.IsWorkingDay(D(2026, 12, 12)).Should().BeFalse("only that one Saturday is special");
        calendar.NextWorkingDay(D(2026, 12, 5)).Should().Be(D(2026, 12, 5));
        calendar.AddWorkingDays(D(2026, 12, 4), 1).Should().Be(D(2026, 12, 5));
        calendar.WorkingDaysBetween(D(2026, 11, 30), D(2026, 12, 6)).Should().Be(6);
        calendar.GetWorkingHours(D(2026, 12, 5)).Should().Be((new TimeOnly(9, 0), new TimeOnly(13, 0)));
    }

    // ── Year boundary ─────────────────────────────────────────────────────────

    [Fact]
    public void YearBoundary_CalculationsCrossFromDecemberIntoJanuary()
    {
        D(2026, 12, 31).DayOfWeek.Should().Be(DayOfWeek.Thursday);

        // New Year's Day 2027 is a Friday, followed by the weekend.
        var calendar = Standard(Holiday("New Year", D(2027, 1, 1), D(2027, 1, 1)));

        calendar.AddWorkingDays(D(2026, 12, 31), 1).Should().Be(D(2027, 1, 4));
        calendar.AddWorkingDays(D(2027, 1, 4), -1).Should().Be(D(2026, 12, 31));
        calendar.NextWorkingDay(D(2027, 1, 1)).Should().Be(D(2027, 1, 4));
        calendar.WorkingDaysBetween(D(2026, 12, 28), D(2027, 1, 8)).Should().Be(9); // 10 weekdays − 1 holiday
    }

    [Fact]
    public void HolidayRange_SpanningTheYearBoundary_IsHonouredOnBothSides()
    {
        var calendar = Standard(Holiday("Winter closure", D(2026, 12, 30), D(2027, 1, 5)));

        calendar.IsWorkingDay(D(2026, 12, 29)).Should().BeTrue();
        calendar.IsWorkingDay(D(2026, 12, 31)).Should().BeFalse();
        calendar.IsWorkingDay(D(2027, 1, 5)).Should().BeFalse();
        calendar.NextWorkingDay(D(2026, 12, 30)).Should().Be(D(2027, 1, 6));
    }

    // ── Overlapping exceptions / reduced hours ────────────────────────────────

    [Fact]
    public void OverlappingExceptions_TheShortestOneWins()
    {
        var ramadan = new WorkCalendarException
        {
            Name         = "Ramadan hours",
            StartDate    = D(2027, 2, 8),
            EndDate      = D(2027, 3, 9),
            IsWorkingDay = true,
            StartTime    = new TimeOnly(9, 0),
            EndTime      = new TimeOnly(14, 0),
        };
        var holiday = Holiday("Holiday inside Ramadan", D(2027, 2, 16), D(2027, 2, 17));

        // Same result whichever order the rows arrive in.
        foreach (var calendar in new[] { Standard(ramadan, holiday), Standard(holiday, ramadan) })
        {
            calendar.IsWorkingDay(D(2027, 2, 15)).Should().BeTrue();
            calendar.GetWorkingHours(D(2027, 2, 15)).Should().Be((new TimeOnly(9, 0), new TimeOnly(14, 0)));

            calendar.IsWorkingDay(D(2027, 2, 16)).Should().BeFalse("the two-day holiday is more specific");
            calendar.IsWorkingDay(D(2027, 2, 17)).Should().BeFalse();
            calendar.IsWorkingDay(D(2027, 2, 18)).Should().BeTrue();
        }
    }

    [Fact]
    public void TiedNonWorkingAndWorkingExceptions_TheNonWorkingOneWins_WhateverTheOrder()
    {
        var open   = Working("Open day", D(2026, 12, 10), D(2026, 12, 10), null, null);
        var closed = Holiday("Closed", D(2026, 12, 10), D(2026, 12, 10));

        Standard(open, closed).IsWorkingDay(D(2026, 12, 10)).Should().BeFalse();
        Standard(closed, open).IsWorkingDay(D(2026, 12, 10)).Should().BeFalse();
    }

    // Two working exceptions of the same length on the same date used to be
    // decided by the order the rows came from the database.

    [Fact]
    public void TiedWorkingExceptions_WithDifferentHours_TheShorterWorkingDayWins_WhateverTheOrder()
    {
        var examHours = Working("Exam week hours", D(2026, 12, 7), D(2026, 12, 11), new TimeOnly(8, 0), new TimeOnly(15, 0));
        var shortDay  = Working("Short week",      D(2026, 12, 7), D(2026, 12, 11), new TimeOnly(9, 0), new TimeOnly(13, 0));

        foreach (var calendar in new[] { Standard(examHours, shortDay), Standard(shortDay, examHours) })
        {
            calendar.GetWorkingHours(D(2026, 12, 9))
                .Should().Be((new TimeOnly(9, 0), new TimeOnly(13, 0)), "a four-hour day is more specific than a seven-hour one");
        }
    }

    [Fact]
    public void TiedWorkingExceptions_WithEqualLengthHours_TheEarlierStartWins_WhateverTheOrder()
    {
        var morning   = Working("Morning shift",   D(2026, 12, 10), D(2026, 12, 10), new TimeOnly(8, 0), new TimeOnly(12, 0));
        var afternoon = Working("Afternoon shift", D(2026, 12, 10), D(2026, 12, 10), new TimeOnly(9, 0), new TimeOnly(13, 0));

        foreach (var calendar in new[] { Standard(morning, afternoon), Standard(afternoon, morning) })
            calendar.GetWorkingHours(D(2026, 12, 10)).Should().Be((new TimeOnly(8, 0), new TimeOnly(12, 0)));
    }

    [Fact]
    public void TiedWorkingExceptions_OneWithHoursOfItsOwn_BeatsOneWithout_WhateverTheOrder()
    {
        var marked  = Working("Marked day",   D(2026, 12, 10), D(2026, 12, 10), null, null);
        var reduced = Working("Reduced hours", D(2026, 12, 10), D(2026, 12, 10), new TimeOnly(10, 0), new TimeOnly(14, 0));

        foreach (var calendar in new[] { Standard(marked, reduced), Standard(reduced, marked) })
            calendar.GetWorkingHours(D(2026, 12, 10)).Should().Be((new TimeOnly(10, 0), new TimeOnly(14, 0)));
    }

    private static WorkCalendarException Working(
        string name, DateOnly start, DateOnly end, TimeOnly? startTime, TimeOnly? endTime) =>
        new()
        {
            Name = name, StartDate = start, EndDate = end, IsWorkingDay = true,
            StartTime = startTime, EndTime = endTime,
        };

    [Fact]
    public void WorkingException_WithoutHours_KeepsTheWeeklyHours()
    {
        var calendar = Standard(new WorkCalendarException
        {
            Name = "Marked week", StartDate = D(2026, 11, 23), EndDate = D(2026, 11, 27), IsWorkingDay = true,
        });

        calendar.GetWorkingHours(D(2026, 11, 24)).Should().Be((Eight, Seventeen));
    }

    // ── Safety net ────────────────────────────────────────────────────────────

    [Fact]
    public void CalendarWithNoWorkingDay_ThrowsInsteadOfLoopingForever()
    {
        var calendar = new WorkCalendar { Name = "Closed" };
        foreach (var dayOfWeek in Enum.GetValues<DayOfWeek>())
            calendar.Days.Add(new WorkCalendarDay { DayOfWeek = dayOfWeek, IsWorkingDay = false });

        var resolved = new ResolvedWorkCalendar(calendar);

        var act = () => resolved.NextWorkingDay(D(2026, 11, 23));
        act.Should().Throw<InvalidOperationException>();
    }
}
