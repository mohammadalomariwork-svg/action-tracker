using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ActionTracker.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ActionTracker.Application.Helpers;

public class JwtHelper
{
    private readonly IConfiguration _configuration;

    public JwtHelper(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Generates a signed HS256 JWT access token containing sub, email, name, role, and jti claims.
    /// Expiry is driven by JwtSettings:AccessTokenExpiryMinutes.
    /// </summary>
    public string GenerateAccessToken(ApplicationUser user, IList<string> roles)
    {
        var secretKey = _configuration["JwtSettings:SecretKey"]!;
        var issuer    = _configuration["JwtSettings:Issuer"];
        var audience  = _configuration["JwtSettings:Audience"];
        var expiryMinutes = int.Parse(_configuration["JwtSettings:AccessTokenExpiryMinutes"]!);

        var key         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Name,  user.FullName),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
        };

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var token = new JwtSecurityToken(
            issuer:             issuer,
            audience:           audience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Returns a cryptographically random 64-character base64 string (48 random bytes).
    /// </summary>
    public string GenerateRefreshToken()
    {
        // 48 bytes → 64 base64 characters (no padding needed)
        var randomBytes = new byte[48];
        RandomNumberGenerator.Fill(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    /// <summary>
    /// Validates a JWT ignoring its expiry and returns the ClaimsPrincipal, or null if invalid.
    /// </summary>
    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var secretKey = _configuration["JwtSettings:SecretKey"]!;

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ValidateIssuer           = false,
            ValidateAudience         = false,
            ValidateLifetime         = false, // intentionally ignore expiry
            ClockSkew                = TimeSpan.Zero,
        };

        try
        {
            var principal = new JwtSecurityTokenHandler()
                .ValidateToken(token, validationParameters, out var securityToken);

            // Reject tokens signed with a different algorithm
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
