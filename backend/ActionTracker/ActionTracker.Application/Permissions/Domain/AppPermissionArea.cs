namespace ActionTracker.Application.Permissions.Domain;

public class AppPermissionArea
{
    public Guid Id { get; set; }

    /// <summary>Internal name used as a key (e.g. "Projects").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Human-readable label shown in the UI (e.g. "Projects").</summary>
    public string DisplayName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Controls display order in the permission matrix.</summary>
    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>User ID string (no FK constraint to AspNetUsers).</summary>
    public string CreatedBy { get; set; } = string.Empty;
}
