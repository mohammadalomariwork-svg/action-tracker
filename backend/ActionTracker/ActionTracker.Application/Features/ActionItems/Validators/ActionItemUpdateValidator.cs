using ActionTracker.Application.Features.ActionItems.DTOs;
using ActionTracker.Application.Features.Tags;
using FluentValidation;

namespace ActionTracker.Application.Features.ActionItems.Validators;

/// <summary>
/// PUT /api/action-items/{id}. Only the tag rules (WO-2026-11 A-5) — the other
/// fields keep their data annotations and the service's checks.
/// </summary>
public class ActionItemUpdateValidator : AbstractValidator<ActionItemUpdateDto>
{
    public ActionItemUpdateValidator()
    {
        RuleFor(x => x.Tags)
            .Must(tags => TagRules.Problem(tags) is null)
            .WithMessage(x => TagRules.Problem(x.Tags) ?? string.Empty);
    }
}
