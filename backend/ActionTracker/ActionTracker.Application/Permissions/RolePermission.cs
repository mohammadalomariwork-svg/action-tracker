namespace ActionTracker.Application.Permissions;

public class RolePermission
{
    public Guid Id { get; set; }

    /// <summary>The name of the ASP.NET Identity role this permission applies to.</summary>
    public string RoleName { get; set; } = string.Empty;

    public PermissionArea Area { get; set; }
    public PermissionAction Action { get; set; }
    public OrgUnitScope OrgUnitScope { get; set; }

    /// <summary>Populated when <see cref="OrgUnitScope"/> is <see cref="OrgUnitScope.SpecificOrgUnit"/>.</summary>
    public Guid? OrgUnitId { get; set; }

    /// <summary>Denormalized display name of the org unit — avoids a join when reading permissions.</summary>
    public string? OrgUnitName { get; set; }

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
