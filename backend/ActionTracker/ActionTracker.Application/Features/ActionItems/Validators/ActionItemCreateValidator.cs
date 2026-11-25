using ActionTracker.Application.Features.ActionItems.DTOs;
using ActionTracker.Application.Features.Tags;
using FluentValidation;

namespace ActionTracker.Application.Features.ActionItems.Validators;

public class ActionItemCreateValidator : AbstractValidator<ActionItemCreateDto>
{
    public ActionItemCreateValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(255).WithMessage("Title must not exceed 255 characters.");

        RuleFor(x => x.WorkspaceId)
            .NotEmpty().WithMessage("WorkspaceId is required.");

        RuleFor(x => x.AssigneeIds)
            .NotEmpty().WithMessage("At least one assignee is required.")
            .Must(ids => ids.All(id => !string.IsNullOrWhiteSpace(id)))
            .WithMessage("Assignee IDs must not be empty.");

        RuleFor(x => x.DueDate)
            .NotEmpty().WithMessage("DueDate is required.");

        RuleFor(x => x.Progress)
            .InclusiveBetween(0, 100).WithMessage("Progress must be between 0 and 100.");

        // WO-2026-11 A-5 — the same rules the service applies (TagRules).
        RuleFor(x => x.Tags)
            .Must(tags => TagRules.Problem(tags) is null)
            .WithMessage(x => TagRules.Problem(x.Tags) ?? string.Empty);
    }
}
