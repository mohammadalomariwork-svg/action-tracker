using ActionTracker.Application.Features.ProjectPeople.Interfaces;
using ActionTracker.Domain.Enums;

namespace ActionTracker.Application.Features.ProjectPeople;

/// <summary>
/// Pure rules for project people (WO-2026-11 P-2). The database-backed checks
/// (PM list, active user) are in <c>ProjectPeopleService</c>.
/// </summary>
public static class ProjectPeopleRules
{
    /// <summary>The roles that can be added through the people endpoints.</summary>
    public static readonly IReadOnlyList<string> AddableRoles = new[]
    {
        ProjectAccessRoles.ProjectManager,
        ProjectAccessRoles.Sponsor,
        ProjectAccessRoles.Viewer,
        ProjectAccessRoles.TeamMember,
    };

    /// <summary>The wire role name in its canonical spelling, or null for an unknown one.</summary>
    public static string? NormalizeRole(string? role)
        => AddableRoles.FirstOrDefault(r => string.Equals(r, role?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>The <see cref="ProjectMemberRole"/> of a wire role, or null for the sponsor role (stored in ProjectSponsors).</summary>
    public static ProjectMemberRole? MemberRole(string role) => role switch
    {
        ProjectAccessRoles.ProjectManager => ProjectMemberRole.ProjectManager,
        ProjectAccessRoles.Viewer         => ProjectMemberRole.Viewer,
        ProjectAccessRoles.TeamMember     => ProjectMemberRole.TeamMember,
        _                                 => null,
    };

    /// <summary>User ids trimmed, blanks dropped, de-duplicated (ids compare without regard to case, as in Identity).</summary>
    public static List<string> CleanIds(IEnumerable<string>? ids)
        => (ids ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public const string UnknownRole =
        "Role must be one of: projectManager, sponsor, viewer, teamMember.";

    public const string LeadIsNotAdditional =
        "The lead project manager cannot also be added as an additional project manager.";

    public const string NotOnPmList =
        "The selected user is not on this workspace's Project Managers list. Add them on the workspace settings page first.";

    public const string InactiveUser =
        "Only active users can be added to a project.";

    public const string LastSponsor =
        "A project needs at least one sponsor.";
}
