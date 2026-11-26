using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.ProjectPeople.Interfaces;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ActionTracker.Application.Features.ProjectPeople.Services;

/// <inheritdoc />
public class ProjectAccessService : IProjectAccessService
{
    private readonly IAppDbContext _db;

    /// <summary>
    /// Needs only the db context: like the query filters, it works from the request
    /// user and the data, so services can create it next to their db context.
    /// </summary>
    public ProjectAccessService(IAppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<ProjectAccess?> GetAsync(Guid projectId)
    {
        var userId = _db.CurrentUserId;

        // Through the query filters: a project the caller cannot see is null.
        var row = await _db.Projects.AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new
            {
                p.Id,
                p.WorkspaceId,
                IsLead    = userId != null && p.ProjectManagerUserId == userId,
                IsSponsor = userId != null && p.Sponsors.Any(s => s.UserId == userId),
                Roles     = p.People.Where(m => m.UserId == userId).Select(m => m.Role).ToList(),
            })
            .FirstOrDefaultAsync();
        if (row == null) return null;

        var workspaceLevel = userId == null
            || await WorkspaceLevelAsync(userId, row.WorkspaceId);

        return new ProjectAccess(
            row.Id,
            row.WorkspaceId,
            workspaceLevel,
            row.IsLead,
            row.Roles.Contains(ProjectMemberRole.ProjectManager),
            row.IsSponsor,
            row.Roles.Contains(ProjectMemberRole.Viewer),
            row.Roles.Contains(ProjectMemberRole.TeamMember));
    }

    /// <inheritdoc />
    public async Task<ProjectAccess> EnsureCanManageAsync(Guid projectId)
    {
        var access = await GetAsync(projectId)
            ?? throw new KeyNotFoundException($"Project {projectId} not found.");
        if (!access.CanManage)
            throw new ProjectAccessDeniedException(access.IsTeamMember
                ? ProjectAccessDeniedException.ManagersOnly
                : ProjectAccessDeniedException.ReadOnly);
        return access;
    }

    /// <inheritdoc />
    public async Task<bool> CanWorkOnItemAsync(Guid actionItemId, Guid? subItemId = null)
        => await DecideItemAsync(actionItemId, subItemId) == ItemDecision.Allowed;

    /// <inheritdoc />
    public async Task EnsureCanWorkOnItemAsync(Guid actionItemId, Guid? subItemId = null)
    {
        switch (await DecideItemAsync(actionItemId, subItemId))
        {
            case ItemDecision.Allowed:
                return;
            case ItemDecision.Hidden:
                throw new KeyNotFoundException($"ActionItem {actionItemId} not found.");
            case ItemDecision.TeamMemberNotAssigned:
                throw new ProjectAccessDeniedException(ProjectAccessDeniedException.OwnItemsOnly);
            default:
                throw new ProjectAccessDeniedException(ProjectAccessDeniedException.ReadOnly);
        }
    }

    /// <inheritdoc />
    public Task<bool> IsWorkspaceLevelAsync(Guid workspaceId)
        => _db.CurrentUserId == null
            ? Task.FromResult(true)
            : WorkspaceLevelAsync(_db.CurrentUserId, workspaceId);

    /// <summary>The workspace-level rule (the private-workspace filter as before P-2).</summary>
    private Task<bool> WorkspaceLevelAsync(string userId, Guid workspaceId)
        => _db.Workspaces.IgnoreQueryFilters()
            .Where(w => w.Id == workspaceId)
            .WhereWorkspaceLevelVisibleTo(userId)
            .AnyAsync();

    private enum ItemDecision { Allowed, Hidden, ReadOnly, TeamMemberNotAssigned }

    private async Task<ItemDecision> DecideItemAsync(Guid actionItemId, Guid? subItemId)
    {
        var userId = _db.CurrentUserId;

        var item = await _db.ActionItems.AsNoTracking()
            .Where(a => a.Id == actionItemId)
            .Select(a => new
            {
                a.WorkspaceId,
                a.ProjectId,
                Assigned = userId != null && a.Assignees.Any(x => x.UserId == userId),
            })
            .FirstOrDefaultAsync();
        if (item == null) return ItemDecision.Hidden;
        if (userId == null) return ItemDecision.Allowed;

        // Today's rule, unchanged: anyone who sees the workspace at workspace level.
        if (await WorkspaceLevelAsync(userId, item.WorkspaceId))
            return ItemDecision.Allowed;

        // Otherwise the caller reaches the item only through its project.
        if (item.ProjectId == null) return ItemDecision.ReadOnly;
        var access = await GetAsync(item.ProjectId.Value);
        if (access == null) return ItemDecision.Hidden;
        if (access.IsProjectManager) return ItemDecision.Allowed;
        if (!access.IsTeamMember) return ItemDecision.ReadOnly;
        if (item.Assigned) return ItemDecision.Allowed;

        if (subItemId != null
            && await _db.ActionItemSubItemAssignees.AnyAsync(x =>
                x.SubItemId == subItemId.Value
                && x.UserId == userId
                && x.SubItem.ActionItemId == actionItemId))
            return ItemDecision.Allowed;

        return ItemDecision.TeamMemberNotAssigned;
    }
}
