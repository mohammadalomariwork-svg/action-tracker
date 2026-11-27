using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.Auth.DTOs;
using ActionTracker.Domain.Constants;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using ActionTracker.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ActionTracker.Tests.Integration;

/// <summary>
/// WO-2026-11 A-1b through the real pipeline (isolated InMemory database,
/// default role permissions seeded at startup):
/// <list type="bullet">
/// <item>POST /api/action-items needs ActionItems.Create; PUT /{id} and PATCH /{id}/status need ActionItems.Edit.</item>
/// <item>The body the edit screens sent before A-1b (<c>isStandalone: true</c>, no <c>projectId</c>) keeps a project item linked.</item>
/// </list>
/// </summary>
public class ActionItemWritePermissionIntegrationTests
    : IClassFixture<ActionTrackerWebApplicationFactory>
{
    private const string Password = "WritePermission-Test-2026!";
    private const string Monday   = "2030-06-03";   // a working day of the seeded KU Standard calendar

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly ActionTrackerWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ActionItemWritePermissionIntegrationTests(ActionTrackerWebApplicationFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string EmailFor(string role) =>
        "a1b." + role.Replace(" ", "-").ToLowerInvariant() + "@integration.test";

    private async Task<string> SignInAsync(string role)
    {
        var email = EmailFor(role);
        await _factory.EnsureUserAsync(email, Password, role);
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        var body = JsonSerializer.Deserialize<ApiResponse<AuthResponseDto>>(await response.Content.ReadAsStringAsync(), JsonOptions)!;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.Data!.AccessToken);

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return (await users.FindByEmailAsync(email))!.Id;
    }

    /// <summary>Adds a workspace, optionally a project, and one To Do item directly (system context).</summary>
    private async Task<(Guid WorkspaceId, Guid? ProjectId, Guid ItemId)> AddItemAsync(string userId, ProjectStatus? projectStatus)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var workspace = new Workspace
        {
            Id = Guid.NewGuid(), Title = "A-1b workspace " + Guid.NewGuid().ToString("N")[..6],
            OrganizationUnit = "Integration", CreatedAt = DateTime.UtcNow,
        };
        // WO-2026-11 P-2b (B2): a standalone item is added by a member of its workspace.
        workspace.Members.Add(new WorkspaceMember { WorkspaceId = workspace.Id, UserId = userId, UserName = userId });
        db.Workspaces.Add(workspace);

        Guid? projectId = null;
        if (projectStatus is not null)
        {
            var project = new Project
            {
                Id = Guid.NewGuid(), ProjectCode = "PRJ-A1B-" + Guid.NewGuid().ToString("N")[..4], Name = "A-1b project",
                WorkspaceId = workspace.Id, ProjectType = ProjectType.Operational, Status = projectStatus.Value,
                Priority = ActionPriority.Medium, ProjectManagerUserId = userId,
                PlannedStartDate = new DateTime(2030, 5, 1), PlannedEndDate = new DateTime(2030, 12, 31),
                Currency = "AED", CreatedBy = userId, CreatedAt = DateTime.UtcNow,
            };
            db.Projects.Add(project);
            projectId = project.Id;
        }

        var item = new ActionItem
        {
            Id = Guid.NewGuid(), ActionId = "ACT-A1B-" + Guid.NewGuid().ToString("N")[..4], Title = "A-1b item",
            Description = "", WorkspaceId = workspace.Id, ProjectId = projectId, IsStandalone = projectId is null,
            Status = ActionStatus.ToDo, Priority = ActionPriority.Medium, DueDate = new DateTime(2030, 6, 3),
            CreatedByUserId = userId, CreatedAt = DateTime.UtcNow,
        };
        item.Assignees.Add(new ActionItemAssignee { ActionItemId = item.Id, UserId = userId });
        db.ActionItems.Add(item);
        await db.SaveChangesAsync();
        return (workspace.Id, projectId, item.Id);
    }

    private async Task<ActionItem> LoadAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ActionItems.IgnoreQueryFilters().AsNoTracking().FirstAsync(a => a.Id == id);
    }

    private static object CreateBody(Guid workspaceId, string assigneeId) => new
    {
        title       = "A-1b created",
        workspaceId,
        assigneeIds = new[] { assigneeId },
        priority    = "medium",
        status      = "toDo",
        dueDate     = Monday,
    };

    // ── ActionItems.Edit on PUT and PATCH /status ─────────────────────────────

    [Theory]
    [InlineData(AppRoles.Viewer)]
    [InlineData(AppRoles.WorkspaceAdmin)]   // ActionItems.View only by default
    public async Task PutAndPatchStatus_WithoutActionItemsEdit_Return403_AndChangeNothing(string role)
    {
        var userId = await SignInAsync(role);
        var (_, _, itemId) = await AddItemAsync(userId, projectStatus: null);

        (await _client.PutAsJsonAsync($"/api/action-items/{itemId}", new { id = itemId, title = "Changed" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.PatchAsJsonAsync($"/api/action-items/{itemId}/status", new { status = "inProgress" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var item = await LoadAsync(itemId);
        item.Title.Should().Be("A-1b item");
        item.Status.Should().Be(ActionStatus.ToDo);
    }

    [Fact]
    public async Task PutAndPatchStatus_AsTeamMember_WithActionItemsEdit_Return200()
    {
        var userId = await SignInAsync(AppRoles.TeamMember);
        var (_, _, itemId) = await AddItemAsync(userId, projectStatus: ProjectStatus.Draft);

        (await _client.PutAsJsonAsync($"/api/action-items/{itemId}", new { id = itemId, title = "Changed" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.PatchAsJsonAsync($"/api/action-items/{itemId}/status", new { status = "inProgress" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var item = await LoadAsync(itemId);
        item.Title.Should().Be("Changed");
        item.Status.Should().Be(ActionStatus.InProgress);
    }

    // ── ActionItems.Create on POST ────────────────────────────────────────────

    [Theory]
    [InlineData(AppRoles.Viewer)]
    [InlineData(AppRoles.WorkspaceAdmin)]
    public async Task Post_WithoutActionItemsCreate_Returns403(string role)
    {
        var userId = await SignInAsync(role);
        var (workspaceId, _, _) = await AddItemAsync(userId, projectStatus: null);

        (await _client.PostAsJsonAsync("/api/action-items", CreateBody(workspaceId, userId)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(AppRoles.ProjectCoordinator)]
    [InlineData(AppRoles.TeamMember)]   // granted by the seeder since A-1b, as in production
    public async Task Post_WithActionItemsCreate_Returns201(string role)
    {
        var userId = await SignInAsync(role);
        var (workspaceId, _, _) = await AddItemAsync(userId, projectStatus: null);

        (await _client.PostAsJsonAsync("/api/action-items", CreateBody(workspaceId, userId)))
            .StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // ── isStandalone on edit ──────────────────────────────────────────────────

    [Theory]
    [InlineData(ProjectStatus.Draft)]
    [InlineData(ProjectStatus.Active)]
    public async Task Put_WithTheBodyTheEditScreensSent_KeepsAProjectItemLinked(ProjectStatus projectStatus)
    {
        var userId = await SignInAsync(AppRoles.Admin);
        var (workspaceId, projectId, itemId) = await AddItemAsync(userId, projectStatus);

        // As My Actions / workspace detail / the action form sent it before
        // A-1b: numeric enums, day strings, isStandalone true, no projectId.
        var json = $$"""
            {
              "title": "A-1b item (edited)", "description": "", "workspaceId": "{{workspaceId}}",
              "isStandalone": true, "assigneeIds": ["{{userId}}"], "priority": 2, "status": 1,
              "startDate": null, "dueDate": "{{Monday}}", "progress": 0, "isEscalated": false
            }
            """;
        var response = await _client.PutAsync($"/api/action-items/{itemId}",
            new StringContent(json, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = await LoadAsync(itemId);
        item.Title.Should().Be("A-1b item (edited)");
        item.ProjectId.Should().Be(projectId);
        item.IsStandalone.Should().BeFalse();
    }
}
