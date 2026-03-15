namespace ActionTracker.Application.Permissions;

public class RolePermission
{
    public Guid Id { get; set; }

    /// <summary>The name of the ASP.NET Identity role this permission applies to.</summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>ID of the AppPermissionArea record (no FK constraint).</summary>
    public Guid AreaId { get; set; }

    /// <summary>Denormalized area name for fast reads (e.g. "Projects").</summary>
    public string AreaName { get; set; } = string.Empty;

    /// <summary>ID of the AppPermissionAction record (no FK constraint).</summary>
    public Guid ActionId { get; set; }

    /// <summary>Denormalized action name for fast reads (e.g. "View").</summary>
    public string ActionName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    // ── Audit ──────────────────────────────────────────────────────────────
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>User ID string (no FK constraint to AspNetUsers).</summary>
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>User ID string (no FK constraint to AspNetUsers).</summary>
    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; } = false;
}
