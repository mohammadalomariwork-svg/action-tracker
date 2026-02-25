using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Features.Auth.DTOs;

/// <summary>
/// Request payload for obtaining a new access token using an existing
/// refresh token, without requiring the user to log in again.
/// </summary>
public class RefreshTokenRequestDto
{
    /// <summary>
    /// The expired (or near-expiry) access token. Provided so the server can
    /// identify which user and session the refresh request belongs to.
    /// </summary>
    [Required]
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// The long-lived refresh token issued during the original login.
    /// Must be valid and not yet expired or revoked.
    /// </summary>
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
