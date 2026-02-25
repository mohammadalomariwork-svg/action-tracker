using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.Auth.DTOs;
using ActionTracker.Application.Features.Auth.Interfaces;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

// Alias resolves ambiguity: the new IAuthService in Features.Auth namespace
// would shadow this one via parent-namespace lookup without the alias.
using ILegacyAuthService = ActionTracker.Application.Features.Auth.Interfaces.IAuthService;

namespace ActionTracker.Application.Features.Auth.Services;

public class AuthService : ILegacyAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly JwtHelper _jwtHelper;
    private readonly IAppDbContext _dbContext;
    private readonly ILogger<AuthService> _logger;
    private readonly IConfiguration _configuration;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        JwtHelper jwtHelper,
        IAppDbContext dbContext,
        ILogger<AuthService> logger,
        IConfiguration configuration)
    {
        _userManager   = userManager;
        _jwtHelper     = jwtHelper;
        _dbContext     = dbContext;
        _logger        = logger;
        _configuration = configuration;
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto dto, string ipAddress, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);

        if (user is null || !await _userManager.CheckPasswordAsync(user, dto.Password))
            throw new UnauthorizedAccessException("Invalid email or password.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Account is inactive.");

        var roles         = await _userManager.GetRolesAsync(user);
        var accessToken   = _jwtHelper.GenerateAccessToken(user, roles);
        var refreshToken  = _jwtHelper.GenerateRefreshToken();
        var expiryMinutes = int.Parse(_configuration["JwtSettings:AccessTokenExpiryMinutes"] ?? "60");
        var expiryDays    = int.Parse(_configuration["JwtSettings:RefreshTokenExpiryDays"]   ?? "7");

        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            Token        = refreshToken,
            UserId       = user.Id,
            ExpiresAt    = DateTime.UtcNow.AddDays(expiryDays),
            CreatedByIp  = ipAddress,
        });

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("User {Email} logged in from {Ip}", user.Email, ipAddress);

        return BuildLoginResponse(user, roles, accessToken, refreshToken, expiryMinutes);
    }

    public async Task<LoginResponseDto> RefreshTokenAsync(string refreshToken, string ipAddress, CancellationToken ct)
    {
        var stored = await _dbContext.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Token == refreshToken, ct);

        if (stored is null || !stored.IsActive())
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        // Rotate: revoke old, issue new
        stored.IsRevoked = true;

        var user          = stored.User;
        var roles         = await _userManager.GetRolesAsync(user);
        var newAccess     = _jwtHelper.GenerateAccessToken(user, roles);
        var newRefresh    = _jwtHelper.GenerateRefreshToken();
        var expiryMinutes = int.Parse(_configuration["JwtSettings:AccessTokenExpiryMinutes"] ?? "60");

        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            Token       = newRefresh,
            UserId      = user.Id,
            ExpiresAt   = stored.ExpiresAt, // preserve original expiry window
            CreatedByIp = ipAddress,
        });

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Refresh token rotated for user {UserId} from {Ip}", user.Id, ipAddress);

        return BuildLoginResponse(user, roles, newAccess, newRefresh, expiryMinutes);
    }

    public async Task RegisterAsync(RegisterRequestDto dto, CancellationToken ct)
    {
        if (await _userManager.FindByEmailAsync(dto.Email) is not null)
            throw new InvalidOperationException($"Email '{dto.Email}' is already registered.");

        var user = new ApplicationUser
        {
            UserName   = dto.Email,
            Email      = dto.Email,
            FirstName  = dto.FirstName,
            LastName   = dto.LastName,
            Department = dto.Department,
            IsActive   = true,
            CreatedAt  = DateTime.UtcNow,
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Registration failed: {errors}");
        }

        var role = string.IsNullOrWhiteSpace(dto.Role) ? "TeamMember" : dto.Role;
        await _userManager.AddToRoleAsync(user, role);

        _logger.LogInformation("New user registered: {Email} with role {Role}", dto.Email, role);
    }

    public async Task RevokeTokenAsync(string refreshToken, CancellationToken ct)
    {
        var token = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(r => r.Token == refreshToken, ct);

        if (token is null || !token.IsActive())
            throw new UnauthorizedAccessException("Invalid or already revoked token.");

        token.IsRevoked = true;
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Refresh token revoked.");
    }

    // -------------------------------------------------------------------------
    private static LoginResponseDto BuildLoginResponse(
        ApplicationUser user,
        IList<string> roles,
        string accessToken,
        string refreshToken,
        int expiryMinutes) => new()
    {
        AccessToken  = accessToken,
        RefreshToken = refreshToken,
        ExpiresAt    = DateTime.UtcNow.AddMinutes(expiryMinutes),
        User = new DTOs.UserInfoDto
        {
            Id         = user.Id,
            Email      = user.Email!,
            FullName   = user.FullName,
            Role       = roles.FirstOrDefault() ?? string.Empty,
            Department = user.Department,
        },
    };
}
