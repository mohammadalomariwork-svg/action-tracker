using System.Text.RegularExpressions;
using ActionTracker.Application.Common;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.ActionItemUpdates;
using ActionTracker.Application.Features.ActionItemUpdates.DTOs;
using ActionTracker.Application.Features.ActionItemUpdates.Services;
using ActionTracker.Application.Features.ActionItemUpdates.Validators;
using ActionTracker.Application.Features.Notifications;
using ActionTracker.Application.Features.Notifications.DTOs;
using ActionTracker.Application.Features.Workflow.Interfaces;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using ActionTracker.Infrastructure.Data;
using ActionTracker.Infrastructure.Data.Migrations;
using ActionTracker.Infrastructure.Data.Seeders;
using ActionTracker.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ActionTracker.Tests.Unit;

/// <summary>
/// WO-2026-11 A-3: Action Updates — the Q11 rules (a draft is its author's
/// only; submit is one-way; a submitted update is 409 for everyone), the
/// visibility of the parent and the sub-item (deleted / private → not found),
/// updates on Done and Cancelled items, the query filter, notifications on
/// submit only and never to the submitter, the e-mail templates and their
/// placeholders (incl. the direction e-mail key, D7), the validators, and that
/// the re-runnable copy script carries the migration's INSERT.
/// </summary>
public class ActionItemUpdateServiceTests : IDisposable
{
    private const string Author   = "user-author";    // workspace admin; creates the item
    private const string Alice    = "user-alice";     // workspace member
    private const string Admin    = "user-admin";     // another user (roles do not matter to the rules)
    private const string Outsider = "user-outsider";  // in no workspace list

    private readonly AppDbContext _db;
    private string? _currentUserId = Author;
    private readonly Guid _workspaceId = Guid.NewGuid();
    private readonly Mock<IWorkflowNotificationHelper> _notify = new();

    public ActionItemUpdateServiceTests()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(() => _currentUserId);
        _db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            currentUser.Object);

        _db.Users.AddRange(User(Author, "Ann", "Author"), User(Alice, "Alice", "Smith"),
                           User(Admin, "Adam", "Admin"), User(Outsider, "Olly", "Outsider"));
        var workspace = new Workspace
        {
            Id = _workspaceId, Title = "Updates workspace", OrganizationUnit = "Org", CreatedAt = DateTime.UtcNow,
        };
        workspace.Admins.Add(new WorkspaceAdmin { WorkspaceId = _workspaceId, AdminUserId = Author, AdminUserName = "Ann Author" });
        workspace.Members.Add(new WorkspaceMember { WorkspaceId = _workspaceId, UserId = Alice, UserName = "Alice Smith" });
        workspace.Members.Add(new WorkspaceMember { WorkspaceId = _workspaceId, UserId = Admin, UserName = "Adam Admin" });
        _db.Workspaces.Add(workspace);
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ApplicationUser User(string id, string first, string last, bool active = true) => new()
    {
        Id = id, UserName = id + "@test", Email = id + "@test", FirstName = first, LastName = last,
        IsActive = active, CreatedAt = DateTime.UtcNow,
    };

    private IServiceScopeFactory ScopeFactory()
    {
        var provider = new Mock<IServiceProvider>();
        provider.Setup(p => p.GetService(typeof(IWorkflowNotificationHelper))).Returns(_notify.Object);
        var scope = new Mock<IServiceScope>();
        scope.SetupGet(s => s.ServiceProvider).Returns(provider.Object);
        var factory = new Mock<IServiceScopeFactory>();
        factory.Setup(f => f.CreateScope()).Returns(scope.Object);
        return factory.Object;
    }

    private ActionItemUpdateService Service() =>
        new(_db, ScopeFactory(), NullLogger<ActionItemUpdateService>.Instance);

    /// <summary>Runs <paramref name="act"/> as <paramref name="userId"/> (the query filters read the current user).</summary>
    private async Task<T> As<T>(string? userId, Func<ActionItemUpdateService, Task<T>> act)
    {
        var previous = _currentUserId;
        _currentUserId = userId;
        _db.ChangeTracker.Clear();
        try { return await act(Service()); }
        finally { _currentUserId = previous; _db.ChangeTracker.Clear(); }
    }

    private Task As(string? userId, Func<ActionItemUpdateService, Task> act) =>
        As<object?>(userId, async s => { await act(s); return null; });

    private async Task<ActionItem> AddItemAsync(
        ActionStatus status = ActionStatus.ToDo, bool isPrivateWorkspace = false, params string[] assignees)
    {
        var workspaceId = _workspaceId;
        if (isPrivateWorkspace)
        {
            var ws = new Workspace
            {
                Id = Guid.NewGuid(), Title = "Private", OrganizationUnit = "Org", IsPrivate = true, CreatedAt = DateTime.UtcNow,
            };
            ws.Admins.Add(new WorkspaceAdmin { WorkspaceId = ws.Id, AdminUserId = Author, AdminUserName = "Ann Author" });
            _db.Workspaces.Add(ws);
            workspaceId = ws.Id;
        }

        var item = new ActionItem
        {
            Id = Guid.NewGuid(), ActionId = "ACT-" + Guid.NewGuid().ToString("N")[..5], Title = "Parent <b>&</b>",
            Description = "", WorkspaceId = workspaceId, IsStandalone = true, Status = status,
            Priority = ActionPriority.Medium, DueDate = new DateTime(2030, 6, 28, 0, 0, 0, DateTimeKind.Utc),
            CreatedByUserId = Author, CreatedAt = DateTime.UtcNow,
        };
        foreach (var a in assignees.DefaultIfEmpty(Author))
            item.Assignees.Add(new ActionItemAssignee { ActionItemId = item.Id, UserId = a });
        _db.ActionItems.Add(item);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return item;
    }

    private async Task<ActionItemSubItem> AddSubItemAsync(Guid parentId, bool isDeleted = false, params string[] assignees)
    {
        var s = new ActionItemSubItem
        {
            Id = Guid.NewGuid(), ActionItemId = parentId, Title = "Step <1>", SortOrder = 1, IsDeleted = isDeleted,
            EndDate = new DateTime(2030, 6, 7, 0, 0, 0, DateTimeKind.Utc), CreatedAt = DateTime.UtcNow,
        };
        foreach (var a in assignees)
            s.Assignees.Add(new ActionItemSubItemAssignee { SubItemId = s.Id, UserId = a });
        _db.ActionItemSubItems.Add(s);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return s;
    }

    private static CreateActionUpdateDto Draft(string text = "Progress so far", bool high = false) =>
        new() { Content = text, IsHighImportance = high };

    private static CreateActionUpdateDto Submitted(string text = "Done the first part", bool high = false) =>
        new() { Content = text, IsHighImportance = high, Submit = true };

    private ActionItemUpdate Raw(Guid id)
    {
        _db.ChangeTracker.Clear();
        return _db.ActionItemUpdates.IgnoreQueryFilters().AsNoTracking().First(u => u.Id == id);
    }

    /// <summary>Notifications run fire-and-forget; wait for the mocked helper to be reached.</summary>
    private async Task WaitForNotificationAsync(int count = 1)
    {
        for (var i = 0; i < 100 && _notify.Invocations.Count < count; i++)
            await Task.Delay(20);
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_saves_a_trimmed_draft_that_only_its_author_can_see()
    {
        var item = await AddItemAsync();

        var draft = await As(Author, s => s.CreateAsync(item.Id, null, Draft("  Half way  ", high: true), Author));

        draft.Status.Should().Be(ActionItemUpdateStatus.Draft);
        draft.IsDraft.Should().BeTrue();
        draft.Content.Should().Be("Half way");
        draft.IsHighImportance.Should().BeTrue();
        draft.SubmittedAt.Should().BeNull();
        draft.CanEdit.Should().BeTrue();
        draft.AuthorName.Should().Be("Ann Author");

        (await As(Author, s => s.GetAsync(item.Id, null))).Items.Should().ContainSingle(u => u.Id == draft.Id);
        foreach (var other in new[] { Alice, Admin })
        {
            var list = await As(other, s => s.GetAsync(item.Id, null));
            list.Items.Should().BeEmpty();
            list.DraftCount.Should().Be(0);
        }

        await Task.Delay(150);
        _notify.Invocations.Should().BeEmpty("a draft never notifies");
    }

    [Fact]
    public async Task Create_with_submit_stores_a_submitted_update_everyone_sees_and_notifies_once()
    {
        var item = await AddItemAsync();

        var update = await As(Author, s => s.CreateAsync(item.Id, null, Submitted(), Author));

        update.Status.Should().Be(ActionItemUpdateStatus.Submitted);
        update.SubmittedAt.Should().NotBeNull();
        update.CanEdit.Should().BeFalse();
        (await As(Alice, s => s.GetAsync(item.Id, null))).Items.Should().ContainSingle(u => u.Id == update.Id && !u.CanEdit);

        await WaitForNotificationAsync();
        _notify.Verify(n => n.NotifyActionUpdateSubmittedAsync(update.Id, Author), Times.Once);
        _notify.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(ActionStatus.Done)]
    [InlineData(ActionStatus.Cancelled)]
    [InlineData(ActionStatus.Deferred)]
    public async Task Updates_are_allowed_whatever_the_parent_status(ActionStatus status)
    {
        var item = await AddItemAsync(status);

        var draft = await As(Author, s => s.CreateAsync(item.Id, null, Draft(), Author));
        await As(Author, s => s.SubmitAsync(item.Id, null, draft.Id, Author));
        var direct = await As(Author, s => s.CreateAsync(item.Id, null, Submitted(), Author));

        (await As(Alice, s => s.GetAsync(item.Id, null))).SubmittedCount.Should().Be(2);
        direct.Status.Should().Be(ActionItemUpdateStatus.Submitted);
    }

    // ── Draft rules (Q11) ─────────────────────────────────────────────────────

    [Fact]
    public async Task The_author_can_edit_submit_and_delete_a_draft()
    {
        var item = await AddItemAsync();
        var a = await As(Author, s => s.CreateAsync(item.Id, null, Draft("first"), Author));
        var b = await As(Author, s => s.CreateAsync(item.Id, null, Draft("second"), Author));

        var edited = await As(Author, s => s.EditAsync(item.Id, null, a.Id,
            new EditActionUpdateDto { Content = " first, edited ", IsHighImportance = true }, Author));
        edited.Content.Should().Be("first, edited");
        edited.IsHighImportance.Should().BeTrue();
        edited.UpdatedAt.Should().NotBeNull();
        edited.Status.Should().Be(ActionItemUpdateStatus.Draft);

        var submitted = await As(Author, s => s.SubmitAsync(item.Id, null, a.Id, Author));
        submitted.Status.Should().Be(ActionItemUpdateStatus.Submitted);
        submitted.SubmittedAt.Should().NotBeNull();

        await As(Author, s => s.DeleteAsync(item.Id, null, b.Id, Author));
        Raw(b.Id).IsDeleted.Should().BeTrue("a draft is soft-deleted (D2)");

        var list = await As(Author, s => s.GetAsync(item.Id, null));
        list.Items.Select(i => i.Id).Should().Equal(a.Id);

        await WaitForNotificationAsync();
        _notify.Verify(n => n.NotifyActionUpdateSubmittedAsync(a.Id, Author), Times.Once);
        _notify.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Another_user_cannot_see_edit_submit_or_delete_a_draft_not_even_an_admin()
    {
        var item = await AddItemAsync();
        var draft = await As(Author, s => s.CreateAsync(item.Id, null, Draft(), Author));

        foreach (var other in new[] { Alice, Admin })
        {
            await FluentActions.Awaiting(() => As(other, s => s.EditAsync(item.Id, null, draft.Id,
                    new EditActionUpdateDto { Content = "hijack" }, other)))
                .Should().ThrowAsync<KeyNotFoundException>();
            await FluentActions.Awaiting(() => As(other, s => s.SubmitAsync(item.Id, null, draft.Id, other)))
                .Should().ThrowAsync<KeyNotFoundException>();
            await FluentActions.Awaiting(() => As(other, s => s.DeleteAsync(item.Id, null, draft.Id, other)))
                .Should().ThrowAsync<KeyNotFoundException>();
            (await As(other, s => s.GetCountsAsync(item.Id))).Item.DraftCount.Should().Be(0);
        }

        var raw = Raw(draft.Id);
        raw.Content.Should().Be("Progress so far");
        raw.Status.Should().Be(ActionItemUpdateStatus.Draft);
        raw.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task A_submitted_update_is_409_for_everyone_including_its_author()
    {
        var item = await AddItemAsync();
        var update = await As(Author, s => s.CreateAsync(item.Id, null, Submitted("final words"), Author));

        foreach (var user in new[] { Author, Alice, Admin })
        {
            await FluentActions.Awaiting(() => As(user, s => s.EditAsync(item.Id, null, update.Id,
                    new EditActionUpdateDto { Content = "changed" }, user)))
                .Should().ThrowAsync<ActionItemUpdateLockedException>();
            await FluentActions.Awaiting(() => As(user, s => s.SubmitAsync(item.Id, null, update.Id, user)))
                .Should().ThrowAsync<ActionItemUpdateLockedException>();
            await FluentActions.Awaiting(() => As(user, s => s.DeleteAsync(item.Id, null, update.Id, user)))
                .Should().ThrowAsync<ActionItemUpdateLockedException>()
                .WithMessage(ActionItemUpdateLockedException.SubmittedMessage);
        }

        var raw = Raw(update.Id);
        raw.Content.Should().Be("final words");
        raw.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Existing_rows_written_before_A3_are_submitted_and_read_only()
    {
        // A row as the migration's backfill leaves it (Status = Submitted, SubmittedAt = CreatedAt).
        var item = await AddItemAsync();
        var created = DateTime.UtcNow.AddDays(-30);
        var legacy = new ActionItemUpdate
        {
            Id = Guid.NewGuid(), ActionItemId = item.Id, Content = "Old comment", AuthorUserId = Alice,
            Status = ActionItemUpdateStatus.Submitted, SubmittedAt = created, CreatedAt = created,
        };
        _db.ActionItemUpdates.Add(legacy);
        await _db.SaveChangesAsync();

        (await As(Outsider, s => s.GetAsync(item.Id, null))).Items.Should().ContainSingle(u => u.Id == legacy.Id);
        await FluentActions.Awaiting(() => As(Alice, s => s.EditAsync(item.Id, null, legacy.Id,
                new EditActionUpdateDto { Content = "x" }, Alice)))
            .Should().ThrowAsync<ActionItemUpdateLockedException>();
    }

    [Fact]
    public async Task A_new_entity_defaults_to_draft()
    {
        new ActionItemUpdate().Status.Should().Be(ActionItemUpdateStatus.Draft);

        // The database default (Submitted) must never replace an explicit Draft.
        var item = await AddItemAsync();
        var draft = new ActionItemUpdate { Id = Guid.NewGuid(), ActionItemId = item.Id, Content = "x", AuthorUserId = Author };
        _db.ActionItemUpdates.Add(draft);
        await _db.SaveChangesAsync();
        Raw(draft.Id).Status.Should().Be(ActionItemUpdateStatus.Draft);
    }

    // ── Visibility of the parent and the sub-item ────────────────────────────

    [Fact]
    public async Task A_deleted_action_item_is_not_found_and_hides_its_updates()
    {
        var item = await AddItemAsync();
        var update = await As(Author, s => s.CreateAsync(item.Id, null, Submitted(), Author));
        var tracked = await _db.ActionItems.FirstAsync(a => a.Id == item.Id);
        tracked.IsDeleted = true;
        await _db.SaveChangesAsync();

        await FluentActions.Awaiting(() => As(Author, s => s.GetAsync(item.Id, null))).Should().ThrowAsync<KeyNotFoundException>();
        await FluentActions.Awaiting(() => As(Author, s => s.CreateAsync(item.Id, null, Draft(), Author)))
            .Should().ThrowAsync<KeyNotFoundException>();
        _currentUserId = Author;
        (await _db.ActionItemUpdates.AnyAsync(u => u.Id == update.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task A_private_workspace_item_is_not_found_for_a_non_member()
    {
        var item = await AddItemAsync(isPrivateWorkspace: true);
        var update = await As(Author, s => s.CreateAsync(item.Id, null, Submitted("secret"), Author));

        await FluentActions.Awaiting(() => As(Outsider, s => s.GetAsync(item.Id, null))).Should().ThrowAsync<KeyNotFoundException>();
        await FluentActions.Awaiting(() => As(Outsider, s => s.GetCountsAsync(item.Id))).Should().ThrowAsync<KeyNotFoundException>();
        await FluentActions.Awaiting(() => As(Outsider, s => s.CreateAsync(item.Id, null, Draft(), Outsider)))
            .Should().ThrowAsync<KeyNotFoundException>();
        await FluentActions.Awaiting(() => As(Outsider, s => s.DeleteAsync(item.Id, null, update.Id, Outsider)))
            .Should().ThrowAsync<KeyNotFoundException>("existence is not revealed — 404, not 409");

        _currentUserId = Outsider;
        (await _db.ActionItemUpdates.CountAsync()).Should().Be(0, "the query filter hides private-workspace updates");
        _currentUserId = Author;
        (await As(Author, s => s.GetAsync(item.Id, null))).Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Sub_item_updates_are_kept_apart_from_item_updates_and_counted_per_sub_item()
    {
        var item = await AddItemAsync();
        var sub = await AddSubItemAsync(item.Id);
        // WO-2026-11 P-2b: Alice works on the other sub-item as its assignee (updates of that sub-item).
        var other = await AddSubItemAsync(item.Id, false, Alice);

        var onItem = await As(Author, s => s.CreateAsync(item.Id, null, Submitted("item"), Author));
        var onSub  = await As(Author, s => s.CreateAsync(item.Id, sub.Id, Submitted("sub"), Author));
        await As(Author, s => s.CreateAsync(item.Id, sub.Id, Draft("my sub draft"), Author));
        await As(Alice, s => s.CreateAsync(item.Id, other.Id, Draft("alice's draft"), Alice));

        onSub.SubItemId.Should().Be(sub.Id);
        (await As(Author, s => s.GetAsync(item.Id, null))).Items.Select(i => i.Id).Should().Equal(onItem.Id);
        var subList = await As(Author, s => s.GetAsync(item.Id, sub.Id));
        subList.SubmittedCount.Should().Be(1);
        subList.DraftCount.Should().Be(1);
        subList.Items.First().IsDraft.Should().BeTrue("drafts come first");

        var counts = await As(Author, s => s.GetCountsAsync(item.Id));
        counts.Item.SubmittedCount.Should().Be(1);
        counts.SubItems.Should().ContainSingle(c => c.SubItemId == sub.Id && c.SubmittedCount == 1 && c.DraftCount == 1);
        counts.SubItems.Should().NotContain(c => c.SubItemId == other.Id, "Alice's draft is hers only");

        // An update id used through the wrong level is not found.
        await FluentActions.Awaiting(() => As(Author, s => s.EditAsync(item.Id, null, onSub.Id,
                new EditActionUpdateDto { Content = "x" }, Author)))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task A_deleted_sub_item_or_a_sub_item_of_another_item_is_not_found()
    {
        var item = await AddItemAsync();
        var otherItem = await AddItemAsync();
        var deleted = await AddSubItemAsync(item.Id, isDeleted: true);
        var foreign = await AddSubItemAsync(otherItem.Id);

        await FluentActions.Awaiting(() => As(Author, s => s.GetAsync(item.Id, deleted.Id))).Should().ThrowAsync<KeyNotFoundException>();
        await FluentActions.Awaiting(() => As(Author, s => s.CreateAsync(item.Id, deleted.Id, Draft(), Author)))
            .Should().ThrowAsync<KeyNotFoundException>();
        await FluentActions.Awaiting(() => As(Author, s => s.CreateAsync(item.Id, foreign.Id, Draft(), Author)))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Updates_of_a_deleted_sub_item_are_hidden_by_the_query_filter()
    {
        var item = await AddItemAsync();
        var sub = await AddSubItemAsync(item.Id);
        var update = await As(Author, s => s.CreateAsync(item.Id, sub.Id, Submitted(), Author));
        var tracked = await _db.ActionItemSubItems.FirstAsync(s => s.Id == sub.Id);
        tracked.IsDeleted = true;
        await _db.SaveChangesAsync();

        (await As(Author, s => s.GetCountsAsync(item.Id))).SubItems.Should().BeEmpty();
        _currentUserId = Author;
        (await _db.ActionItemUpdates.AnyAsync(u => u.Id == update.Id)).Should().BeFalse();
    }

    // ── Notifications (helper) ────────────────────────────────────────────────

    private WorkflowNotificationHelper Helper(Mock<INotificationService> notifications, Mock<IEmailSender> email) =>
        new(_db, notifications.Object, email.Object,
            Options.Create(new AppSettings { FrontendBaseUrl = "http://localhost:4200" }),
            NullLogger<WorkflowNotificationHelper>.Instance);

    private async Task<ActionItemUpdate> AddRawUpdateAsync(
        Guid itemId, Guid? subItemId, ActionItemUpdateStatus status, string author, string content = "Line 1 <script>x</script>",
        bool high = false)
    {
        var u = new ActionItemUpdate
        {
            Id = Guid.NewGuid(), ActionItemId = itemId, SubItemId = subItemId, Content = content, AuthorUserId = author,
            IsHighImportance = high, Status = status,
            SubmittedAt = status == ActionItemUpdateStatus.Submitted ? DateTime.UtcNow : null,
        };
        _db.ActionItemUpdates.Add(u);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return u;
    }

    [Fact]
    public async Task Helper_notifies_creator_and_assignees_not_the_submitter_nor_inactive_users()
    {
        _db.Users.Add(User("user-gone", "Gone", "User", active: false));
        await _db.SaveChangesAsync();
        var item = await AddItemAsync(assignees: new[] { Alice, "user-gone", Admin });   // creator: Author
        var update = await AddRawUpdateAsync(item.Id, null, ActionItemUpdateStatus.Submitted, Admin, high: true);
        var notifications = new Mock<INotificationService>();
        var email = new Mock<IEmailSender>();

        _currentUserId = null;   // runs in the background, without a user
        await Helper(notifications, email).NotifyActionUpdateSubmittedAsync(update.Id, Admin);

        var notified = notifications.Invocations.Select(i => (CreateNotificationDto)i.Arguments[0]).ToList();
        notified.Select(d => d.UserId).Should().BeEquivalentTo(new[] { Author, Alice });
        notified.Should().OnlyContain(d => d.ActionType == "UpdateSubmitted" && d.Type == "ActionItem"
            && d.Title == "High-importance action update" && d.RelatedEntityId == item.Id
            && d.Url == $"/actions/{item.Id}/view" && d.Message.StartsWith("Adam Admin submitted an action update on "));

        email.Verify(e => e.SendEmailAsync("ActionItem.UpdateSubmitted",
            It.Is<Dictionary<string, string>>(p =>
                p["UpdateContent"] == "Line 1 &lt;script&gt;x&lt;/script&gt;"
                && p["Title"] == "Parent &lt;b&gt;&amp;&lt;/b&gt;"
                && p["SubjectTitle"] == "Parent <b>&</b>"
                && p["ImportancePrefix"] == "High importance: "
                && p["AuthorName"] == "Adam Admin"
                && p["ItemUrl"] == $"http://localhost:4200/actions/{item.Id}/view"),
            It.IsAny<List<string>>(), "ActionItem", item.Id, Admin), Times.Exactly(2));
        email.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
            It.Is<List<string>>(r => r.Contains(Admin + "@test") || r.Contains("user-gone@test")),
            It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task Helper_adds_the_sub_item_assignees_for_a_sub_item_update()
    {
        var item = await AddItemAsync(assignees: new[] { Author });
        var sub = await AddSubItemAsync(item.Id, false, Alice, Outsider);
        var update = await AddRawUpdateAsync(item.Id, sub.Id, ActionItemUpdateStatus.Submitted, Author);
        var notifications = new Mock<INotificationService>();
        var email = new Mock<IEmailSender>();

        _currentUserId = null;
        await Helper(notifications, email).NotifyActionUpdateSubmittedAsync(update.Id, Author);

        notifications.Invocations.Select(i => ((CreateNotificationDto)i.Arguments[0]).UserId)
            .Should().BeEquivalentTo(new[] { Alice, Outsider });
        email.Verify(e => e.SendEmailAsync("ActionItem.UpdateSubmitted",
            It.Is<Dictionary<string, string>>(p => p["UpdateLevel"].Contains("Step &lt;1&gt;") && p["ImportancePrefix"] == ""),
            It.IsAny<List<string>>(), "ActionItem", item.Id, Author), Times.Exactly(2));
    }

    [Fact]
    public async Task Helper_sends_nothing_for_a_draft_or_a_deleted_update()
    {
        var item = await AddItemAsync(assignees: new[] { Alice });
        var draft = await AddRawUpdateAsync(item.Id, null, ActionItemUpdateStatus.Draft, Author);
        var gone = await AddRawUpdateAsync(item.Id, null, ActionItemUpdateStatus.Submitted, Author);
        var tracked = await _db.ActionItemUpdates.IgnoreQueryFilters().FirstAsync(u => u.Id == gone.Id);
        tracked.IsDeleted = true;
        await _db.SaveChangesAsync();
        var notifications = new Mock<INotificationService>();
        var email = new Mock<IEmailSender>();

        _currentUserId = null;
        await Helper(notifications, email).NotifyActionUpdateSubmittedAsync(draft.Id, Author);
        await Helper(notifications, email).NotifyActionUpdateSubmittedAsync(gone.Id, Author);

        notifications.Invocations.Should().BeEmpty();
        email.Invocations.Should().BeEmpty();
    }

    // ── E-mail templates and placeholders ────────────────────────────────────

    private static readonly Regex Placeholder = new(@"\{\{([^}]+)\}\}");

    private static IReadOnlySet<string> PlaceholdersOf(string templateKey)
    {
        var template = EmailTemplateSeeder.SeedTemplates().Single(t => t.TemplateKey == templateKey);
        return Placeholder.Matches(template.Subject + template.HtmlBody).Select(m => m.Groups[1].Value).ToHashSet();
    }

    [Fact]
    public void The_update_submitted_template_gets_every_placeholder_it_uses()
    {
        var update = new ActionItemUpdate
        {
            Content = "x", CreatedAt = DateTime.UtcNow,
            ActionItem = new ActionItem { ActionId = "ACT-1", Title = "T", Workspace = new Workspace { Title = "W" } },
        };
        var supplied = WorkflowNotificationHelper.BuildActionUpdatePlaceholders(update, "Ann", "http://x").Keys
            .Append("RecipientName").ToHashSet();

        PlaceholdersOf(WorkflowNotificationHelper.ActionUpdateSubmittedTemplateKey).Should().BeSubsetOf(supplied);
    }

    [Fact]
    public void The_direction_email_uses_the_seeded_key_and_fills_every_placeholder()
    {
        // D7: the code used "Workflow.DirectionGiven", which was never seeded.
        ActionItemWorkflowService.DirectionGivenTemplateKey.Should().Be("ActionItem.DirectionGiven");
        EmailTemplateSeeder.SeedTemplates().Should().Contain(t => t.TemplateKey == ActionItemWorkflowService.DirectionGivenTemplateKey);

        var placeholders = ActionItemWorkflowService.BuildDirectionGivenPlaceholders(
            "ACT-7", "Fix <b>&</b> it", "Dana <Director>", " Do <b>this</b> ", "http://x/actions/1/view", "Rae");

        PlaceholdersOf(ActionItemWorkflowService.DirectionGivenTemplateKey).Should().BeSubsetOf(placeholders.Keys.ToHashSet());
        placeholders["Reason"].Should().Be("Do &lt;b&gt;this&lt;/b&gt;");
        placeholders["DirectorName"].Should().Be("Dana &lt;Director&gt;");
        placeholders["RecipientName"].Should().Be("Rae");
        placeholders["Title"].Should().Be("Fix &lt;b&gt;&amp;&lt;/b&gt; it");

        // The subject is plain text: the encoded title reads as typed there.
        var template = EmailTemplateSeeder.SeedTemplates().Single(t => t.TemplateKey == ActionItemWorkflowService.DirectionGivenTemplateKey);
        EmailSender.ResolveSubject(template.Subject, placeholders).Should().Be("Direction Given — ACT-7: Fix <b>&</b> it");
    }

    // ── Validators ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("ok", true)]
    public void Content_is_required(string content, bool valid)
    {
        new CreateActionUpdateValidator().Validate(new CreateActionUpdateDto { Content = content }).IsValid.Should().Be(valid);
        new EditActionUpdateValidator().Validate(new EditActionUpdateDto { Content = content }).IsValid.Should().Be(valid);
    }

    [Fact]
    public void Content_may_have_2000_characters_after_trimming()
    {
        var validator = new CreateActionUpdateValidator();
        validator.Validate(new CreateActionUpdateDto { Content = new string('a', 2000) + "   " }).IsValid.Should().BeTrue();
        validator.Validate(new CreateActionUpdateDto { Content = new string('a', 2001) }).IsValid.Should().BeFalse();
    }

    // ── Migration backfill and the re-runnable copy script ───────────────────

    private static string Normalize(string sql) => Regex.Replace(sql, @"\s+", " ").Trim();

    [Fact]
    public void The_copy_script_contains_exactly_the_migrations_insert()
    {
        var sqls = new Add_ActionItemUpdates().UpOperations.OfType<SqlOperation>().Select(o => o.Sql).ToList();
        sqls.Should().HaveCount(2, "one UPDATE of the existing rows and one INSERT from Comments");
        Normalize(sqls[0]).Should().Be(
            "UPDATE dbo.ActionItemComments SET Status = 1, SubmittedAt = CreatedAt WHERE SubmittedAt IS NULL;");
        var insert = Normalize(sqls[1]);
        insert.Should().StartWith("INSERT INTO dbo.ActionItemComments").And.Contain("NOT EXISTS");

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ActionTracker.sln")))
            dir = dir.Parent;
        dir.Should().NotBeNull("the tests run inside the repository");
        var script = File.ReadAllText(Path.Combine(dir!.FullName,
            "ActionTracker.Infrastructure", "Data", "Scripts", "A3-copy-action-item-comments.sql"));
        var match = Regex.Match(script, @"-- <copy>(.*?)-- </copy>", RegexOptions.Singleline);
        match.Success.Should().BeTrue();
        Normalize(match.Groups[1].Value).Should().Be(insert);

        // The migration drops and deletes nothing.
        new Add_ActionItemUpdates().UpOperations
            .Should().NotContain(o => o is DropColumnOperation || o is DropTableOperation || o is RenameColumnOperation);
        sqls.Should().NotContain(s => Regex.IsMatch(s, @"(DELETE|DROP|TRUNCATE)", RegexOptions.IgnoreCase));
    }
}
