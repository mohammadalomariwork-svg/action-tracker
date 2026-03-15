using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Permissions.DTOs;

public class CreateRolePermissionDto
{
    [Required]
    [MaxLength(256)]
    public string RoleName { get; set; } = string.Empty;

    [Required]
    public Guid AreaId { get; set; }

    [Required]
    public Guid ActionId { get; set; }

    [Required]
    [Range(0, 2)]
    public int OrgUnitScope { get; set; }

    /// <summary>Required when <see cref="OrgUnitScope"/> equals 1 (Specific Org Unit).</summary>
    public Guid? OrgUnitId { get; set; }

    [MaxLength(256)]
    public string? OrgUnitName { get; set; }
}
