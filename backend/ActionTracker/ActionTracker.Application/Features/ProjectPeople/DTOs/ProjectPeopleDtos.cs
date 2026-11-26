namespace ActionTracker.Application.Features.ProjectPeople.DTOs;

/// <summary>One person on a project (WO-2026-11 P-2).</summary>
public class ProjectPersonDto
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? OrgUnitName { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// <c>GET /api/projects/{id}/people</c>: everyone linked to the project, by role.
/// The lead PM and sponsors live where they always did (<c>Projects</c>,
/// <c>ProjectSponsors</c>); the other three roles in <c>ProjectMembers</c>.
/// </summary>
public class ProjectPeopleDto
{
    public Guid ProjectId { get; set; }
    public ProjectPersonDto? LeadProjectManager { get; set; }
    public List<ProjectPersonDto> ProjectManagers { get; set; } = new();
    public List<ProjectPersonDto> Sponsors { get; set; } = new();
    public List<ProjectPersonDto> Viewers { get; set; } = new();
    public List<ProjectPersonDto> TeamMembers { get; set; } = new();

    /// <summary>The caller may add and remove people.</summary>
    public bool CanManage { get; set; }

    /// <summary>
    /// The caller may add project managers — needs the workspace's Project Managers
    /// list, which project-only callers do not see (decision D4).
    /// </summary>
    public bool CanAddProjectManagers { get; set; }

    /// <summary>The caller's own roles on the project (wire names).</summary>
    public List<string> MyRoles { get; set; } = new();
}

/// <summary><c>POST /api/projects/{id}/people</c>.</summary>
public class AddProjectPersonDto
{
    public string UserId { get; set; } = string.Empty;

    /// <summary><c>projectManager</c>, <c>sponsor</c>, <c>viewer</c> or <c>teamMember</c>.</summary>
    public string Role { get; set; } = string.Empty;
}

/// <summary>
/// The three new people lists of a project save (POST / PUT <c>/api/projects</c>).
/// On PUT a null list keeps what the project has; on POST null = none.
/// </summary>
public sealed record ProjectPeopleLists(
    IReadOnlyCollection<string>? AdditionalManagerUserIds,
    IReadOnlyCollection<string>? ViewerUserIds,
    IReadOnlyCollection<string>? TeamMemberUserIds);

/// <summary>A person who was given a role in a save — notified afterwards.</summary>
public sealed record ProjectPersonAdded(string UserId, string Role);
