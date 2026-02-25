using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Features.Auth.DTOs;

/// <summary>
/// Request payload for authenticating a user via Microsoft Entra ID (Azure AD).
/// The frontend acquires this token through the MSAL library after the user
/// completes the Microsoft login flow, then passes it here for server-side validation.
/// </summary>
public class AzureAdLoginRequestDto
{
    /// <summary>
    /// The access token obtained from Microsoft Entra ID after a successful
    /// interactive login on the frontend. The API validates this token,
    /// extracts the user's identity (oid, email, name claims), and issues
    /// an application-level JWT in return.
    /// </summary>
    [Required]
    public string AccessToken { get; set; } = string.Empty;
}
