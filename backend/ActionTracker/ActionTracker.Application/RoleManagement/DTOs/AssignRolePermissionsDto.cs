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

    /// <summary>0 = All, 1 = SpecificOrgUnit, 2 = OwnOnly.</summary>
    public int OrgUnitScope { get; set; }

    public Guid? OrgUnitId { get; set; }
    public string? OrgUnitName { get; set; }
}
