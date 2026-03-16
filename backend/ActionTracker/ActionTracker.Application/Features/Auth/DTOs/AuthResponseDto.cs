namespace ActionTracker.Application.Features.Auth.DTOs;

/// <summary>
/// Unified authentication response returned after a successful login,
/// regardless of whether the user authenticated locally or via Azure AD.
/// Contains the tokens needed for subsequent API calls and the essential
/// identity information required by the frontend.
/// </summary>
public class AuthResponseDto
{
    /// <summary>
    /// A short-lived JWT used to authenticate API requests.
    /// Should be sent as a Bearer token in the Authorization header.
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// A long-lived opaque token used to obtain a new access token
    /// without requiring the user to log in again.
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// The UTC date and time at which the access token expires.
    /// Clients should proactively refresh before this time.
    /// </summary>
    public DateTime AccessTokenExpiry { get; set; }

    /// <summary>
    /// The authenticated user's unique identifier (AspNetUsers.Id).
    /// Used by the frontend to identify "me" when filtering action items.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// The authenticated user's email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// The name to show in the UI. Sourced from <c>DisplayName</c> if set,
    /// otherwise falls back to the user's full name (FirstName + LastName).
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Indicates how the user authenticated. Either <c>"Local"</c> for
    /// username/password accounts or <c>"AzureAD"</c> for federated accounts.
    /// </summary>
    public string LoginProvider { get; set; } = string.Empty;

    /// <summary>
    /// The list of application roles assigned to the user (e.g. "Admin", "Manager", "TeamMember").
    /// Used by the frontend to gate access to role-restricted views.
    /// </summary>
    public IList<string> Roles { get; set; } = new List<string>();
}
