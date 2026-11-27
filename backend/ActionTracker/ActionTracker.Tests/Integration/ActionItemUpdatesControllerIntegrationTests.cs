using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.ActionItems.DTOs;
using ActionTracker.Application.Features.ActionItemUpdates.DTOs;
using ActionTracker.Application.Features.Auth.DTOs;
using ActionTracker.Application.Features.Comments.DTOs;
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
/// WO-2026-11 A-3 through the real pipeline (InMemory database, default role
/// permissions seeded at startup, the recording e-mail sender): policies
/// (401 / 403), the draft → submit flow at item and sub-item level, 409 on a
/// submitted update, drafts invisible to others, 404 for a private workspace,
/// e-mail on submit only and never to the submitter, the direction e-mail key
/// (D7), the shared comments API (fix 3), the removed comment routes, and
/// Swagger. Seeded grants used: Team Member = ActionItems View/Create/Edit,
/// Workspace Admin = ActionItems View only, Viewer = no ActionItems access.
/// </summary>
public class ActionItemUpdatesControllerIntegrationTests
    : IClassFixture<ActionTrackerWebApplicationFactory>
{
    private const string Password = "Updates-Test-2026!";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly ActionTrackerWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ActionItemUpdatesControllerIntegrationTests(ActionTrackerWebApplicationFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private sealed record Session(string UserId, string Token, string Email);

    private async Task<Session> SignInAsync(string name, string role)
    {
        var email = $"a3.{name}@integration.test";
        await _factory.EnsureUserAsync(email, Password, role);
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        var body = JsonSerializer.Deserialize<ApiResponse<AuthResponseDto>>(await response.Content.ReadAsStringAsync(), JsonOptions)!;

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return new Session((await users.FindByEmailAsync(email))!.Id, body.Data!.AccessToken, email);
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

    /// <summary>Workspace + one action item (creator = admins[0], assignees as given), written in system context.</summary>
    private async Task<Guid> AddItemAsync(
        string[] admins, string[] members, string[]? assignees = null, bool isPrivate = false,
        ActionStatus status = ActionStatus.ToDo, Guid? subItemId = null, string[]? subItemAssignees = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var workspace = new Workspace
        {
            Id = Guid.NewGuid(), Title = "A-3 workspace " + Guid.NewGuid().ToString("N")[..6],
            OrganizationUnit = "Integration", IsPrivate = isPrivate, CreatedAt = DateTime.UtcNow,
        };
        foreach (var a in admins)
            workspace.Admins.Add(new WorkspaceAdmin { WorkspaceId = workspace.Id, AdminUserId = a, AdminUserName = a });
        foreach (var m in members)
            workspace.Members.Add(new WorkspaceMember { WorkspaceId = workspace.Id, UserId = m, UserName = m });
        db.Workspaces.Add(workspace);

        var item = new ActionItem
        {
            Id = Guid.NewGuid(), ActionId = "ACT-A3-" + Guid.NewGuid().ToString("N")[..4], Title = "A-3 item",
            Description = "", WorkspaceId = workspace.Id, IsStandalone = true, Status = status,
            Priority = ActionPriority.Medium,
            StartDate = new DateTime(2030, 6, 3), DueDate = new DateTime(2030, 6, 28),
            CreatedByUserId = admins[0], CreatedAt = DateTime.UtcNow,
        };
        foreach (var a in assignees ?? new[] { admins[0] })
            item.Assignees.Add(new ActionItemAssignee { ActionItemId = item.Id, UserId = a });
        db.ActionItems.Add(item);

        if (subItemId is { } sid)
        {
            var sub = new ActionItemSubItem
            {
                Id = sid, ActionItemId = item.Id, Title = "A-3 sub-item", SortOrder = 1,
                EndDate = new DateTime(2030, 6, 7), CreatedAt = DateTime.UtcNow,
            };
            foreach (var a in subItemAssignees ?? Array.Empty<string>())
                sub.Assignees.Add(new ActionItemSubItemAssignee { SubItemId = sid, UserId = a });
            db.ActionItemSubItems.Add(sub);
        }

        await db.SaveChangesAsync();
        return item.Id;
    }

    private static string Url(Guid itemId, string suffix = "") => $"/api/action-items/{itemId}/updates{suffix}";
    private static string SubUrl(Guid itemId, Guid subId, string suffix = "") =>
        $"/api/action-items/{itemId}/sub-items/{subId}/updates{suffix}";

    private static object Body(string content, bool submit = false, bool high = false) =>
        new { content, isHighImportance = high, submit };

    // ── Policies ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Anonymous_gets_401()
    {
        (await SendAsync(HttpMethod.Get, Url(Guid.NewGuid()), null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await SendAsync(HttpMethod.Post, Url(Guid.NewGuid()), null, Body("x"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Viewer_gets_403_and_a_view_only_user_can_read_but_not_write()
    {
        var editor = await SignInAsync("editor-403", AppRoles.TeamMember);
        var viewer = await SignInAsync("viewer", "Viewer");
        var viewOnly = await SignInAsync("viewonly", AppRoles.WorkspaceAdmin);
        var sub = Guid.NewGuid();
        var item = await AddItemAsync(new[] { editor.UserId }, new[] { viewer.UserId, viewOnly.UserId }, subItemId: sub);

        (await SendAsync(HttpMethod.Get, Url(item), viewer)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Get, Url(item, "/counts"), viewer)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Get, SubUrl(item, sub), viewer)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await SendAsync(HttpMethod.Get, Url(item), viewOnly)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Post, Url(item), viewOnly, Body("x"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Post, SubUrl(item, sub), viewOnly, Body("x"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Put, Url(item, $"/{Guid.NewGuid()}"), viewOnly, Body("x"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Post, Url(item, $"/{Guid.NewGuid()}/submit"), viewOnly)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Delete, Url(item, $"/{Guid.NewGuid()}"), viewOnly)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Flow ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Draft_edit_submit_then_locked_and_others_never_see_a_draft()
    {
        var author = await SignInAsync("author-flow", AppRoles.TeamMember);
        var other = await SignInAsync("other-flow", AppRoles.TeamMember);
        var item = await AddItemAsync(new[] { author.UserId }, new[] { other.UserId }, status: ActionStatus.Done);

        var post = await SendAsync(HttpMethod.Post, Url(item), author, Body("  first draft  "));
        post.StatusCode.Should().Be(HttpStatusCode.Created);
        var draft = await ReadAsync<ActionUpdateDto>(post);
        draft.Status.Should().Be(ActionItemUpdateStatus.Draft);
        draft.Content.Should().Be("first draft");

        // Invisible to everyone else: not listed, not counted, 404 on every write.
        (await ReadAsync<ActionUpdateListDto>(await SendAsync(HttpMethod.Get, Url(item), other))).Items.Should().BeEmpty();
        (await ReadAsync<ActionUpdateCountsDto>(await SendAsync(HttpMethod.Get, Url(item, "/counts"), other))).Item.DraftCount.Should().Be(0);
        (await SendAsync(HttpMethod.Put, Url(item, $"/{draft.Id}"), other, Body("hijack"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Post, Url(item, $"/{draft.Id}/submit"), other)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Delete, Url(item, $"/{draft.Id}"), other)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var put = await SendAsync(HttpMethod.Put, Url(item, $"/{draft.Id}"), author, Body("draft, edited", high: true));
        put.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<ActionUpdateDto>(put)).IsHighImportance.Should().BeTrue();

        var submit = await SendAsync(HttpMethod.Post, Url(item, $"/{draft.Id}/submit"), author);
        submit.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<ActionUpdateDto>(submit)).Status.Should().Be(ActionItemUpdateStatus.Submitted);

        // Submitted: 409 for everyone, the author included.
        foreach (var user in new[] { author, other })
        {
            var edit = await SendAsync(HttpMethod.Put, Url(item, $"/{draft.Id}"), user, Body("changed"));
            edit.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await MessageAsync(edit)).Should().Contain("cannot be changed or deleted");
            (await SendAsync(HttpMethod.Delete, Url(item, $"/{draft.Id}"), user)).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await SendAsync(HttpMethod.Post, Url(item, $"/{draft.Id}/submit"), user)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        var list = await ReadAsync<ActionUpdateListDto>(await SendAsync(HttpMethod.Get, Url(item), other));
        list.Items.Should().ContainSingle(u => u.Id == draft.Id && u.Content == "draft, edited" && !u.CanEdit);

        // A second draft is deleted.
        var second = await ReadAsync<ActionUpdateDto>(await SendAsync(HttpMethod.Post, Url(item), author, Body("scrap")));
        (await SendAsync(HttpMethod.Delete, Url(item, $"/{second.Id}"), author)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadAsync<ActionUpdateListDto>(await SendAsync(HttpMethod.Get, Url(item), author))).Items
            .Select(u => u.Id).Should().Equal(draft.Id);
    }

    [Fact]
    public async Task Empty_or_too_long_text_is_400()
    {
        var author = await SignInAsync("author-400", AppRoles.TeamMember);
        var item = await AddItemAsync(new[] { author.UserId }, Array.Empty<string>());

        (await SendAsync(HttpMethod.Post, Url(item), author, Body("   "))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await SendAsync(HttpMethod.Post, Url(item), author, Body(new string('x', 2001)))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await SendAsync(HttpMethod.Post, Url(item), author, Body(new string('x', 2000)))).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Sub_item_updates_have_their_own_list_and_counts()
    {
        var author = await SignInAsync("author-sub", AppRoles.TeamMember);
        var sub = Guid.NewGuid();
        var item = await AddItemAsync(new[] { author.UserId }, Array.Empty<string>(), subItemId: sub);

        (await SendAsync(HttpMethod.Post, SubUrl(item, sub), author, Body("on the sub-item", submit: true)))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        var draft = await ReadAsync<ActionUpdateDto>(await SendAsync(HttpMethod.Post, SubUrl(item, sub), author, Body("sub draft")));
        (await SendAsync(HttpMethod.Put, SubUrl(item, sub, $"/{draft.Id}"), author, Body("sub draft v2"))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Post, SubUrl(item, sub, $"/{draft.Id}/submit"), author)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Delete, SubUrl(item, sub, $"/{draft.Id}"), author)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var subList = await ReadAsync<ActionUpdateListDto>(await SendAsync(HttpMethod.Get, SubUrl(item, sub), author));
        subList.SubmittedCount.Should().Be(2);
        subList.Items.Should().OnlyContain(u => u.SubItemId == sub);
        (await ReadAsync<ActionUpdateListDto>(await SendAsync(HttpMethod.Get, Url(item), author))).Items.Should().BeEmpty();

        var counts = await ReadAsync<ActionUpdateCountsDto>(await SendAsync(HttpMethod.Get, Url(item, "/counts"), author));
        counts.Item.SubmittedCount.Should().Be(0);
        counts.SubItems.Should().ContainSingle(c => c.SubItemId == sub && c.SubmittedCount == 2);

        (await SendAsync(HttpMethod.Get, SubUrl(item, Guid.NewGuid()), author)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_private_workspace_item_is_404_for_a_non_member()
    {
        var owner = await SignInAsync("owner-private", AppRoles.TeamMember);
        var stranger = await SignInAsync("stranger-private", AppRoles.TeamMember);
        var sub = Guid.NewGuid();
        var item = await AddItemAsync(new[] { owner.UserId }, Array.Empty<string>(), isPrivate: true, subItemId: sub);
        var update = await ReadAsync<ActionUpdateDto>(await SendAsync(HttpMethod.Post, Url(item), owner, Body("secret", submit: true)));

        (await SendAsync(HttpMethod.Get, Url(item), stranger)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Get, Url(item, "/counts"), stranger)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Get, SubUrl(item, sub), stranger)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Post, Url(item), stranger, Body("x"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Post, SubUrl(item, sub), stranger, Body("x"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Delete, Url(item, $"/{update.Id}"), stranger)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await SendAsync(HttpMethod.Get, Url(item), owner)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_Admin_gets_409_on_a_submitted_update_and_never_sees_another_users_draft()
    {
        // Q11: "nobody, including Admin". Admin holds every permission (seeded),
        // so the answers below come from the update rules, not from a policy.
        var author = await SignInAsync("author-admin", AppRoles.TeamMember);
        var admin  = await SignInAsync("admin", AppRoles.Admin);
        var sub = Guid.NewGuid();
        var item = await AddItemAsync(new[] { author.UserId }, Array.Empty<string>(), subItemId: sub);

        var submitted = await ReadAsync<ActionUpdateDto>(await SendAsync(HttpMethod.Post, Url(item), author, Body("Final words", submit: true)));
        var draft     = await ReadAsync<ActionUpdateDto>(await SendAsync(HttpMethod.Post, Url(item), author, Body("Author's draft")));
        var subDraft  = await ReadAsync<ActionUpdateDto>(await SendAsync(HttpMethod.Post, SubUrl(item, sub), author, Body("Sub-item draft")));

        // A submitted update: 409 for Admin on edit, delete and submit.
        var edit = await SendAsync(HttpMethod.Put, Url(item, $"/{submitted.Id}"), admin, Body("Admin rewrite"));
        edit.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageAsync(edit)).Should().Contain("cannot be changed or deleted");
        (await SendAsync(HttpMethod.Delete, Url(item, $"/{submitted.Id}"), admin)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await SendAsync(HttpMethod.Post, Url(item, $"/{submitted.Id}/submit"), admin)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Another user's drafts: not listed, not counted, 404 on every write — item and sub-item level.
        var list = await ReadAsync<ActionUpdateListDto>(await SendAsync(HttpMethod.Get, Url(item), admin));
        list.Items.Select(u => u.Id).Should().Equal(submitted.Id);
        list.DraftCount.Should().Be(0);
        list.Items.Single().CanEdit.Should().BeFalse();
        (await ReadAsync<ActionUpdateListDto>(await SendAsync(HttpMethod.Get, SubUrl(item, sub), admin))).Items.Should().BeEmpty();
        var counts = await ReadAsync<ActionUpdateCountsDto>(await SendAsync(HttpMethod.Get, Url(item, "/counts"), admin));
        counts.Item.Should().BeEquivalentTo(new { SubmittedCount = 1, DraftCount = 0 });
        counts.SubItems.Should().BeEmpty();

        (await SendAsync(HttpMethod.Put, Url(item, $"/{draft.Id}"), admin, Body("x"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Post, Url(item, $"/{draft.Id}/submit"), admin)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Delete, Url(item, $"/{draft.Id}"), admin)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Put, SubUrl(item, sub, $"/{subDraft.Id}"), admin, Body("x"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Delete, SubUrl(item, sub, $"/{subDraft.Id}"), admin)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Nothing changed: the author still sees the same three updates.
        var mine = await ReadAsync<ActionUpdateListDto>(await SendAsync(HttpMethod.Get, Url(item), author));
        mine.Items.Should().ContainSingle(u => u.Id == submitted.Id && u.Content == "Final words" && !u.IsDraft);
        mine.Items.Should().ContainSingle(u => u.Id == draft.Id && u.Content == "Author's draft" && u.IsDraft && u.CanEdit);
        (await ReadAsync<ActionUpdateListDto>(await SendAsync(HttpMethod.Get, SubUrl(item, sub), author))).Items
            .Should().ContainSingle(u => u.Id == subDraft.Id && u.IsDraft);
    }

    // ── Notifications ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Submitting_emails_creator_and_assignees_but_not_the_submitter_and_a_draft_emails_nobody()
    {
        var creator = await SignInAsync("creator-mail", AppRoles.TeamMember);
        var assignee = await SignInAsync("assignee-mail", AppRoles.TeamMember);
        var subAssignee = await SignInAsync("subassignee-mail", AppRoles.TeamMember);
        var sub = Guid.NewGuid();
        var item = await AddItemAsync(new[] { creator.UserId }, new[] { assignee.UserId, subAssignee.UserId },
            assignees: new[] { assignee.UserId }, subItemId: sub, subItemAssignees: new[] { subAssignee.UserId });

        bool ForItem(RecordingEmailSender.SentEmail e) =>
            e.TemplateKey == "ActionItem.UpdateSubmitted" && e.RelatedEntityId == item;

        var draft = await ReadAsync<ActionUpdateDto>(await SendAsync(HttpMethod.Post, Url(item), assignee, Body("draft only")));
        (await _factory.Emails.WaitForAsync(ForItem, timeout: TimeSpan.FromMilliseconds(500))).Should().BeEmpty("a draft never notifies");

        (await SendAsync(HttpMethod.Post, Url(item, $"/{draft.Id}/submit"), assignee)).StatusCode.Should().Be(HttpStatusCode.OK);
        var itemMails = await _factory.Emails.WaitForAsync(ForItem);
        await Task.Delay(300);   // let any extra (wrong) e-mail arrive before asserting the exact set
        itemMails = _factory.Emails.Sent.Where(ForItem).ToList();
        itemMails.SelectMany(e => e.Recipients).Should().BeEquivalentTo(new[] { creator.Email },
            "the submitter (the only assignee) is skipped; the sub-item assignee is not involved at item level");

        (await SendAsync(HttpMethod.Post, SubUrl(item, sub), creator, Body("about the sub-item", submit: true)))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        var all = await _factory.Emails.WaitForAsync(ForItem, count: 3);
        await Task.Delay(300);
        all = _factory.Emails.Sent.Where(ForItem).ToList();
        all.Skip(1).SelectMany(e => e.Recipients).Should().BeEquivalentTo(new[] { assignee.Email, subAssignee.Email });
        all.Should().OnlyContain(e => e.TriggeredByUserId != null);
    }

    [Fact]
    public async Task Giving_direction_stores_a_submitted_update_and_sends_the_seeded_direction_email()
    {
        var director = await SignInAsync("director", AppRoles.TeamMember);
        var assignee = await SignInAsync("directed", AppRoles.TeamMember);
        var item = await AddItemAsync(new[] { director.UserId }, new[] { assignee.UserId }, assignees: new[] { assignee.UserId });

        // WO-2026-11 P-2b (B4): only on an escalated item (409 before).
        (await SendAsync(HttpMethod.Post, "/api/action-items/workflow/give-direction", director,
            new { actionItemId = item, directionText = "Too early" })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.ActionItems.FindAsync(item))!.IsEscalated = true;
            await db.SaveChangesAsync();
        }

        var response = await SendAsync(HttpMethod.Post, "/api/action-items/workflow/give-direction", director,
            new { actionItemId = item, directionText = "Call the <vendor> today" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await ReadAsync<ActionUpdateListDto>(await SendAsync(HttpMethod.Get, Url(item), assignee));
        list.Items.Should().ContainSingle(u => u.Content == "[Direction] Call the <vendor> today"
            && u.Status == ActionItemUpdateStatus.Submitted && u.IsHighImportance && u.SubmittedAt != null);

        var mails = await _factory.Emails.WaitForAsync(e => e.RelatedEntityId == item && e.TemplateKey == "ActionItem.DirectionGiven");
        mails.Should().ContainSingle();
        mails[0].Recipients.Should().Equal(assignee.Email);
        mails[0].Placeholders["Reason"].Should().Be("Call the &lt;vendor&gt; today");
        mails[0].Placeholders.Should().ContainKeys("DirectorName", "RecipientName", "ActionUrl", "ActionId", "Title");
        _factory.Emails.Sent.Should().NotContain(e => e.TemplateKey == "Workflow.DirectionGiven");
        _factory.Emails.Sent.Should().NotContain(e => e.RelatedEntityId == item && e.TemplateKey == "ActionItem.UpdateSubmitted",
            "a direction keeps its own notification");
    }

    // ── Old comment routes and the shared comments API ───────────────────────

    [Fact]
    public async Task The_old_action_item_comment_routes_are_gone()
    {
        var user = await SignInAsync("old-routes", AppRoles.TeamMember);
        var item = await AddItemAsync(new[] { user.UserId }, Array.Empty<string>());

        (await SendAsync(HttpMethod.Get, $"/api/action-items/{item}/comments", user)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Post, $"/api/action-items/{item}/comments", user, Body("x")))
            .StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);

        var json = await (await SendAsync(HttpMethod.Get, $"/api/action-items/{item}", user)).Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("data").TryGetProperty("comments", out _).Should().BeFalse("the unused comments array was removed (D5)");
    }

    [Fact]
    public async Task Shared_comments_are_read_only_for_action_items_and_hidden_for_private_parents()
    {
        var owner = await SignInAsync("owner-comments", AppRoles.TeamMember);
        var stranger = await SignInAsync("stranger-comments", AppRoles.TeamMember);
        var publicItem = await AddItemAsync(new[] { owner.UserId }, Array.Empty<string>());
        var privateItem = await AddItemAsync(new[] { owner.UserId }, Array.Empty<string>(), isPrivate: true);

        Guid projectId, oldCommentId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var workspaceId = await db.ActionItems.IgnoreQueryFilters().Where(a => a.Id == privateItem)
                .Select(a => a.WorkspaceId).FirstAsync();
            var project = new Project
            {
                Id = Guid.NewGuid(), ProjectCode = "PRJ-A3-" + Guid.NewGuid().ToString("N")[..4], Name = "Private project",
                WorkspaceId = workspaceId, ProjectManagerUserId = owner.UserId, CreatedBy = owner.UserId,
                PlannedStartDate = new DateTime(2030, 6, 3), PlannedEndDate = new DateTime(2030, 12, 31),
                CreatedAt = DateTime.UtcNow,
            };
            db.Projects.Add(project);
            var old = new Comment
            {
                Id = Guid.NewGuid(), RelatedEntityType = "ActionItem", RelatedEntityId = publicItem,
                Content = "written before A-3", AuthorUserId = owner.UserId, CreatedAt = DateTime.UtcNow,
            };
            db.Comments.Add(old);
            await db.SaveChangesAsync();
            projectId = project.Id;
            oldCommentId = old.Id;
        }

        // Action items: reads still work, writes are refused with the reason.
        (await SendAsync(HttpMethod.Get, $"/api/comments?entityType=ActionItem&entityId={publicItem}", owner))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var post = await SendAsync(HttpMethod.Post, $"/api/comments?entityType=ActionItem&entityId={publicItem}", owner, Body("x"));
        post.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageAsync(post)).Should().Contain("Action Updates");
        (await SendAsync(HttpMethod.Put, $"/api/comments/{oldCommentId}", owner, Body("x"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await SendAsync(HttpMethod.Delete, $"/api/comments/{oldCommentId}", owner)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Unknown entity type.
        (await SendAsync(HttpMethod.Get, $"/api/comments?entityType=Secret&entityId={publicItem}", owner))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Private parent: 404 for a non-member, works for the member.
        (await SendAsync(HttpMethod.Get, $"/api/comments?entityType=ActionItem&entityId={privateItem}", stranger))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Get, $"/api/comments?entityType=Project&entityId={projectId}", stranger))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Post, $"/api/comments?entityType=Project&entityId={projectId}", stranger, Body("x")))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var created = await SendAsync(HttpMethod.Post, $"/api/comments?entityType=Project&entityId={projectId}", owner, Body("member note"));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var comment = await ReadAsync<CommentResponseDto>(created);
        (await SendAsync(HttpMethod.Put, $"/api/comments/{comment.Id}", stranger, Body("x"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Delete, $"/api/comments/{comment.Id}", stranger)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Put, $"/api/comments/{comment.Id}", owner, Body("member note, edited"))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Delete, $"/api/comments/{comment.Id}", owner)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── Swagger ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Swagger_lists_the_update_endpoints()
    {
        var json = await _client.GetStringAsync("/swagger/v1/swagger.json");
        using var doc = JsonDocument.Parse(json);
        var paths = doc.RootElement.GetProperty("paths").EnumerateObject()
            .ToDictionary(p => p.Name.ToLowerInvariant(), p => p.Value);

        bool Has(string path, string verb) => paths.TryGetValue(path, out var p) && p.TryGetProperty(verb, out _);

        const string item = "/api/action-items/{actionitemid}/updates";
        const string sub  = "/api/action-items/{actionitemid}/sub-items/{subitemid}/updates";
        foreach (var basePath in new[] { item, sub })
        {
            Has(basePath, "get").Should().BeTrue(basePath);
            Has(basePath, "post").Should().BeTrue(basePath);
            Has(basePath + "/{updateid}", "put").Should().BeTrue(basePath);
            Has(basePath + "/{updateid}", "delete").Should().BeTrue(basePath);
            Has(basePath + "/{updateid}/submit", "post").Should().BeTrue(basePath);
        }
        Has(item + "/counts", "get").Should().BeTrue();
        paths.Keys.Should().NotContain(k => k.StartsWith("/api/action-items/{id}/comments"));
    }
}
