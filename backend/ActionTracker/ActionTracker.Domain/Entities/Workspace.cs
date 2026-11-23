using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Domain.Entities;

public class Workspace
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string OrganizationUnit { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Private workspaces are visible only to their own admins and members.
    /// Everyone else — including system Admins / PMO roles — cannot see the
    /// workspace, and its projects / milestones / action items are excluded
    /// from every query and aggregate (enforced by EF global query filters).
    /// </summary>
    public bool IsPrivate { get; set; }

    /// <summary>Foreign key to the OrgUnit this workspace belongs to (for access scoping).</summary>
    public Guid? OrgUnitId { get; set; }

    /// <summary>Optional strategic plan whose objectives this workspace's projects may pull from.</summary>
    public Guid? StrategicPlanId { get; set; }

    /// <summary>
    /// Working calendar used for this workspace's date calculations.
    /// Null means "use the default calendar".
    /// </summary>
    public Guid? WorkCalendarId { get; set; }

    public WorkCalendar? WorkCalendar { get; set; }

    public ICollection<WorkspaceAdmin> Admins { get; set; } = new List<WorkspaceAdmin>();

    public ICollection<WorkspaceMember> Members { get; set; } = new List<WorkspaceMember>();

    /// <summary>Users eligible to be picked as Project Manager in this workspace.</summary>
    public ICollection<WorkspaceProjectManager> ProjectManagers { get; set; }
        = new List<WorkspaceProjectManager>();

    /// <summary>
    /// Sequential chain of approvers for strategic projects in this workspace.
    /// Operational projects bypass approval; only strategic projects consult
    /// this chain.
    /// </summary>
    public ICollection<WorkspaceStrategicProjectApprover> StrategicProjectApprovers { get; set; }
        = new List<WorkspaceStrategicProjectApprover>();
}
