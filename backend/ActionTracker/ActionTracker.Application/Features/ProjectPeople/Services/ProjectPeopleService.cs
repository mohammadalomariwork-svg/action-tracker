using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.ActionItems.DTOs;
using ActionTracker.Application.Features.ProjectPeople.DTOs;
using ActionTracker.Application.Features.ProjectPeople.Interfaces;
using ActionTracker.Application.Features.Workflow.Interfaces;
using ActionTracker.Application.Features.Workspaces.DTOs;
using ActionTracker.Application.Features.Workspaces.Interfaces;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Application.Features.ProjectPeople.Services;

/// <summary>409: the person already has that role on the project.</summary>
public sealed class ProjectPersonConflictException : InvalidOperationException
{
    public ProjectPersonConflictException(string message) : base(message) { }
}

/// <inheritdoc />
public class ProjectPeopleService : IProjectPeopleService
{
    private readonly IAppDbContext _db;
    private readonly IProjectAccessService _access;
    private readonly IWorkspaceService _workspaces;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProjectPeopleService> _logger;

    public ProjectPeopleService(
        IAppDbContext db,
        IProjectAccessService access,
        IWorkspaceService workspaces,
        IServiceScopeFactory scopeFactory,
        ILogger<ProjectPeopleService> logger)
    {
        _db = db;
        _access = access;
        _workspaces = workspaces;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    // ── Read ─────────────────────────────────────────────────────────────────

    public async Task<ProjectPeopleDto> GetAsync(Guid projectId)
    {
        var access = await _access.GetAsync(projectId)
            ?? throw new KeyNotFoundException($"Project {projectId} not found.");

        var project = await _db.Projects.AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new
            {
                p.ProjectManagerUserId,
                Sponsors = p.Sponsors.Select(s => s.UserId).ToList(),
                People   = p.People.Select(m => new { m.UserId, m.Role }).ToList(),
            })
            .FirstAsync();

        var allIds = new List<string> { project.ProjectManagerUserId };
        allIds.AddRange(project.Sponsors);
        allIds.AddRange(project.People.Select(m => m.UserId));
        var persons = await LoadPersonsAsync(allIds);

        List<ProjectPersonDto> Of(IEnumerable<string> ids) => ids
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(id => persons.TryGetValue(id, out var p) ? p : new ProjectPersonDto { UserId = id, FullName = id })
            .OrderBy(p => p.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ProjectPeopleDto
        {
            ProjectId             = projectId,
            LeadProjectManager    = persons.TryGetValue(project.ProjectManagerUserId, out var lead) ? lead : null,
            ProjectManagers       = Of(project.People.Where(m => m.Role == ProjectMemberRole.ProjectManager).Select(m => m.UserId)),
            Sponsors              = Of(project.Sponsors),
            Viewers               = Of(project.People.Where(m => m.Role == ProjectMemberRole.Viewer).Select(m => m.UserId)),
            TeamMembers           = Of(project.People.Where(m => m.Role == ProjectMemberRole.TeamMember).Select(m => m.UserId)),
            CanManage             = access.CanManage,
            CanAddProjectManagers = access.CanManage && access.WorkspaceLevel,
            MyRoles               = access.Roles.ToList(),
        };
    }

    // ── Add / remove ─────────────────────────────────────────────────────────

    public async Task<ProjectPeopleDto> AddAsync(Guid projectId, AddProjectPersonDto dto)
    {
        var role = ProjectPeopleRules.NormalizeRole(dto.Role)
            ?? throw new ArgumentException(ProjectPeopleRules.UnknownRole);
        var userId = dto.UserId?.Trim() ?? string.Empty;
        if (userId.Length == 0) throw new ArgumentException("A user is required.");

        var access = await _access.EnsureCanManageAsync(projectId);
        var project = await _db.Projects
            .Include(p => p.Sponsors)
            .Include(p => p.People)
            .FirstAsync(p => p.Id == projectId);

        if (role == ProjectAccessRoles.Sponsor)
        {
            if (project.Sponsors.Any(s => SameId(s.UserId, userId)))
                throw new ProjectPersonConflictException("This person is already a sponsor of the project.");
            await EnsureActiveUsersAsync(new[] { userId });
            _db.ProjectSponsors.Add(new ProjectSponsor { ProjectId = projectId, UserId = userId });
        }
        else
        {
            var memberRole = ProjectPeopleRules.MemberRole(role)!.Value;
            if (project.People.Any(m => m.Role == memberRole && SameId(m.UserId, userId)))
                throw new ProjectPersonConflictException("This person already has that role on the project.");

            if (memberRole == ProjectMemberRole.ProjectManager)
                await EnsureCanAddProjectManagersAsync(project, new[] { userId }, access.WorkspaceLevel);
            else
                await EnsureActiveUsersAsync(new[] { userId });

            _db.ProjectMembers.Add(NewMember(projectId, userId, memberRole));
        }

        await _db.SaveChangesAsync();
        NotifyInBackground(projectId, new[] { new ProjectPersonAdded(userId, role) });
        return await GetAsync(projectId);
    }

    public async Task<ProjectPeopleDto> RemoveAsync(Guid projectId, string role, string userId)
    {
        var normalized = ProjectPeopleRules.NormalizeRole(role)
            ?? throw new ArgumentException(ProjectPeopleRules.UnknownRole);
        await _access.EnsureCanManageAsync(projectId);

        if (normalized == ProjectAccessRoles.Sponsor)
        {
            var sponsors = await _db.ProjectSponsors.Where(s => s.ProjectId == projectId).ToListAsync();
            var row = sponsors.FirstOrDefault(s => SameId(s.UserId, userId))
                ?? throw new KeyNotFoundException("This person is not a sponsor of the project.");
            if (sponsors.Count == 1)
                throw new ArgumentException(ProjectPeopleRules.LastSponsor);
            _db.ProjectSponsors.Remove(row);
        }
        else
        {
            var memberRole = ProjectPeopleRules.MemberRole(normalized)!.Value;
            var rows = await _db.ProjectMembers
                .Where(m => m.ProjectId == projectId && m.Role == memberRole && m.UserId == userId)
                .ToListAsync();
            if (rows.Count == 0)
                throw new KeyNotFoundException("This person does not have that role on the project.");
            _db.ProjectMembers.RemoveRange(rows);
        }

        await _db.SaveChangesAsync();
        return await GetAsync(projectId);
    }

    // ── Project create / update ──────────────────────────────────────────────

    public async Task<List<ProjectPersonAdded>> StageForNewProjectAsync(
        Project project, IReadOnlyCollection<string> sponsorUserIds, ProjectPeopleLists lists)
    {
        var added = new List<ProjectPersonAdded>();

        var sponsors = ProjectPeopleRules.CleanIds(sponsorUserIds);
        await EnsureActiveUsersAsync(sponsors);
        foreach (var id in sponsors)
        {
            _db.ProjectSponsors.Add(new ProjectSponsor { ProjectId = project.Id, UserId = id });
            added.Add(new ProjectPersonAdded(id, ProjectAccessRoles.Sponsor));
        }

        // A new project is created by a workspace-level caller (ProjectService checks).
        added.AddRange(await StageMembersAsync(project, ProjectMemberRole.ProjectManager,
            lists.AdditionalManagerUserIds, existing: Array.Empty<ProjectMember>(), callerHasWorkspaceLevel: true));
        added.AddRange(await StageMembersAsync(project, ProjectMemberRole.Viewer,
            lists.ViewerUserIds, existing: Array.Empty<ProjectMember>(), callerHasWorkspaceLevel: true));
        added.AddRange(await StageMembersAsync(project, ProjectMemberRole.TeamMember,
            lists.TeamMemberUserIds, existing: Array.Empty<ProjectMember>(), callerHasWorkspaceLevel: true));
        return added;
    }

    public async Task<List<ProjectPersonAdded>> StageForProjectUpdateAsync(
        Project project, string previousLeadProjectManagerId, IReadOnlyCollection<string> sponsorUserIds,
        ProjectPeopleLists lists, bool callerHasWorkspaceLevel)
    {
        var added = new List<ProjectPersonAdded>();
        var existing = await _db.ProjectMembers.Where(m => m.ProjectId == project.Id).ToListAsync();

        // Lead PM (decision D10: a new lead PM is told). The PM-list rule itself
        // stays in ProjectService; a project-only caller cannot change the lead,
        // because that needs the workspace's Project Managers list (D4).
        if (!SameId(previousLeadProjectManagerId, project.ProjectManagerUserId))
        {
            if (!callerHasWorkspaceLevel)
                throw new ProjectAccessDeniedException(
                    "Only the workspace's admins and members can change the lead project manager.");
            added.Add(new ProjectPersonAdded(project.ProjectManagerUserId, ProjectAccessRoles.LeadProjectManager));
            // The lead is never also an additional PM.
            _db.ProjectMembers.RemoveRange(existing.Where(m =>
                m.Role == ProjectMemberRole.ProjectManager && SameId(m.UserId, project.ProjectManagerUserId)));
        }

        // Sponsors: the full list, as before P-2 (at least one is checked by ProjectService).
        var desiredSponsors = ProjectPeopleRules.CleanIds(sponsorUserIds);
        var currentSponsors = await _db.ProjectSponsors.Where(s => s.ProjectId == project.Id).ToListAsync();
        var newSponsors = desiredSponsors.Where(id => !currentSponsors.Any(s => SameId(s.UserId, id))).ToList();
        await EnsureActiveUsersAsync(newSponsors);
        _db.ProjectSponsors.RemoveRange(currentSponsors.Where(s => !desiredSponsors.Any(id => SameId(id, s.UserId))));
        foreach (var id in newSponsors)
        {
            _db.ProjectSponsors.Add(new ProjectSponsor { ProjectId = project.Id, UserId = id });
            added.Add(new ProjectPersonAdded(id, ProjectAccessRoles.Sponsor));
        }

        added.AddRange(await StageMembersAsync(project, ProjectMemberRole.ProjectManager,
            lists.AdditionalManagerUserIds, existing, callerHasWorkspaceLevel));
        added.AddRange(await StageMembersAsync(project, ProjectMemberRole.Viewer,
            lists.ViewerUserIds, existing, callerHasWorkspaceLevel));
        added.AddRange(await StageMembersAsync(project, ProjectMemberRole.TeamMember,
            lists.TeamMemberUserIds, existing, callerHasWorkspaceLevel));
        return added;
    }

    /// <summary>Replaces one role's rows with <paramref name="desired"/> (null keeps them).</summary>
    private async Task<List<ProjectPersonAdded>> StageMembersAsync(
        Project project, ProjectMemberRole role, IReadOnlyCollection<string>? desired,
        IReadOnlyCollection<ProjectMember> existing, bool callerHasWorkspaceLevel)
    {
        var added = new List<ProjectPersonAdded>();
        if (desired == null) return added;

        var wanted  = ProjectPeopleRules.CleanIds(desired);
        var current = existing.Where(m => m.Role == role).ToList();
        var toAdd   = wanted.Where(id => !current.Any(m => SameId(m.UserId, id))).ToList();

        if (role == ProjectMemberRole.ProjectManager)
        {
            if (wanted.Any(id => SameId(id, project.ProjectManagerUserId)))
                throw new ArgumentException(ProjectPeopleRules.LeadIsNotAdditional);
            await EnsureCanAddProjectManagersAsync(project, toAdd, callerHasWorkspaceLevel);
        }
        else
        {
            await EnsureActiveUsersAsync(toAdd);
        }

        _db.ProjectMembers.RemoveRange(current.Where(m => !wanted.Any(id => SameId(id, m.UserId))));
        foreach (var id in toAdd)
        {
            _db.ProjectMembers.Add(NewMember(project.Id, id, role));
            added.Add(new ProjectPersonAdded(id, ProjectAccessRoles.For(role)));
        }
        return added;
    }

    // ── Notifications ────────────────────────────────────────────────────────

    public void NotifyInBackground(Guid projectId, IReadOnlyCollection<ProjectPersonAdded> added)
    {
        if (added.Count == 0) return;
        var actorUserId = _db.CurrentUserId;
        var list = added.Select(a => (a.UserId, a.Role)).ToList();
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var helper = scope.ServiceProvider.GetRequiredService<IWorkflowNotificationHelper>();
                await helper.NotifyProjectPeopleAddedAsync(projectId, list, actorUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify people added to project {ProjectId}", projectId);
            }
        });
    }

    // ── Pickers ──────────────────────────────────────────────────────────────

    public async Task<List<UserDropdownItemDto>> LookupUsersAsync(string? search, int take)
    {
        take = Math.Clamp(take <= 0 ? 20 : take, 1, 50);
        var term = search?.Trim().ToLower();

        var query = _db.Users.AsNoTracking().Where(u => u.IsActive);
        if (!string.IsNullOrEmpty(term))
            query = query.Where(u =>
                (u.DisplayName != null && u.DisplayName.ToLower().Contains(term))
                || (u.FirstName + " " + u.LastName).ToLower().Contains(term)
                || (u.Email != null && u.Email.ToLower().Contains(term)));

        return await query
            .OrderBy(u => u.DisplayName ?? (u.FirstName + " " + u.LastName))
            .Take(take)
            .Select(u => new UserDropdownItemDto
            {
                Id          = u.Id,
                DisplayName = u.DisplayName ?? (u.FirstName + " " + u.LastName),
                Email       = u.Email,
                OrgUnitName = u.OrgUnitId == null ? null
                    : _db.OrgUnits.Where(o => o.Id == u.OrgUnitId).Select(o => o.Name).FirstOrDefault(),
            })
            .ToListAsync();
    }

    public async Task<List<AssignableUserDto>> GetAssignableUsersAsync(Guid projectId)
    {
        var access = await _access.GetAsync(projectId)
            ?? throw new KeyNotFoundException($"Project {projectId} not found.");

        var project = await _db.Projects.AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new
            {
                p.ProjectManagerUserId,
                Managers = p.People.Where(m => m.Role == ProjectMemberRole.ProjectManager).Select(m => m.UserId).ToList(),
                Team     = p.People.Where(m => m.Role == ProjectMemberRole.TeamMember).Select(m => m.UserId).ToList(),
            })
            .FirstAsync();

        var result = new Dictionary<string, AssignableUserDto>(StringComparer.OrdinalIgnoreCase);
        if (access.WorkspaceLevel)
        {
            foreach (var u in await _workspaces.GetAssignableUsersAsync(access.WorkspaceId))
                result[u.UserId] = u;
        }
        else
        {
            // Decision D4: the project's own people only — never the workspace's lists.
            var own = new List<string> { project.ProjectManagerUserId };
            own.AddRange(project.Managers);
            await AddPersonsAsync(result, own, "ProjectManager");
        }
        await AddPersonsAsync(result, project.Team, "ProjectTeam");

        return result.Values.OrderBy(u => u.UserName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<HashSet<string>> GetAssigneePoolAsync(Guid workspaceId, Guid? projectId)
    {
        var pool = (await _workspaces.GetAssignableUsersAsync(workspaceId))
            .Select(u => u.UserId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (projectId is { } pid)
        {
            var team = await _db.ProjectMembers.AsNoTracking()
                .Where(m => m.ProjectId == pid && m.Role == ProjectMemberRole.TeamMember && m.User.IsActive)
                .Select(m => m.UserId)
                .ToListAsync();
            pool.UnionWith(team);
        }
        return pool;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private ProjectMember NewMember(Guid projectId, string userId, ProjectMemberRole role) => new()
    {
        Id            = Guid.NewGuid(),
        ProjectId     = projectId,
        UserId        = userId,
        Role          = role,
        AddedAt       = DateTime.UtcNow,
        AddedByUserId = _db.CurrentUserId,
    };

    /// <summary>Additional PMs (D4): workspace-level callers only, on the workspace's PM list, active.</summary>
    private async Task EnsureCanAddProjectManagersAsync(Project project, IReadOnlyCollection<string> userIds, bool callerHasWorkspaceLevel)
    {
        if (userIds.Count == 0) return;
        if (!callerHasWorkspaceLevel)
            throw new ProjectAccessDeniedException(
                "Only the workspace's admins and members can add project managers to this project.");
        if (userIds.Any(id => SameId(id, project.ProjectManagerUserId)))
            throw new ArgumentException(ProjectPeopleRules.LeadIsNotAdditional);

        var onList = await _db.WorkspaceProjectManagers.AsNoTracking()
            .Where(p => p.WorkspaceId == project.WorkspaceId && userIds.Contains(p.UserId))
            .Select(p => p.UserId)
            .ToListAsync();
        if (userIds.Any(id => !onList.Any(x => SameId(x, id))))
            throw new ArgumentException(ProjectPeopleRules.NotOnPmList);

        await EnsureActiveUsersAsync(userIds);
    }

    private async Task EnsureActiveUsersAsync(IReadOnlyCollection<string> userIds)
    {
        if (userIds.Count == 0) return;
        var active = await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id) && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync();
        if (userIds.Any(id => !active.Any(a => SameId(a, id))))
            throw new ArgumentException(ProjectPeopleRules.InactiveUser);
    }

    private async Task<Dictionary<string, ProjectPersonDto>> LoadPersonsAsync(IEnumerable<string> userIds)
    {
        var ids = userIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        var rows = await _db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new ProjectPersonDto
            {
                UserId      = u.Id,
                FullName    = u.DisplayName ?? (u.FirstName + " " + u.LastName),
                Email       = u.Email ?? string.Empty,
                IsActive    = u.IsActive,
                OrgUnitName = u.OrgUnitId == null ? null
                    : _db.OrgUnits.Where(o => o.Id == u.OrgUnitId).Select(o => o.Name).FirstOrDefault(),
            })
            .ToListAsync();
        return rows.ToDictionary(r => r.UserId, StringComparer.OrdinalIgnoreCase);
    }

    private async Task AddPersonsAsync(Dictionary<string, AssignableUserDto> into, IEnumerable<string> userIds, string source)
    {
        var persons = await LoadPersonsAsync(userIds);
        foreach (var p in persons.Values.Where(p => p.IsActive))
        {
            if (into.TryGetValue(p.UserId, out var existing))
            {
                if (!existing.Sources.Contains(source)) existing.Sources.Add(source);
                continue;
            }
            into[p.UserId] = new AssignableUserDto
            {
                UserId      = p.UserId,
                UserName    = p.FullName,
                Email       = p.Email,
                OrgUnitName = p.OrgUnitName,
                Sources     = new List<string> { source },
            };
        }
    }

    private static bool SameId(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
