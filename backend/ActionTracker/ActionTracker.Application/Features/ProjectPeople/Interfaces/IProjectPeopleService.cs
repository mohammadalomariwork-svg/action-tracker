using ActionTracker.Application.Features.ActionItems.DTOs;
using ActionTracker.Application.Features.ProjectPeople.DTOs;
using ActionTracker.Application.Features.Workspaces.DTOs;
using ActionTracker.Domain.Entities;

namespace ActionTracker.Application.Features.ProjectPeople.Interfaces;

/// <summary>
/// Project people (WO-2026-11 P-2): lead PM (on the project), additional PMs,
/// sponsors (ProjectSponsors), viewers and team members (ProjectMembers).
/// </summary>
public interface IProjectPeopleService
{
    /// <summary>Everyone on the project, by role. 404 when the project is hidden.</summary>
    Task<ProjectPeopleDto> GetAsync(Guid projectId);

    /// <summary>
    /// Adds one person in one role. 404 hidden project, 403 when the caller may not
    /// manage it (or, without workspace-level access, adds a project manager —
    /// decision D4), 400 for a rule, 409 when they already have that role.
    /// Notifies the person (never the caller).
    /// </summary>
    Task<ProjectPeopleDto> AddAsync(Guid projectId, AddProjectPersonDto dto);

    /// <summary>Removes one role of one person (access ends on the next request). 404 when they do not have it; 400 for the last sponsor.</summary>
    Task<ProjectPeopleDto> RemoveAsync(Guid projectId, string role, string userId);

    /// <summary>
    /// For <c>ProjectService.CreateAsync</c>: validates and stages (tracked, the caller
    /// saves) the sponsors and the three new lists. Returns who was given a role
    /// (the lead PM is not listed — they get <c>Project.Created</c>).
    /// </summary>
    Task<List<ProjectPersonAdded>> StageForNewProjectAsync(
        Project project, IReadOnlyCollection<string> sponsorUserIds, ProjectPeopleLists lists);

    /// <summary>
    /// For <c>ProjectService.UpdateAsync</c>, after the project fields are set: syncs
    /// the sponsors, applies the three lists (null keeps), drops an additional-PM row
    /// of a new lead PM. A caller without workspace-level access may not change the
    /// lead PM or add project managers (403, decision D4). Returns who was given a
    /// role, a new lead PM included (decision D10).
    /// </summary>
    Task<List<ProjectPersonAdded>> StageForProjectUpdateAsync(
        Project project, string previousLeadProjectManagerId, IReadOnlyCollection<string> sponsorUserIds,
        ProjectPeopleLists lists, bool callerHasWorkspaceLevel);

    /// <summary>Sends the "added to a project" notifications in the background (never fails the save).</summary>
    void NotifyInBackground(Guid projectId, IReadOnlyCollection<ProjectPersonAdded> added);

    /// <summary>Active users matching the search, for the sponsor / viewer / team pickers (decision D13).</summary>
    Task<List<UserDropdownItemDto>> LookupUsersAsync(string? search, int take);

    /// <summary>
    /// The assignee picker of the project's action items and sub-items. Workspace-level
    /// callers: the workspace's assignable users plus the project's team members.
    /// Project-only callers: the project's own people (lead PM, additional PMs, team
    /// members) — never the workspace's member lists (decision D4). 404 when hidden.
    /// </summary>
    Task<List<AssignableUserDto>> GetAssignableUsersAsync(Guid projectId);

    /// <summary>
    /// The server-side assignee pool: the workspace's assignable users, plus the
    /// project's team members when <paramref name="projectId"/> is given.
    /// </summary>
    Task<HashSet<string>> GetAssigneePoolAsync(Guid workspaceId, Guid? projectId);
}
