using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.Auth;
using ActionTracker.Application.Features.Auth.DTOs;
using ActionTracker.Domain.Entities;
using ActionTracker.Infrastructure.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace ActionTracker.Infrastructure.Features.Auth;

/// <summary>
/// Infrastructure implementation of <see cref="IAuthService"/>.
/// Handles local email/password login, Azure AD federated login,
/// refresh-token rotation, and full-session revocation.
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly JwtTokenHelper               _jwtTokenHelper;
    private readonly IAppDbContext                _dbContext;
    private readonly IConfiguration               _configuration;
    private readonly ILogger<AuthService>         _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        JwtTokenHelper               jwtTokenHelper,
        IAppDbContext                dbContext,
        IConfiguration               configuration,
        ILogger<AuthService>         logger)
    {
        _userManager    = userManager;
        _jwtTokenHelper = jwtTokenHelper;
        _dbContext      = dbContext;
        _configuration  = configuration;
        _logger         = logger;
    }

    // -------------------------------------------------------------------------
    // 1. Local login
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
    {
        _logger.LogInformation("Local login attempt for {Email}", request.Email);

        var user = await _userManager.FindByEmailAsync(request.Email);

        // Intentionally give the same error for "not found" and "wrong password"
        // to avoid user-enumeration attacks.
        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            _logger.LogWarning("Failed local login for {Email}: invalid credentials", request.Email);
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (user.LoginProvider != "Local")
        {
            _logger.LogWarning("Local login rejected for {Email}: account uses provider '{Provider}'",
                request.Email, user.LoginProvider);
            throw new UnauthorizedAccessException("Use Azure AD login for this account.");
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Local login rejected for {Email}: account is disabled", request.Email);
            throw new UnauthorizedAccessException("Account is disabled.");
        }

        var roles        = await _userManager.GetRolesAsync(user);
        var accessToken  = _jwtTokenHelper.GenerateAccessToken(user, roles);
        var refreshToken = _jwtTokenHelper.GenerateRefreshToken();

        await PersistRefreshTokenAsync(user.Id, refreshToken);

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("User {UserId} authenticated via local login", user.Id);

        return BuildResponse(user, roles, accessToken, refreshToken);
    }

    // -------------------------------------------------------------------------
    // 2. Azure AD login
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<AuthResponseDto> LoginWithAzureAdAsync(AzureAdLoginRequestDto request)
    {
        _logger.LogInformation("Azure AD login attempt");

        // --- Validate the Azure AD access token via OIDC discovery ---
        var principal = await ValidateAzureAdTokenAsync(request.AccessToken);

        // --- Extract required claims ---
        var azureObjectId =
            principal.FindFirstValue("oid") ??
            principal.FindFirstValue(
                "http://schemas.microsoft.com/identity/claims/objectidentifier") ??
            throw new UnauthorizedAccessException(
                "Azure AD token is missing the oid claim.");

        var email =
            principal.FindFirstValue("preferred_username") ??
            principal.FindFirstValue(JwtRegisteredClaimNames.Email) ??
            principal.FindFirstValue("email") ??
            throw new UnauthorizedAccessException(
                "Azure AD token is missing an email or preferred_username claim.");

        var displayName =
            principal.FindFirstValue("name") ??
            principal.FindFirstValue(JwtRegisteredClaimNames.Name) ??
            email;

        _logger.LogInformation(
            "Azure AD token valid for oid={AzureObjectId}, email={Email}",
            azureObjectId, email);

        // --- Resolve user: by AzureObjectId first, then by email ---
        var user = await _dbContext.Users
                       .FirstOrDefaultAsync(u => u.AzureObjectId == azureObjectId)
                   ?? await _userManager.FindByEmailAsync(email);

        // --- Auto-provision if completely new ---
        if (user is null)
        {
            _logger.LogInformation("Auto-provisioning Azure AD user {Email}", email);

            user = new ApplicationUser
            {
                UserName       = email,
                Email          = email,
                EmailConfirmed = true,
                LoginProvider  = "AzureAD",
                AzureObjectId  = azureObjectId,
                DisplayName    = displayName,
                IsActive       = true,
                CreatedAt      = DateTime.UtcNow,
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
                _logger.LogError(
                    "Failed to provision Azure AD user {Email}: {Errors}", email, errors);
                throw new InvalidOperationException($"User provisioning failed: {errors}");
            }

            await _userManager.AddToRoleAsync(user, "User");
            _logger.LogInformation(
                "Provisioned Azure AD user {UserId} with role 'User'", user.Id);
        }

        // --- Guard: local-only accounts may not authenticate via Azure AD ---
        if (user.LoginProvider == "Local")
        {
            _logger.LogWarning(
                "Azure AD login rejected for {Email}: account is Local-only", email);
            throw new UnauthorizedAccessException(
                "Use username/password login for this account.");
        }

        // --- Backfill AzureObjectId for existing AzureAD users found only by email ---
        if (user.AzureObjectId is null)
        {
            user.AzureObjectId = azureObjectId;
            await _userManager.UpdateAsync(user);
        }

        if (!user.IsActive)
        {
            _logger.LogWarning(
                "Azure AD login rejected for {Email}: account is disabled", email);
            throw new UnauthorizedAccessException("Account is disabled.");
        }

        // --- Issue tokens ---
        var roles        = await _userManager.GetRolesAsync(user);
        var accessToken  = _jwtTokenHelper.GenerateAccessToken(user, roles);
        var refreshToken = _jwtTokenHelper.GenerateRefreshToken();

        await PersistRefreshTokenAsync(user.Id, refreshToken);

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("User {UserId} authenticated via Azure AD", user.Id);

        return BuildResponse(user, roles, accessToken, refreshToken);
    }

    // -------------------------------------------------------------------------
    // 3. Refresh token
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request)
    {
        _logger.LogInformation("Token refresh requested");

        // --- Validate the (possibly expired) access token to identify the user ---
        var principal = _jwtTokenHelper.GetPrincipalFromExpiredToken(request.AccessToken)
            ?? throw new UnauthorizedAccessException("Access token is invalid.");

        var userId =
            principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
            principal.FindFirstValue(ClaimTypes.NameIdentifier) ??
            throw new UnauthorizedAccessException(
                "Access token is missing the sub claim.");

        // --- Locate and validate the refresh token ---
        var stored = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == request.RefreshToken && t.UserId == userId);

        if (stored is null)
        {
            _logger.LogWarning("Refresh token not found for user {UserId}", userId);
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        if (stored.IsRevoked)
        {
            _logger.LogWarning("Revoked refresh token reused by user {UserId}", userId);
            throw new UnauthorizedAccessException("Refresh token has been revoked.");
        }

        if (stored.IsExpired())
        {
            _logger.LogWarning("Expired refresh token used by user {UserId}", userId);
            throw new UnauthorizedAccessException("Refresh token has expired.");
        }

        // --- Resolve user ---
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new UnauthorizedAccessException("User account no longer exists.");

        // --- Rotate: revoke old token, issue new pair ---
        stored.IsRevoked = true;

        var roles           = await _userManager.GetRolesAsync(user);
        var newAccessToken  = _jwtTokenHelper.GenerateAccessToken(user, roles);
        var newRefreshToken = _jwtTokenHelper.GenerateRefreshToken();

        await PersistRefreshTokenAsync(user.Id, newRefreshToken);

        _logger.LogInformation("Tokens rotated for user {UserId}", user.Id);

        return BuildResponse(user, roles, newAccessToken, newRefreshToken);
    }

    // -------------------------------------------------------------------------
    // 4. Revoke all tokens for a user
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task RevokeTokenAsync(string userId)
    {
        _logger.LogInformation("Revoking all refresh tokens for user {UserId}", userId);

        var tokens = await _dbContext.RefreshTokens
            .Where(t => t.UserId == userId && !t.IsRevoked)
            .ToListAsync();

        foreach (var t in tokens)
            t.IsRevoked = true;

        await _dbContext.SaveChangesAsync(CancellationToken.None);

        _logger.LogInformation(
            "Revoked {Count} refresh token(s) for user {UserId}", tokens.Count, userId);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Validates an Azure AD access token by fetching signing keys from the
    /// tenant's OIDC discovery endpoint and verifying the token signature,
    /// issuer, audience, and lifetime.
    /// </summary>
    private async Task<ClaimsPrincipal> ValidateAzureAdTokenAsync(string token)
    {
        var tenantId = _configuration["AzureAd:TenantId"];
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new InvalidOperationException(
                "AzureAd:TenantId is not configured. Set it in appsettings or Secret Manager.");

        var clientId = _configuration["AzureAd:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException(
                "AzureAd:ClientId is not configured. Set it in appsettings or Secret Manager.");

        var metadataAddress =
            $"https://login.microsoftonline.com/{tenantId}/v2.0/.well-known/openid-configuration";

        var configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            metadataAddress,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever { RequireHttps = true });

        OpenIdConnectConfiguration oidcConfig;
        try
        {
            oidcConfig = await configManager.GetConfigurationAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve Azure AD OIDC configuration");
            throw new UnauthorizedAccessException(
                "Could not validate Azure AD token at this time.");
        }

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys        = oidcConfig.SigningKeys,
            ValidateIssuer           = true,
            ValidIssuer              = oidcConfig.Issuer,
            ValidateAudience         = true,
            ValidAudiences           = new[] { clientId, $"api://{clientId}" },
            ValidateLifetime         = true,
            ClockSkew                = TimeSpan.FromMinutes(5),
        };

        try
        {
            return new JwtSecurityTokenHandler()
                .ValidateToken(token, validationParameters, out _);
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "Azure AD token validation failed");
            throw new UnauthorizedAccessException("Azure AD token is invalid or expired.");
        }
    }

    /// <summary>
    /// Creates and saves a new <see cref="RefreshToken"/> record for the given user.
    /// Expiry is driven by <c>Jwt:RefreshTokenExpiryDays</c> (default 7 days).
    /// </summary>
    private async Task PersistRefreshTokenAsync(string userId, string refreshToken)
    {
        var expiryDays = int.Parse(_configuration["Jwt:RefreshTokenExpiryDays"] ?? "7");

        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            Token     = refreshToken,
            UserId    = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(expiryDays),
        });

        await _dbContext.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>
    /// Builds the <see cref="AuthResponseDto"/> returned to the caller.
    /// Access-token expiry is derived from <c>Jwt:ExpiryMinutes</c> (default 60).
    /// </summary>
    private AuthResponseDto BuildResponse(
        ApplicationUser user,
        IList<string>   roles,
        string          accessToken,
        string          refreshToken)
    {
        var expiryMinutes = int.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "60");

        return new AuthResponseDto
        {
            AccessToken       = accessToken,
            RefreshToken      = refreshToken,
            AccessTokenExpiry = DateTime.UtcNow.AddMinutes(expiryMinutes),
            Email             = user.Email!,
            DisplayName       = user.DisplayName ?? user.FullName,
            LoginProvider     = user.LoginProvider,
            Roles             = roles,
        };
    }
}
