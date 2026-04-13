using ActionTracker.Application.Features.Projects.DTOs;
using FluentValidation;

namespace ActionTracker.Application.Features.Projects.Validators;

public class ReviewProjectApprovalRequestValidator : AbstractValidator<ReviewProjectApprovalRequestDto>
{
    public ReviewProjectApprovalRequestValidator()
    {
        RuleFor(x => x.RequestId)
            .NotEmpty().WithMessage("RequestId is required.");

        RuleFor(x => x.ReviewComment)
            .MaximumLength(2000).WithMessage("ReviewComment must not exceed 2000 characters.");

        When(x => !x.IsApproved, () =>
        {
            RuleFor(x => x.ReviewComment)
                .NotEmpty().WithMessage("ReviewComment is required when rejecting a request.");
        });
    }
}
