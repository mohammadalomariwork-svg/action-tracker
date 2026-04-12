using ActionTracker.Application.Features.ProjectRisks.DTOs;
using FluentValidation;

namespace ActionTracker.Application.Features.ProjectRisks.Validators;

public class UpdateProjectRiskDtoValidator : AbstractValidator<UpdateProjectRiskDto>
{
    private static readonly string[] ValidStatuses =
        { "Open", "Mitigating", "Accepted", "Transferred", "Closed" };

    public UpdateProjectRiskDtoValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(300).WithMessage("Title must not exceed 300 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters.");

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Category is required.")
            .MaximumLength(100).WithMessage("Category must not exceed 100 characters.");

        RuleFor(x => x.ProbabilityScore)
            .InclusiveBetween(1, 5).WithMessage("ProbabilityScore must be between 1 and 5.");

        RuleFor(x => x.ImpactScore)
            .InclusiveBetween(1, 5).WithMessage("ImpactScore must be between 1 and 5.");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required.")
            .Must(s => ValidStatuses.Contains(s))
            .WithMessage("Status must be one of: Open, Mitigating, Accepted, Transferred, Closed.");

        RuleFor(x => x.MitigationPlan)
            .MaximumLength(2000).WithMessage("MitigationPlan must not exceed 2000 characters.");

        RuleFor(x => x.ContingencyPlan)
            .MaximumLength(2000).WithMessage("ContingencyPlan must not exceed 2000 characters.");

        RuleFor(x => x.Notes)
            .MaximumLength(2000).WithMessage("Notes must not exceed 2000 characters.");
    }
}
