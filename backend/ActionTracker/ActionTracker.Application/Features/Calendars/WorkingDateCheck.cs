namespace ActionTracker.Application.Features.Calendars;

/// <summary>
/// One date a save is about to store, paired with the value it replaces, for
/// <c>IWorkCalendarService.EnsureWorkingDaysAsync</c> (WO-2026-11 C-3).
///
/// Only a date that is created or changed is checked (Q8): an existing record
/// that already sits on a non-working day can still be saved as long as that
/// date is left alone. Dates are compared by calendar day, so the same day
/// with another time of day counts as unchanged.
/// </summary>
/// <param name="Field">How the field reads in the error message, e.g. "Due date".</param>
/// <param name="Value">The value being saved; null = the field is not being set.</param>
/// <param name="Current">The stored value it replaces; null on create.</param>
public readonly record struct WorkingDateCheck(string Field, DateTime? Value, DateTime? Current = null)
{
    /// <summary>A date on a new record: checked whenever it is set.</summary>
    public static WorkingDateCheck New(string field, DateTime? value) => new(field, value);

    /// <summary>A date on an existing record: checked only when its calendar day changes.</summary>
    public static WorkingDateCheck Changed(string field, DateTime? value, DateTime? current) => new(field, value, current);

    /// <summary>The calendar day being saved; null when the field is not being set.</summary>
    public DateOnly? Date => Value.HasValue ? DateOnly.FromDateTime(Value.Value) : null;

    /// <summary>True when this check has to run: a value is set and its day differs from the stored one.</summary>
    public bool IsChanged =>
        Value.HasValue
        && (!Current.HasValue || DateOnly.FromDateTime(Value.Value) != DateOnly.FromDateTime(Current.Value));
}
