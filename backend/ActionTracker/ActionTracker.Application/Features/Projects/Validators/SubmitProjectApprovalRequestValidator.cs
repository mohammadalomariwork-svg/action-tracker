using ActionTracker.Application.Features.Projects.DTOs;
using FluentValidation;

namespace ActionTracker.Application.Features.Projects.Validators;

public class SubmitProjectApprovalRequestValidator : AbstractValidator<SubmitProjectApprovalRequestDto>
{
    public SubmitProjectApprovalRequestValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage("ProjectId is required.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required.")
            .MaximumLength(2000).WithMessage("Reason must not exceed 2000 characters.");
    }
}
