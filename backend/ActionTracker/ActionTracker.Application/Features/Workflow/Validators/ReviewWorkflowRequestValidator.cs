using ActionTracker.Application.Features.Workflow.DTOs;
using FluentValidation;

namespace ActionTracker.Application.Features.Workflow.Validators;

public class ReviewWorkflowRequestValidator : AbstractValidator<ReviewWorkflowRequestDto>
{
    public ReviewWorkflowRequestValidator()
    {
        RuleFor(x => x.ReviewComment)
            .MaximumLength(2000).WithMessage("ReviewComment must not exceed 2000 characters.");

        When(x => !x.IsApproved, () =>
        {
            RuleFor(x => x.ReviewComment)
                .NotEmpty().WithMessage("ReviewComment is required when rejecting a request.");
        });
    }
}
