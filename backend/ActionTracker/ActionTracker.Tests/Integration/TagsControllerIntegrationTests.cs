using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ActionTracker.API.Models;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.ActionItems.DTOs;
using ActionTracker.Application.Features.Auth.DTOs;
using ActionTracker.Application.Features.Tags.DTOs;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Constants;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using ActionTracker.Infrastructure.Data;
using ActionTracker.Tests.Unit.Tags;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ActionTracker.Tests.Integration;

/// <summary>Shared helpers of the A-5 integration tests.</summary>
public abstract class TagsIntegrationTestBase
{
    protected const string Password = "Tags-Test-2026!";

    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    protected static readonly DateTime Friday = new(2030, 6, 28);   // a working day of "KU Standard"

    protected readonly ActionTrackerWebApplicationFactory Factory;
    protected readonly HttpClient Client;

    protected TagsIntegrationTestBase(ActionTrackerWebApplicationFactory factory)
    {
        Factory = factory;
        Client  = factory.CreateClient();
    }

    protected sealed record Session(string UserId, string Token);

    protected async Task<Session> SignInAsync(string name, string role)
    {
        var email = $"a5.{name}@integration.test";
        await Factory.EnsureUserAsync(email, Password, role);
        var response = await Client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        var body = JsonSerializer.Deserialize<ApiResponse<AuthResponseDto>>(await response.Content.ReadAsStringAsync(), JsonOptions)!;

        using var scope = Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return new Session((await users.FindByEmailAsync(email))!.Id, body.Data!.AccessToken);
    }

    protected async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, Session? as_, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
        if (as_ is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", as_.Token);
        return await Client.SendAsync(request);
    }

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<ApiResponse<T>>(await response.Content.ReadAsStringAsync(), JsonOptions)!.Data!;

    protected static async Task<string?> MessageAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<ApiResponse<object>>(await response.Content.ReadAsStringAsync(), JsonOptions)?.Message;

    /// <summary>A workspace with the given admins / members, in system context.</summary>
    protected async Task<Guid> AddWorkspaceAsync(string title, string[] admins, string[] members, bool isPrivate = false)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var workspace = new Workspace
        {
            Id = Guid.NewGuid(), Title = title + " " + Guid.NewGuid().ToString("N")[..6],
            OrganizationUnit = "Integration", IsPrivate = isPrivate, CreatedAt = DateTime.UtcNow,
        };
        foreach (var a in admins)
            workspace.Admins.Add(new WorkspaceAdmin { WorkspaceId = workspace.Id, AdminUserId = a, AdminUserName = a });
        foreach (var m in members)
            workspace.Members.Add(new WorkspaceMember { WorkspaceId = workspace.Id, UserId = m, UserName = m });
        db.Workspaces.Add(workspace);
        await db.SaveChangesAsync();
        return workspace.Id;
    }

    protected Task<HttpResponseMessage> CreateItemAsync(Session as_, Guid workspaceId, string[]? tags, string? assignee = null) =>
        SendAsync(HttpMethod.Post, "/api/action-items", as_, new
        {
            title = "A-5 item", description = "", workspaceId, assigneeIds = new[] { assignee ?? as_.UserId },
            priority = "medium", dueDate = Friday, tags,
        });

    protected async Task SetStatusAsync(Guid itemId, ActionStatus status)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var item = await db.ActionItems.FindAsync(itemId);
        item!.Status = status;
        await db.SaveChangesAsync();
    }
}

/// <summary>
/// WO-2026-11 A-5 through the real pipeline: tags saved with the item (POST /
/// PUT), suggestions of one workspace and My Actions options (TagsController),
/// the any-of filter by id on the lists, ActionItems.View for both reads and
/// the filter (403), hidden workspaces (404), the tag rules (400), a Done item
/// (422), and Swagger. Seeded grants: Team Member = ActionItems View / Create /
/// Edit, Viewer = no ActionItems access.
/// </summary>
public class TagsControllerIntegrationTests : TagsIntegrationTestBase, IClassFixture<ActionTrackerWebApplicationFactory>
{
    public TagsControllerIntegrationTests(ActionTrackerWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Both_reads_need_a_signed_in_user()
    {
        (await SendAsync(HttpMethod.Get, $"/api/workspaces/{Guid.NewGuid()}/tags", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await SendAsync(HttpMethod.Get, "/api/action-items/my-actions/tags", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Suggestions_options_and_the_tag_filter_need_ActionItems_View()
    {
        var viewer = await SignInAsync("viewer", AppRoles.Viewer);
        var ws = await AddWorkspaceAsync("A-5 viewer", [viewer.UserId], []);

        (await SendAsync(HttpMethod.Get, $"/api/workspaces/{ws}/tags", viewer)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Get, "/api/action-items/my-actions/tags", viewer)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var filtered = await SendAsync(HttpMethod.Get, $"/api/action-items/my-actions?tagIds={Guid.NewGuid()}", viewer);
        filtered.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await MessageAsync(filtered)).Should().Be("You need the ActionItems.View permission to filter by tag.");
        (await SendAsync(HttpMethod.Get, $"/api/action-items?tagIds={Guid.NewGuid()}", viewer)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Without tagIds the lists behave as before.
        (await SendAsync(HttpMethod.Get, "/api/action-items/my-actions", viewer)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Get, "/api/action-items", viewer)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Tags_save_suggest_and_filter_within_the_workspace()
    {
        var member = await SignInAsync("member", AppRoles.TeamMember);
        var wsA = await AddWorkspaceAsync("A-5 alpha", [member.UserId], []);
        var wsB = await AddWorkspaceAsync("A-5 beta", [member.UserId], []);

        var created = await CreateItemAsync(member, wsA, [" Urgent ", "Board  pack", "urgent"]);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var itemA = await ReadAsync<ActionItemResponseDto>(created);
        itemA.Tags.Select(t => t.Name).Should().Equal("Board pack", "Urgent");

        var itemB = await ReadAsync<ActionItemResponseDto>(await CreateItemAsync(member, wsB, ["Urgent"]));
        var plain = await ReadAsync<ActionItemResponseDto>(await CreateItemAsync(member, wsA, null));

        // PUT replaces; GET by id shows the same.
        var put = await SendAsync(HttpMethod.Put, $"/api/action-items/{plain.Id}", member, new { id = plain.Id, tags = new[] { "urgent", "Q4" } });
        put.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<ActionItemResponseDto>(put)).Tags.Select(t => t.Name).Should().Equal("Q4", "Urgent");

        // Suggestions: workspace A's tags only, with counts; search ignores case.
        var suggestions = await ReadAsync<List<WorkspaceTagDto>>(await SendAsync(HttpMethod.Get, $"/api/workspaces/{wsA}/tags", member));
        suggestions.Select(t => (t.Name, t.ItemCount)).Should().Equal(("Board pack", 1), ("Q4", 1), ("Urgent", 2));
        (await ReadAsync<List<WorkspaceTagDto>>(await SendAsync(HttpMethod.Get, $"/api/workspaces/{wsA}/tags?search=URG", member)))
            .Select(t => t.Name).Should().Equal("Urgent");

        // Filter by id, any of — workspace A's "Urgent" never matches workspace B's.
        var urgentA = itemA.Tags.Single(t => t.Name == "Urgent").Id;
        var q4 = (await ReadAsync<ActionItemResponseDto>(put)).Tags.Single(t => t.Name == "Q4").Id;
        var listed = await ReadAsync<PagedResult<ActionItemResponseDto>>(
            await SendAsync(HttpMethod.Get, $"/api/action-items/my-actions?tagIds={urgentA}&pageSize=100", member));
        listed.Items.Select(i => i.Id).Should().BeEquivalentTo(new[] { itemA.Id, plain.Id });
        (await ReadAsync<PagedResult<ActionItemResponseDto>>(
            await SendAsync(HttpMethod.Get, $"/api/action-items?workspaceId={wsA}&tagIds={q4}&tagIds={itemB.Tags[0].Id}", member)))
            .Items.Select(i => i.Id).Should().Equal(plain.Id);

        // My Actions options: labelled with their workspace; the same name twice.
        var options = await ReadAsync<List<MyActionTagOptionDto>>(await SendAsync(HttpMethod.Get, "/api/action-items/my-actions/tags", member));
        options.Where(o => o.Name == "Urgent").Select(o => o.WorkspaceId).Should().BeEquivalentTo(new[] { wsA, wsB });
        options.Single(o => o.Id == urgentA).ItemCount.Should().Be(2);
    }

    [Fact]
    public async Task Broken_tag_rules_are_400_and_a_Done_item_is_422()
    {
        var member = await SignInAsync("rules", AppRoles.TeamMember);
        var ws = await AddWorkspaceAsync("A-5 rules", [member.UserId], []);

        (await CreateItemAsync(member, ws, ["a,b"])).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var item = await ReadAsync<ActionItemResponseDto>(await CreateItemAsync(member, ws, ["kept"]));

        var tooMany = await SendAsync(HttpMethod.Put, $"/api/action-items/{item.Id}", member,
            new { id = item.Id, tags = Enumerable.Range(1, 11).Select(i => $"t{i}").ToArray() });
        tooMany.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await tooMany.Content.ReadAsStringAsync()).Should().Contain("An action item can have at most 10 tags.");
        (await SendAsync(HttpMethod.Put, $"/api/action-items/{item.Id}", member, new { id = item.Id, tags = new[] { new string('x', 31) } }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await SetStatusAsync(item.Id, ActionStatus.Done);
        (await SendAsync(HttpMethod.Put, $"/api/action-items/{item.Id}", member, new { id = item.Id, tags = new[] { "new" } }))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        await SetStatusAsync(item.Id, ActionStatus.Cancelled);
        var cancelled = await SendAsync(HttpMethod.Put, $"/api/action-items/{item.Id}", member, new { id = item.Id, tags = new[] { "new" } });
        cancelled.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<ActionItemResponseDto>(cancelled)).Tags.Select(t => t.Name).Should().Equal("new");
    }

    [Fact]
    public async Task Hidden_workspaces_give_404_and_private_tag_names_never_leak()
    {
        var member   = await SignInAsync("private-member", AppRoles.TeamMember);
        var outsider = await SignInAsync("outsider", AppRoles.TeamMember);
        var admin    = await SignInAsync("admin", AppRoles.Admin);
        var priv   = await AddWorkspaceAsync("A-5 private", [member.UserId], [], isPrivate: true);
        var notMine = await AddWorkspaceAsync("A-5 not mine", [member.UserId], []);

        var secret = await ReadAsync<ActionItemResponseDto>(await CreateItemAsync(member, priv, ["Secret plan"]));
        await CreateItemAsync(member, notMine, ["Other team"]);

        (await SendAsync(HttpMethod.Get, $"/api/workspaces/{priv}/tags", member)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Get, $"/api/workspaces/{priv}/tags", outsider)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Get, $"/api/workspaces/{priv}/tags", admin)).StatusCode.Should().Be(HttpStatusCode.NotFound,
            "a private workspace is hidden from Admin too");
        (await SendAsync(HttpMethod.Get, $"/api/workspaces/{notMine}/tags", outsider)).StatusCode.Should().Be(HttpStatusCode.NotFound,
            "a regular user only sees the workspaces they are in");
        (await SendAsync(HttpMethod.Get, $"/api/workspaces/{notMine}/tags", admin)).StatusCode.Should().Be(HttpStatusCode.OK);

        var filtered = await ReadAsync<PagedResult<ActionItemResponseDto>>(
            await SendAsync(HttpMethod.Get, $"/api/action-items?tagIds={secret.Tags[0].Id}", admin));
        filtered.TotalCount.Should().Be(0);
        var adminOptions = await SendAsync(HttpMethod.Get, "/api/action-items/my-actions/tags", admin);
        (await adminOptions.Content.ReadAsStringAsync()).Should().NotContain("Secret plan");
    }

    [Fact]
    public async Task Swagger_lists_the_tag_endpoints()
    {
        var json = await Client.GetStringAsync("/swagger/v1/swagger.json");
        using var doc = JsonDocument.Parse(json);
        var paths = doc.RootElement.GetProperty("paths").EnumerateObject().ToDictionary(p => p.Name.ToLowerInvariant(), p => p.Value);

        paths.Should().ContainKey("/api/workspaces/{workspaceid}/tags");
        paths["/api/workspaces/{workspaceid}/tags"].TryGetProperty("get", out _).Should().BeTrue();
        paths.Should().ContainKey("/api/action-items/my-actions/tags");
        json.Should().ContainEquivalentOf("\"TagIds\"", "the list filter is documented (query binding ignores case)");
    }
}

/// <summary>A test host whose database has no tag tables yet (migration pending).</summary>
public sealed class TagsMissingWebApplicationFactory : ActionTrackerWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITagSchemaState>();
            services.AddSingleton<ITagSchemaState>(new FixedTagSchemaState(false));
        });
    }
}

/// <summary>
/// D7 through the real pipeline: while the tag tables are missing, action items
/// are read and saved as before (no tags), saves with tags and the tag filter
/// get 503 with the reason, suggestions and options are empty.
/// </summary>
public class TagsMigrationPendingIntegrationTests : TagsIntegrationTestBase, IClassFixture<TagsMissingWebApplicationFactory>
{
    public TagsMigrationPendingIntegrationTests(TagsMissingWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Items_work_without_tags_and_tag_calls_get_503()
    {
        var member = await SignInAsync("pending", AppRoles.TeamMember);
        var ws = await AddWorkspaceAsync("A-5 pending", [member.UserId], []);

        var created = await CreateItemAsync(member, ws, []);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var item = await ReadAsync<ActionItemResponseDto>(created);
        item.Tags.Should().BeEmpty();

        (await SendAsync(HttpMethod.Get, $"/api/action-items/{item.Id}", member)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Get, "/api/action-items/my-actions", member)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Put, $"/api/action-items/{item.Id}", member, new { id = item.Id, title = "Renamed", tags = Array.Empty<string>() }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        const string reason = "Tags are not available until the database update for tags (Add_ActionItemTags) has been applied.";
        var post = await CreateItemAsync(member, ws, ["x"]);
        post.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await MessageAsync(post)).Should().Be(reason);
        var put = await SendAsync(HttpMethod.Put, $"/api/action-items/{item.Id}", member, new { id = item.Id, tags = new[] { "x" } });
        put.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await MessageAsync(put)).Should().Be(reason);
        (await SendAsync(HttpMethod.Get, $"/api/action-items/my-actions?tagIds={Guid.NewGuid()}", member))
            .StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        (await ReadAsync<List<WorkspaceTagDto>>(await SendAsync(HttpMethod.Get, $"/api/workspaces/{ws}/tags", member))).Should().BeEmpty();
        (await ReadAsync<List<MyActionTagOptionDto>>(await SendAsync(HttpMethod.Get, "/api/action-items/my-actions/tags", member))).Should().BeEmpty();
    }
}
