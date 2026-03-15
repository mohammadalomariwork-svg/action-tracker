namespace ActionTracker.Application.Permissions.DTOs;

public class RolePermissionDto
{
    public Guid Id { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public Guid AreaId { get; set; }
    public string AreaName { get; set; } = string.Empty;
    public Guid ActionId { get; set; }
    public string ActionName { get; set; } = string.Empty;
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
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}
