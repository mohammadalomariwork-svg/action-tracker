using ActionTracker.Application.Common.Extensions;
using ActionTracker.Domain.Enums;

namespace ActionTracker.Application.Features.Projects.DTOs;

public class ProjectResponseDto
{
    public Guid Id { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid WorkspaceId { get; set; }
    public string WorkspaceTitle { get; set; } = string.Empty;

    /// <summary>Global classification (Operational / Strategic), shown as "Classification (global)".</summary>
    public ProjectType ProjectType { get; set; }
    public ProjectStatus Status { get; set; }
    public ActionPriority Priority { get; set; }

    /// <summary>
    /// Internal classification of the project's workspace (WO-2026-11 P-1), or
    /// null. Only ever a classification of the project's own workspace.
    /// </summary>
    public Guid? InternalClassificationId { get; set; }
    public string? InternalClassificationName { get; set; }

    /// <summary>False when the project keeps a classification that has since been deactivated.</summary>
    public bool? InternalClassificationIsActive { get; set; }

    public Guid? StrategicObjectiveId { get; set; }
    public string? StrategicObjectiveStatement { get; set; }

    public string ProjectManagerUserId { get; set; } = string.Empty;
    public string ProjectManagerName { get; set; } = string.Empty;

    public List<SponsorDto> Sponsors { get; set; } = new();

    public Guid? OwnerOrgUnitId { get; set; }
    public string? OwnerOrgUnitName { get; set; }

    public DateTime PlannedStartDate { get; set; }
    public DateTime PlannedEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }

    public decimal? ApprovedBudget { get; set; }
    public string Currency { get; set; } = "AED";
    public bool IsBaselined { get; set; }
    public bool IsDeleted { get; set; }

    public int ActionItemCount { get; set; }

    /// <summary>
    /// WO-2026-11 P-2: the caller's roles on this project — <c>leadProjectManager</c>,
    /// <c>projectManager</c>, <c>sponsor</c>, <c>viewer</c>, <c>teamMember</c>.
    /// </summary>
    public List<string> MyRoles { get; set; } = new();

    /// <summary>P-2: the caller may change the project, its people, milestones and risks.</summary>
    public bool CanManage { get; set; }

    /// <summary>P-2: the caller sees the project's workspace itself (false = only through a project role).</summary>
    public bool CanOpenWorkspace { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Human-readable labels
    public string ProjectTypeLabel => ProjectType.GetDescription();
    public string StatusLabel      => Status.GetDescription();
    public string PriorityLabel    => Priority.GetDescription();
}

public class SponsorDto
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class ProjectCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid WorkspaceId { get; set; }
    public ProjectType ProjectType { get; set; }

    /// <summary>Optional internal classification (WO-2026-11 P-1): an active one of <see cref="WorkspaceId"/>, else 400.</summary>
    public Guid? InternalClassificationId { get; set; }

    public Guid? StrategicObjectiveId { get; set; }
    public ActionPriority Priority { get; set; }
    public string ProjectManagerUserId { get; set; } = string.Empty;
    public List<string> SponsorUserIds { get; set; } = new();

    /// <summary>WO-2026-11 P-2: additional project managers (on the workspace's PM list).</summary>
    public List<string>? AdditionalManagerUserIds { get; set; }
    /// <summary>WO-2026-11 P-2: viewers (any active user).</summary>
    public List<string>? ViewerUserIds { get; set; }
    /// <summary>WO-2026-11 P-2: team members (any active user).</summary>
    public List<string>? TeamMemberUserIds { get; set; }

    public Guid? OwnerOrgUnitId { get; set; }
    public DateTime PlannedStartDate { get; set; }
    public DateTime PlannedEndDate { get; set; }
    public decimal? ApprovedBudget { get; set; }
}

public class ProjectUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ProjectType ProjectType { get; set; }

    /// <summary>
    /// Internal classification (WO-2026-11 P-1). A value sets it (an active one
    /// of the project's workspace; the one the project already has is accepted
    /// even when inactive); null or missing keeps the current one, so screens
    /// that do not send it never clear it.
    /// </summary>
    public Guid? InternalClassificationId { get; set; }

    /// <summary>True removes the internal classification. Not together with <see cref="InternalClassificationId"/>.</summary>
    public bool ClearInternalClassification { get; set; }

    public ProjectStatus Status { get; set; }
    public Guid? StrategicObjectiveId { get; set; }
    public ActionPriority Priority { get; set; }
    public string ProjectManagerUserId { get; set; } = string.Empty;
    public List<string> SponsorUserIds { get; set; } = new();

    /// <summary>WO-2026-11 P-2: additional project managers. Missing (null) keeps them.</summary>
    public List<string>? AdditionalManagerUserIds { get; set; }
    /// <summary>WO-2026-11 P-2: viewers. Missing (null) keeps them.</summary>
    public List<string>? ViewerUserIds { get; set; }
    /// <summary>WO-2026-11 P-2: team members. Missing (null) keeps them.</summary>
    public List<string>? TeamMemberUserIds { get; set; }

    public Guid? OwnerOrgUnitId { get; set; }
    public DateTime PlannedStartDate { get; set; }
    public DateTime PlannedEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public decimal? ApprovedBudget { get; set; }
}

public class StrategicObjectiveOptionDto
{
    public Guid Id { get; set; }
    public string ObjectiveCode { get; set; } = string.Empty;
    public string Statement { get; set; } = string.Empty;
}

public class ProjectStatsDto
{
    public int MilestoneCount       { get; set; }
    public int ActionItemCount      { get; set; }
    public decimal CompletionRate   { get; set; }  // 0-100 %
    public decimal OnTimeRate       { get; set; }  // 0-100 %
    public int EscalatedCount       { get; set; }
}

public class ProjectFilterDto
{
    public Guid? WorkspaceId { get; set; }
    public ProjectStatus? Status { get; set; }
    public ProjectType? ProjectType { get; set; }
    public ActionPriority? Priority { get; set; }

    /// <summary>Only projects with this internal classification (by id — WO-2026-11 P-1).</summary>
    public Guid? InternalClassificationId { get; set; }

    /// <summary>True = only projects without an internal classification.</summary>
    public bool Unclassified { get; set; }

    public string? SearchTerm { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string SortBy { get; set; } = "createdAt";
    public bool SortDescending { get; set; } = true;
    public bool IncludeDeleted { get; set; } = false;

    /// <summary>
    /// When non-null and non-empty, only projects whose OwnerOrgUnitId is in this
    /// list (or is null) are returned. Populated server-side from the user's
    /// org unit scope — never sent from the client.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<Guid>? VisibleOrgUnitIds { get; set; }

    /// <summary>
    /// When non-null, only projects belonging to one of these workspace IDs are
    /// returned. Populated server-side from <c>IWorkspaceAccessResolver</c>; a
    /// <c>null</c> value means "no workspace restriction" (privileged role).
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<Guid>? VisibleWorkspaceIds { get; set; }

    /// <summary>
    /// WO-2026-11 P-2: projects where the caller is lead PM, sponsor or a project
    /// member — listed even outside <see cref="VisibleWorkspaceIds"/>. Always set by
    /// the controller from <c>IWorkspaceAccessResolver</c> (a query-string value is
    /// overwritten).
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<Guid> RoleProjectIds { get; set; } = new();

    /// <summary>WO-2026-11 P-2 (My Projects): only projects where the caller holds a project role.</summary>
    public bool Mine { get; set; }

    /// <summary>
    /// When non-null and non-empty, only projects whose StrategicObjectiveId is
    /// in this list are returned. Used by the strategy page to fetch projects
    /// linked to the objectives of the currently-viewed strategic plan.
    /// </summary>
    public List<Guid>? StrategicObjectiveIds { get; set; }
}
