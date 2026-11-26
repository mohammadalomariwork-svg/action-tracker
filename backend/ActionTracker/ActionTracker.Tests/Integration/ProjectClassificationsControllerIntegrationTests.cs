using System.Net;
using System.Text.Json;
using ActionTracker.Application.Features.ProjectClassifications.DTOs;
using ActionTracker.Application.Features.Projects.DTOs;
using ActionTracker.Domain.Constants;
using ActionTracker.Domain.Entities;
using ActionTracker.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ActionTracker.Tests.Integration;

/// <summary>
/// WO-2026-11 P-1 through the real pipeline: the workspace's classification
/// list (WorkspaceProjectClassificationsController) — 401 / 403 / 404, the
/// Projects.View-or-Workspaces.View read rule, the Project Managers list,
/// add / rename / deactivate / reorder / delete with the 400 and 409 reasons —
/// and the classification on POST / PUT / GET <c>/api/projects</c>. Seeded
/// grants: Viewer = neither permission, Team Member = Projects.View,
/// Workspace Admin = Workspaces.View / Edit.
/// </summary>
public class ProjectClassificationsControllerIntegrationTests
    : TagsIntegrationTestBase, IClassFixture<ActionTrackerWebApplicationFactory>
{
    private static readonly DateTime Monday = new(2030, 1, 7);

    public ProjectClassificationsControllerIntegrationTests(ActionTrackerWebApplicationFactory factory) : base(factory) { }

    private static string Url(Guid workspaceId, string rest = "") =>
        $"/api/workspaces/{workspaceId}/project-classifications{rest}";

    private async Task AddProjectManagerAsync(Guid workspaceId, string userId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.WorkspaceProjectManagers.Add(new WorkspaceProjectManager { WorkspaceId = workspaceId, UserId = userId, UserName = userId });
        await db.SaveChangesAsync();
    }

    private async Task<HttpResponseMessage> CreateProjectAsync(Session as_, Guid workspaceId, Guid? classificationId)
    {
        // WO-2026-11 P-2: POST /api/projects needs Projects.Create, which the seeded
        // Workspace Admin role does not hold (nor does it in production; the UI's
        // New Project button needs it too). Being put on a workspace's PM list gives
        // the Project Manager role in the app, so the test does the same.
        using (var scope = Factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
            var user = await users.FindByIdAsync(as_.UserId);
            if (user != null && !await users.IsInRoleAsync(user, AppRoles.ProjectManager))
                await users.AddToRoleAsync(user, AppRoles.ProjectManager);
        }
        return await SendAsync(HttpMethod.Post, "/api/projects", as_, new
        {
            name = "P-1 project " + Guid.NewGuid().ToString("N")[..6], workspaceId, projectType = "operational",
            priority = "medium", projectManagerUserId = as_.UserId, sponsorUserIds = new[] { as_.UserId },
            plannedStartDate = Monday, plannedEndDate = Friday, internalClassificationId = classificationId,
        });
    }

    [Fact]
    public async Task Every_route_needs_a_signed_in_user()
    {
        var ws = Guid.NewGuid();
        (await SendAsync(HttpMethod.Get, Url(ws), null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await SendAsync(HttpMethod.Post, Url(ws), null, new { name = "X" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await SendAsync(HttpMethod.Put, Url(ws, "/reorder"), null, new { classificationIds = Array.Empty<Guid>() })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await SendAsync(HttpMethod.Put, Url(ws, $"/{Guid.NewGuid()}"), null, new { name = "X", isActive = true })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await SendAsync(HttpMethod.Delete, Url(ws, $"/{Guid.NewGuid()}"), null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Reading_needs_Projects_View_or_Workspaces_View_and_writing_needs_Workspaces_Edit()
    {
        var viewer = await SignInAsync("p1.viewer", AppRoles.Viewer);
        var member = await SignInAsync("p1.member", AppRoles.TeamMember);
        var ws = await AddWorkspaceAsync("P-1 rights", [viewer.UserId], [member.UserId]);

        var denied = await SendAsync(HttpMethod.Get, Url(ws), viewer);
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await MessageAsync(denied)).Should().Be("You need the Projects.View or Workspaces.View permission to see project classifications.");
        (await SendAsync(HttpMethod.Post, Url(ws), viewer, new { name = "X" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Team Member: Projects.View only — reads, cannot write.
        (await SendAsync(HttpMethod.Get, Url(ws), member)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Post, Url(ws), member, new { name = "X" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Delete, Url(ws, $"/{Guid.NewGuid()}"), member)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_workspace_manager_maintains_the_list()
    {
        var admin = await SignInAsync("p1.wsadmin", AppRoles.WorkspaceAdmin);
        var ws = await AddWorkspaceAsync("P-1 flow", [admin.UserId], []);

        var created = await SendAsync(HttpMethod.Post, Url(ws), admin, new { name = "  Capital   works " });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var capital = await ReadAsync<ProjectClassificationDto>(created);
        capital.Name.Should().Be("Capital works");
        var research = await ReadAsync<ProjectClassificationDto>(await SendAsync(HttpMethod.Post, Url(ws), admin, new { name = "Research" }));

        // Same name in another case: 409 with the reason.
        var duplicate = await SendAsync(HttpMethod.Post, Url(ws), admin, new { name = "CAPITAL WORKS" });
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageAsync(duplicate)).Should().Contain("already has a classification named");

        // Empty name: FluentValidation 400.
        (await SendAsync(HttpMethod.Post, Url(ws), admin, new { name = "   " })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Rename + deactivate, reorder.
        var renamed = await SendAsync(HttpMethod.Put, Url(ws, $"/{research.Id}"), admin, new { name = "Research grants", isActive = false });
        renamed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<ProjectClassificationDto>(renamed)).Should().Match<ProjectClassificationDto>(c => c.Name == "Research grants" && !c.IsActive);
        var reordered = await SendAsync(HttpMethod.Put, Url(ws, "/reorder"), admin, new { classificationIds = new[] { research.Id, capital.Id } });
        reordered.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<List<ProjectClassificationDto>>(reordered)).Select(c => c.Name).Should().Equal("Research grants", "Capital works");
        var badOrder = await SendAsync(HttpMethod.Put, Url(ws, "/reorder"), admin, new { classificationIds = new[] { capital.Id } });
        badOrder.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageAsync(badOrder)).Should().Contain("exactly once");

        // A project uses "Capital works": delete refused with the reason; the unused one goes.
        await AddProjectManagerAsync(ws, admin.UserId);
        (await CreateProjectAsync(admin, ws, capital.Id)).StatusCode.Should().Be(HttpStatusCode.Created);
        var blocked = await SendAsync(HttpMethod.Delete, Url(ws, $"/{capital.Id}"), admin);
        blocked.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageAsync(blocked)).Should().StartWith("\"Capital works\" is used by 1 project and cannot be deleted.");
        (await SendAsync(HttpMethod.Delete, Url(ws, $"/{research.Id}"), admin)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await ReadAsync<List<ProjectClassificationDto>>(await SendAsync(HttpMethod.Get, Url(ws), admin));
        list.Should().ContainSingle().Which.Should().Match<ProjectClassificationDto>(c =>
            c.Name == "Capital works" && c.SortOrder == 1 && c.ProjectCount == 1 && !c.CanDelete);
    }

    [Fact]
    public async Task Hidden_workspaces_are_not_found()
    {
        var admin = await SignInAsync("p1.wsadmin.hidden", AppRoles.WorkspaceAdmin);
        var other = await SignInAsync("p1.other", AppRoles.WorkspaceAdmin);
        var privateWs = await AddWorkspaceAsync("P-1 private", [other.UserId], [], isPrivate: true);
        var notMine   = await AddWorkspaceAsync("P-1 not mine", [other.UserId], []);

        foreach (var ws in new[] { privateWs, notMine, Guid.NewGuid() })
        {
            (await SendAsync(HttpMethod.Get, Url(ws), admin)).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await SendAsync(HttpMethod.Post, Url(ws), admin, new { name = "X" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }

    [Fact]
    public async Task A_project_manager_on_the_list_reads_it_without_being_a_member()
    {
        var admin = await SignInAsync("p1.wsadmin.pm", AppRoles.WorkspaceAdmin);
        var pm    = await SignInAsync("p1.pm", AppRoles.ProjectManager);
        var ws = await AddWorkspaceAsync("P-1 pm", [admin.UserId], []);
        await SendAsync(HttpMethod.Post, Url(ws), admin, new { name = "Capital works" });

        (await SendAsync(HttpMethod.Get, Url(ws), pm)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AddProjectManagerAsync(ws, pm.UserId);

        var list = await SendAsync(HttpMethod.Get, Url(ws), pm);
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<List<ProjectClassificationDto>>(list)).Should().ContainSingle(c => c.Name == "Capital works");
    }

    [Fact]
    public async Task A_project_takes_only_a_classification_of_its_own_workspace()
    {
        var admin = await SignInAsync("p1.wsadmin.project", AppRoles.WorkspaceAdmin);
        var wsA = await AddWorkspaceAsync("P-1 alpha", [admin.UserId], []);
        var wsB = await AddWorkspaceAsync("P-1 beta", [admin.UserId], []);
        await AddProjectManagerAsync(wsA, admin.UserId);
        var alpha = await ReadAsync<ProjectClassificationDto>(await SendAsync(HttpMethod.Post, Url(wsA), admin, new { name = "Alpha class" }));
        var beta  = await ReadAsync<ProjectClassificationDto>(await SendAsync(HttpMethod.Post, Url(wsB), admin, new { name = "Beta class" }));

        var wrong = await CreateProjectAsync(admin, wsA, beta.Id);
        wrong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageAsync(wrong)).Should().Be("The chosen internal classification does not belong to this project's workspace.");

        var created = await CreateProjectAsync(admin, wsA, alpha.Id);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var project = await ReadAsync<ProjectResponseDto>(created);
        project.InternalClassificationName.Should().Be("Alpha class");

        // GET by id and the list filter by id.
        (await ReadAsync<ProjectResponseDto>(await SendAsync(HttpMethod.Get, $"/api/projects/{project.Id}", admin)))
            .InternalClassificationId.Should().Be(alpha.Id);
        var listJson = await (await SendAsync(HttpMethod.Get, $"/api/projects?workspaceId={wsA}&internalClassificationId={alpha.Id}", admin)).Content.ReadAsStringAsync();
        listJson.Should().Contain(project.Id.ToString());

        // PUT without the field keeps it; clearInternalClassification removes it.
        var body = new
        {
            name = project.Name, projectType = "operational", status = "draft", priority = "medium",
            projectManagerUserId = admin.UserId, sponsorUserIds = new[] { admin.UserId },
            plannedStartDate = Monday, plannedEndDate = Friday,
        };
        (await ReadAsync<ProjectResponseDto>(await SendAsync(HttpMethod.Put, $"/api/projects/{project.Id}", admin, body)))
            .InternalClassificationName.Should().Be("Alpha class");
        var cleared = await SendAsync(HttpMethod.Put, $"/api/projects/{project.Id}", admin, new
        {
            body.name, body.projectType, body.status, body.priority, body.projectManagerUserId, body.sponsorUserIds,
            body.plannedStartDate, body.plannedEndDate, clearInternalClassification = true,
        });
        cleared.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<ProjectResponseDto>(cleared)).InternalClassificationId.Should().BeNull();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Projects.IgnoreQueryFilters().SingleAsync(p => p.Id == project.Id)).InternalClassificationId.Should().BeNull();
    }

    [Fact]
    public async Task Swagger_lists_the_classification_endpoints()
    {
        var json = await Client.GetStringAsync("/swagger/v1/swagger.json");
        using var doc = JsonDocument.Parse(json);
        var paths = doc.RootElement.GetProperty("paths").EnumerateObject().ToDictionary(p => p.Name.ToLowerInvariant(), p => p.Value);

        const string list = "/api/workspaces/{workspaceid}/project-classifications";
        paths.Should().ContainKey(list);
        paths[list].TryGetProperty("get", out _).Should().BeTrue();
        paths[list].TryGetProperty("post", out _).Should().BeTrue();
        paths.Should().ContainKey(list + "/reorder");
        paths.Should().ContainKey(list + "/{classificationid}");
        paths[list + "/{classificationid}"].TryGetProperty("put", out _).Should().BeTrue();
        paths[list + "/{classificationid}"].TryGetProperty("delete", out _).Should().BeTrue();
        json.Should().ContainEquivalentOf("\"InternalClassificationId\"", "the project list filter is documented");
    }
}
