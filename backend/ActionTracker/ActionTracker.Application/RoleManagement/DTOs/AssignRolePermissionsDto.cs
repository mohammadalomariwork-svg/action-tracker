using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.RoleManagement.DTOs;

public class AssignRolePermissionsDto
{
    [Required]
    public string RoleName { get; set; } = string.Empty;

    public List<AssignPermissionEntryDto> Permissions { get; set; } = new();
}

public class AssignPermissionEntryDto
{
    public Guid AreaId { get; set; }
    public Guid ActionId { get; set; }
}
