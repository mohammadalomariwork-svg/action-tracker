using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Features.UserManagement.DTOs;

public class UpdateUserRoleRequestDto
{
    [Required]
    public string UserId   { get; set; } = string.Empty;

    [Required]
    public string RoleName { get; set; } = string.Empty;
}
