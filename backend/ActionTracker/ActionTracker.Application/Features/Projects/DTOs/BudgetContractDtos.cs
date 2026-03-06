using System;
using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Features.Projects.DTOs;

// ── Budget ────────────────────────────────────────────────────────────────────

/// <summary>
/// Read model for a project's budget record.
/// </summary>
public class ProjectBudgetDto
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>Project this budget belongs to.</summary>
    public int ProjectId { get; set; }

    /// <summary>Total approved budget for the project.</summary>
    public decimal TotalBudget { get; set; }

    /// <summary>Amount spent to date.</summary>
    public decimal SpentAmount { get; set; }

    /// <summary>ISO 4217 currency code (e.g. "AED", "USD").</summary>
    public string Currency { get; set; } = "AED";

    /// <summary>Optional notes about the budget, funding sources, or approval references.</summary>
    public string? BudgetNotes { get; set; }

    /// <summary>
    /// Remaining budget, calculated as <c>TotalBudget − SpentAmount</c>.
    /// Read-only — derived at the service or mapping layer.
    /// </summary>
    public decimal RemainingBudget => TotalBudget - SpentAmount;

    /// <summary>UTC timestamp when the budget record was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the most recent update, or <c>null</c> if never updated.</summary>
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Payload for creating or fully replacing a project budget record.
/// Used for both the initial POST and subsequent PUT operations (upsert pattern).
/// </summary>
public class CreateUpdateBudgetDto
{
    /// <summary>Project the budget belongs to (required).</summary>
    [Required]
    public int ProjectId { get; set; }

    /// <summary>
    /// Total approved budget (required).
    /// Must be between 0.01 and 999,999,999.
    /// </summary>
    [Required]
    [Range(0.01, 999_999_999,
        ErrorMessage = "TotalBudget must be between 0.01 and 999,999,999.")]
    public decimal TotalBudget { get; set; }

    /// <summary>
    /// Amount spent to date. Defaults to <c>0</c> for a new budget record.
    /// </summary>
    [Range(0, 999_999_999,
        ErrorMessage = "SpentAmount must be between 0 and 999,999,999.")]
    public decimal SpentAmount { get; set; } = 0m;

    /// <summary>
    /// ISO 4217 currency code for all monetary values (max 10 chars).
    /// Defaults to <c>"AED"</c>.
    /// </summary>
    [MaxLength(10)]
    public string Currency { get; set; } = "AED";

    /// <summary>Optional free-text notes (max 1000 chars).</summary>
    [MaxLength(1000)]
    public string? BudgetNotes { get; set; }
}

// ── Contract ──────────────────────────────────────────────────────────────────

/// <summary>
/// Read model for a contract associated with a project.
/// </summary>
public class ContractDto
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>Project this contract is associated with.</summary>
    public int ProjectId { get; set; }

    /// <summary>Official contract or purchase-order number.</summary>
    public string ContractNumber { get; set; } = string.Empty;

    /// <summary>Legal name of the contractor / vendor organisation.</summary>
    public string ContractorName { get; set; } = string.Empty;

    /// <summary>Name, phone, or e-mail of the primary contractor contact.</summary>
    public string? ContractorContact { get; set; }

    /// <summary>Total monetary value of this contract.</summary>
    public decimal ContractValue { get; set; }

    /// <summary>ISO 4217 currency code for the contract value.</summary>
    public string Currency { get; set; } = "AED";

    /// <summary>Date the contract became / becomes effective (UTC).</summary>
    public DateTime StartDate { get; set; }

    /// <summary>Contract expiry date (UTC), or <c>null</c> for open-ended contracts.</summary>
    public DateTime? EndDate { get; set; }

    /// <summary>Optional description of scope, deliverables, or terms.</summary>
    public string? Description { get; set; }

    /// <summary>Whether this contract record is active.</summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// Payload for creating a new contract record on a project.
/// </summary>
public class CreateContractDto
{
    /// <summary>Project the contract is associated with (required).</summary>
    [Required]
    public int ProjectId { get; set; }

    /// <summary>Official contract or purchase-order number (required, max 100 chars).</summary>
    [Required]
    [MaxLength(100)]
    public string ContractNumber { get; set; } = string.Empty;

    /// <summary>Legal name of the contractor / vendor (required, max 300 chars).</summary>
    [Required]
    [MaxLength(300)]
    public string ContractorName { get; set; } = string.Empty;

    /// <summary>Primary contractor contact — name, phone, or e-mail (max 300 chars).</summary>
    [MaxLength(300)]
    public string? ContractorContact { get; set; }

    /// <summary>Total contract value (required).</summary>
    [Required]
    public decimal ContractValue { get; set; }

    /// <summary>
    /// ISO 4217 currency code for the contract value (max 10 chars).
    /// Defaults to <c>"AED"</c>.
    /// </summary>
    [MaxLength(10)]
    public string Currency { get; set; } = "AED";

    /// <summary>Effective start date of the contract (required).</summary>
    [Required]
    public DateTime StartDate { get; set; }

    /// <summary>Contract expiry date (nullable — omit for open-ended contracts).</summary>
    public DateTime? EndDate { get; set; }

    /// <summary>Optional description of contract scope and deliverables (max 1000 chars).</summary>
    [MaxLength(1000)]
    public string? Description { get; set; }
}

/// <summary>
/// Payload for updating an existing contract record.
/// All fields are optional; only non-null values are applied.
/// </summary>
public class UpdateContractDto
{
    /// <summary>Primary key of the contract to update (required).</summary>
    [Required]
    public int Id { get; set; }

    /// <summary>Updated contract or purchase-order number (max 100 chars).</summary>
    [MaxLength(100)]
    public string? ContractNumber { get; set; }

    /// <summary>Updated contractor / vendor name (max 300 chars).</summary>
    [MaxLength(300)]
    public string? ContractorName { get; set; }

    /// <summary>Updated primary contractor contact (max 300 chars).</summary>
    [MaxLength(300)]
    public string? ContractorContact { get; set; }

    /// <summary>Updated contract value.</summary>
    public decimal? ContractValue { get; set; }

    /// <summary>Updated currency code (max 10 chars).</summary>
    [MaxLength(10)]
    public string? Currency { get; set; }

    /// <summary>Updated effective start date.</summary>
    public DateTime? StartDate { get; set; }

    /// <summary>Updated expiry date (set to <c>null</c> to make the contract open-ended).</summary>
    public DateTime? EndDate { get; set; }

    /// <summary>Updated description (max 1000 chars).</summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>Updated active state.</summary>
    public bool? IsActive { get; set; }
}
