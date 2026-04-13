using ActionTracker.Application.Features.Workflow.DTOs;
using FluentValidation;

namespace ActionTracker.Application.Features.Workflow.Validators;

public class CreateDateChangeRequestValidator : AbstractValidator<CreateDateChangeRequestDto>
{
    public CreateDateChangeRequestValidator()
    {
        RuleFor(x => x.ActionItemId)
            .NotEmpty().WithMessage("ActionItemId is required.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required.")
            .MaximumLength(2000).WithMessage("Reason must not exceed 2000 characters.");

        RuleFor(x => x)
            .Must(x => x.NewStartDate.HasValue || x.NewDueDate.HasValue)
            .WithMessage("At least one of NewStartDate or NewDueDate must be provided.");

        When(x => x.NewStartDate.HasValue && x.NewDueDate.HasValue, () =>
        {
            RuleFor(x => x.NewDueDate)
                .GreaterThan(x => x.NewStartDate)
                .WithMessage("NewDueDate must be after NewStartDate.");
        });
    }
}
