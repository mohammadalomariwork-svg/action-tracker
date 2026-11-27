using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.ProjectPeople.Interfaces;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Constants;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ActionTracker.Application.Features.ProjectPeople.Services;

/// <inheritdoc />
public class ProjectAccessService : IProjectAccessService
{
    /// <summary>System roles that manage every project they see (P-2b).</summary>
    private static readonly string[] ManageRoles = { AppRoles.Admin, AppRoles.PmoHead };

    /// <summary>System roles that may create in every workspace they see (P-2b, B2).</summary>
    private static readonly string[] CreateRoles = { AppRoles.Admin, AppRoles.PmoHead, AppRoles.PmoAnalyst };

    private readonly IAppDbContext _db;

    /// <summary>
    /// Needs only the db context: like the query filters, it works from the request
    /// user and the data, so services can create it next to their db context.
    /// </summary>
    public ProjectAccessService(IAppDbContext db)
    {
        _db = db;
    }

    // ── Projects ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<ProjectAccess?> GetAsync(Guid projectId, bool includeDeleted = false)
    {
        var userId = _db.CurrentUserId;

        // Through the query filters (or, for a deleted project, the same private-
        // workspace rule re-applied): a project the caller cannot see is null.
        var projects = includeDeleted
            ? _db.Projects.IgnoreQueryFilters().WhereWorkspaceVisibleTo(_db)
            : _db.Projects;
        var row = await projects.AsNoTracking()
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

        if (userId == null)
            return new ProjectAccess(row.Id, row.WorkspaceId, true, false, false, false, false, false, IsSystem: true);

        var workspaceLevel = await WorkspaceLevelAsync(userId, row.WorkspaceId);
        return new ProjectAccess(
            row.Id,
            row.WorkspaceId,
            workspaceLevel,
            row.IsLead,
            row.Roles.Contains(ProjectMemberRole.ProjectManager),
            row.IsSponsor,
            row.Roles.Contains(ProjectMemberRole.Viewer),
            row.Roles.Contains(ProjectMemberRole.TeamMember),
            IsWorkspaceAdmin: workspaceLevel && await IsWorkspaceAdminAsync(userId, row.WorkspaceId),
            HasManageRole: HasRole(ManageRoles),
            HasAdminRole: HasRole(AppRoles.Admin));
    }

    /// <inheritdoc />
    public async Task<ProjectAccess> EnsureCanManageAsync(Guid projectId, bool includeDeleted = false)
    {
        var access = await GetAsync(projectId, includeDeleted)
            ?? throw new KeyNotFoundException($"Project {projectId} not found.");
        if (!access.CanManage)
            throw new ProjectAccessDeniedException(access.WorkspaceLevel || access.IsTeamMember
                ? ProjectAccessDeniedException.ManagersOnly
                : ProjectAccessDeniedException.ReadOnly);
        return access;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, ProjectAccess>> GetManyAsync(IReadOnlyCollection<ProjectFacts> projects)
    {
        var result = new Dictionary<Guid, ProjectAccess>();
        if (projects.Count == 0) return result;
        var userId = _db.CurrentUserId;
        if (userId == null)
        {
            foreach (var p in projects)
                result[p.ProjectId] = new ProjectAccess(p.ProjectId, p.WorkspaceId, true, false, false, false, false, false, IsSystem: true);
            return result;
        }

        var projectIds = projects.Select(p => p.ProjectId).Distinct().ToList();
        var workspaceIds = projects.Select(p => p.WorkspaceId).Distinct().ToList();
        var workspaceLevel = await WorkspaceLevelSetAsync(userId, workspaceIds);
        var workspaceAdmin = await WorkspaceAdminSetAsync(userId, workspaceIds);
        var sponsorOf = (await _db.ProjectSponsors.AsNoTracking()
            .Where(s => s.UserId == userId && projectIds.Contains(s.ProjectId))
            .Select(s => s.ProjectId).ToListAsync()).ToHashSet();
        var memberRoles = (await _db.ProjectMembers.AsNoTracking()
            .Where(m => m.UserId == userId && projectIds.Contains(m.ProjectId))
            .Select(m => new { m.ProjectId, m.Role }).ToListAsync())
            .Select(m => (m.ProjectId, m.Role)).ToHashSet();
        var hasManageRole = HasRole(ManageRoles);
        var hasAdminRole = HasRole(AppRoles.Admin);

        foreach (var p in projects)
        {
            var level = workspaceLevel.Contains(p.WorkspaceId);
            result[p.ProjectId] = new ProjectAccess(
                p.ProjectId,
                p.WorkspaceId,
                level,
                Same(p.LeadProjectManagerUserId, userId),
                memberRoles.Contains((p.ProjectId, ProjectMemberRole.ProjectManager)),
                sponsorOf.Contains(p.ProjectId),
                memberRoles.Contains((p.ProjectId, ProjectMemberRole.Viewer)),
                memberRoles.Contains((p.ProjectId, ProjectMemberRole.TeamMember)),
                IsWorkspaceAdmin: level && workspaceAdmin.Contains(p.WorkspaceId),
                HasManageRole: hasManageRole,
                HasAdminRole: hasAdminRole);
        }
        return result;
    }

    // ── Action items ─────────────────────────────────────────────────────────

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
            case ItemDecision.NotLinkedProjectItem:
                throw new ProjectAccessDeniedException(ProjectAccessDeniedException.ItemWorkersOnly);
            case ItemDecision.NotLinkedStandalone:
                throw new ProjectAccessDeniedException(ProjectAccessDeniedException.StandaloneWorkersOnly);
            default:
                throw new ProjectAccessDeniedException(ProjectAccessDeniedException.ReadOnly);
        }
    }

    /// <inheritdoc />
    public async Task EnsureCanDeleteItemAsync(Guid workspaceId, Guid? projectId, string? createdByUserId)
    {
        var userId = _db.CurrentUserId;
        if (userId == null) return;

        if (projectId is { } pid)
        {
            await EnsureCanManageAsync(pid);
            return;
        }

        // A standalone item is seen only at workspace level (it has no project to reach it through).
        if (!await WorkspaceLevelAsync(userId, workspaceId))
            throw new KeyNotFoundException($"Workspace {workspaceId} not found.");
        if (Same(createdByUserId, userId)
            || HasRole(AppRoles.Admin)
            || await IsWorkspaceAdminAsync(userId, workspaceId))
            return;
        throw new ProjectAccessDeniedException(ProjectAccessDeniedException.StandaloneDeleteOnly);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, ItemRights>> GetItemRightsAsync(IReadOnlyCollection<ItemFacts> items)
    {
        var result = new Dictionary<Guid, ItemRights>();
        if (items.Count == 0) return result;
        var userId = _db.CurrentUserId;
        if (userId == null)
        {
            foreach (var i in items) result[i.ItemId] = new ItemRights(true, true, false);
            return result;
        }

        // Ids of items the caller already has in the response: only the creator is read.
        var itemIds = items.Select(i => i.ItemId).Distinct().ToList();
        var creators = await _db.ActionItems.IgnoreQueryFilters().AsNoTracking()
            .Where(a => itemIds.Contains(a.Id))
            .Select(a => new { a.Id, a.CreatedByUserId })
            .ToDictionaryAsync(a => a.Id, a => a.CreatedByUserId);

        var projectIds = items.Where(i => i.ProjectId != null).Select(i => i.ProjectId!.Value).Distinct().ToList();
        var projects = projectIds.Count == 0
            ? new Dictionary<Guid, (Guid WorkspaceId, string LeadPm, ProjectStatus Status, bool IsDeleted)>()
            : (await _db.Projects.IgnoreQueryFilters().AsNoTracking()
                .Where(p => projectIds.Contains(p.Id))
                .Select(p => new { p.Id, p.WorkspaceId, p.ProjectManagerUserId, p.Status, p.IsDeleted })
                .ToListAsync())
                .ToDictionary(p => p.Id, p => (WorkspaceId: p.WorkspaceId, LeadPm: p.ProjectManagerUserId, Status: p.Status, IsDeleted: p.IsDeleted));
        var projectAccess = await GetManyAsync(projects
            .Select(p => new ProjectFacts(p.Key, p.Value.WorkspaceId, p.Value.LeadPm)).ToList());

        var workspaceIds = items.Select(i => i.WorkspaceId).Distinct().ToList();
        var workspaceLevel = await WorkspaceLevelSetAsync(userId, workspaceIds);
        var workspaceAdmin = await WorkspaceAdminSetAsync(userId, workspaceIds);
        var isAdmin = HasRole(AppRoles.Admin);

        foreach (var i in items)
        {
            var level = workspaceLevel.Contains(i.WorkspaceId);
            var assigned = i.AssigneeUserIds.Any(a => Same(a, userId));
            var creator = Same(creators.GetValueOrDefault(i.ItemId), userId);

            ProjectAccess? pa = null;
            (Guid WorkspaceId, string LeadPm, ProjectStatus Status, bool IsDeleted) project = default;
            if (i.ProjectId is { } pid && projects.TryGetValue(pid, out project))
                pa = projectAccess.GetValueOrDefault(pid);
            // GetAsync goes through the filters: a deleted project grants nothing.
            var liveProjectAccess = project.IsDeleted ? null : pa;

            bool canWork, canDelete;
            if (!level)
            {
                // P-2, unchanged: only through the project role.
                canWork = liveProjectAccess != null
                    && (liveProjectAccess.IsProjectManager || (liveProjectAccess.IsTeamMember && assigned));
                canDelete = liveProjectAccess?.CanManage == true
                    && (i.IsDeleted || project.Status == ProjectStatus.Draft);
            }
            else if (i.ProjectId != null)
            {
                canWork = creator || assigned || liveProjectAccess?.CanManage == true;
                // Delete: also refused once the project left Draft; restore: refused while the project is deleted.
                canDelete = liveProjectAccess?.CanManage == true
                    && (i.IsDeleted || project.Status == ProjectStatus.Draft);
            }
            else
            {
                var standaloneManager = isAdmin || workspaceAdmin.Contains(i.WorkspaceId);
                canWork = creator || assigned || standaloneManager;
                canDelete = creator || standaloneManager;
            }

            result[i.ItemId] = new ItemRights(canWork, canDelete, !level);
        }
        return result;
    }

    /// <inheritdoc />
    public async Task<bool> CanGiveDirectionAsync(Guid actionItemId)
        => await DecideDirectionAsync(actionItemId) == DirectionDecision.Allowed;

    /// <inheritdoc />
    public async Task EnsureCanGiveDirectionAsync(Guid actionItemId)
    {
        switch (await DecideDirectionAsync(actionItemId))
        {
            case DirectionDecision.Allowed:
                return;
            case DirectionDecision.Hidden:
                throw new KeyNotFoundException($"ActionItem {actionItemId} not found.");
            case DirectionDecision.DeniedStandalone:
                throw new ProjectAccessDeniedException(ProjectAccessDeniedException.StandaloneDirectionOnly);
            default:
                throw new ProjectAccessDeniedException(ProjectAccessDeniedException.DirectionOnly);
        }
    }

    // ── Workspaces ───────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<bool> IsWorkspaceLevelAsync(Guid workspaceId)
        => _db.CurrentUserId == null
            ? Task.FromResult(true)
            : WorkspaceLevelAsync(_db.CurrentUserId, workspaceId);

    /// <inheritdoc />
    public async Task EnsureCanCreateInWorkspaceAsync(Guid workspaceId, bool project)
    {
        switch (await DecideCreateAsync(workspaceId, project))
        {
            case null:
                // P-2 (D12): never into a private workspace reached only through a project role.
                throw new KeyNotFoundException($"Workspace {workspaceId} not found.");
            case false:
                throw new ProjectAccessDeniedException(project
                    ? ProjectAccessDeniedException.CreateProjectMembersOnly
                    : ProjectAccessDeniedException.CreateItemMembersOnly);
        }
    }

    /// <inheritdoc />
    public async Task<bool> CanCreateInWorkspaceAsync(Guid workspaceId, bool project)
        => await DecideCreateAsync(workspaceId, project) == true;

    /// <summary>null = not seen at workspace level; otherwise whether the caller may create there (B2).</summary>
    private async Task<bool?> DecideCreateAsync(Guid workspaceId, bool project)
    {
        var userId = _db.CurrentUserId;
        if (userId == null) return true;
        if (!await WorkspaceLevelAsync(userId, workspaceId)) return null;

        return HasRole(CreateRoles)
            || await IsWorkspaceAdminAsync(userId, workspaceId)
            || await _db.WorkspaceMembers.AnyAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId)
            || (project && await _db.WorkspaceProjectManagers.AnyAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId));
    }

    /// <inheritdoc />
    public async Task<bool> CanManageWorkspaceAsync(Guid workspaceId)
        => await DecideWorkspaceAsync(workspaceId) == true;

    /// <inheritdoc />
    public async Task EnsureCanManageWorkspaceAsync(Guid workspaceId)
    {
        switch (await DecideWorkspaceAsync(workspaceId))
        {
            case null:
                throw new KeyNotFoundException($"Workspace {workspaceId} not found.");
            case false:
                throw new ProjectAccessDeniedException(ProjectAccessDeniedException.WorkspaceAdminsOnly);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlySet<Guid>> GetManageableWorkspaceIdsAsync(IReadOnlyCollection<Guid> workspaceIds)
    {
        var ids = workspaceIds.Distinct().ToList();
        var userId = _db.CurrentUserId;
        if (ids.Count == 0) return new HashSet<Guid>();
        if (userId == null) return ids.ToHashSet();

        var level = await WorkspaceLevelSetAsync(userId, ids);
        if (HasRole(AppRoles.Admin)) return level;
        var admin = await WorkspaceAdminSetAsync(userId, ids);
        return level.Where(admin.Contains).ToHashSet();
    }

    // ── Rules ────────────────────────────────────────────────────────────────

    private enum ItemDecision { Allowed, Hidden, ReadOnly, TeamMemberNotAssigned, NotLinkedProjectItem, NotLinkedStandalone }

    private async Task<ItemDecision> DecideItemAsync(Guid actionItemId, Guid? subItemId)
    {
        var userId = _db.CurrentUserId;

        var item = await _db.ActionItems.AsNoTracking()
            .Where(a => a.Id == actionItemId)
            .Select(a => new
            {
                a.WorkspaceId,
                a.ProjectId,
                a.CreatedByUserId,
                Assigned = userId != null && a.Assignees.Any(x => x.UserId == userId),
            })
            .FirstOrDefaultAsync();
        if (item == null) return ItemDecision.Hidden;
        if (userId == null) return ItemDecision.Allowed;

        // Tick, updates and files of one sub-item: also its assignees.
        var subItemAssigned = subItemId != null
            && await _db.ActionItemSubItemAssignees.AnyAsync(x =>
                x.SubItemId == subItemId.Value
                && x.UserId == userId
                && x.SubItem.ActionItemId == actionItemId);

        if (!await WorkspaceLevelAsync(userId, item.WorkspaceId))
        {
            // Reached only through the project role: P-2 unchanged (the ceiling).
            if (item.ProjectId == null) return ItemDecision.ReadOnly;
            var role = await GetAsync(item.ProjectId.Value);
            if (role == null) return ItemDecision.Hidden;
            if (role.IsProjectManager) return ItemDecision.Allowed;
            if (!role.IsTeamMember) return ItemDecision.ReadOnly;
            return item.Assigned || subItemAssigned ? ItemDecision.Allowed : ItemDecision.TeamMemberNotAssigned;
        }

        // P-2b ownership rule.
        if (Same(item.CreatedByUserId, userId) || item.Assigned || subItemAssigned)
            return ItemDecision.Allowed;

        if (item.ProjectId is { } projectId)
            return (await GetAsync(projectId))?.CanManage == true
                ? ItemDecision.Allowed
                : ItemDecision.NotLinkedProjectItem;

        return HasRole(AppRoles.Admin) || await IsWorkspaceAdminAsync(userId, item.WorkspaceId)
            ? ItemDecision.Allowed
            : ItemDecision.NotLinkedStandalone;
    }

    private enum DirectionDecision { Allowed, Hidden, Denied, DeniedStandalone }

    private async Task<DirectionDecision> DecideDirectionAsync(Guid actionItemId)
    {
        var userId = _db.CurrentUserId;

        var item = await _db.ActionItems.AsNoTracking()
            .Where(a => a.Id == actionItemId)
            .Select(a => new
            {
                a.WorkspaceId,
                a.ProjectId,
                a.CreatedByUserId,
                AssigneeIds = a.Assignees.Select(x => x.UserId).ToList(),
            })
            .FirstOrDefaultAsync();
        if (item == null) return DirectionDecision.Hidden;
        if (userId == null) return DirectionDecision.Allowed;

        var denied = item.ProjectId == null ? DirectionDecision.DeniedStandalone : DirectionDecision.Denied;

        // Through a project role only, the P-2 ceiling also applies: they must be able to work on it.
        if (!await WorkspaceLevelAsync(userId, item.WorkspaceId)
            && await DecideItemAsync(actionItemId, null) != ItemDecision.Allowed)
            return denied;

        if (Same(item.CreatedByUserId, userId))
            return DirectionDecision.Allowed;

        if (item.ProjectId is { } projectId)
        {
            if ((await GetAsync(projectId))?.CanManage == true) return DirectionDecision.Allowed;
        }
        else if (HasRole(AppRoles.Admin) || await IsWorkspaceAdminAsync(userId, item.WorkspaceId))
        {
            return DirectionDecision.Allowed;
        }

        return await IsLineManagerOfAnyAsync(userId, item.AssigneeIds) ? DirectionDecision.Allowed : denied;
    }

    /// <summary>null = hidden or missing; otherwise whether the caller may change the settings.</summary>
    private async Task<bool?> DecideWorkspaceAsync(Guid workspaceId)
    {
        var userId = _db.CurrentUserId;
        var workspace = _db.Workspaces.IgnoreQueryFilters().Where(w => w.Id == workspaceId);
        if (userId == null) return await workspace.AnyAsync() ? true : null;
        if (!await workspace.WhereWorkspaceLevelVisibleTo(userId).AnyAsync()) return null;
        return HasRole(AppRoles.Admin) || await IsWorkspaceAdminAsync(userId, workspaceId);
    }

    /// <summary>
    /// The caller is the HR supervisor (KuEmployeeInfo) of one of the assignees —
    /// the line-manager part of the date / status request reviewer rule
    /// (<c>ActionItemWorkflowService.CanUserReviewAsync</c>, unchanged).
    /// </summary>
    private async Task<bool> IsLineManagerOfAnyAsync(string userId, IReadOnlyCollection<string> assigneeIds)
    {
        if (assigneeIds.Count == 0) return false;
        var myEmail = await _db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Email).FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(myEmail)) return false;

        var assigneeEmails = await _db.Users.AsNoTracking()
            .Where(u => assigneeIds.Contains(u.Id) && u.Email != null)
            .Select(u => u.Email!.ToLower())
            .ToListAsync();
        if (assigneeEmails.Count == 0) return false;

        var supervisorNumbers = await _db.KuEmployeeInfo.AsNoTracking()
            .Where(e => e.EmailAddress != null && assigneeEmails.Contains(e.EmailAddress.ToLower()) && e.SupervisorNumber != null)
            .Select(e => e.SupervisorNumber!)
            .ToListAsync();
        if (supervisorNumbers.Count == 0) return false;

        var me = myEmail.ToLower();
        return await _db.KuEmployeeInfo.AsNoTracking()
            .AnyAsync(e => supervisorNumbers.Contains(e.EmpNo!) && e.EmailAddress != null && e.EmailAddress.ToLower() == me);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>The workspace-level rule (the private-workspace filter as before P-2).</summary>
    private Task<bool> WorkspaceLevelAsync(string userId, Guid workspaceId)
        => _db.Workspaces.IgnoreQueryFilters()
            .Where(w => w.Id == workspaceId)
            .WhereWorkspaceLevelVisibleTo(userId)
            .AnyAsync();

    private async Task<HashSet<Guid>> WorkspaceLevelSetAsync(string userId, List<Guid> workspaceIds)
        => (await _db.Workspaces.IgnoreQueryFilters()
            .Where(w => workspaceIds.Contains(w.Id))
            .WhereWorkspaceLevelVisibleTo(userId)
            .Select(w => w.Id)
            .ToListAsync()).ToHashSet();

    private Task<bool> IsWorkspaceAdminAsync(string userId, Guid workspaceId)
        => _db.WorkspaceAdmins.IgnoreQueryFilters()
            .AnyAsync(a => a.WorkspaceId == workspaceId && a.AdminUserId == userId);

    private async Task<HashSet<Guid>> WorkspaceAdminSetAsync(string userId, List<Guid> workspaceIds)
        => (await _db.WorkspaceAdmins.IgnoreQueryFilters()
            .Where(a => a.AdminUserId == userId && workspaceIds.Contains(a.WorkspaceId))
            .Select(a => a.WorkspaceId)
            .ToListAsync()).ToHashSet();

    private bool HasRole(params string[] roles)
        => roles.Any(r => _db.CurrentUserRoles.Contains(r, StringComparer.OrdinalIgnoreCase));

    private static bool Same(string? a, string? b)
        => a != null && b != null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
