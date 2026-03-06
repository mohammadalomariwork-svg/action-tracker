using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ActionTracker.Application.Features.Projects.Models;

/// <summary>
/// Holds the financial budget for a project.
/// Each project may have at most one budget record (1-to-1 relationship).
/// Tracks total approved budget, actual spend, and currency.
/// </summary>
public class ProjectBudget
{
    /// <summary>Primary key — auto-incremented integer identity.</summary>
    public int Id { get; set; }

    /// <summary>
    /// Foreign key of the project this budget belongs to.
    /// Unique — each project may have at most one budget record.
    /// </summary>
    [Required]
    public int ProjectId { get; set; }

    /// <summary>
    /// Total approved budget for the project.
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalBudget { get; set; }

    /// <summary>
    /// Amount spent to date. Updated as expenditure is recorded.
    /// Defaults to zero at creation.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? SpentAmount { get; set; } = 0m;

    /// <summary>
    /// ISO 4217 currency code for all monetary values in this record
    /// (e.g. "AED", "USD", "EUR"). Defaults to "AED".
    /// </summary>
    [Required]
    [MaxLength(10)]
    public string Currency { get; set; } = "AED";

    /// <summary>
    /// Optional free-text notes about the budget, funding sources,
    /// or approval references.
    /// </summary>
    [MaxLength(1000)]
    public string? BudgetNotes { get; set; }

    /// <summary>UTC timestamp when the budget record was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the most recent update, or <c>null</c> if never updated.</summary>
    public DateTime? UpdatedAt { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────────

    /// <summary>The project this budget is associated with.</summary>
    public Project Project { get; set; } = null!;
}
