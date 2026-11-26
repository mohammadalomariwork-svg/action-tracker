using ActionTracker.Domain.Enums;

namespace ActionTracker.Application.Features.ProjectPeople.Interfaces;

/// <summary>
/// The caller's link to one project (WO-2026-11 P-2).
/// <see cref="WorkspaceLevel"/> is the rule as it was before P-2 — a public
/// workspace, or a private one the caller is an admin or member of — and keeps
/// every right exactly as today. Project roles only add rights; where the role
/// is the only way in, they are the ceiling.
/// </summary>
public sealed record ProjectAccess(
    Guid ProjectId,
    Guid WorkspaceId,
    bool WorkspaceLevel,
    bool IsLeadProjectManager,
    bool IsAdditionalProjectManager,
    bool IsSponsor,
    bool IsViewer,
    bool IsTeamMember)
{
    public bool IsProjectManager => IsLeadProjectManager || IsAdditionalProjectManager;

    /// <summary>May change the project, its people, milestones, risks, files and comments, and add / delete its action items.</summary>
    public bool CanManage => WorkspaceLevel || IsProjectManager;

    /// <summary>The caller's roles on the project, lead PM first (wire names).</summary>
    public IReadOnlyList<string> Roles
    {
        get
        {
            var roles = new List<string>();
            if (IsLeadProjectManager) roles.Add(ProjectAccessRoles.LeadProjectManager);
            if (IsAdditionalProjectManager) roles.Add(ProjectAccessRoles.ProjectManager);
            if (IsSponsor) roles.Add(ProjectAccessRoles.Sponsor);
            if (IsViewer) roles.Add(ProjectAccessRoles.Viewer);
            if (IsTeamMember) roles.Add(ProjectAccessRoles.TeamMember);
            return roles;
        }
    }
}

/// <summary>Wire names of the project roles (P-2), as on <c>myRoles</c> and the people endpoints.</summary>
public static class ProjectAccessRoles
{
    public const string LeadProjectManager = "leadProjectManager";
    public const string ProjectManager     = "projectManager";
    public const string Sponsor            = "sponsor";
    public const string Viewer             = "viewer";
    public const string TeamMember         = "teamMember";

    public static string For(ProjectMemberRole role) => role switch
    {
        ProjectMemberRole.ProjectManager => ProjectManager,
        ProjectMemberRole.Viewer         => Viewer,
        ProjectMemberRole.TeamMember     => TeamMember,
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };
}

/// <summary>
/// WO-2026-11 P-2 access decisions for project data. Visibility itself is the
/// query filters' job (a hidden record is 404 before these are asked); these
/// decide what a visible caller may change. In system context (no user)
/// everything is allowed.
/// </summary>
public interface IProjectAccessService
{
    /// <summary>The caller's access to the project, or null when they cannot see it.</summary>
    Task<ProjectAccess?> GetAsync(Guid projectId);

    /// <summary>
    /// Throws <see cref="KeyNotFoundException"/> (404) when the project is hidden and
    /// <see cref="ProjectAccessDeniedException"/> (403) when the caller may not manage it.
    /// </summary>
    Task<ProjectAccess> EnsureCanManageAsync(Guid projectId);

    /// <summary>
    /// Whether the caller may work on the action item (edit, status, requests,
    /// updates, files, sub-items): the workspace rule as today, a project manager,
    /// or a team member assigned to it — for a sub-item, assigned to the item or
    /// to that sub-item. False when the item is hidden.
    /// </summary>
    Task<bool> CanWorkOnItemAsync(Guid actionItemId, Guid? subItemId = null);

    /// <summary>
    /// <see cref="CanWorkOnItemAsync"/> that throws: 404 when hidden, 403 when not allowed.
    /// </summary>
    Task EnsureCanWorkOnItemAsync(Guid actionItemId, Guid? subItemId = null);

    /// <summary>
    /// Whether the caller sees the item's workspace at workspace level (the rule as
    /// before P-2). False means they reach it only through a project role.
    /// </summary>
    Task<bool> IsWorkspaceLevelAsync(Guid workspaceId);
}
