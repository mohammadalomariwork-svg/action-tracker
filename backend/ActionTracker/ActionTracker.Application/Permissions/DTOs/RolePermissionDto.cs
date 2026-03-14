namespace ActionTracker.Application.Permissions.DTOs;

public class RolePermissionDto
{
    public Guid Id { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string OrgUnitScope { get; set; } = string.Empty;
    public Guid? OrgUnitId { get; set; }
    public string? OrgUnitName { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}
