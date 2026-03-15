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
}
