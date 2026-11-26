using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Domain.Entities;

namespace ActionTracker.Application.Helpers;

/// <summary>
/// Re-applies the private-workspace restriction to queries that call
/// <c>IgnoreQueryFilters()</c> (e.g. "include deleted" lists, restores), which
/// would otherwise bypass the global filter in <c>AppDbContext</c>.
/// A <c>null</c> user id means system context and leaves the query unrestricted.
///
/// WO-2026-11 P-2: the overloads that take the db context repeat the filters
/// exactly — workspace admins and members, plus the project people of the
/// record's project (<see cref="IAppDbContext.ProjectRoleProjectIds"/>). They
/// replace the user-id overloads, so no caller can keep the old rule by mistake.
/// <see cref="WhereWorkspaceLevelVisibleTo"/> is the old rule on its own, for
/// reads of a workspace's own data (its lists, members, settings, tags).
/// </summary>
public static class PrivateWorkspaceQueryExtensions
{
    public static IQueryable<ActionItem> WhereWorkspaceVisibleTo(this IQueryable<ActionItem> query, IAppDbContext db)
    {
        var userId = db.CurrentUserId;
        if (userId == null) return query;
        var roleProjectIds = db.ProjectRoleProjectIds.ToList();
        return query.Where(a => !a.Workspace.IsPrivate
            || a.Workspace.Admins.Any(x => x.AdminUserId == userId)
            || a.Workspace.Members.Any(m => m.UserId == userId)
            || (a.ProjectId != null && roleProjectIds.Contains(a.ProjectId.Value)
                && a.Project!.WorkspaceId == a.WorkspaceId));
    }

    /// <summary>Sub-items (WO-2026-11 A-2): the private-workspace rule of their parent action item.</summary>
    public static IQueryable<ActionItemSubItem> WhereWorkspaceVisibleTo(this IQueryable<ActionItemSubItem> query, IAppDbContext db)
    {
        var userId = db.CurrentUserId;
        if (userId == null) return query;
        var roleProjectIds = db.ProjectRoleProjectIds.ToList();
        return query.Where(s => !s.ActionItem.Workspace.IsPrivate
            || s.ActionItem.Workspace.Admins.Any(x => x.AdminUserId == userId)
            || s.ActionItem.Workspace.Members.Any(m => m.UserId == userId)
            || (s.ActionItem.ProjectId != null && roleProjectIds.Contains(s.ActionItem.ProjectId.Value)
                && s.ActionItem.Project!.WorkspaceId == s.ActionItem.WorkspaceId));
    }

    /// <summary>Tag links (WO-2026-11 A-5): the private-workspace rule of their action item.</summary>
    public static IQueryable<ActionItemTag> WhereWorkspaceVisibleTo(this IQueryable<ActionItemTag> query, IAppDbContext db)
    {
        var userId = db.CurrentUserId;
        if (userId == null) return query;
        var roleProjectIds = db.ProjectRoleProjectIds.ToList();
        return query.Where(x => !x.ActionItem.Workspace.IsPrivate
            || x.ActionItem.Workspace.Admins.Any(a => a.AdminUserId == userId)
            || x.ActionItem.Workspace.Members.Any(m => m.UserId == userId)
            || (x.ActionItem.ProjectId != null && roleProjectIds.Contains(x.ActionItem.ProjectId.Value)
                && x.ActionItem.Project!.WorkspaceId == x.ActionItem.WorkspaceId));
    }

    public static IQueryable<Project> WhereWorkspaceVisibleTo(this IQueryable<Project> query, IAppDbContext db)
    {
        var userId = db.CurrentUserId;
        if (userId == null) return query;
        var roleProjectIds = db.ProjectRoleProjectIds.ToList();
        return query.Where(p => !p.Workspace.IsPrivate
            || p.Workspace.Admins.Any(x => x.AdminUserId == userId)
            || p.Workspace.Members.Any(m => m.UserId == userId)
            || roleProjectIds.Contains(p.Id));
    }

    /// <summary>Milestones (WO-2026-11 P-2): the rule of their project.</summary>
    public static IQueryable<Milestone> WhereWorkspaceVisibleTo(this IQueryable<Milestone> query, IAppDbContext db)
    {
        var userId = db.CurrentUserId;
        if (userId == null) return query;
        var roleProjectIds = db.ProjectRoleProjectIds.ToList();
        return query.Where(m => !m.Project.Workspace.IsPrivate
            || m.Project.Workspace.Admins.Any(x => x.AdminUserId == userId)
            || m.Project.Workspace.Members.Any(wm => wm.UserId == userId)
            || roleProjectIds.Contains(m.ProjectId));
    }

    /// <summary>Project risks (WO-2026-11 P-2): the rule of their project.</summary>
    public static IQueryable<ProjectRisk> WhereWorkspaceVisibleTo(this IQueryable<ProjectRisk> query, IAppDbContext db)
    {
        var userId = db.CurrentUserId;
        if (userId == null) return query;
        var roleProjectIds = db.ProjectRoleProjectIds.ToList();
        return query.Where(r => !r.Project.Workspace.IsPrivate
            || r.Project.Workspace.Admins.Any(x => x.AdminUserId == userId)
            || r.Project.Workspace.Members.Any(wm => wm.UserId == userId)
            || roleProjectIds.Contains(r.ProjectId));
    }

    /// <summary>
    /// WO-2026-11 P-2. The workspace-level rule — the private-workspace filter as it
    /// was before project people: a private workspace only for its admins and
    /// members. Use it for every read of a workspace's own data, because the
    /// Workspace query filter now also lets project people's workspaces through.
    /// </summary>
    public static IQueryable<Workspace> WhereWorkspaceLevelVisibleTo(this IQueryable<Workspace> query, string? userId)
        => userId == null
            ? query
            : query.Where(w => !w.IsPrivate
                || w.Admins.Any(a => a.AdminUserId == userId)
                || w.Members.Any(m => m.UserId == userId));
}
