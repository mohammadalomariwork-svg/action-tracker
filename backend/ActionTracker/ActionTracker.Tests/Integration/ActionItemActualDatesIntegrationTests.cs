using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.ActionItems;
using ActionTracker.Application.Features.Auth.DTOs;
using ActionTracker.Domain.Constants;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using ActionTracker.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ActionTracker.Tests.Integration;

/// <summary>
/// WO-2026-11 A-1 through the real pipeline (isolated InMemory database, the
/// seeded "KU Standard" calendar in Asia/Dubai): actual dates on PUT
/// /api/action-items/{id} and what GET returns.
/// </summary>
public class ActionItemActualDatesIntegrationTests
    : IClassFixture<ActionTrackerWebApplicationFactory>
{
    private const string Password   = "ActualDates-Test-2026!";
    private const string AdminEmail = "a1.admin@integration.test";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly ActionTrackerWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ActionItemActualDatesIntegrationTests(ActionTrackerWebApplicationFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    private static DateTime Today => ActionItemActualDates.Today("Asia/Dubai", DateTime.UtcNow);
    private static string Day(DateTime d) => d.ToString("yyyy-MM-dd");

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string> SignInAsAdminAsync()
    {
        await _factory.EnsureUserAsync(AdminEmail, Password, AppRoles.Admin);
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email = AdminEmail, password = Password });
        response.EnsureSuccessStatusCode();
        var body = JsonSerializer.Deserialize<ApiResponse<AuthResponseDto>>(await response.Content.ReadAsStringAsync(), JsonOptions)!;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.Data!.AccessToken);

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return (await users.FindByEmailAsync(AdminEmail))!.Id;
    }

    /// <summary>Adds a workspace and one action item directly (system context).</summary>
    private async Task<Guid> AddItemAsync(string userId, ActionStatus status, ProjectStatus? projectStatus = null,
        DateTime? actualEnd = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var workspace = new Workspace
        {
            Id = Guid.NewGuid(), Title = "A-1 workspace " + Guid.NewGuid().ToString("N")[..6],
            OrganizationUnit = "Integration", CreatedAt = DateTime.UtcNow,
        };
        db.Workspaces.Add(workspace);

        Guid? projectId = null;
        if (projectStatus is not null)
        {
            var project = new Project
            {
                Id = Guid.NewGuid(), ProjectCode = "PRJ-A1-" + Guid.NewGuid().ToString("N")[..4], Name = "A-1 project",
                WorkspaceId = workspace.Id, ProjectType = ProjectType.Operational, Status = projectStatus.Value,
                Priority = ActionPriority.Medium, ProjectManagerUserId = userId,
                PlannedStartDate = Today.AddDays(-30), PlannedEndDate = Today.AddDays(60),
                Currency = "AED", CreatedBy = userId, CreatedAt = DateTime.UtcNow,
            };
            db.Projects.Add(project);
            projectId = project.Id;
        }

        var item = new ActionItem
        {
            Id = Guid.NewGuid(), ActionId = "ACT-A1-" + Guid.NewGuid().ToString("N")[..4], Title = "A-1 item",
            WorkspaceId = workspace.Id, ProjectId = projectId, IsStandalone = projectId is null,
            Status = status, Priority = ActionPriority.Medium, DueDate = Today.AddDays(-2),
            Progress = status == ActionStatus.Done ? 100 : 0, ActualEndDate = actualEnd,
            CreatedByUserId = userId, CreatedAt = DateTime.UtcNow,
        };
        db.ActionItems.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    private async Task<JsonElement> GetDataAsync(Guid id)
    {
        var response = await _client.GetAsync($"/api/action-items/{id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").Clone();
    }

    private static async Task<string?> MessageAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : null;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Put_ActualStart_OnAProjectItemWhosePlannedDatesAreFrozen_Returns200()
    {
        var userId = await SignInAsAdminAsync();
        var id = await AddItemAsync(userId, ActionStatus.InProgress, ProjectStatus.Active);
        var start = Today.AddDays(-5);

        var response = await _client.PutAsJsonAsync($"/api/action-items/{id}", new { id, actualStartDate = Day(start) });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetDataAsync(id)).GetProperty("actualStartDate").GetString().Should().StartWith(Day(start));
    }

    [Fact]
    public async Task Put_AnActualStartAfterToday_Returns400WithTheReason()
    {
        var userId = await SignInAsAdminAsync();
        var id = await AddItemAsync(userId, ActionStatus.InProgress, ProjectStatus.Draft);

        var response = await _client.PutAsJsonAsync($"/api/action-items/{id}",
            new { id, actualStartDate = Day(Today.AddDays(1)) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageAsync(response)).Should().Be(
            $"Actual start ({Day(Today.AddDays(1))}) cannot be after today ({Day(Today)}).");
    }

    [Fact]
    public async Task Put_OnADoneItem_AcceptsOnlyActualDates_AndReturnsTheVariance()
    {
        var userId = await SignInAsAdminAsync();
        var id = await AddItemAsync(userId, ActionStatus.Done, actualEnd: Today);

        var ok = await _client.PutAsJsonAsync($"/api/action-items/{id}",
            new { id, actualStartDate = Day(Today.AddDays(-6)), actualEndDate = Day(Today.AddDays(-1)) });
        ok.StatusCode.Should().Be(HttpStatusCode.OK);

        var data = await GetDataAsync(id);
        data.GetProperty("actualStartDate").GetString().Should().StartWith(Day(Today.AddDays(-6)));
        data.GetProperty("actualEndDate").GetString().Should().StartWith(Day(Today.AddDays(-1)));
        data.GetProperty("actualEndVarianceDays").GetInt32().Should().Be(1);   // planned end was two days ago

        var refused = await _client.PutAsJsonAsync($"/api/action-items/{id}",
            new { id, title = "Renamed", actualEndDate = Day(Today) });
        refused.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
