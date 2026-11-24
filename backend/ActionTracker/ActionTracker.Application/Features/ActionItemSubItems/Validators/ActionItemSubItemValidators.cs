using ActionTracker.Application.Features.ActionItemSubItems.DTOs;
using FluentValidation;

namespace ActionTracker.Application.Features.ActionItemSubItems.Validators;

/// <summary>
/// Field rules for sub-item create / update (WO-2026-11 A-2). The rules that
/// need the database — dates within the parent's planned dates, working days,
/// the assignee pool, a Done / Cancelled parent — are applied by the service.
/// </summary>
public class CreateActionItemSubItemValidator : AbstractValidator<CreateActionItemSubItemDto>
{
    public CreateActionItemSubItemValidator()
    {
        RuleFor(x => x.Title)
            .Must(t => !string.IsNullOrWhiteSpace(t)).WithMessage("Title is required.")
            .MaximumLength(255).WithMessage("Title must not exceed 255 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(5000).WithMessage("Description must not exceed 5000 characters.");

        RuleFor(x => x.EndDate)
            .NotEmpty().WithMessage("Sub-item end is required.");

        RuleFor(x => x.AssigneeIds)
            .NotNull().WithMessage("AssigneeIds is required (an empty list is allowed).")
            .Must(ids => ids == null || ids.All(id => !string.IsNullOrWhiteSpace(id)))
            .WithMessage("Assignee IDs must not be empty.");
    }
}

public class UpdateActionItemSubItemValidator : AbstractValidator<UpdateActionItemSubItemDto>
{
    public UpdateActionItemSubItemValidator()
    {
        RuleFor(x => x.Title)
            .Must(t => !string.IsNullOrWhiteSpace(t)).WithMessage("Title is required.")
            .MaximumLength(255).WithMessage("Title must not exceed 255 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(5000).WithMessage("Description must not exceed 5000 characters.");

        RuleFor(x => x.EndDate)
            .NotEmpty().WithMessage("Sub-item end is required.");

        RuleFor(x => x.AssigneeIds)
            .NotNull().WithMessage("AssigneeIds is required (an empty list is allowed).")
            .Must(ids => ids == null || ids.All(id => !string.IsNullOrWhiteSpace(id)))
            .WithMessage("Assignee IDs must not be empty.");
    }
}

public class ReorderActionItemSubItemsValidator : AbstractValidator<ReorderActionItemSubItemsDto>
{
    public ReorderActionItemSubItemsValidator()
    {
        RuleFor(x => x.SubItemIds)
            .NotNull().WithMessage("SubItemIds is required.")
            .Must(ids => ids == null || ids.Distinct().Count() == ids.Count)
            .WithMessage("SubItemIds must not contain duplicates.");
    }
}
