using ActionTracker.Application.Features.Calendars.DTOs;
using FluentValidation;

namespace ActionTracker.Application.Features.Calendars.Validators;

/// <summary>
/// Rules shared by calendar create and update:
/// a complete seven-day week with at least one working day, working days with
/// <c>StartTime &lt; EndTime</c>, and exceptions with <c>EndDate ≥ StartDate</c>.
/// </summary>
public abstract class WorkCalendarInputValidator<T> : AbstractValidator<T>
    where T : WorkCalendarInputDto
{
    public const int NameMaxLength          = 100;
    public const int DescriptionMaxLength   = 500;
    public const int TimeZoneIdMaxLength    = 100;
    public const int ExceptionNameMaxLength = 150;

    protected WorkCalendarInputValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(NameMaxLength).WithMessage($"Name must not exceed {NameMaxLength} characters.");

        RuleFor(x => x.Description)
            .MaximumLength(DescriptionMaxLength)
            .WithMessage($"Description must not exceed {DescriptionMaxLength} characters.");

        RuleFor(x => x.TimeZoneId)
            .NotEmpty().WithMessage("TimeZoneId is required.")
            .MaximumLength(TimeZoneIdMaxLength).WithMessage($"TimeZoneId must not exceed {TimeZoneIdMaxLength} characters.")
            .Must(BeAKnownTimeZone).WithMessage(x => $"'{x.TimeZoneId}' is not a known time zone id.");

        // ── Weekly pattern ───────────────────────────────────────────────────
        const string sevenDaysMessage = "Days must contain exactly seven rows, one for each day of the week.";

        RuleFor(x => x.Days)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage(sevenDaysMessage)
            .Must(HaveEachDayOfWeekOnce).WithMessage(sevenDaysMessage);

        RuleFor(x => x.Days)
            .Must(days => days.Any(d => d.IsWorkingDay))
            .When(x => HaveEachDayOfWeekOnce(x.Days))
            .WithMessage("At least one day of the week must be a working day.");

        RuleForEach(x => x.Days)
            .ChildRules(day =>
            {
                day.RuleFor(d => d.EndTime)
                    .Must((d, end) => d.StartTime.HasValue && end.HasValue && d.StartTime.Value < end.Value)
                    .When(d => d.IsWorkingDay)
                    .WithMessage(d => $"{d.DayOfWeek} is a working day and needs a start time earlier than its end time.");
            })
            .When(x => x.Days is not null);

        // ── Exceptions ───────────────────────────────────────────────────────
        RuleForEach(x => x.Exceptions)
            .ChildRules(exception =>
            {
                exception.RuleFor(e => e.Name)
                    .NotEmpty().WithMessage("Exception name is required.")
                    .MaximumLength(ExceptionNameMaxLength)
                    .WithMessage($"Exception name must not exceed {ExceptionNameMaxLength} characters.");

                exception.RuleFor(e => e.EndDate)
                    .GreaterThanOrEqualTo(e => e.StartDate)
                    .WithMessage(e => $"Exception '{e.Name}': end date must be on or after the start date.");

                // Hours are optional on a working exception, but when given
                // they must be a complete, ordered pair.
                exception.RuleFor(e => e.EndTime)
                    .Must((e, end) =>
                        (!e.StartTime.HasValue && !end.HasValue)
                        || (e.StartTime.HasValue && end.HasValue && e.StartTime.Value < end.Value))
                    .When(e => e.IsWorkingDay)
                    .WithMessage(e => $"Exception '{e.Name}': working hours need a start time earlier than the end time.");
            })
            .When(x => x.Exceptions is not null);
    }

    private static bool HaveEachDayOfWeekOnce(List<WorkCalendarDayDto>? days) =>
        days is { Count: 7 }
        && days.All(d => Enum.IsDefined(d.DayOfWeek))
        && days.Select(d => d.DayOfWeek).Distinct().Count() == 7;

    private static bool BeAKnownTimeZone(string? timeZoneId) =>
        !string.IsNullOrWhiteSpace(timeZoneId)
        && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId.Trim(), out _);
}

public class CreateWorkCalendarDtoValidator : WorkCalendarInputValidator<CreateWorkCalendarDto>
{
}

public class UpdateWorkCalendarDtoValidator : WorkCalendarInputValidator<UpdateWorkCalendarDto>
{
}
