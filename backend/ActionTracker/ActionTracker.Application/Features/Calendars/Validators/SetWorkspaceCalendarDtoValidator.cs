using ActionTracker.Application.Features.Calendars.DTOs;
using FluentValidation;

namespace ActionTracker.Application.Features.Calendars.Validators;

/// <summary>
/// Shape of a workspace calendar change. Whether the calendar exists and is
/// active is checked by <c>WorkCalendarService.SetWorkspaceCalendarAsync</c>.
/// </summary>
public class SetWorkspaceCalendarDtoValidator : AbstractValidator<SetWorkspaceCalendarDto>
{
    public SetWorkspaceCalendarDtoValidator()
    {
        RuleFor(x => x.WorkCalendarId)
            .NotEqual(Guid.Empty)
            .When(x => x.WorkCalendarId.HasValue)
            .WithMessage("WorkCalendarId must be a calendar id, or null to use the default calendar.");
    }
}
