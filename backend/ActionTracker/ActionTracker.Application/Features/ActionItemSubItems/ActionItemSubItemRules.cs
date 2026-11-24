using System.Globalization;
using ActionTracker.Domain.Enums;

namespace ActionTracker.Application.Features.ActionItemSubItems;

/// <summary>
/// Pure rules for sub-items (WO-2026-11 A-2). The client repeats the date rule
/// in <c>frontend/.../shared/utils/sub-item-dates.util.ts</c> — change both
/// together (the spec there repeats <c>ActionItemSubItemRulesTests</c>).
///
/// All comparisons are by calendar day, like the planned dates of the parent.
/// </summary>
public static class ActionItemSubItemRules
{
    /// <summary>Field names used in the messages (also by the working-day rule).</summary>
    public const string StartField = "Sub-item start";
    public const string EndField   = "Sub-item end";

    /// <summary>
    /// No sub-item may be added, changed, ticked, reordered or removed while
    /// the parent is Done or Cancelled.
    /// </summary>
    public static bool ParentAcceptsChanges(ActionStatus parentStatus) =>
        parentStatus is not (ActionStatus.Done or ActionStatus.Cancelled);

    public static string ParentLockedMessage(ActionStatus parentStatus) =>
        $"Sub-items cannot be added or changed while the action item is {(parentStatus == ActionStatus.Done ? "Done" : "Cancelled")}.";

    /// <summary>
    /// The first broken date rule, or null when the dates are fine:
    /// end not before start; neither date after the parent's planned end;
    /// neither date before the parent's planned start when it has one.
    /// </summary>
    public static string? DateRangeProblem(DateTime? start, DateTime end, DateTime? parentStart, DateTime parentEnd)
    {
        var s  = start.HasValue ? DateOnly.FromDateTime(start.Value) : (DateOnly?)null;
        var e  = DateOnly.FromDateTime(end);
        var ps = parentStart.HasValue ? DateOnly.FromDateTime(parentStart.Value) : (DateOnly?)null;
        var pe = DateOnly.FromDateTime(parentEnd);

        if (s.HasValue && e < s.Value)
            return $"{EndField} {Format(e)} is before the {StartField.ToLowerInvariant()} {Format(s.Value)}.";
        if (s.HasValue && ps.HasValue && s.Value < ps.Value)
            return $"{StartField} {Format(s.Value)} is before the action item's planned start {Format(ps.Value)}.";
        if (s.HasValue && s.Value > pe)
            return $"{StartField} {Format(s.Value)} is after the action item's planned end {Format(pe)}.";
        if (ps.HasValue && e < ps.Value)
            return $"{EndField} {Format(e)} is before the action item's planned start {Format(ps.Value)}.";
        if (e > pe)
            return $"{EndField} {Format(e)} is after the action item's planned end {Format(pe)}.";
        return null;
    }

    /// <summary>
    /// True when the sub-item's dates lie outside the parent's planned dates —
    /// possible when the parent's dates changed after the sub-item was saved
    /// (a parent date change is never blocked by its sub-items).
    /// </summary>
    public static bool IsOutsideParentDates(DateTime? start, DateTime end, DateTime? parentStart, DateTime parentEnd)
    {
        var pe = DateOnly.FromDateTime(parentEnd);
        var ps = parentStart.HasValue ? DateOnly.FromDateTime(parentStart.Value) : (DateOnly?)null;
        var e  = DateOnly.FromDateTime(end);
        var s  = start.HasValue ? DateOnly.FromDateTime(start.Value) : (DateOnly?)null;

        if (e > pe || (s.HasValue && s.Value > pe)) return true;
        if (ps.HasValue && (e < ps.Value || (s.HasValue && s.Value < ps.Value))) return true;
        return false;
    }

    /// <summary>True when two optional dates fall on the same calendar day (both null counts as the same).</summary>
    public static bool SameDay(DateTime? a, DateTime? b) =>
        a.HasValue == b.HasValue
        && (!a.HasValue || DateOnly.FromDateTime(a.Value) == DateOnly.FromDateTime(b!.Value));

    /// <summary>The calendar day at midnight (UTC kind), the way dates are stored.</summary>
    public static DateTime ToStoredDay(DateTime value) => DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);

    private static string Format(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
