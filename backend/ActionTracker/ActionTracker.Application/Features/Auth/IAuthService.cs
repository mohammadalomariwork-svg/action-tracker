using ActionTracker.Application.Features.Auth.DTOs;

namespace ActionTracker.Application.Features.Auth;

/// <summary>
/// Defines the contract for application-level authentication operations,
/// supporting both local username/password and Azure AD federated login flows.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Authenticates a locally registered user using their email and password.
    /// Validates the credentials against the stored password hash and, on success,
    /// issues a new access token and refresh token.
    /// </summary>
    /// <param name="request">
    /// The login payload containing the user's <c>Email</c> and <c>Password</c>.
    /// </param>
    /// <returns>
    /// An <see cref="AuthResponseDto"/> containing the access token, refresh token,
    /// token expiry, and the authenticated user's identity information.
    /// </returns>
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request);

    /// <summary>
    /// Authenticates a user who has already completed the Microsoft Entra ID
    /// (Azure AD) interactive login on the frontend. Validates the supplied
    /// access token with Microsoft's identity platform, resolves or provisions
    /// the corresponding local user record, and issues application-level tokens.
    /// </summary>
    /// <param name="request">
    /// The payload containing the <c>AccessToken</c> obtained from MSAL after
    /// the user completes the Microsoft login flow.
    /// </param>
    /// <returns>
    /// An <see cref="AuthResponseDto"/> containing the application access token,
    /// refresh token, token expiry, and the user's identity information.
    /// </returns>
    Task<AuthResponseDto> LoginWithAzureAdAsync(AzureAdLoginRequestDto request);

    /// <summary>
    /// Issues a new access token and refresh token pair using a valid, unexpired
    /// refresh token, allowing the user to remain authenticated without logging in again.
    /// The old refresh token is rotated (invalidated) as part of this operation.
    /// </summary>
    /// <param name="request">
    /// The payload containing the expired <c>AccessToken</c> (for user identification)
    /// and the current <c>RefreshToken</c> to be exchanged.
    /// </param>
    /// <returns>
    /// A new <see cref="AuthResponseDto"/> with a fresh access token, a rotated
    /// refresh token, and updated expiry information.
    /// </returns>
    Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request);

    /// <summary>
    /// Revokes all active refresh tokens belonging to the specified user,
    /// effectively signing them out from all sessions. Subsequent refresh
    /// attempts with any previously issued token will be rejected.
    /// </summary>
    /// <param name="userId">
    /// The unique identifier of the user whose tokens should be revoked.
    /// </param>
    Task RevokeTokenAsync(string userId);
}
