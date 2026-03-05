using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Features.UserManagement.DTOs;

public class RegisterADUserRequestDto
{
    [Required]
    [EmailAddress]
    public string  Email       { get; set; } = string.Empty;

    [Required]
    public string  FullName    { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string? EmployeeId  { get; set; }

    public string? Department  { get; set; }

    public string? JobTitle    { get; set; }

    [Required]
    public string  RoleName    { get; set; } = string.Empty;
}
