using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Features.Auth.DTOs;

/// <summary>
/// Request payload for authenticating a locally registered user
/// with an email address and password.
/// </summary>
public class LoginRequestDto
{
    /// <summary>
    /// The user's registered email address. Used as the unique login identifier.
    /// </summary>
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// The user's plain-text password. Validated against the stored hash server-side.
    /// </summary>
    [Required]
    public string Password { get; set; } = string.Empty;
}
