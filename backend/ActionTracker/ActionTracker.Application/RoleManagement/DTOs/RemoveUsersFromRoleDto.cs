using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.RoleManagement.DTOs;

public class RemoveUsersFromRoleDto
{
    [Required]
    public string RoleName { get; set; } = string.Empty;

    [Required]
    public List<string> UserIds { get; set; } = new();
}
