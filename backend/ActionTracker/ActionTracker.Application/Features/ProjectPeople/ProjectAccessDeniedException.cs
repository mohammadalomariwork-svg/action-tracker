namespace ActionTracker.Application.Features.ProjectPeople;

/// <summary>
/// WO-2026-11 P-2 / P-2b: the caller can see the record but their link to it does
/// not allow this change — 403 with the message. Derives from
/// <see cref="UnauthorizedAccessException"/> so controllers that already turn that
/// into 403 do the same here; <c>ExceptionMiddleware</c> maps this type to 403
/// before its 401 case and puts the message on <c>message</c>.
/// </summary>
public sealed class ProjectAccessDeniedException : UnauthorizedAccessException
{
    public ProjectAccessDeniedException(string message) : base(message) { }

    public const string ReadOnly =
        "Your link to this project is read-only.";

    /// <summary>P-2b: who may manage a project (also shown to project-only team members).</summary>
    public const string ManagersOnly =
        "Only the project's project managers, the workspace's admins, an Admin or the PMO Head can change this.";

    public const string OwnItemsOnly =
        "As a team member of this project you can only work on the action items and sub-items assigned to you.";

    /// <summary>P-2b: a project item, caller sees the workspace but has no link to the item.</summary>
    public const string ItemWorkersOnly =
        "Only the action item's creator and assignees, or the people who manage its project, can change it.";

    /// <summary>P-2b: a standalone item, caller has no link to it.</summary>
    public const string StandaloneWorkersOnly =
        "Only the action item's creator and assignees, the workspace's admins or an Admin can change it.";

    /// <summary>P-2b (B3): delete / restore of a standalone item.</summary>
    public const string StandaloneDeleteOnly =
        "Only the action item's creator, the workspace's admins or an Admin can delete or restore it.";

    /// <summary>P-2b (B4).</summary>
    public const string DirectionOnly =
        "Only the action item's creator, a line manager of one of its assignees or the people who manage its project can give direction.";

    /// <summary>P-2b (B4), standalone item.</summary>
    public const string StandaloneDirectionOnly =
        "Only the action item's creator, a line manager of one of its assignees, the workspace's admins or an Admin can give direction.";

    /// <summary>P-2b: workspace settings.</summary>
    public const string WorkspaceAdminsOnly =
        "Only this workspace's admins or an Admin can change its settings.";

    /// <summary>P-2b (B2): a standalone action item.</summary>
    public const string CreateItemMembersOnly =
        "Only the workspace's admins and members can add action items to it.";

    /// <summary>P-2b (B2): a project.</summary>
    public const string CreateProjectMembersOnly =
        "Only the workspace's admins and members, or people on its Project Managers list, can create projects in it.";

    /// <summary>P-2b: files on a project, milestone or risk.</summary>
    public const string FileDeleteOnly =
        "Only the person who uploaded this file or the people who manage the project can delete it.";

    public const string NoMove =
        "Through your project role you cannot move an action item to another workspace or project.";

    public const string TagsReadOnly =
        "Tags of this project's action items can only be changed by the workspace's admins and members.";
}
