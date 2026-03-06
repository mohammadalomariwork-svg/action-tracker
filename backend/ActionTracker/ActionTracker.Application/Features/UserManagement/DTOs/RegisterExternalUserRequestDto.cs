using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Features.UserManagement.DTOs;

public class RegisterExternalUserRequestDto
{
    [Required]
    [EmailAddress]
    public string  Email       { get; set; } = string.Empty;

    [Required]
    public string  FullName    { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    [Required]
    [MinLength(8)]
    public string  Password    { get; set; } = string.Empty;

    [Required]
    public string  RoleName    { get; set; } = string.Empty;
}
