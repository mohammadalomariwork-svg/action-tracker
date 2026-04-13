using ActionTracker.Application.Features.Workflow.DTOs;
using FluentValidation;

namespace ActionTracker.Application.Features.Workflow.Validators;

public class EscalateActionItemValidator : AbstractValidator<EscalateActionItemDto>
{
    public EscalateActionItemValidator()
    {
        RuleFor(x => x.ActionItemId)
            .NotEmpty().WithMessage("ActionItemId is required.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required.")
            .MaximumLength(2000).WithMessage("Reason must not exceed 2000 characters.");
    }
}
