using ActionTracker.Domain.Enums;

namespace ActionTracker.Application.Features.Projects;

/// <summary>
/// WO-2026-11 P-2b (fix 1, decision B5): the status changes a plain project edit
/// (<c>PUT /api/projects/{id}</c>) may make. Leaving Draft and entering or leaving
/// Pending Approval belong to the approval workflow (Submit / review), which this
/// does not touch. Responses carry the result as <c>allowedStatuses</c>, which the
/// four project edit screens offer — there is no client copy of these rules.
/// </summary>
public static class ProjectStatusRules
{
    public const string SubmitToActivate =
        "A Draft project can only become Active through Submit for Approval. A plain edit can keep it in Draft or cancel it.";

    public const string PendingApprovalLocked =
        "Project status can only be changed from PendingApproval through the approval workflow.";

    public const string SubmitToRequestApproval =
        "Use Submit for Approval to send a project for approval.";

    public const string NoReturnToDraft =
        "An approved project cannot go back to Draft.";

    public const string Closed =
        "A Completed or Cancelled project cannot change status.";

    /// <summary>The statuses a plain edit may move a project to from <paramref name="from"/> (its own status included).</summary>
    public static IReadOnlyList<ProjectStatus> AllowedTargets(ProjectStatus from) =>
        Enum.GetValues<ProjectStatus>().Where(to => RefusalReason(from, to) is null).ToList();

    /// <summary>Null when a plain edit may change <paramref name="from"/> into <paramref name="to"/>; else the reason (400).</summary>
    public static string? RefusalReason(ProjectStatus from, ProjectStatus to)
    {
        if (from == to) return null;
        return (from, to) switch
        {
            (ProjectStatus.Draft, ProjectStatus.Cancelled) => null,
            (ProjectStatus.Active, ProjectStatus.OnHold) => null,
            (ProjectStatus.OnHold, ProjectStatus.Active) => null,
            (ProjectStatus.Active or ProjectStatus.OnHold, ProjectStatus.Completed or ProjectStatus.Cancelled) => null,

            (ProjectStatus.PendingApproval, _) => PendingApprovalLocked,
            (_, ProjectStatus.PendingApproval) => SubmitToRequestApproval,
            (ProjectStatus.Draft, _) => SubmitToActivate,
            (ProjectStatus.Active or ProjectStatus.OnHold, ProjectStatus.Draft) => NoReturnToDraft,
            _ => Closed,
        };
    }
}
