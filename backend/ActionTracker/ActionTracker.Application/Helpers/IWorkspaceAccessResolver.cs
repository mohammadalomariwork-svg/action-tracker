namespace ActionTracker.Application.Helpers;

/// <summary>
/// Resolves which workspaces a user is allowed to see.
///
/// Privileged roles (Admin, PMO Head, PMO Analyst) get full visibility
/// (the resolver returns <c>null</c> meaning "no restriction").
///
/// Other users are restricted to workspaces where they are either an
/// admin (<c>WorkspaceAdmin</c>) or a member (<c>WorkspaceMember</c>).
/// </summary>
public interface IWorkspaceAccessResolver
{
    /// <summary>
    /// Returns the set of workspace IDs visible to the user, or <c>null</c>
    /// to indicate "no restriction" (i.e. the user can see every workspace).
    /// An empty list means the user has no workspace access.
    /// </summary>
    Task<List<Guid>?> GetVisibleWorkspaceIdsAsync(string userId);
}
