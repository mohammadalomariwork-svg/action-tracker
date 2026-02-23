using ActionTracker.Application.Features.Auth.DTOs;
using ActionTracker.Application.Features.Auth.Services;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Entities;
using ActionTracker.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace ActionTracker.Tests.Unit;

public class AuthServiceTests : IDisposable
{
    private readonly AppDbContext                        _dbContext;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly JwtHelper                          _jwtHelper;
    private readonly Mock<ILogger<AuthService>>         _loggerMock;
    private readonly IConfiguration                     _configuration;
    private readonly AuthService                        _service;

    public AuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);

        var store = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:SecretKey"]                = "ActionTrackerSuperSecretKeyForJWT2026MustBe32Chars!",
                ["JwtSettings:Issuer"]                   = "ActionTrackerAPI",
                ["JwtSettings:Audience"]                 = "ActionTrackerClient",
                ["JwtSettings:AccessTokenExpiryMinutes"] = "60",
                ["JwtSettings:RefreshTokenExpiryDays"]   = "7",
            })
            .Build();

        _jwtHelper  = new JwtHelper(_configuration);
        _loggerMock = new Mock<ILogger<AuthService>>();

        _service = new AuthService(
            _userManagerMock.Object,
            _jwtHelper,
            _dbContext,
            _loggerMock.Object,
            _configuration);
    }

    public void Dispose() => _dbContext.Dispose();

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static ApplicationUser MakeUser(string email, bool isActive = true) => new()
    {
        Id        = Guid.NewGuid().ToString(),
        UserName  = email,
        Email     = email,
        FirstName = "Test",
        LastName  = "User",
        IsActive  = isActive,
        CreatedAt = DateTime.UtcNow,
    };

    // -------------------------------------------------------------------------
    // LoginAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsTokens()
    {
        // Arrange
        var user = MakeUser("alice@test.com");

        _userManagerMock.Setup(um => um.FindByEmailAsync("alice@test.com"))
                        .ReturnsAsync(user);
        _userManagerMock.Setup(um => um.CheckPasswordAsync(user, "Correct@123"))
                        .ReturnsAsync(true);
        _userManagerMock.Setup(um => um.GetRolesAsync(user))
                        .ReturnsAsync(new List<string> { "TeamMember" });
        _userManagerMock.Setup(um => um.UpdateAsync(user))
                        .ReturnsAsync(IdentityResult.Success);

        var dto = new LoginRequestDto { Email = "alice@test.com", Password = "Correct@123" };

        // Act
        var result = await _service.LoginAsync(dto, "127.0.0.1", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        result.User.Email.Should().Be("alice@test.com");
        result.User.Role.Should().Be("TeamMember");
    }

    [Fact]
    public async Task LoginAsync_WithInvalidPassword_ThrowsException()
    {
        // Arrange
        var user = MakeUser("alice@test.com");

        _userManagerMock.Setup(um => um.FindByEmailAsync("alice@test.com"))
                        .ReturnsAsync(user);
        _userManagerMock.Setup(um => um.CheckPasswordAsync(user, "Wrong@123"))
                        .ReturnsAsync(false);

        var dto = new LoginRequestDto { Email = "alice@test.com", Password = "Wrong@123" };

        // Act
        var act = async () => await _service.LoginAsync(dto, "127.0.0.1", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
                 .WithMessage("Invalid email or password.");
    }

    // -------------------------------------------------------------------------
    // RefreshTokenAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RefreshTokenAsync_WithValidToken_ReturnsNewTokens()
    {
        // Arrange
        var user = MakeUser("alice@test.com");
        _dbContext.Users.Add(user);

        var storedToken = new RefreshToken
        {
            Token       = "valid-refresh-token",
            UserId      = user.Id,
            User        = user,
            ExpiresAt   = DateTime.UtcNow.AddDays(7),
            IsRevoked   = false,
            CreatedByIp = "127.0.0.1",
        };
        _dbContext.RefreshTokens.Add(storedToken);
        await _dbContext.SaveChangesAsync();

        _userManagerMock.Setup(um => um.GetRolesAsync(It.IsAny<ApplicationUser>()))
                        .ReturnsAsync(new List<string> { "TeamMember" });

        // Act
        var result = await _service.RefreshTokenAsync(
            "valid-refresh-token", "127.0.0.1", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBe("valid-refresh-token"); // token was rotated
    }

    [Fact]
    public async Task RefreshTokenAsync_WithExpiredToken_ThrowsException()
    {
        // Arrange
        var user = MakeUser("alice@test.com");
        _dbContext.Users.Add(user);

        var storedToken = new RefreshToken
        {
            Token       = "expired-refresh-token",
            UserId      = user.Id,
            User        = user,
            ExpiresAt   = DateTime.UtcNow.AddDays(-1), // already expired
            IsRevoked   = false,
            CreatedByIp = "127.0.0.1",
        };
        _dbContext.RefreshTokens.Add(storedToken);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = async () => await _service.RefreshTokenAsync(
            "expired-refresh-token", "127.0.0.1", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
                 .WithMessage("Invalid or expired refresh token.");
    }

    [Fact]
    public async Task RefreshTokenAsync_WithRevokedToken_ThrowsException()
    {
        // Arrange
        var user = MakeUser("alice@test.com");
        _dbContext.Users.Add(user);

        var storedToken = new RefreshToken
        {
            Token       = "revoked-refresh-token",
            UserId      = user.Id,
            User        = user,
            ExpiresAt   = DateTime.UtcNow.AddDays(7),
            IsRevoked   = true, // explicitly revoked
            CreatedByIp = "127.0.0.1",
        };
        _dbContext.RefreshTokens.Add(storedToken);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = async () => await _service.RefreshTokenAsync(
            "revoked-refresh-token", "127.0.0.1", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
                 .WithMessage("Invalid or expired refresh token.");
    }
}
