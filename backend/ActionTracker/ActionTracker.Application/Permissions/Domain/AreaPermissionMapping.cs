namespace ActionTracker.Application.Permissions.Domain;

/// <summary>
/// Defines which actions are valid/applicable for a given area.
/// For example, "Dashboard" may support only "View" and "Export",
/// while "Projects" supports all seven actions.
/// This prevents the UI from showing inapplicable toggles.
/// </summary>
public class AreaPermissionMapping
{
    public Guid Id { get; set; }

    /// <summary>ID of the AppPermissionArea (no FK constraint).</summary>
    public Guid AreaId { get; set; }

    /// <summary>Denormalized area name for fast reads.</summary>
    public string AreaName { get; set; } = string.Empty;

    /// <summary>ID of the AppPermissionAction (no FK constraint).</summary>
    public Guid ActionId { get; set; }

    /// <summary>Denormalized action name for fast reads.</summary>
    public string ActionName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>User ID string (no FK constraint to AspNetUsers).</summary>
    public string CreatedBy { get; set; } = string.Empty;
}
