using ActionTracker.Domain.Enums;

namespace ActionTracker.Application.Features.ProjectPeople.Interfaces;

/// <summary>
/// The caller's link to one project (WO-2026-11 P-2, ownership rule P-2b).
/// <see cref="WorkspaceLevel"/> is the visibility rule as it was before P-2 — a
/// public workspace, or a private one the caller is an admin or member of. Seeing
/// a project gives read access only; the right to change it comes from a link:
/// lead or additional PM, admin of the project's workspace, or the Admin / PMO
/// Head role (P-2b). Where a project role is the only way in (private workspace),
/// the P-2 decision is the ceiling: only the project's PMs manage.
/// </summary>
public sealed record ProjectAccess(
    Guid ProjectId,
    Guid WorkspaceId,
    bool WorkspaceLevel,
    bool IsLeadProjectManager,
    bool IsAdditionalProjectManager,
    bool IsSponsor,
    bool IsViewer,
    bool IsTeamMember,
    bool IsWorkspaceAdmin = false,
    bool HasManageRole = false,
    bool IsSystem = false,
    bool HasAdminRole = false)
{
    public bool IsProjectManager => IsLeadProjectManager || IsAdditionalProjectManager;

    /// <summary>
    /// May change the project, its people, milestones, risks, files and comments, and
    /// add / delete / move its action items: a PM of the project, or — seen at
    /// workspace level — an admin of its workspace or an Admin / PMO Head (P-2b).
    /// </summary>
    public bool CanManage => IsSystem || IsProjectManager || (WorkspaceLevel && (IsWorkspaceAdmin || HasManageRole));

    /// <summary>
    /// May press Submit for Approval (whether the project is ready is checked on submit):
    /// a PM of the project (P-2, D3) or — seen at workspace level — the Admin role
    /// (P-2b, B5). Routing, reviewers and notifications do not depend on who submits.
    /// </summary>
    public bool CanSubmit => IsSystem || IsProjectManager || (WorkspaceLevel && HasAdminRole);

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

/// <summary>What the batch project decision needs to know about one project of a response.</summary>
public sealed record ProjectFacts(Guid ProjectId, Guid WorkspaceId, string? LeadProjectManagerUserId);

/// <summary>What the batch item decision needs to know about one action item of a response.</summary>
public sealed record ItemFacts(
    Guid ItemId,
    Guid WorkspaceId,
    Guid? ProjectId,
    IReadOnlyCollection<string> AssigneeUserIds,
    bool IsDeleted);

/// <summary>
/// The caller's rights on one action item, as on the item response:
/// <see cref="CanWork"/> = <see cref="IProjectAccessService.EnsureCanWorkOnItemAsync"/> succeeds;
/// <see cref="CanDelete"/> = on a live item the delete succeeds, on a deleted one the restore;
/// <see cref="ViaProjectRole"/> = reached only through a role on its project (P-2).
/// </summary>
public sealed record ItemRights(bool CanWork, bool CanDelete, bool ViaProjectRole);

/// <summary>
/// WO-2026-11 P-2 / P-2b access decisions. Visibility itself is the query filters'
/// job (a hidden record is 404 before these are asked); these decide what a visible
/// caller may change (403 <see cref="ProjectAccessDeniedException"/> with the reason).
/// In system context (no user) everything is allowed. Permission policies still apply
/// on top of every decision here. The response flags (<c>canManage</c>, <c>canWork</c>,
/// <c>canDelete</c>, <c>canManageSettings</c>, <c>canGiveDirection</c>) come from the
/// batch methods below, which apply the same rule.
/// </summary>
public interface IProjectAccessService
{
    /// <summary>
    /// The caller's access to the project, or null when they cannot see it.
    /// <paramref name="includeDeleted"/> also finds a soft-deleted project (restore).
    /// </summary>
    Task<ProjectAccess?> GetAsync(Guid projectId, bool includeDeleted = false);

    /// <summary>
    /// Throws <see cref="KeyNotFoundException"/> (404) when the project is hidden and
    /// <see cref="ProjectAccessDeniedException"/> (403) when the caller may not manage it.
    /// </summary>
    Task<ProjectAccess> EnsureCanManageAsync(Guid projectId, bool includeDeleted = false);

    /// <summary>
    /// Whether the caller may work on the action item (edit, status, requests,
    /// escalation, updates, files, sub-items). P-2b: whoever manages its project, the
    /// item's creator and assignees; a standalone item: creator, assignees, admins of
    /// its workspace, the Admin role. With <paramref name="subItemId"/> (tick, updates
    /// and files of that sub-item) also that sub-item's assignees. Through a project
    /// role only (P-2 ceiling): a PM, or a team member assigned to the item / sub-item.
    /// False when the item is hidden.
    /// </summary>
    Task<bool> CanWorkOnItemAsync(Guid actionItemId, Guid? subItemId = null);

    /// <summary><see cref="CanWorkOnItemAsync"/> that throws: 404 when hidden, 403 when not allowed.</summary>
    Task EnsureCanWorkOnItemAsync(Guid actionItemId, Guid? subItemId = null);

    /// <summary>
    /// Delete or restore an action item (P-2b, B3): a project item needs the manage
    /// right on its project; a standalone item its creator, an admin of its workspace
    /// or the Admin role (not its assignees). The caller has already found the item.
    /// </summary>
    Task EnsureCanDeleteItemAsync(Guid workspaceId, Guid? projectId, string? createdByUserId);

    /// <summary>
    /// Give direction on an escalated item (P-2b, B4): its creator, a line manager
    /// (HR supervisor) of one of its assignees, whoever manages its project; for a
    /// standalone item the creator, a line manager, the workspace's admins and the
    /// Admin role. Never because of being an assignee. Whether the item is escalated
    /// is not part of this decision (the caller answers 409 for that).
    /// </summary>
    Task<bool> CanGiveDirectionAsync(Guid actionItemId);

    /// <summary><see cref="CanGiveDirectionAsync"/> that throws: 404 when hidden, 403 when not allowed.</summary>
    Task EnsureCanGiveDirectionAsync(Guid actionItemId);

    /// <summary>
    /// Whether the caller sees the item's workspace at workspace level (the rule as
    /// before P-2). False means they reach it only through a project role.
    /// </summary>
    Task<bool> IsWorkspaceLevelAsync(Guid workspaceId);

    /// <summary>
    /// Create in a workspace (P-2b, B2) — also the target of a move into another
    /// workspace. 404 when the caller does not see the workspace at workspace level;
    /// 403 unless they are an admin or member of it or hold the Admin, PMO Head or
    /// PMO Analyst role — for a project (<paramref name="project"/>) also when they
    /// are on its Project Managers list.
    /// </summary>
    Task EnsureCanCreateInWorkspaceAsync(Guid workspaceId, bool project);

    /// <summary><see cref="EnsureCanCreateInWorkspaceAsync"/> as a flag (workspace responses): false when hidden or not allowed.</summary>
    Task<bool> CanCreateInWorkspaceAsync(Guid workspaceId, bool project);

    /// <summary>
    /// Change a workspace's settings (P-2b): an admin of that workspace or the Admin
    /// role. False when hidden. Also finds a soft-deleted workspace (restore).
    /// </summary>
    Task<bool> CanManageWorkspaceAsync(Guid workspaceId);

    /// <summary><see cref="CanManageWorkspaceAsync"/> that throws: 404 when hidden, 403 when not allowed.</summary>
    Task EnsureCanManageWorkspaceAsync(Guid workspaceId);

    /// <summary>The caller's access to each project, for the project response flags (same rule as <see cref="GetAsync"/>).</summary>
    Task<IReadOnlyDictionary<Guid, ProjectAccess>> GetManyAsync(IReadOnlyCollection<ProjectFacts> projects);

    /// <summary>The caller's rights on each item, for the item response flags (same rules as the guards).</summary>
    Task<IReadOnlyDictionary<Guid, ItemRights>> GetItemRightsAsync(IReadOnlyCollection<ItemFacts> items);

    /// <summary>Of <paramref name="workspaceIds"/>, those whose settings the caller may change (<c>canManageSettings</c>).</summary>
    Task<IReadOnlySet<Guid>> GetManageableWorkspaceIdsAsync(IReadOnlyCollection<Guid> workspaceIds);
}
