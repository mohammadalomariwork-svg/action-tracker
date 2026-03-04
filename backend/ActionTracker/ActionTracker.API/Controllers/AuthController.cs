using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.Auth;
using ActionTracker.Application.Features.Auth.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

/// <summary>
/// Handles all authentication operations: local login, Azure AD federated login,
/// access-token refresh, and session logout.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService            _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger      = logger;
    }

    // -------------------------------------------------------------------------
    // POST api/auth/login
    // -------------------------------------------------------------------------

    /// <summary>
    /// Authenticates a locally registered user with their email and password.
    /// On success, returns a short-lived JWT access token and a long-lived
    /// refresh token. Returns 401 if the credentials are wrong, the account
    /// is disabled, or the account is registered for Azure AD login only.
    /// Returns 400 if the request body fails validation.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>),          StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<string>),          StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        try
        {
            var result = await _authService.LoginAsync(request);
            return Ok(ApiResponse<AuthResponseDto>.Ok(result));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Local login failed for {Email}: {Message}", request.Email, ex.Message);
            return Unauthorized(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // -------------------------------------------------------------------------
    // POST api/auth/azure-login
    // -------------------------------------------------------------------------

    /// <summary>
    /// Authenticates a user who has completed the Microsoft Entra ID (Azure AD)
    /// interactive login on the frontend. Validates the supplied MSAL access
    /// token, resolves or auto-provisions the local user record, and returns
    /// application-level JWT tokens. Returns 401 if the Azure AD token is
    /// invalid or the resolved account is a Local-only account.
    /// Returns 400 if the request body fails validation.
    /// </summary>
    [HttpPost("azure-login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>),          StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<string>),          StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AzureLogin([FromBody] AzureAdLoginRequestDto request)
    {
        try
        {
            var result = await _authService.LoginWithAzureAdAsync(request);
            return Ok(ApiResponse<AuthResponseDto>.Ok(result));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError("Azure AD is not configured: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                ApiResponse<string>.Fail("Azure AD authentication is not configured on this server."));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Azure AD login failed: {Message}", ex.Message);
            return Unauthorized(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // -------------------------------------------------------------------------
    // POST api/auth/refresh-token
    // -------------------------------------------------------------------------

    /// <summary>
    /// Issues a new access token and refresh token pair in exchange for a valid,
    /// unexpired refresh token paired with its corresponding (possibly expired)
    /// access token. The supplied refresh token is rotated (invalidated) as part
    /// of this operation. Returns 401 if either token is invalid, revoked, or
    /// expired. Returns 400 if the request body fails validation.
    /// </summary>
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>),          StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<string>),          StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto request)
    {
        try
        {
            var result = await _authService.RefreshTokenAsync(request);
            return Ok(ApiResponse<AuthResponseDto>.Ok(result));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Token refresh failed: {Message}", ex.Message);
            return Unauthorized(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // -------------------------------------------------------------------------
    // POST api/auth/logout
    // -------------------------------------------------------------------------

    /// <summary>
    /// Revokes all active refresh tokens for the currently authenticated user,
    /// effectively ending all sessions. Requires a valid Bearer token in the
    /// Authorization header. The user ID is read directly from the JWT sub claim.
    /// Returns 401 if the caller is not authenticated or the sub claim is absent.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout()
    {
        // JWT sub is mapped to ClaimTypes.NameIdentifier by the JwtBearer middleware;
        // fall back to the raw "sub" claim name for robustness.
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Logout attempted but user ID could not be resolved from claims");
            return Unauthorized(ApiResponse<string>.Fail("User identity could not be determined."));
        }

        await _authService.RevokeTokenAsync(userId);

        _logger.LogInformation("User {UserId} logged out — all refresh tokens revoked", userId);

        return Ok(ApiResponse<string>.Ok("Logged out successfully."));
    }
}
