namespace ActionTracker.Application.Permissions;

public class UserPermissionOverride
{
    public Guid Id { get; set; }

    /// <summary>ASP.NET Identity user ID (no FK constraint to AspNetUsers).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Denormalized display name — avoids a join when reading overrides.</summary>
    public string UserDisplayName { get; set; } = string.Empty;

    public PermissionArea Area { get; set; }
    public PermissionAction Action { get; set; }
    public OrgUnitScope OrgUnitScope { get; set; }

    /// <summary>Populated when <see cref="OrgUnitScope"/> is <see cref="OrgUnitScope.SpecificOrgUnit"/>.</summary>
    public Guid? OrgUnitId { get; set; }

    /// <summary>Denormalized display name of the org unit — avoids a join when reading overrides.</summary>
    public string? OrgUnitName { get; set; }

    /// <summary>
    /// <c>true</c> = explicitly granted beyond the role's permissions;
    /// <c>false</c> = explicitly revoked regardless of the role's permissions.
    /// </summary>
    public bool IsGranted { get; set; }

    /// <summary>Optional explanation for why this override was created.</summary>
    public string? Reason { get; set; }

    /// <summary>When null the override does not expire.</summary>
    public DateTime? ExpiresAt { get; set; }

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
