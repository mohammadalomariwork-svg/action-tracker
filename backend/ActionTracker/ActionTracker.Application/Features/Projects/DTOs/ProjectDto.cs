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

    public ProjectType ProjectType { get; set; }
    public ProjectStatus Status { get; set; }
    public ActionPriority Priority { get; set; }

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
    public Guid? StrategicObjectiveId { get; set; }
    public ActionPriority Priority { get; set; }
    public string ProjectManagerUserId { get; set; } = string.Empty;
    public List<string> SponsorUserIds { get; set; } = new();
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
    public ProjectStatus Status { get; set; }
    public Guid? StrategicObjectiveId { get; set; }
    public ActionPriority Priority { get; set; }
    public string ProjectManagerUserId { get; set; } = string.Empty;
    public List<string> SponsorUserIds { get; set; } = new();
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

public class ProjectFilterDto
{
    public Guid? WorkspaceId { get; set; }
    public ProjectStatus? Status { get; set; }
    public ProjectType? ProjectType { get; set; }
    public ActionPriority? Priority { get; set; }
    public string? SearchTerm { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string SortBy { get; set; } = "createdAt";
    public bool SortDescending { get; set; } = true;
}
