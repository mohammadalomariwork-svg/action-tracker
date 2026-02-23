using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.Auth.DTOs;
using FluentAssertions;

namespace ActionTracker.Tests.Integration;

/// <summary>
/// Integration tests for the AuthController.
/// Uses a dedicated <see cref="ActionTrackerWebApplicationFactory"/> instance
/// (and therefore its own InMemory database) so these tests are fully isolated
/// from the ActionItemsControllerIntegrationTests suite.
/// </summary>
public class AuthControllerIntegrationTests
    : IClassFixture<ActionTrackerWebApplicationFactory>
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public AuthControllerIntegrationTests(ActionTrackerWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // -------------------------------------------------------------------------
    // POST api/auth/login — valid credentials → 200 with tokens
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Login_WithValidCredentials_Returns200WithTokens()
    {
        // Arrange — admin user is seeded during app startup
        var payload = new { email = "admin@actiontracker.com", password = "Admin@2026!" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        var body = JsonSerializer.Deserialize<ApiResponse<LoginResponseDto>>(json, JsonOptions);

        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.AccessToken.Should().NotBeNullOrEmpty();
        body.Data.RefreshToken.Should().NotBeNullOrEmpty();
        body.Data.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        body.Data.User.Email.Should().Be("admin@actiontracker.com");
    }

    // -------------------------------------------------------------------------
    // POST api/auth/login — invalid credentials → 401
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Login_WithInvalidCredentials_Returns401()
    {
        // Arrange
        var payload = new { email = "admin@actiontracker.com", password = "WrongPassword!" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // -------------------------------------------------------------------------
    // POST api/auth/refresh — valid refresh token → 200 with new tokens
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Refresh_WithValidRefreshToken_Returns200WithNewTokens()
    {
        // Arrange — first login to acquire a refresh token
        var loginPayload = new { email = "admin@actiontracker.com", password = "Admin@2026!" };
        var loginResp    = await _client.PostAsJsonAsync("/api/auth/login", loginPayload);
        loginResp.EnsureSuccessStatusCode();

        var loginJson    = await loginResp.Content.ReadAsStringAsync();
        var loginBody    = JsonSerializer.Deserialize<ApiResponse<LoginResponseDto>>(loginJson, JsonOptions);
        var refreshToken = loginBody!.Data!.RefreshToken;

        var refreshPayload = new { refreshToken };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", refreshPayload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        var body = JsonSerializer.Deserialize<ApiResponse<LoginResponseDto>>(json, JsonOptions);

        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.AccessToken.Should().NotBeNullOrEmpty();
        body.Data.RefreshToken.Should().NotBeNullOrEmpty();
        // Token rotation: the new refresh token must be different from the one we sent
        body.Data.RefreshToken.Should().NotBe(refreshToken);
    }
}
