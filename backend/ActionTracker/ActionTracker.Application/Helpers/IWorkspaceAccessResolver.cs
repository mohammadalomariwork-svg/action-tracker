namespace ActionTracker.Application.Helpers;

/// <summary>
/// Resolves which workspaces a user is allowed to see.
///
/// Privileged roles (Admin, PMO Head, PMO Analyst) get full visibility
/// (the resolver returns <c>null</c> meaning "no restriction").
///
/// Other users are restricted to workspaces where they are either an
/// admin (<c>WorkspaceAdmin</c>) or a member (<c>WorkspaceMember</c>).
///
/// WO-2026-11 P-2: project people (lead PM, sponsors, additional PMs, viewers,
/// team members) do not gain the workspace. Lists add their projects through
/// <see cref="GetProjectRoleProjectIdsAsync"/>; a workspace's own data stays
/// behind <see cref="IsWorkspaceLevelVisibleAsync"/>.
/// </summary>
public interface IWorkspaceAccessResolver
{
    /// <summary>
    /// Returns the set of workspace IDs visible to the user, or <c>null</c>
    /// to indicate "no restriction" (i.e. the user can see every workspace).
    /// An empty list means the user has no workspace access.
    /// </summary>
    Task<List<Guid>?> GetVisibleWorkspaceIdsAsync(string userId);

    /// <summary>
    /// WO-2026-11 P-2: live projects where the user is lead PM, sponsor or a
    /// project member. Lists scoped by <see cref="GetVisibleWorkspaceIdsAsync"/>
    /// also include these projects (and their action items).
    /// </summary>
    Task<List<Guid>> GetProjectRoleProjectIdsAsync(string userId);

    /// <summary>
    /// WO-2026-11 P-2: whether the user may see the workspace's own data — its
    /// lists, members, project managers, approvers, settings, tags, calendar
    /// setting and stats. The private-workspace rule as before P-2: a public
    /// workspace for everyone, a private one only for its admins and members.
    /// False for an unknown workspace. <c>null</c> user = system context (true
    /// when the workspace exists).
    /// </summary>
    Task<bool> IsWorkspaceLevelVisibleAsync(string? userId, Guid workspaceId);
}
