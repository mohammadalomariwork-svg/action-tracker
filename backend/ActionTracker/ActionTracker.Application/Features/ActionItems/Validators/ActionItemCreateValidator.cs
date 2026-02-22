using ActionTracker.Application.Features.ActionItems.DTOs;
using FluentValidation;

namespace ActionTracker.Application.Features.ActionItems.Validators;

public class ActionItemCreateValidator : AbstractValidator<ActionItemCreateDto>
{
    public ActionItemCreateValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(255).WithMessage("Title must not exceed 255 characters.");

        RuleFor(x => x.AssigneeId)
            .NotEmpty().WithMessage("AssigneeId is required.");

        RuleFor(x => x.DueDate)
            .NotEmpty().WithMessage("DueDate is required.")
            .GreaterThanOrEqualTo(_ => DateTime.Today)
            .WithMessage("DueDate must be today or in the future.");

        RuleFor(x => x.Progress)
            .InclusiveBetween(0, 100).WithMessage("Progress must be between 0 and 100.");
    }
}
