using ActionTracker.Application.Features.Workspaces.DTOs;
using FluentValidation;

namespace ActionTracker.Application.Features.Workspaces.Validators;

/// <summary>
/// WO-2026-11 P-2b (fix 5): <c>POST /api/workspaces/{id}/project-managers</c> needs a
/// user (an empty body used to add an empty row). That the user exists and is active
/// is checked by <c>WorkspaceService</c> (400 with the reason).
/// </summary>
public class AddWorkspaceProjectManagerValidator : AbstractValidator<AddWorkspaceProjectManagerDto>
{
    public AddWorkspaceProjectManagerValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("Choose the user to add to the Project Managers list.")
            .MaximumLength(450);
        RuleFor(x => x.UserName).MaximumLength(256);
    }
}

/// <summary>WO-2026-11 P-2b (fix 5): the same for <c>POST /api/workspaces/{id}/strategic-project-approvers</c>.</summary>
public class AddWorkspaceStrategicProjectApproverValidator : AbstractValidator<AddWorkspaceStrategicProjectApproverDto>
{
    public AddWorkspaceStrategicProjectApproverValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("Choose the user to add as a strategic-project approver.")
            .MaximumLength(450);
        RuleFor(x => x.UserName).MaximumLength(256);
    }
}
