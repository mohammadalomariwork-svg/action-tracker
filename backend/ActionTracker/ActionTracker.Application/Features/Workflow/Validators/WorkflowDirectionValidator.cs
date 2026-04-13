using ActionTracker.Application.Features.Workflow.DTOs;
using FluentValidation;

namespace ActionTracker.Application.Features.Workflow.Validators;

public class WorkflowDirectionValidator : AbstractValidator<WorkflowDirectionDto>
{
    public WorkflowDirectionValidator()
    {
        RuleFor(x => x.ActionItemId)
            .NotEmpty().WithMessage("ActionItemId is required.");

        RuleFor(x => x.DirectionText)
            .NotEmpty().WithMessage("DirectionText is required.")
            .MaximumLength(2000).WithMessage("DirectionText must not exceed 2000 characters.");
    }
}
