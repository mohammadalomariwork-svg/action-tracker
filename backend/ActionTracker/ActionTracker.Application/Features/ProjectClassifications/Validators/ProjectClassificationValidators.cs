using ActionTracker.Application.Features.ProjectClassifications.DTOs;
using FluentValidation;

namespace ActionTracker.Application.Features.ProjectClassifications.Validators;

/// <summary>Name rules of <see cref="ProjectClassificationRules"/> (the service checks them again).</summary>
public class CreateProjectClassificationValidator : AbstractValidator<CreateProjectClassificationDto>
{
    public CreateProjectClassificationValidator()
    {
        RuleFor(x => x.Name).Custom((name, ctx) =>
        {
            var problem = ProjectClassificationRules.NameProblem(name);
            if (problem is not null) ctx.AddFailure(nameof(CreateProjectClassificationDto.Name), problem);
        });
    }
}

public class UpdateProjectClassificationValidator : AbstractValidator<UpdateProjectClassificationDto>
{
    public UpdateProjectClassificationValidator()
    {
        RuleFor(x => x.Name).Custom((name, ctx) =>
        {
            var problem = ProjectClassificationRules.NameProblem(name);
            if (problem is not null) ctx.AddFailure(nameof(UpdateProjectClassificationDto.Name), problem);
        });
    }
}

public class ReorderProjectClassificationsValidator : AbstractValidator<ReorderProjectClassificationsDto>
{
    public ReorderProjectClassificationsValidator()
    {
        RuleFor(x => x.ClassificationIds)
            .NotNull()
            .Must(ids => ids is null || ids.Distinct().Count() == ids.Count)
            .WithMessage("Each classification can appear only once in the new order.");
        RuleForEach(x => x.ClassificationIds)
            .NotEqual(Guid.Empty).WithMessage("The new order contains an empty id.");
    }
}
