using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.ActionItems.DTOs;
using ActionTracker.Application.Features.Auth.DTOs;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Enums;
using FluentAssertions;

namespace ActionTracker.Tests.Integration;

/// <summary>
/// Integration tests for the ActionItemsController.
/// Each test class gets its own <see cref="ActionTrackerWebApplicationFactory"/>
/// (and therefore its own InMemory database) via <see cref="IClassFixture{T}"/>.
/// The admin user is seeded automatically during application startup.
/// </summary>
public class ActionItemsControllerIntegrationTests
    : IClassFixture<ActionTrackerWebApplicationFactory>
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public ActionItemsControllerIntegrationTests(ActionTrackerWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // -------------------------------------------------------------------------
    // Auth helpers
    // -------------------------------------------------------------------------

    private async Task<LoginResponseDto> LoginAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var body = JsonSerializer.Deserialize<ApiResponse<LoginResponseDto>>(json, JsonOptions);
        return body!.Data!;
    }

    /// <summary>Logs in as the seeded admin user, sets Bearer token, and returns the DTO.</summary>
    private async Task<LoginResponseDto> AuthenticateAsAdminAsync()
    {
        var tokens = await LoginAsync("admin@actiontracker.com", "Admin@2026!");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return tokens;
    }

    /// <summary>
    /// Registers (if needed) and logs in as a TeamMember user, then sets Bearer token.
    /// Registration errors are silently ignored so the helper is idempotent within a test run.
    /// </summary>
    private async Task AuthenticateAsTeamMemberAsync()
    {
        // Attempt registration – ignore 4xx/5xx if user already exists
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email     = "member@integration.test",
            password  = "Member@2026!",
            firstName = "Integration",
            lastName  = "Member",
            role      = "TeamMember",
        });

        var tokens = await LoginAsync("member@integration.test", "Member@2026!");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
    }

    private static ActionItemCreateDto MakeCreateDto(string assigneeId) => new()
    {
        Title       = "Integration Test Item",
        Description = "Created by integration test",
        AssigneeId  = assigneeId,
        Category    = ActionCategory.IT,
        Priority    = ActionPriority.Medium,
        Status      = ActionStatus.ToDo,
        DueDate     = DateTime.UtcNow.AddDays(14),
    };

    // -------------------------------------------------------------------------
    // GET api/action-items — returns 200 with paged result
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAll_WhenAuthenticated_Returns200WithPagedResult()
    {
        // Arrange
        await AuthenticateAsAdminAsync();

        // Act
        var response = await _client.GetAsync("/api/action-items");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        var body = JsonSerializer.Deserialize<ApiResponse<PagedResult<ActionItemResponseDto>>>(json, JsonOptions);

        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.Items.Should().NotBeNull();
    }

    // -------------------------------------------------------------------------
    // POST api/action-items — valid data returns 201
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_WithValidData_Returns201()
    {
        // Arrange
        var adminTokens = await AuthenticateAsAdminAsync();
        var dto         = MakeCreateDto(adminTokens.User.Id);

        // Act
        var response = await _client.PostAsJsonAsync("/api/action-items", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var json = await response.Content.ReadAsStringAsync();
        var body = JsonSerializer.Deserialize<ApiResponse<ActionItemResponseDto>>(json, JsonOptions);

        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data!.Title.Should().Be("Integration Test Item");
        body.Data.ActionId.Should().MatchRegex(@"^ACT-\d{3}$");
    }

    // -------------------------------------------------------------------------
    // POST api/action-items — no auth header returns 401
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_WithoutAuth_Returns401()
    {
        // Arrange — ensure no auth header is set
        _client.DefaultRequestHeaders.Authorization = null;
        var dto = MakeCreateDto("any-user-id");

        // Act
        var response = await _client.PostAsJsonAsync("/api/action-items", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // -------------------------------------------------------------------------
    // PUT api/action-items/{id} — updates item and returns 200
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Update_ExistingItem_Returns200WithUpdatedData()
    {
        // Arrange — create an item to update
        var adminTokens  = await AuthenticateAsAdminAsync();
        var createResp   = await _client.PostAsJsonAsync("/api/action-items", MakeCreateDto(adminTokens.User.Id));
        createResp.EnsureSuccessStatusCode();

        var createJson   = await createResp.Content.ReadAsStringAsync();
        var created      = JsonSerializer.Deserialize<ApiResponse<ActionItemResponseDto>>(createJson, JsonOptions);
        var itemId       = created!.Data!.Id;

        var updateDto    = new ActionItemUpdateDto
        {
            Id       = itemId,
            Title    = "Updated via Integration Test",
            Priority = ActionPriority.High,
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/action-items/{itemId}", updateDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        var body = JsonSerializer.Deserialize<ApiResponse<ActionItemResponseDto>>(json, JsonOptions);

        body!.Data!.Title.Should().Be("Updated via Integration Test");
        body.Data.Priority.Should().Be(ActionPriority.High);
    }

    // -------------------------------------------------------------------------
    // DELETE api/action-items/{id} — TeamMember (no Manager role) returns 403
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Delete_AsTeamMember_Returns403()
    {
        // Arrange — create item as admin
        var adminTokens = await AuthenticateAsAdminAsync();
        var createResp  = await _client.PostAsJsonAsync("/api/action-items", MakeCreateDto(adminTokens.User.Id));
        createResp.EnsureSuccessStatusCode();

        var createJson  = await createResp.Content.ReadAsStringAsync();
        var created     = JsonSerializer.Deserialize<ApiResponse<ActionItemResponseDto>>(createJson, JsonOptions);
        var itemId      = created!.Data!.Id;

        // Switch to a TeamMember identity
        await AuthenticateAsTeamMemberAsync();

        // Act
        var response = await _client.DeleteAsync($"/api/action-items/{itemId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
