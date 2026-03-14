using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Permissions.DTOs;

public class CreateRolePermissionDto
{
    [Required]
    [MaxLength(256)]
    public string RoleName { get; set; } = string.Empty;

    [Required]
    public int Area { get; set; }

    [Required]
    public int Action { get; set; }

    [Required]
    public int OrgUnitScope { get; set; }

    /// <summary>Required when <see cref="OrgUnitScope"/> equals <see cref="Permissions.OrgUnitScope.SpecificOrgUnit"/>.</summary>
    public Guid? OrgUnitId { get; set; }

    [MaxLength(256)]
    public string? OrgUnitName { get; set; }
}
