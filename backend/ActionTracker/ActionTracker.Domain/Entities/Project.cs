using ActionTracker.Domain.Enums;

namespace ActionTracker.Domain.Entities;

public class Project
{
    public Guid Id { get; set; }

    /// <summary>Auto-generated code in format PRJ-2025-001.</summary>
    public string ProjectCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid WorkspaceId { get; set; }

    /// <summary>Global classification (shown as "Classification (global)"); drives the approval chain.</summary>
    public ProjectType ProjectType { get; set; }

    /// <summary>
    /// Optional internal classification of the project's own workspace
    /// (WO-2026-11 P-1). A label only — outside the approval workflows and the
    /// date freeze.
    /// </summary>
    public Guid? InternalClassificationId { get; set; }

    public ProjectStatus Status { get; set; } = ProjectStatus.Draft;

    /// <summary>Required when ProjectType is Strategic.</summary>
    public Guid? StrategicObjectiveId { get; set; }

    public ActionPriority Priority { get; set; }

    /// <summary>Project manager — single user (internal or external).</summary>
    public string ProjectManagerUserId { get; set; } = string.Empty;

    /// <summary>Owner department / org unit from existing org chart.</summary>
    public Guid? OwnerOrgUnitId { get; set; }

    public DateTime PlannedStartDate { get; set; }
    public DateTime PlannedEndDate { get; set; }

    /// <summary>Set when project goes Active.</summary>
    public DateTime? ActualStartDate { get; set; }

    /// <summary>Decimal, nullable at creation. Currency is always AED.</summary>
    public decimal? ApprovedBudget { get; set; }

    /// <summary>Always AED.</summary>
    public string Currency { get; set; } = "AED";

    /// <summary>False at creation, locked later.</summary>
    public bool IsBaselined { get; set; } = false;

    public bool IsDeleted { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }

    // Navigation
    public Workspace Workspace { get; set; } = null!;
    public StrategicObjective? StrategicObjective { get; set; }
    public WorkspaceProjectClassification? InternalClassification { get; set; }
    public ApplicationUser ProjectManager { get; set; } = null!;
    public OrgUnit? OwnerOrgUnit { get; set; }
    public ICollection<ProjectSponsor> Sponsors { get; set; } = new List<ProjectSponsor>();
    public ICollection<ProjectRisk> Risks { get; set; } = new List<ProjectRisk>();
    public ICollection<ProjectApprovalRequest> ApprovalRequests { get; set; } = new List<ProjectApprovalRequest>();
}
