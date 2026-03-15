namespace ActionTracker.Application.Permissions.DTOs;

public class EffectivePermissionDto
{
    public string UserId { get; set; } = string.Empty;
    public string UserDisplayName { get; set; } = string.Empty;
    public Guid AreaId { get; set; }
    public string AreaName { get; set; } = string.Empty;
    public Guid ActionId { get; set; }
    public string ActionName { get; set; } = string.Empty;
    public bool IsAllowed { get; set; }

    /// <summary>
    /// Indicates where the final permission decision came from.
    /// Possible values: "Role", "UserOverride-Granted", "UserOverride-Revoked".
    /// </summary>
    public string Source { get; set; } = string.Empty;

    public int OrgUnitScope { get; set; }
    public string OrgUnitScopeLabel => OrgUnitScope switch
    {
        0 => "All",
        1 => "Specific Org Unit",
        2 => "Own Only",
        _ => OrgUnitScope.ToString()
    };
    public Guid? OrgUnitId { get; set; }
    public string? OrgUnitName { get; set; }
}
