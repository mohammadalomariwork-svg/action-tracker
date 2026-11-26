using ActionTracker.Application.Features.ProjectPeople.DTOs;
using FluentValidation;

namespace ActionTracker.Application.Features.ProjectPeople.Validators;

public class AddProjectPersonValidator : AbstractValidator<AddProjectPersonDto>
{
    public AddProjectPersonValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("A user is required.")
            .MaximumLength(450);

        RuleFor(x => x.Role)
            .Must(r => ProjectPeopleRules.NormalizeRole(r) != null)
            .WithMessage(ProjectPeopleRules.UnknownRole);
    }
}
