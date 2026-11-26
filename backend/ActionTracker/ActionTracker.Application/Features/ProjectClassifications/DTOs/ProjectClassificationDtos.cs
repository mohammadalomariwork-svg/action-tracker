namespace ActionTracker.Application.Features.ProjectClassifications.DTOs;

/// <summary>One internal project classification of a workspace (WO-2026-11 P-1).</summary>
public class ProjectClassificationDto
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>1-based position in the workspace's list.</summary>
    public int SortOrder { get; set; }

    /// <summary>False = kept on the projects that have it, not offered for new choices.</summary>
    public bool IsActive { get; set; }

    /// <summary>Live projects of the workspace that carry it.</summary>
    public int ProjectCount { get; set; }

    /// <summary>Soft-deleted projects that still carry it (they block a delete too).</summary>
    public int DeletedProjectCount { get; set; }

    /// <summary>True when no project, live or deleted, uses it — a delete would be accepted.</summary>
    public bool CanDelete => ProjectCount == 0 && DeletedProjectCount == 0;
}

/// <summary>Body of POST <c>/api/workspaces/{id}/project-classifications</c>. Added at the end, active.</summary>
public class CreateProjectClassificationDto
{
    public string Name { get; set; } = string.Empty;
}

/// <summary>Body of PUT <c>/api/workspaces/{id}/project-classifications/{classificationId}</c>: rename and / or (de)activate.</summary>
public class UpdateProjectClassificationDto
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

/// <summary>Body of PUT <c>/api/workspaces/{id}/project-classifications/reorder</c>: every live classification once, in the new order.</summary>
public class ReorderProjectClassificationsDto
{
    public List<Guid> ClassificationIds { get; set; } = new();
}
