using ActionTracker.Application.Features.ActionItemUpdates.DTOs;
using FluentValidation;

namespace ActionTracker.Application.Features.ActionItemUpdates.Validators;

/// <summary>Field rules for action updates (WO-2026-11 A-3): text required, at most 2,000 characters after trimming.</summary>
public static class ActionItemUpdateContentRules
{
    public const int MaxLength = 2000;

    public static IRuleBuilderOptions<T, string> ValidUpdateContent<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("Update text is required.")
            .Must(c => c == null || c.Trim().Length <= MaxLength)
            .WithMessage($"Update text must not exceed {MaxLength} characters.");
}

public class CreateActionUpdateValidator : AbstractValidator<CreateActionUpdateDto>
{
    public CreateActionUpdateValidator()
    {
        RuleFor(x => x.Content).ValidUpdateContent();
    }
}

public class EditActionUpdateValidator : AbstractValidator<EditActionUpdateDto>
{
    public EditActionUpdateValidator()
    {
        RuleFor(x => x.Content).ValidUpdateContent();
    }
}
