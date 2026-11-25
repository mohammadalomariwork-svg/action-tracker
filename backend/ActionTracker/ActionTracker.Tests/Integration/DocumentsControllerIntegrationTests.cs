using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.ActionItemSubItems.DTOs;
using ActionTracker.Application.Features.ActionItemUpdates.DTOs;
using ActionTracker.Application.Features.Auth.DTOs;
using ActionTracker.Application.Features.Documents.DTOs;
using ActionTracker.Domain.Constants;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using ActionTracker.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ActionTracker.Tests.Integration;

/// <summary>
/// WO-2026-11 A-4 through the real pipeline: files at all three levels
/// (action item, sub-item, action update), View / Edit per type (403), a
/// hidden parent is 404 — also a download by document id —, Done / Cancelled
/// items and submitted updates are locked (409), drafts' files are their
/// author's only (Admin included), delete rights (D4), unknown types (400),
/// and Swagger. Seeded grants: Team Member = ActionItems View/Create/Edit,
/// Viewer = no ActionItems access, Admin = everything.
/// </summary>
public class DocumentsControllerIntegrationTests : IClassFixture<ActionTrackerWebApplicationFactory>
{
    private const string Password = "Documents-Test-2026!";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly ActionTrackerWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DocumentsControllerIntegrationTests(ActionTrackerWebApplicationFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private sealed record Session(string UserId, string Token);

    private async Task<Session> SignInAsync(string name, string role)
    {
        var email = $"a4.{name}@integration.test";
        await _factory.EnsureUserAsync(email, Password, role);
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        var body = JsonSerializer.Deserialize<ApiResponse<AuthResponseDto>>(await response.Content.ReadAsStringAsync(), JsonOptions)!;

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return new Session((await users.FindByEmailAsync(email))!.Id, body.Data!.AccessToken);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, Session? as_, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        if (as_ is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", as_.Token);
        return await _client.SendAsync(request);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<ApiResponse<T>>(await response.Content.ReadAsStringAsync(), JsonOptions)!.Data!;

    private static async Task<string?> MessageAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<ApiResponse<object>>(await response.Content.ReadAsStringAsync(), JsonOptions)?.Message;

    private static MultipartFormDataContent Form(string type, Guid id, string name = "Evidence", string fileName = "evidence.pdf")
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(type), "entityType" },
            { new StringContent(id.ToString()), "entityId" },
            { new StringContent(name), "name" },
        };
        var file = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4 integration test"));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", fileName);
        return form;
    }

    private Task<HttpResponseMessage> UploadAsync(Session as_, string type, Guid id, string name = "Evidence") =>
        SendAsync(HttpMethod.Post, "/api/documents", as_, Form(type, id, name));

    private Task<HttpResponseMessage> ListAsync(Session as_, string type, Guid id) =>
        SendAsync(HttpMethod.Get, $"/api/documents?entityType={type}&entityId={id}", as_);

    /// <summary>Workspace + item (creator = admins[0], assignees as given) + one sub-item, in system context.</summary>
    private async Task<(Guid Item, Guid Sub)> AddItemAsync(
        string[] admins, string[] members, string[]? assignees = null, string[]? subAssignees = null,
        bool isPrivate = false, ActionStatus status = ActionStatus.ToDo)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var workspace = new Workspace
        {
            Id = Guid.NewGuid(), Title = "A-4 workspace " + Guid.NewGuid().ToString("N")[..6],
            OrganizationUnit = "Integration", IsPrivate = isPrivate, CreatedAt = DateTime.UtcNow,
        };
        foreach (var a in admins)
            workspace.Admins.Add(new WorkspaceAdmin { WorkspaceId = workspace.Id, AdminUserId = a, AdminUserName = a });
        foreach (var m in members)
            workspace.Members.Add(new WorkspaceMember { WorkspaceId = workspace.Id, UserId = m, UserName = m });
        db.Workspaces.Add(workspace);

        var item = new ActionItem
        {
            Id = Guid.NewGuid(), ActionId = "ACT-A4-" + Guid.NewGuid().ToString("N")[..4], Title = "A-4 item",
            Description = "", WorkspaceId = workspace.Id, IsStandalone = true, Status = status, Priority = ActionPriority.Medium,
            StartDate = new DateTime(2030, 6, 3), DueDate = new DateTime(2030, 6, 28),
            CreatedByUserId = admins[0], CreatedAt = DateTime.UtcNow,
        };
        foreach (var a in assignees ?? new[] { admins[0] })
            item.Assignees.Add(new ActionItemAssignee { ActionItemId = item.Id, UserId = a });
        db.ActionItems.Add(item);

        var sub = new ActionItemSubItem
        {
            Id = Guid.NewGuid(), ActionItemId = item.Id, Title = "A-4 sub-item", SortOrder = 1,
            EndDate = new DateTime(2030, 6, 7), CreatedAt = DateTime.UtcNow,
        };
        foreach (var a in subAssignees ?? Array.Empty<string>())
            sub.Assignees.Add(new ActionItemSubItemAssignee { SubItemId = sub.Id, UserId = a });
        db.ActionItemSubItems.Add(sub);

        await db.SaveChangesAsync();
        return (item.Id, sub.Id);
    }

    private async Task<ActionUpdateDto> CreateUpdateAsync(Session as_, Guid item, bool submit = false) =>
        await ReadAsync<ActionUpdateDto>(await SendAsync(HttpMethod.Post, $"/api/action-items/{item}/updates", as_,
            JsonContent.Create(new { content = "Update with files", isHighImportance = false, submit })));

    // ── Policies ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Anonymous_gets_401()
    {
        (await SendAsync(HttpMethod.Get, $"/api/documents?entityType=ActionItem&entityId={Guid.NewGuid()}", null))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await SendAsync(HttpMethod.Get, $"/api/documents/{Guid.NewGuid()}/download", null))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Without_ActionItems_permissions_the_action_item_types_are_403()
    {
        var editor = await SignInAsync("editor-403", AppRoles.TeamMember);
        var viewer = await SignInAsync("viewer-403", "Viewer");
        var (item, sub) = await AddItemAsync(new[] { editor.UserId }, new[] { viewer.UserId });
        var doc = await ReadAsync<DocumentResponseDto>(await UploadAsync(editor, "ActionItem", item));

        (await ListAsync(viewer, "ActionItem", item)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ListAsync(viewer, "ActionItemSubItem", sub)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await UploadAsync(viewer, "ActionItem", item)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Get, $"/api/documents/{doc.Id}/download", viewer)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Delete, $"/api/documents/{doc.Id}", viewer)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_unsupported_type_is_400()
    {
        var editor = await SignInAsync("editor-400", AppRoles.TeamMember);
        var response = await ListAsync(editor, "Workspace", Guid.NewGuid());
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageAsync(response)).Should().Contain("Supported types");
    }

    // ── Three levels ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Files_attach_to_the_item_its_sub_item_and_a_draft_update_then_lock_on_submit()
    {
        var author = await SignInAsync("author-levels", AppRoles.TeamMember);
        var other = await SignInAsync("other-levels", AppRoles.TeamMember);
        var (item, sub) = await AddItemAsync(new[] { author.UserId }, new[] { other.UserId });

        (await UploadAsync(author, "ActionItem", item, "Plan")).StatusCode.Should().Be(HttpStatusCode.Created);
        (await UploadAsync(author, "ActionItemSubItem", sub, "Checklist")).StatusCode.Should().Be(HttpStatusCode.Created);
        var update = await CreateUpdateAsync(author, item);
        var onDraft = await UploadAsync(author, "ActionItemUpdate", update.Id, "Minutes");
        onDraft.StatusCode.Should().Be(HttpStatusCode.Created);
        var draftDoc = await ReadAsync<DocumentResponseDto>(onDraft);
        draftDoc.CanDelete.Should().BeTrue();

        // The draft and its file are invisible to anyone else.
        (await ListAsync(other, "ActionItemUpdate", update.Id)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Get, $"/api/documents/{draftDoc.Id}/download", other)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Counts on the sub-item and the update (paperclips).
        var subs = await ReadAsync<ActionItemSubItemListDto>(await SendAsync(HttpMethod.Get, $"/api/action-items/{item}/sub-items", author));
        subs.Items.Single().DocumentCount.Should().Be(1);
        var updates = await ReadAsync<ActionUpdateListDto>(await SendAsync(HttpMethod.Get, $"/api/action-items/{item}/updates", author));
        updates.Items.Single().DocumentCount.Should().Be(1);

        // Submit: the file becomes visible to others and locked for everyone.
        (await SendAsync(HttpMethod.Post, $"/api/action-items/{item}/updates/{update.Id}/submit", author)).StatusCode.Should().Be(HttpStatusCode.OK);
        var download = await SendAsync(HttpMethod.Get, $"/api/documents/{draftDoc.Id}/download", other);
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        (await download.Content.ReadAsStringAsync()).Should().StartWith("%PDF");
        var upload = await UploadAsync(author, "ActionItemUpdate", update.Id, "Too late");
        upload.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageAsync(upload)).Should().Be("This action update has been submitted. Its files cannot be changed.");
        (await SendAsync(HttpMethod.Delete, $"/api/documents/{draftDoc.Id}", author)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadAsync<List<DocumentResponseDto>>(await ListAsync(other, "ActionItemUpdate", update.Id)))
            .Should().ContainSingle().Which.CanDelete.Should().BeFalse();
    }

    [Fact]
    public async Task An_Admin_cannot_reach_another_users_draft_files_and_gets_409_on_submitted_ones()
    {
        var author = await SignInAsync("author-admin", AppRoles.TeamMember);
        var admin = await SignInAsync("admin-docs", AppRoles.Admin);
        var (item, _) = await AddItemAsync(new[] { author.UserId }, Array.Empty<string>());
        var draft = await CreateUpdateAsync(author, item);
        var draftDoc = await ReadAsync<DocumentResponseDto>(await UploadAsync(author, "ActionItemUpdate", draft.Id));
        var submitted = await CreateUpdateAsync(author, item);
        var submittedDoc = await ReadAsync<DocumentResponseDto>(await UploadAsync(author, "ActionItemUpdate", submitted.Id));
        await SendAsync(HttpMethod.Post, $"/api/action-items/{item}/updates/{submitted.Id}/submit", author);

        (await ListAsync(admin, "ActionItemUpdate", draft.Id)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await UploadAsync(admin, "ActionItemUpdate", draft.Id)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Get, $"/api/documents/{draftDoc.Id}/download", admin)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Delete, $"/api/documents/{draftDoc.Id}", admin)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await UploadAsync(admin, "ActionItemUpdate", submitted.Id)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await SendAsync(HttpMethod.Delete, $"/api/documents/{submittedDoc.Id}", admin)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── Private workspace: 404, also by document id ──────────────────────────

    [Fact]
    public async Task A_non_member_gets_404_for_a_private_workspaces_files_including_download_by_id()
    {
        var owner = await SignInAsync("owner-private", AppRoles.TeamMember);
        var stranger = await SignInAsync("stranger-private", AppRoles.TeamMember);
        var (item, sub) = await AddItemAsync(new[] { owner.UserId }, Array.Empty<string>(), isPrivate: true);
        var itemDoc = await ReadAsync<DocumentResponseDto>(await UploadAsync(owner, "ActionItem", item));
        var subDoc = await ReadAsync<DocumentResponseDto>(await UploadAsync(owner, "ActionItemSubItem", sub));

        foreach (var (type, parent, docId) in new[] { ("ActionItem", item, itemDoc.Id), ("ActionItemSubItem", sub, subDoc.Id) })
        {
            (await ListAsync(stranger, type, parent)).StatusCode.Should().Be(HttpStatusCode.NotFound, type);
            (await UploadAsync(stranger, type, parent)).StatusCode.Should().Be(HttpStatusCode.NotFound, type);
            (await SendAsync(HttpMethod.Get, $"/api/documents/{docId}/download", stranger)).StatusCode.Should().Be(HttpStatusCode.NotFound, type);
            (await SendAsync(HttpMethod.Delete, $"/api/documents/{docId}", stranger)).StatusCode.Should().Be(HttpStatusCode.NotFound, type);
        }
        (await SendAsync(HttpMethod.Get, $"/api/documents/{itemDoc.Id}/download", owner)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Done / Cancelled (D1) ────────────────────────────────────────────────

    [Theory]
    [InlineData(ActionStatus.Done)]
    [InlineData(ActionStatus.Cancelled)]
    public async Task A_Done_or_Cancelled_item_refuses_file_changes_on_it_and_its_sub_items_but_updates_take_files(ActionStatus status)
    {
        var editor = await SignInAsync("editor-done-" + status, AppRoles.TeamMember);
        var (item, sub) = await AddItemAsync(new[] { editor.UserId }, Array.Empty<string>(), status: status);

        var onItem = await UploadAsync(editor, "ActionItem", item);
        onItem.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageAsync(onItem)).Should().EndWith("Add them to an action update instead.");
        (await UploadAsync(editor, "ActionItemSubItem", sub)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ListAsync(editor, "ActionItem", item)).StatusCode.Should().Be(HttpStatusCode.OK);

        var update = await CreateUpdateAsync(editor, item);
        (await UploadAsync(editor, "ActionItemUpdate", update.Id)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // ── Delete rights (D4) ───────────────────────────────────────────────────

    [Fact]
    public async Task Another_editor_can_upload_but_not_delete_someone_elses_file_an_assignee_can()
    {
        var creator = await SignInAsync("creator-d4", AppRoles.TeamMember);
        var assignee = await SignInAsync("assignee-d4", AppRoles.TeamMember);
        var subAssignee = await SignInAsync("subassignee-d4", AppRoles.TeamMember);
        var editor = await SignInAsync("editor-d4", AppRoles.TeamMember);
        var (item, sub) = await AddItemAsync(new[] { creator.UserId }, new[] { assignee.UserId, subAssignee.UserId, editor.UserId },
            assignees: new[] { assignee.UserId }, subAssignees: new[] { subAssignee.UserId });

        var creatorsFile = await ReadAsync<DocumentResponseDto>(await UploadAsync(creator, "ActionItem", item));
        var editorsFile = await ReadAsync<DocumentResponseDto>(await UploadAsync(editor, "ActionItem", item));
        var subFile = await ReadAsync<DocumentResponseDto>(await UploadAsync(creator, "ActionItemSubItem", sub));

        var asEditor = await ReadAsync<List<DocumentResponseDto>>(await ListAsync(editor, "ActionItem", item));
        asEditor.Single(d => d.Id == creatorsFile.Id).CanDelete.Should().BeFalse();
        asEditor.Single(d => d.Id == editorsFile.Id).CanDelete.Should().BeTrue();

        var refused = await SendAsync(HttpMethod.Delete, $"/api/documents/{creatorsFile.Id}", editor);
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await MessageAsync(refused)).Should().StartWith("Only the person who uploaded this file");
        (await SendAsync(HttpMethod.Delete, $"/api/documents/{subFile.Id}", editor)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await SendAsync(HttpMethod.Delete, $"/api/documents/{editorsFile.Id}", editor)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SendAsync(HttpMethod.Delete, $"/api/documents/{creatorsFile.Id}", assignee)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SendAsync(HttpMethod.Delete, $"/api/documents/{subFile.Id}", subAssignee)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── Swagger ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Swagger_lists_the_document_endpoints()
    {
        var json = await _client.GetStringAsync("/swagger/v1/swagger.json");
        using var doc = JsonDocument.Parse(json);
        var paths = doc.RootElement.GetProperty("paths").EnumerateObject().ToDictionary(p => p.Name.ToLowerInvariant(), p => p.Value);
        bool Has(string path, string verb) => paths.TryGetValue(path, out var p) && p.TryGetProperty(verb, out _);

        Has("/api/documents", "get").Should().BeTrue();
        Has("/api/documents", "post").Should().BeTrue();
        Has("/api/documents/{id}/download", "get").Should().BeTrue();
        Has("/api/documents/{id}", "delete").Should().BeTrue();
    }
}
