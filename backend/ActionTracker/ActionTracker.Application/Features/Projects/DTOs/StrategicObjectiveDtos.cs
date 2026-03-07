using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Features.Projects.DTOs;

/// <summary>
/// Read model for a strategic objective — returned in list and detail responses.
/// </summary>
public class StrategicObjectiveDto
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Short, descriptive title of the objective.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional extended description.</summary>
    public string? Description { get; set; }

    /// <summary>Organisational unit that owns this objective.</summary>
    public string OrganizationUnit { get; set; } = string.Empty;

    /// <summary>Fiscal year the objective applies to (e.g. 2025).</summary>
    public int FiscalYear { get; set; }

    /// <summary>Whether the objective is active and available for project alignment.</summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// Payload for creating a new strategic objective.
/// </summary>
public class CreateStrategicObjectiveDto
{
    /// <summary>Short, descriptive title of the objective (required, max 300 chars).</summary>
    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional extended description of scope and success criteria.</summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>Organisational unit the objective belongs to (required, max 200 chars).</summary>
    [Required]
    [MaxLength(200)]
    public string OrganizationUnit { get; set; } = string.Empty;

    /// <summary>Fiscal year the objective applies to (required, e.g. 2025).</summary>
    [Required]
    public int FiscalYear { get; set; }
}

/// <summary>
/// Payload for updating an existing strategic objective.
/// </summary>
public class UpdateStrategicObjectiveDto
{
    /// <summary>Primary key of the objective to update.</summary>
    [Required]
    public Guid Id { get; set; }

    /// <summary>Updated title (max 300 chars).</summary>
    [MaxLength(300)]
    public string? Title { get; set; }

    /// <summary>Updated description (max 1000 chars).</summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>Updated organisational unit (max 200 chars).</summary>
    [MaxLength(200)]
    public string? OrganizationUnit { get; set; }

    /// <summary>Updated fiscal year.</summary>
    public int? FiscalYear { get; set; }

    /// <summary>Updated active state.</summary>
    public bool? IsActive { get; set; }
}
