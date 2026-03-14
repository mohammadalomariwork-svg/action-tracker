namespace ActionTracker.Application.Permissions.DTOs;

public class UpdateRolePermissionDto
{
    public int OrgUnitScope { get; set; }
    public Guid? OrgUnitId { get; set; }
    public string? OrgUnitName { get; set; }
    public bool IsActive { get; set; }
}
