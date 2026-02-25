using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ActionTracker.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ActionTracker.Infrastructure.Helpers;

/// <summary>
/// Provides JWT access-token generation, cryptographically secure refresh-token
/// creation, and expired-token principal extraction.
/// <para>
/// Reads all signing material from the <c>Jwt</c> configuration section
/// (<c>Jwt:Key</c>, <c>Jwt:Issuer</c>, <c>Jwt:Audience</c>, <c>Jwt:ExpiryMinutes</c>).
/// No secrets are hardcoded in this class.
/// </para>
/// <para>Register as a <b>scoped</b> service in the DI container.</para>
/// </summary>
public class JwtTokenHelper
{
    private readonly IConfiguration _configuration;
    private readonly UserManager<ApplicationUser> _userManager;

    /// <summary>
    /// Initialises a new instance of <see cref="JwtTokenHelper"/>.
    /// </summary>
    /// <param name="configuration">
    /// Application configuration used to resolve <c>Jwt:Key</c>,
    /// <c>Jwt:Issuer</c>, <c>Jwt:Audience</c>, and <c>Jwt:ExpiryMinutes</c>.
    /// </param>
    /// <param name="userManager">
    /// ASP.NET Core Identity user manager, used to resolve user-specific
    /// data (such as the login provider) when building token claims.
    /// </param>
    public JwtTokenHelper(IConfiguration configuration, UserManager<ApplicationUser> userManager)
    {
        _configuration = configuration;
        _userManager   = userManager;
    }

    /// <summary>
    /// Generates a signed HS256 JWT access token for the given user.
    /// </summary>
    /// <remarks>
    /// The token includes the following claims:
    /// <list type="bullet">
    ///   <item><term>sub</term><description>The user's unique identifier.</description></item>
    ///   <item><term>email</term><description>The user's email address.</description></item>
    ///   <item><term>name</term><description>The user's full display name.</description></item>
    ///   <item><term>loginProvider</term><description>"Local" or "AzureAD" — how the user authenticated.</description></item>
    ///   <item><term>role</term><description>One claim per application role assigned to the user.</description></item>
    ///   <item><term>jti</term><description>A unique token identifier (GUID) to support revocation.</description></item>
    /// </list>
    /// Token lifetime and signing material are read from <c>IConfiguration</c>:
    /// <c>Jwt:Key</c>, <c>Jwt:Issuer</c>, <c>Jwt:Audience</c>, <c>Jwt:ExpiryMinutes</c>.
    /// </remarks>
    /// <param name="user">The <see cref="ApplicationUser"/> for whom the token is issued.</param>
    /// <param name="roles">The list of application roles assigned to the user.</param>
    /// <returns>A signed JWT string ready to be sent to the client.</returns>
    public string GenerateAccessToken(ApplicationUser user, IList<string> roles)
    {
        var key           = _configuration["Jwt:Key"]!;
        var issuer        = _configuration["Jwt:Issuer"];
        var audience      = _configuration["Jwt:Audience"];
        var expiryMinutes = int.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "60");

        var signingKey  = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Name,  user.DisplayName ?? user.FullName),
            new("loginProvider",               user.LoginProvider),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
        };

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var token = new JwtSecurityToken(
            issuer:             issuer,
            audience:           audience,
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Generates a cryptographically secure, URL-safe refresh token.
    /// </summary>
    /// <remarks>
    /// Fills 64 random bytes from <see cref="RandomNumberGenerator"/> and
    /// returns them as a Base64 string (~88 characters). The output has no
    /// structure and carries no user information — it is an opaque handle
    /// stored server-side alongside its owning user record.
    /// </remarks>
    /// <returns>A Base64-encoded 64-byte random string.</returns>
    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        RandomNumberGenerator.Fill(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    /// <summary>
    /// Extracts and validates the <see cref="ClaimsPrincipal"/> from a JWT that
    /// may already be expired, returning <see langword="null"/> if the token is
    /// structurally invalid or was signed with a different key.
    /// </summary>
    /// <remarks>
    /// This method is intended for token-refresh flows where the caller needs to
    /// identify the user from an expired access token before issuing a new one.
    /// Lifetime validation is deliberately disabled; all other structural checks
    /// (signature, issuer signing key, algorithm) remain active.
    /// Only tokens signed with HS256 are accepted; any other algorithm causes
    /// the method to return <see langword="null"/>.
    /// </remarks>
    /// <param name="token">The expired (or valid) JWT string to inspect.</param>
    /// <returns>
    /// The <see cref="ClaimsPrincipal"/> extracted from the token, or
    /// <see langword="null"/> if validation fails for any reason.
    /// </returns>
    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var key = _configuration["Jwt:Key"]!;

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidateIssuer           = false,
            ValidateAudience         = false,
            ValidateLifetime         = false, // intentionally skip expiry — caller handles this
            ClockSkew                = TimeSpan.Zero,
        };

        try
        {
            var principal = new JwtSecurityTokenHandler()
                .ValidateToken(token, validationParameters, out var securityToken);

            // Reject tokens signed with any algorithm other than HS256
            if (securityToken is not JwtSecurityToken jwt ||
                !jwt.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return principal;
        }
        catch
        {
            return null;
        }
    }
}
