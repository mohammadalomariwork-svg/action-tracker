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
    /// ASP.NET Identity user ID of the manager.
    /// Stored as a plain string so that managers who are not yet registered
    /// in the system can still be referenced by an external HR ID.
    /// </summary>
    public string? ManagerId { get; set; }

    public string? ManagerName { get; set; }

    // ── Navigation ─────────────────────────────────────────────────────────────

    public ApplicationUser User { get; set; } = null!;
}
