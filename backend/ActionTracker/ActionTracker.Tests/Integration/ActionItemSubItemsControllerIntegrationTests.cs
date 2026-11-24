using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.ActionItems.DTOs;
using ActionTracker.Application.Features.ActionItemSubItems.DTOs;
using ActionTracker.Application.Features.Auth.DTOs;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Constants;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using ActionTracker.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ActionTracker.Tests.Integration;

/// <summary>
/// WO-2026-11 A-2 through the real pipeline (InMemory database, default role
/// permissions and the "KU Standard" Mon–Fri calendar seeded at startup):
/// policies (401 / 403), the CRUD flow, rule failures as 400 with the reason,
/// 409 on a Done parent, the assignee-may-tick exception, 404 for a private
/// workspace, the My Actions endpoint, and Swagger.
/// Seeded grants used: Team Member = ActionItems View/Create/Edit,
/// Workspace Admin = ActionItems View only, Viewer = no ActionItems access.
/// </summary>
public class ActionItemSubItemsControllerIntegrationTests
    : IClassFixture<ActionTrackerWebApplicationFactory>
{
    private const string Password = "SubItems-Test-2026!";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly ActionTrackerWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ActionItemSubItemsControllerIntegrationTests(ActionTrackerWebApplicationFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private sealed record Session(string UserId, string Token);

    private async Task<Session> SignInAsync(string name, string role)
    {
        var email = $"a2.{name}@integration.test";
        await _factory.EnsureUserAsync(email, Password, role);
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        var body = JsonSerializer.Deserialize<ApiResponse<AuthResponseDto>>(await response.Content.ReadAsStringAsync(), JsonOptions)!;

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return new Session((await users.FindByEmailAsync(email))!.Id, body.Data!.AccessToken);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, Session? as_, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (as_ is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", as_.Token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<ApiResponse<T>>(await response.Content.ReadAsStringAsync(), JsonOptions)!.Data!;

    private static async Task<string?> MessageAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<ApiResponse<object>>(await response.Content.ReadAsStringAsync(), JsonOptions)?.Message;

    /// <summary>Workspace (admins / members as given) + one parent item, written in system context.</summary>
    private async Task<Guid> AddParentAsync(
        string[] admins, string[] members, bool isPrivate = false, ActionStatus status = ActionStatus.ToDo)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var workspace = new Workspace
        {
            Id = Guid.NewGuid(), Title = "A-2 workspace " + Guid.NewGuid().ToString("N")[..6],
            OrganizationUnit = "Integration", IsPrivate = isPrivate, CreatedAt = DateTime.UtcNow,
        };
        foreach (var a in admins)
            workspace.Admins.Add(new WorkspaceAdmin { WorkspaceId = workspace.Id, AdminUserId = a, AdminUserName = a });
        foreach (var m in members)
            workspace.Members.Add(new WorkspaceMember { WorkspaceId = workspace.Id, UserId = m, UserName = m });
        db.Workspaces.Add(workspace);

        var item = new ActionItem
        {
            Id = Guid.NewGuid(), ActionId = "ACT-A2-" + Guid.NewGuid().ToString("N")[..4], Title = "A-2 parent",
            Description = "", WorkspaceId = workspace.Id, IsStandalone = true, Status = status,
            Priority = ActionPriority.Medium,
            StartDate = new DateTime(2030, 6, 3), DueDate = new DateTime(2030, 6, 28),
            CreatedByUserId = admins.FirstOrDefault(), CreatedAt = DateTime.UtcNow,
        };
        item.Assignees.Add(new ActionItemAssignee { ActionItemId = item.Id, UserId = admins[0] });
        db.ActionItems.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    private static object SubItemBody(string title, string end, string? start = null, params string[] assignees) =>
        new { title, description = "", startDate = start, endDate = end, assigneeIds = assignees };

    private static string Url(Guid parentId, string suffix = "") => $"/api/action-items/{parentId}/sub-items{suffix}";

    // ── Policies ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Anonymous_gets_401()
    {
        var response = await SendAsync(HttpMethod.Get, Url(Guid.NewGuid()), null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Viewer_without_ActionItems_permissions_gets_403()
    {
        var editor = await SignInAsync("editor-403", AppRoles.TeamMember);
        var viewer = await SignInAsync("viewer", "Viewer");
        var parent = await AddParentAsync(new[] { editor.UserId }, new[] { viewer.UserId });

        (await SendAsync(HttpMethod.Get, Url(parent), viewer)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Post, Url(parent), viewer, SubItemBody("x", "2030-06-07")))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Get, "/api/action-items/assigned-via-sub-items", viewer))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── CRUD flow ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Editor_can_add_edit_reorder_tick_and_delete()
    {
        var editor = await SignInAsync("editor-flow", AppRoles.TeamMember);
        var member = await SignInAsync("member-flow", AppRoles.WorkspaceAdmin);
        var parent = await AddParentAsync(new[] { editor.UserId }, new[] { member.UserId });

        var postA = await SendAsync(HttpMethod.Post, Url(parent), editor, SubItemBody("Draft", "2030-06-07", "2030-06-03", member.UserId));
        postA.StatusCode.Should().Be(HttpStatusCode.Created);
        var a = await ReadAsync<ActionItemSubItemDto>(postA);
        var postB = await SendAsync(HttpMethod.Post, Url(parent), editor, SubItemBody("Review", "2030-06-14"));
        var b = await ReadAsync<ActionItemSubItemDto>(postB);
        a.SortOrder.Should().Be(1);
        b.SortOrder.Should().Be(2);
        a.Assignees.Should().ContainSingle(x => x.UserId == member.UserId);

        var put = await SendAsync(HttpMethod.Put, Url(parent, $"/{a.Id}"), editor,
            SubItemBody("Draft v2", "2030-06-10", "2030-06-04", member.UserId, editor.UserId));
        put.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<ActionItemSubItemDto>(put)).Should().Match<ActionItemSubItemDto>(s =>
            s.Title == "Draft v2" && s.EndDate == new DateTime(2030, 6, 10) && s.Assignees.Count == 2);

        var reorder = await SendAsync(HttpMethod.Put, Url(parent, "/reorder"), editor, new { subItemIds = new[] { b.Id, a.Id } });
        reorder.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<ActionItemSubItemListDto>(reorder)).Items.Select(i => i.Id).Should().Equal(b.Id, a.Id);

        var done = await SendAsync(HttpMethod.Patch, Url(parent, $"/{b.Id}/done"), editor, new { isDone = true });
        done.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<ActionItemSubItemDto>(done)).Should().Match<ActionItemSubItemDto>(s =>
            s.IsDone && s.DoneByUserId == editor.UserId && s.DoneAt != null);

        (await SendAsync(HttpMethod.Delete, Url(parent, $"/{a.Id}"), editor)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await ReadAsync<ActionItemSubItemListDto>(await SendAsync(HttpMethod.Get, Url(parent), editor));
        list.Items.Select(i => i.Id).Should().Equal(b.Id);
        list.TotalCount.Should().Be(1);
        list.DoneCount.Should().Be(1);

        var item = await ReadAsync<ActionItemResponseDto>(await SendAsync(HttpMethod.Get, $"/api/action-items/{parent}", editor));
        item.SubItemCount.Should().Be(1);
        item.SubItemDoneCount.Should().Be(1);
    }

    [Fact]
    public async Task Rule_failures_come_back_as_400_with_the_reason()
    {
        var editor = await SignInAsync("editor-400", AppRoles.TeamMember);
        var outsider = await SignInAsync("outsider-400", AppRoles.TeamMember);
        var parent = await AddParentAsync(new[] { editor.UserId }, Array.Empty<string>());

        var late = await SendAsync(HttpMethod.Post, Url(parent), editor, SubItemBody("Late", "2030-07-01"));
        late.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageAsync(late)).Should().Be("Sub-item end 2030-07-01 is after the action item's planned end 2030-06-28.");

        var saturday = await SendAsync(HttpMethod.Post, Url(parent), editor, SubItemBody("Weekend", "2030-06-08"));
        saturday.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageAsync(saturday)).Should().StartWith("Sub-item end 2030-06-08 (Saturday) is not a working day in the workspace calendar \"KU Standard\".");

        var stranger = await SendAsync(HttpMethod.Post, Url(parent), editor, SubItemBody("Stranger", "2030-06-07", null, outsider.UserId));
        stranger.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageAsync(stranger)).Should().Contain("Not allowed: Integration Team Member.");

        var noTitle = await SendAsync(HttpMethod.Post, Url(parent), editor, SubItemBody("  ", "2030-06-07"));
        noTitle.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var list = await ReadAsync<ActionItemSubItemListDto>(await SendAsync(HttpMethod.Get, Url(parent), editor));
        list.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Done_parent_refuses_writes_with_409()
    {
        var editor = await SignInAsync("editor-409", AppRoles.TeamMember);
        var parent = await AddParentAsync(new[] { editor.UserId }, Array.Empty<string>(), status: ActionStatus.Done);

        var post = await SendAsync(HttpMethod.Post, Url(parent), editor, SubItemBody("Too late", "2030-06-07"));
        post.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageAsync(post)).Should().Be("Sub-items cannot be added or changed while the action item is Done.");

        var list = await ReadAsync<ActionItemSubItemListDto>(await SendAsync(HttpMethod.Get, Url(parent), editor));
        list.ParentAcceptsChanges.Should().BeFalse();
    }

    // ── The assignee exception ────────────────────────────────────────────────

    [Fact]
    public async Task A_user_without_Edit_may_tick_only_a_sub_item_they_are_assigned_to()
    {
        var editor = await SignInAsync("editor-tick", AppRoles.TeamMember);
        var viewOnly = await SignInAsync("viewonly-tick", AppRoles.WorkspaceAdmin);   // ActionItems.View only
        var parent = await AddParentAsync(new[] { editor.UserId }, new[] { viewOnly.UserId });

        var mine = await ReadAsync<ActionItemSubItemDto>(await SendAsync(HttpMethod.Post, Url(parent), editor,
            SubItemBody("Mine", "2030-06-07", null, viewOnly.UserId)));
        var notMine = await ReadAsync<ActionItemSubItemDto>(await SendAsync(HttpMethod.Post, Url(parent), editor,
            SubItemBody("Not mine", "2030-06-07")));

        (await SendAsync(HttpMethod.Get, Url(parent), viewOnly)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Post, Url(parent), viewOnly, SubItemBody("x", "2030-06-07"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Put, Url(parent, $"/{mine.Id}"), viewOnly, SubItemBody("x", "2030-06-07"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Delete, Url(parent, $"/{mine.Id}"), viewOnly)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Put, Url(parent, "/reorder"), viewOnly, new { subItemIds = new[] { notMine.Id, mine.Id } }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await SendAsync(HttpMethod.Patch, Url(parent, $"/{notMine.Id}/done"), viewOnly, new { isDone = true }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var tick = await SendAsync(HttpMethod.Patch, Url(parent, $"/{mine.Id}/done"), viewOnly, new { isDone = true });
        tick.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<ActionItemSubItemDto>(tick)).IsDone.Should().BeTrue();
    }

    // ── Private workspace ─────────────────────────────────────────────────────

    [Fact]
    public async Task A_private_workspace_parent_is_404_for_a_non_member()
    {
        var owner = await SignInAsync("owner-private", AppRoles.TeamMember);
        var stranger = await SignInAsync("stranger-private", AppRoles.TeamMember);
        var parent = await AddParentAsync(new[] { owner.UserId }, Array.Empty<string>(), isPrivate: true);

        var created = await SendAsync(HttpMethod.Post, Url(parent), owner, SubItemBody("Secret", "2030-06-07", null, owner.UserId));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var sub = await ReadAsync<ActionItemSubItemDto>(created);

        (await SendAsync(HttpMethod.Get, Url(parent), stranger)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Post, Url(parent), stranger, SubItemBody("x", "2030-06-07"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Put, Url(parent, $"/{sub.Id}"), stranger, SubItemBody("x", "2030-06-07"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Patch, Url(parent, $"/{sub.Id}/done"), stranger, new { isDone = true })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Delete, Url(parent, $"/{sub.Id}"), stranger)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await SendAsync(HttpMethod.Get, Url(parent), owner)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── My Actions ────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_user_assigned_only_to_a_sub_item_sees_the_parent_in_its_own_list_not_in_my_actions()
    {
        var editor = await SignInAsync("editor-mine", AppRoles.TeamMember);
        var helper = await SignInAsync("helper-mine", AppRoles.WorkspaceAdmin);
        var parent = await AddParentAsync(new[] { editor.UserId }, new[] { helper.UserId });
        await SendAsync(HttpMethod.Post, Url(parent), editor, SubItemBody("Help", "2030-06-07", null, helper.UserId));

        var via = await ReadAsync<PagedResult<ActionItemResponseDto>>(
            await SendAsync(HttpMethod.Get, "/api/action-items/assigned-via-sub-items", helper));
        via.Items.Should().ContainSingle(i => i.Id == parent && i.MySubItemCount == 1 && i.SubItemCount == 1);

        var myActions = await ReadAsync<PagedResult<ActionItemResponseDto>>(
            await SendAsync(HttpMethod.Get, "/api/action-items/my-actions", helper));
        myActions.Items.Should().NotContain(i => i.Id == parent);

        var stats = await ReadAsync<ActionItemMyStatsDto>(await SendAsync(HttpMethod.Get, "/api/action-items/my-stats", helper));
        stats.TotalCount.Should().Be(0);

        // The editor is a parent assignee: not in their "via sub-items" list.
        var editorVia = await ReadAsync<PagedResult<ActionItemResponseDto>>(
            await SendAsync(HttpMethod.Get, "/api/action-items/assigned-via-sub-items", editor));
        editorVia.Items.Should().NotContain(i => i.Id == parent);
    }

    // ── Swagger ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Swagger_lists_the_sub_item_endpoints()
    {
        var json = await _client.GetStringAsync("/swagger/v1/swagger.json");
        using var doc = JsonDocument.Parse(json);
        var paths = doc.RootElement.GetProperty("paths").EnumerateObject()
            .ToDictionary(p => p.Name.ToLowerInvariant(), p => p.Value);

        bool Has(string path, string verb) => paths.TryGetValue(path, out var p) && p.TryGetProperty(verb, out _);

        Has("/api/action-items/{actionitemid}/sub-items", "get").Should().BeTrue();
        Has("/api/action-items/{actionitemid}/sub-items", "post").Should().BeTrue();
        Has("/api/action-items/{actionitemid}/sub-items/{subitemid}", "put").Should().BeTrue();
        Has("/api/action-items/{actionitemid}/sub-items/{subitemid}", "delete").Should().BeTrue();
        Has("/api/action-items/{actionitemid}/sub-items/{subitemid}/done", "patch").Should().BeTrue();
        Has("/api/action-items/{actionitemid}/sub-items/reorder", "put").Should().BeTrue();
        Has("/api/action-items/assigned-via-sub-items", "get").Should().BeTrue();
    }
}
