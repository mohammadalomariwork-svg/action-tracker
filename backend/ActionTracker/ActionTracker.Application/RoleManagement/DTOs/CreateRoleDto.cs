using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.RoleManagement.DTOs;

public class CreateRoleDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public List<Guid> PermissionIds { get; set; } = new();
}
