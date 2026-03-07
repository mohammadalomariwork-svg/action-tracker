using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ActionTracker.Application.Features.Projects.Models;

/// <summary>
/// Represents a contract with a vendor or contractor associated with a project.
/// Contracts track the contracted value, dates, and contact information for
/// procurement governance and spend reporting.
/// </summary>
public class Contract
{
    /// <summary>Primary key — auto-incremented integer identity.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Foreign key of the project this contract belongs to.</summary>
    [Required]
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Official contract or purchase-order number used for tracking
    /// (e.g. "PO-2025-00123", "CNT-007").
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ContractNumber { get; set; } = string.Empty;

    /// <summary>
    /// Legal name of the contractor / vendor organisation.
    /// </summary>
    [Required]
    [MaxLength(300)]
    public string ContractorName { get; set; } = string.Empty;

    /// <summary>
    /// Name, phone, or e-mail of the primary contact at the contractor.
    /// </summary>
    [MaxLength(300)]
    public string? ContractorContact { get; set; }

    /// <summary>
    /// Total monetary value of this contract.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal ContractValue { get; set; }

    /// <summary>
    /// ISO 4217 currency code for the contract value (e.g. "AED", "USD").
    /// Defaults to "AED".
    /// </summary>
    [MaxLength(10)]
    public string Currency { get; set; } = "AED";

    /// <summary>Date the contract becomes / became effective (UTC).</summary>
    public DateTime StartDate { get; set; }

    /// <summary>Date the contract expires, or <c>null</c> for open-ended contracts.</summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Optional description of the contract scope, deliverables, or terms.
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Whether this contract record is active.
    /// <c>false</c> soft-deletes it from normal queries.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>UTC timestamp when this contract record was created.</summary>
    public DateTime CreatedAt { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────────

    /// <summary>The project this contract is associated with.</summary>
    public Project Project { get; set; } = null!;
}
