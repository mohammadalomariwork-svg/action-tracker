using System.ComponentModel;

namespace ActionTracker.Domain.Enums;

/// <summary>
/// Role of a person on one project (WO-2026-11 P-2), stored in <c>ProjectMembers</c>.
/// The lead project manager stays in <c>Project.ProjectManagerUserId</c> and the
/// sponsors in <c>ProjectSponsors</c>; neither is ever a <c>ProjectMember</c> row.
/// </summary>
public enum ProjectMemberRole
{
    /// <summary>Additional project manager: the lead PM's rights on the project, except approval routing.</summary>
    [Description("Project manager")]
    ProjectManager = 1,

    /// <summary>Reads the project and everything under it.</summary>
    [Description("Viewer")]
    Viewer = 2,

    /// <summary>Reads the project and works on the action items and sub-items assigned to them.</summary>
    [Description("Team member")]
    TeamMember = 3,
}
