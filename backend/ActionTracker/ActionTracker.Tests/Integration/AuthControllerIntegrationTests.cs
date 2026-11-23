using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.Auth.DTOs;
using ActionTracker.Domain.Constants;
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
    // Local admin created by the test itself — the app no longer seeds one
    // with these credentials.
    private const string AdminEmail    = "admin@actiontracker.com";
    private const string AdminPassword = "Admin@2026!";

    private readonly ActionTrackerWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public AuthControllerIntegrationTests(ActionTrackerWebApplicationFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    private Task EnsureAdminAsync() =>
        _factory.EnsureUserAsync(AdminEmail, AdminPassword, AppRoles.Admin);

    // -------------------------------------------------------------------------
    // POST api/auth/login — valid credentials → 200 with tokens
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Login_WithValidCredentials_Returns200WithTokens()
    {
        // Arrange
        await EnsureAdminAsync();
        var payload = new { email = AdminEmail, password = AdminPassword };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        var body = JsonSerializer.Deserialize<ApiResponse<AuthResponseDto>>(json, JsonOptions);

        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.AccessToken.Should().NotBeNullOrEmpty();
        body.Data.RefreshToken.Should().NotBeNullOrEmpty();
        body.Data.AccessTokenExpiry.Should().BeAfter(DateTime.UtcNow);
        body.Data.Email.Should().Be(AdminEmail);
    }

    // -------------------------------------------------------------------------
    // POST api/auth/login — invalid credentials → 401
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Login_WithInvalidCredentials_Returns401()
    {
        // Arrange
        await EnsureAdminAsync();
        var payload = new { email = AdminEmail, password = "WrongPassword!" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // -------------------------------------------------------------------------
    // POST api/auth/refresh-token — valid token pair → 200 with new tokens
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Refresh_WithValidRefreshToken_Returns200WithNewTokens()
    {
        // Arrange — first login to acquire a token pair
        await EnsureAdminAsync();
        var loginPayload = new { email = AdminEmail, password = AdminPassword };
        var loginResp    = await _client.PostAsJsonAsync("/api/auth/login", loginPayload);
        loginResp.EnsureSuccessStatusCode();

        var loginJson    = await loginResp.Content.ReadAsStringAsync();
        var loginBody    = JsonSerializer.Deserialize<ApiResponse<AuthResponseDto>>(loginJson, JsonOptions);
        var accessToken  = loginBody!.Data!.AccessToken;
        var refreshToken = loginBody.Data.RefreshToken;

        // The endpoint identifies the user from the (possibly expired) access token.
        var refreshPayload = new { accessToken, refreshToken };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/refresh-token", refreshPayload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        var body = JsonSerializer.Deserialize<ApiResponse<AuthResponseDto>>(json, JsonOptions);

        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.AccessToken.Should().NotBeNullOrEmpty();
        body.Data.RefreshToken.Should().NotBeNullOrEmpty();
        // Token rotation: the new refresh token must be different from the one we sent
        body.Data.RefreshToken.Should().NotBe(refreshToken);
    }
}
