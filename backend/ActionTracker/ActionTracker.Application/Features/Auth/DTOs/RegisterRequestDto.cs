using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Features.Auth.DTOs;

public class RegisterRequestDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;

    public string Department { get; set; } = string.Empty;

    public string Role { get; set; } = "TeamMember";
}
