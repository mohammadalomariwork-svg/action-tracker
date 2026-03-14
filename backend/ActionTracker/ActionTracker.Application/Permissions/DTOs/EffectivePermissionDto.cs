namespace ActionTracker.Application.Permissions.DTOs;

public class EffectivePermissionDto
{
    public string UserId { get; set; } = string.Empty;
    public string UserDisplayName { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public bool IsAllowed { get; set; }

    /// <summary>
    /// Indicates where the final permission decision came from.
    /// Possible values: "Role", "UserOverride-Granted", "UserOverride-Revoked".
    /// </summary>
    public string Source { get; set; } = string.Empty;

    public string OrgUnitScope { get; set; } = string.Empty;
    public Guid? OrgUnitId { get; set; }
    public string? OrgUnitName { get; set; }
}
