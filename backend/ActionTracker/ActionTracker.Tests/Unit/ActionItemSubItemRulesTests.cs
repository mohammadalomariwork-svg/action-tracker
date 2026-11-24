using ActionTracker.Application.Features.ActionItemSubItems;
using ActionTracker.Domain.Enums;
using FluentAssertions;

namespace ActionTracker.Tests.Unit;

/// <summary>
/// WO-2026-11 A-2: the pure sub-item rules. The client copy
/// (<c>shared/utils/sub-item-dates.util.ts</c>) repeats these cases in its spec.
/// </summary>
public class ActionItemSubItemRulesTests
{
    private static DateTime D(int y, int m, int d) => new(y, m, d, 0, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime ParentStart = D(2030, 6, 3);
    private static readonly DateTime ParentEnd   = D(2030, 6, 28);

    [Theory]
    [InlineData(null, "2030-06-10")]           // no start
    [InlineData("2030-06-03", "2030-06-28")]   // exactly the parent's range
    [InlineData("2030-06-10", "2030-06-10")]   // one day
    public void DateRangeProblem_accepts_dates_inside_the_parent(string? start, string end)
    {
        ActionItemSubItemRules.DateRangeProblem(Parse(start), Parse(end)!.Value, ParentStart, ParentEnd)
            .Should().BeNull();
    }

    [Theory]
    [InlineData("2030-06-12", "2030-06-11", "Sub-item end 2030-06-11 is before the sub-item start 2030-06-12.")]
    [InlineData("2030-06-02", "2030-06-10", "Sub-item start 2030-06-02 is before the action item's planned start 2030-06-03.")]
    [InlineData("2030-06-29", "2030-06-30", "Sub-item start 2030-06-29 is after the action item's planned end 2030-06-28.")]
    [InlineData(null, "2030-06-02", "Sub-item end 2030-06-02 is before the action item's planned start 2030-06-03.")]
    [InlineData("2030-06-10", "2030-06-29", "Sub-item end 2030-06-29 is after the action item's planned end 2030-06-28.")]
    public void DateRangeProblem_names_the_first_broken_rule(string? start, string end, string expected)
    {
        ActionItemSubItemRules.DateRangeProblem(Parse(start), Parse(end)!.Value, ParentStart, ParentEnd)
            .Should().Be(expected);
    }

    [Fact]
    public void DateRangeProblem_parent_without_planned_start_only_bounds_the_end()
    {
        ActionItemSubItemRules.DateRangeProblem(D(2020, 1, 6), D(2030, 6, 28), null, ParentEnd).Should().BeNull();
        ActionItemSubItemRules.DateRangeProblem(null, D(2030, 6, 29), null, ParentEnd)
            .Should().Be("Sub-item end 2030-06-29 is after the action item's planned end 2030-06-28.");
    }

    [Fact]
    public void DateRangeProblem_compares_calendar_days_not_times()
    {
        // Parent end stored with a time of day: the same day is still inside.
        ActionItemSubItemRules.DateRangeProblem(null, D(2030, 6, 28), ParentStart, ParentEnd.AddHours(9))
            .Should().BeNull();
        ActionItemSubItemRules.DateRangeProblem(ParentStart.AddHours(15), D(2030, 6, 4), ParentStart.AddHours(8), ParentEnd)
            .Should().BeNull();
    }

    [Theory]
    [InlineData(null, "2030-06-10", false)]
    [InlineData("2030-06-03", "2030-06-28", false)]
    [InlineData("2030-06-02", "2030-06-10", true)]
    [InlineData(null, "2030-06-29", true)]
    [InlineData(null, "2030-06-02", true)]
    [InlineData("2030-06-29", "2030-07-01", true)]
    public void IsOutsideParentDates(string? start, string end, bool expected)
    {
        ActionItemSubItemRules.IsOutsideParentDates(Parse(start), Parse(end)!.Value, ParentStart, ParentEnd)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(ActionStatus.ToDo, true)]
    [InlineData(ActionStatus.InProgress, true)]
    [InlineData(ActionStatus.InReview, true)]
    [InlineData(ActionStatus.Overdue, true)]
    [InlineData(ActionStatus.Deferred, true)]
    [InlineData(ActionStatus.Done, false)]
    [InlineData(ActionStatus.Cancelled, false)]
    public void ParentAcceptsChanges_refuses_Done_and_Cancelled(ActionStatus status, bool expected)
    {
        ActionItemSubItemRules.ParentAcceptsChanges(status).Should().Be(expected);
    }

    [Fact]
    public void SameDay_and_ToStoredDay()
    {
        ActionItemSubItemRules.SameDay(null, null).Should().BeTrue();
        ActionItemSubItemRules.SameDay(D(2030, 6, 3), null).Should().BeFalse();
        ActionItemSubItemRules.SameDay(D(2030, 6, 3).AddHours(23), D(2030, 6, 3)).Should().BeTrue();
        ActionItemSubItemRules.SameDay(D(2030, 6, 3), D(2030, 6, 4)).Should().BeFalse();

        var stored = ActionItemSubItemRules.ToStoredDay(new DateTime(2030, 6, 3, 14, 30, 0, DateTimeKind.Unspecified));
        stored.Should().Be(D(2030, 6, 3));
        stored.Kind.Should().Be(DateTimeKind.Utc);
    }

    private static DateTime? Parse(string? iso) =>
        iso is null ? null : DateTime.SpecifyKind(DateTime.Parse(iso, System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Utc);
}
