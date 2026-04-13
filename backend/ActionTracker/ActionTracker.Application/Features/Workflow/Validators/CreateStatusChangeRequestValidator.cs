using ActionTracker.Application.Features.Workflow.DTOs;
using ActionTracker.Domain.Enums;
using FluentValidation;

namespace ActionTracker.Application.Features.Workflow.Validators;

public class CreateStatusChangeRequestValidator : AbstractValidator<CreateStatusChangeRequestDto>
{
    private static readonly ActionStatus[] AllowedStatuses =
    {
        ActionStatus.Done, ActionStatus.InReview, ActionStatus.Deferred, ActionStatus.Cancelled
    };

    public CreateStatusChangeRequestValidator()
    {
        RuleFor(x => x.ActionItemId)
            .NotEmpty().WithMessage("ActionItemId is required.");

        RuleFor(x => x.NewStatus)
            .Must(s => AllowedStatuses.Contains(s))
            .WithMessage("NewStatus must be one of: Done, InReview, Deferred, or Cancelled.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required.")
            .MaximumLength(2000).WithMessage("Reason must not exceed 2000 characters.");
    }
}
