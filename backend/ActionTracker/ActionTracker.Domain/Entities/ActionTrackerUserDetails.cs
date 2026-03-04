namespace ActionTracker.Domain.Entities;

/// <summary>
/// Extended organisational profile for every registered user.
/// Uses the ASP.NET Identity user ID as both primary key and foreign key,
/// giving a strict one-to-one relationship with <see cref="ApplicationUser"/>.
/// </summary>
public class ActionTrackerUserDetails
{
    /// <summary>
    /// Primary key — mirrors <c>AspNetUsers.Id</c>.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Username copied from <c>AspNetUsers.UserName</c> for quick display.</summary>
    public string? UserName { get; set; }

    /// <summary>Email copied from <c>AspNetUsers.Email</c> for quick display.</summary>
    public string? Email { get; set; }

    /// <summary>
    /// HR / payroll employee identifier (up to 500 characters).
    /// Sourced from the external HR system — distinct from the ASP.NET Identity UserId.
    /// </summary>
    public string? EmpId { get; set; }

    // ── Department ─────────────────────────────────────────────────────────────

    public int? DepartmentId { get; set; }

    public string? DepartmentName { get; set; }

    // ── Unit ───────────────────────────────────────────────────────────────────

    public int? UnitId { get; set; }

    public string? UnitName { get; set; }

    // ── Section ────────────────────────────────────────────────────────────────

    public int? SectionId { get; set; }

    public string? SectionName { get; set; }

    // ── Manager ────────────────────────────────────────────────────────────────

    /// <summary>
    /// HR employee ID of the manager — same format as <see cref="EmpId"/>.
    /// Up to 500 characters; references the manager's HR record, not their
    /// ASP.NET Identity UserId.
    /// </summary>
    public string? ManagerId { get; set; }

    public string? ManagerName { get; set; }

    // ── Navigation ─────────────────────────────────────────────────────────────

    public ApplicationUser User { get; set; } = null!;
}
