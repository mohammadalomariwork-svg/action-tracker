using ActionTracker.Domain.Entities;

namespace ActionTracker.Application.Helpers;

/// <summary>
/// Re-applies the private-workspace restriction to queries that call
/// <c>IgnoreQueryFilters()</c> (e.g. "include deleted" lists, restores), which
/// would otherwise bypass the global filter in <c>AppDbContext</c>.
/// A <c>null</c> user id means system context and leaves the query unrestricted.
/// </summary>
public static class PrivateWorkspaceQueryExtensions
{
    public static IQueryable<ActionItem> WhereWorkspaceVisibleTo(this IQueryable<ActionItem> query, string? userId)
        => userId == null
            ? query
            : query.Where(a => !a.Workspace.IsPrivate
                || a.Workspace.Admins.Any(x => x.AdminUserId == userId)
                || a.Workspace.Members.Any(m => m.UserId == userId));

    /// <summary>Sub-items (WO-2026-11 A-2): the private-workspace rule of their parent action item.</summary>
    public static IQueryable<ActionItemSubItem> WhereWorkspaceVisibleTo(this IQueryable<ActionItemSubItem> query, string? userId)
        => userId == null
            ? query
            : query.Where(s => !s.ActionItem.Workspace.IsPrivate
                || s.ActionItem.Workspace.Admins.Any(x => x.AdminUserId == userId)
                || s.ActionItem.Workspace.Members.Any(m => m.UserId == userId));

    public static IQueryable<Project> WhereWorkspaceVisibleTo(this IQueryable<Project> query, string? userId)
        => userId == null
            ? query
            : query.Where(p => !p.Workspace.IsPrivate
                || p.Workspace.Admins.Any(x => x.AdminUserId == userId)
                || p.Workspace.Members.Any(m => m.UserId == userId));
}
