namespace ActionTracker.Application.Features.ProjectPeople;

/// <summary>
/// WO-2026-11 P-2: the caller can see the project (or the record under it) but
/// their link to it does not allow this change — 403 with the message. Derives
/// from <see cref="UnauthorizedAccessException"/> so controllers that already
/// turn that into 403 do the same here; <c>ExceptionMiddleware</c> maps this type
/// to 403 before its 401 case.
/// </summary>
public sealed class ProjectAccessDeniedException : UnauthorizedAccessException
{
    public ProjectAccessDeniedException(string message) : base(message) { }

    public const string ReadOnly =
        "Your link to this project is read-only.";

    public const string ManagersOnly =
        "Only the project's managers, or the workspace's admins and members, can change this.";

    public const string OwnItemsOnly =
        "As a team member of this project you can only work on the action items and sub-items assigned to you.";

    public const string NoMove =
        "Through your project role you cannot move an action item to another workspace or project.";

    public const string TagsReadOnly =
        "Tags of this project's action items can only be changed by the workspace's admins and members.";
}
