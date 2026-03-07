using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Features.Projects.Models;

/// <summary>
/// Represents an organisational strategic objective for a given fiscal year.
/// Strategic projects are aligned to one objective; operational projects are not.
/// Scoped to an organisational unit so each workspace only sees its own objectives.
/// </summary>
public class StrategicObjective
{
    /// <summary>Primary key — auto-incremented integer identity.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Short, descriptive title of the strategic objective.
    /// </summary>
    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Optional extended description providing context and success criteria.
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Name of the organisational unit this objective belongs to.
    /// Used to filter objectives per workspace so each unit only sees its own.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string OrganizationUnit { get; set; } = string.Empty;

    /// <summary>
    /// The fiscal year this objective applies to (e.g. 2024, 2025).
    /// </summary>
    [Required]
    public int FiscalYear { get; set; }

    /// <summary>
    /// Whether the objective is currently active.
    /// Inactive objectives are hidden from selection dropdowns.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>UTC timestamp when this objective was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Projects that are aligned to this strategic objective.</summary>
    public ICollection<Project> Projects { get; set; } = new List<Project>();
}
