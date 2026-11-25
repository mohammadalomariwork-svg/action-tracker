using ActionTracker.Application.Common;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.ActionItems.DTOs;
using ActionTracker.Application.Features.ActionItems.Services;
using ActionTracker.Application.Features.Calendars.Services;
using ActionTracker.Application.Features.Notifications;
using ActionTracker.Application.Features.Tags;
using ActionTracker.Application.Features.Tags.Interfaces;
using ActionTracker.Application.Features.Workflow.Interfaces;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using ActionTracker.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ActionTracker.Tests.Unit.Tags;

/// <summary>
/// WO-2026-11 A-5: tags on action items through <see cref="ActionItemService"/>
/// and the tag service — save (clean, de-dup, reuse in the workspace), replace /
/// keep / clear, Done locked and Cancelled editable (D1), carried over by name
/// on a workspace move (D2), the any-of filter by id (D3), workspace and
/// private-workspace isolation of suggestions, options and responses, and the
/// pending-migration behaviour (D7).
/// </summary>
public class ActionItemTagTests : IDisposable
{
    private const string Creator  = "user-creator";   // admin of A, B and the private workspace
    private const string Assignee = "user-assignee";  // member of A and B
    private const string Outsider = "user-outsider";  // member of A only — not of the private workspace

    private readonly AppDbContext _db;
    private string? _currentUserId = Creator;
    private readonly FixedTagSchemaState _schema = new(true);
    private readonly Guid _a = Guid.NewGuid();
    private readonly Guid _b = Guid.NewGuid();
    private readonly Guid _private = Guid.NewGuid();
    private List<Guid>? _visibleWorkspaces;                // null = every workspace (privileged role)

    public ActionItemTagTests()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(() => _currentUserId);
        _db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            currentUser.Object);

        foreach (var id in new[] { Creator, Assignee, Outsider })
            _db.Users.Add(new ApplicationUser { Id = id, UserName = id, Email = id + "@t", FirstName = id, LastName = "X", CreatedAt = DateTime.UtcNow });

        Workspace Ws(Guid id, string title, bool isPrivate, params string[] members)
        {
            var ws = new Workspace { Id = id, Title = title, OrganizationUnit = "O", IsPrivate = isPrivate, CreatedAt = DateTime.UtcNow };
            ws.Admins.Add(new WorkspaceAdmin { WorkspaceId = id, AdminUserId = Creator, AdminUserName = "c" });
            foreach (var m in members)
                ws.Members.Add(new WorkspaceMember { WorkspaceId = id, UserId = m, UserName = m });
            return ws;
        }
        _db.Workspaces.AddRange(
            Ws(_a, "Alpha", false, Assignee, Outsider),
            Ws(_b, "Beta", false, Assignee),
            Ws(_private, "Hidden", true, Assignee));
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IActionItemTagService Tags() =>
        TagTestHelpers.NewTagService(_db, _schema, new FixedWorkspaceAccess(_visibleWorkspaces));

    private ActionItemService Service()
    {
        var provider = new Mock<IServiceProvider>();
        provider.Setup(p => p.GetService(typeof(IActionItemWorkflowService))).Returns(Mock.Of<IActionItemWorkflowService>());
        var scope = new Mock<IServiceScope>();
        scope.SetupGet(s => s.ServiceProvider).Returns(provider.Object);
        var factory = new Mock<IServiceScopeFactory>();
        factory.Setup(f => f.CreateScope()).Returns(scope.Object);

        var calendars = new WorkCalendarService(_db, new FixedWorkspaceAccess(), new WorkCalendarCache(), NullLogger<WorkCalendarService>.Instance);
        return new ActionItemService(_db, NullLogger<ActionItemService>.Instance, Mock.Of<IEmailSender>(), Mock.Of<INotificationService>(),
            Options.Create(new AppSettings { FrontendBaseUrl = "http://localhost:4200" }), factory.Object, calendars, Tags());
    }

    /// <summary>Runs as <paramref name="user"/> (the query filters read the current user) on a clean tracker.</summary>
    private async Task<T> As<T>(string user, Func<Task<T>> act)
    {
        var previous = _currentUserId;
        _currentUserId = user;
        _db.ChangeTracker.Clear();
        try { return await act(); }
        finally { _currentUserId = previous; _db.ChangeTracker.Clear(); }
    }

    private Task<ActionItemResponseDto> CreateAsync(Guid workspaceId, params string[] tags) =>
        As(Creator, () => Service().CreateAsync(new ActionItemCreateDto
        {
            Title = "Item", WorkspaceId = workspaceId, AssigneeIds = [Assignee], Priority = ActionPriority.Medium,
            DueDate = new DateTime(2030, 6, 28, 0, 0, 0, DateTimeKind.Utc), Tags = tags.ToList(),
        }, Creator, default));

    private Task<ActionItemResponseDto> UpdateAsync(Guid id, ActionItemUpdateDto dto, string user = Creator) =>
        As(user, () => Service().UpdateAsync(id, dto, user, default));

    private async Task SetStatusAsync(Guid id, ActionStatus status)
    {
        var item = await _db.ActionItems.IgnoreQueryFilters().FirstAsync(a => a.Id == id);
        item.Status = status;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    private Task<PagedResult<ActionItemResponseDto>> ListAsync(Action<ActionItemFilterDto> configure, string user = Creator)
    {
        var filter = new ActionItemFilterDto { PageSize = 100 };
        configure(filter);
        return As(user, () => Service().GetAllAsync(filter, default));
    }

    private static string[] Names(ActionItemResponseDto dto) => dto.Tags.Select(t => t.Name).ToArray();

    // ── Save ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Tags_save_cleaned_and_de_duplicated_in_the_items_workspace()
    {
        var created = await CreateAsync(_a, " Urgent ", "board   pack", "URGENT");

        Names(created).Should().Equal("board pack", "Urgent");      // sorted by name, any case
        var rows = await _db.Tags.IgnoreQueryFilters().ToListAsync();
        rows.Should().HaveCount(2).And.OnlyContain(t => t.WorkspaceId == _a && t.CreatedByUserId == Creator);
        rows.Select(t => t.NormalizedName).Should().BeEquivalentTo("URGENT", "BOARD PACK");
        (await As(Creator, () => Service().GetByIdAsync(created.Id, default)))!.Tags.Should().BeEquivalentTo(created.Tags);
    }

    [Fact]
    public async Task A_name_typed_in_another_case_reuses_the_workspace_tag()
    {
        var first  = await CreateAsync(_a, "Urgent");
        var second = await CreateAsync(_a, "urgent");

        second.Tags.Should().ContainSingle().Which.Should().BeEquivalentTo(first.Tags[0]);   // same id, first spelling
        (await _db.Tags.IgnoreQueryFilters().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task The_same_name_in_two_workspaces_is_two_tags()
    {
        var inA = await CreateAsync(_a, "Urgent");
        var inB = await CreateAsync(_b, "Urgent");

        inA.Tags[0].Id.Should().NotBe(inB.Tags[0].Id);
        (await _db.Tags.IgnoreQueryFilters().Where(t => t.NormalizedName == "URGENT").Select(t => t.WorkspaceId).ToListAsync())
            .Should().BeEquivalentTo(new[] { _a, _b });
    }

    [Fact]
    public async Task Update_replaces_null_keeps_and_an_empty_list_clears()
    {
        var item = await CreateAsync(_a, "one", "two");

        (await UpdateAsync(item.Id, new ActionItemUpdateDto { Id = item.Id, Title = "Renamed" })).Tags
            .Select(t => t.Name).Should().Equal("one", "two");                            // null = leave them

        Names(await UpdateAsync(item.Id, new ActionItemUpdateDto { Id = item.Id, Tags = ["two", "three"] }))
            .Should().Equal("three", "two");

        (await UpdateAsync(item.Id, new ActionItemUpdateDto { Id = item.Id, Tags = [] })).Tags.Should().BeEmpty();
        (await _db.ActionItemTags.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await _db.Tags.IgnoreQueryFilters().CountAsync()).Should().Be(3, "unused tags stay as suggestions");
    }

    [Fact]
    public async Task A_broken_rule_is_refused_by_the_service_too()
    {
        var item = await CreateAsync(_a);
        await FluentActions.Invoking(() => UpdateAsync(item.Id, new ActionItemUpdateDto { Id = item.Id, Tags = ["a,b"] }))
            .Should().ThrowAsync<TagRuleException>();
        await FluentActions.Invoking(() => CreateAsync(_a, Enumerable.Range(1, 11).Select(i => $"t{i}").ToArray()))
            .Should().ThrowAsync<TagRuleException>().WithMessage("An action item can have at most 10 tags.");
        (await _db.Tags.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    // ── D1: Done locked, Cancelled editable ────────────────────────────────────

    [Fact]
    public async Task Tags_on_a_Done_item_are_refused_like_every_other_field()
    {
        var item = await CreateAsync(_a, "kept");
        await SetStatusAsync(item.Id, ActionStatus.Done);

        await FluentActions.Invoking(() => UpdateAsync(item.Id, new ActionItemUpdateDto { Id = item.Id, Tags = ["new"] }))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("Completed action items cannot be edited.");
        await FluentActions.Invoking(() => UpdateAsync(item.Id, new ActionItemUpdateDto { Id = item.Id, Tags = [] }))
            .Should().ThrowAsync<InvalidOperationException>();

        (await As(Creator, () => Service().GetByIdAsync(item.Id, default)))!.Tags.Select(t => t.Name).Should().Equal("kept");
    }

    [Fact]
    public async Task Tags_on_a_Cancelled_item_can_be_changed_like_its_other_fields()
    {
        var item = await CreateAsync(_a, "old");
        await SetStatusAsync(item.Id, ActionStatus.Cancelled);

        Names(await UpdateAsync(item.Id, new ActionItemUpdateDto { Id = item.Id, Tags = ["new"] })).Should().Equal("new");
    }

    // ── D2: carried over by name on a workspace move ──────────────────────────

    [Fact]
    public async Task Moving_an_item_carries_its_tags_over_by_name()
    {
        var item   = await CreateAsync(_a, "Urgent", "Q4");
        var inB    = await CreateAsync(_b, "urgent");                  // B already has "urgent"
        var aTags  = await _db.Tags.IgnoreQueryFilters().Where(t => t.WorkspaceId == _a).Select(t => t.Id).ToListAsync();

        var moved = await UpdateAsync(item.Id, new ActionItemUpdateDto { Id = item.Id, WorkspaceId = _b });

        moved.WorkspaceId.Should().Be(_b);
        Names(moved).Should().Equal("Q4", "urgent");                   // B's own spelling of the existing tag
        moved.Tags.Should().Contain(t => t.Id == inB.Tags[0].Id);
        var links = await _db.ActionItemTags.IgnoreQueryFilters().Where(x => x.ActionItemId == item.Id).Include(x => x.Tag).ToListAsync();
        links.Should().HaveCount(2).And.OnlyContain(x => x.Tag.WorkspaceId == _b);
        (await _db.Tags.IgnoreQueryFilters().Where(t => t.WorkspaceId == _a).Select(t => t.Id).ToListAsync())
            .Should().BeEquivalentTo(aTags, "the old workspace keeps its tags as suggestions");
    }

    [Fact]
    public async Task Moving_with_tags_sent_uses_the_sent_names_in_the_new_workspace()
    {
        var item  = await CreateAsync(_a, "old");
        var moved = await UpdateAsync(item.Id, new ActionItemUpdateDto { Id = item.Id, WorkspaceId = _b, Tags = ["fresh"] });

        Names(moved).Should().Equal("fresh");
        (await _db.Tags.IgnoreQueryFilters().SingleAsync(t => t.NormalizedName == "FRESH")).WorkspaceId.Should().Be(_b);
    }

    // ── D3: filter by id, any of ──────────────────────────────────────────────

    [Fact]
    public async Task The_filter_matches_items_with_any_of_the_chosen_tags_by_id()
    {
        var x    = await CreateAsync(_a, "x");
        var y    = await CreateAsync(_a, "y", "x");
        var none = await CreateAsync(_a);
        var xId  = x.Tags[0].Id;
        var yId  = y.Tags.Single(t => t.Name == "y").Id;

        (await ListAsync(f => f.TagIds = [xId])).Items.Select(i => i.Id).Should().BeEquivalentTo(new[] { x.Id, y.Id });
        (await ListAsync(f => f.TagIds = [yId])).Items.Select(i => i.Id).Should().Equal(y.Id);
        (await ListAsync(f => f.TagIds = [xId, yId])).TotalCount.Should().Be(2, "an item with both tags is listed once");
        (await ListAsync(f => { })).TotalCount.Should().Be(3);
        none.Tags.Should().BeEmpty();
    }

    [Fact]
    public async Task The_same_name_in_another_workspace_never_matches()
    {
        var inA = await CreateAsync(_a, "Urgent");
        var inB = await CreateAsync(_b, "Urgent");

        (await ListAsync(f => f.TagIds = [inA.Tags[0].Id])).Items.Select(i => i.Id).Should().Equal(inA.Id);
        (await ListAsync(f => { f.TagIds = [inB.Tags[0].Id]; f.WorkspaceId = _a; })).Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Tags_do_not_change_any_other_list_or_count()
    {
        await CreateAsync(_a, "a", "b", "c");
        await CreateAsync(_a);

        // No join on the tag tables without tagIds: every item once, in the same order.
        var all = await ListAsync(f => f.OpenOnly = true);
        all.TotalCount.Should().Be(2);
        all.Items.Should().HaveCount(2);
    }

    // ── Isolation ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Suggestions_come_from_the_one_workspace_case_insensitively_with_live_counts()
    {
        var one = await CreateAsync(_a, "Budget", "Board pack");
        await CreateAsync(_a, "budget");
        await CreateAsync(_b, "Budget B", "Other");
        var deleted = await _db.ActionItems.FirstAsync(a => a.Id == one.Id);
        deleted.IsDeleted = true;
        await _db.SaveChangesAsync();

        var all = await As(Creator, () => Tags().GetWorkspaceTagsAsync(_a, Creator, null, 100, default));
        all.Select(t => (t.Name, t.ItemCount)).Should().Equal(("Board pack", 0), ("Budget", 1));

        var search = await As(Creator, () => Tags().GetWorkspaceTagsAsync(_a, Creator, "  bUd ", 100, default));
        search.Select(t => t.Name).Should().Equal("Budget");
        (await As(Creator, () => Tags().GetWorkspaceTagsAsync(_a, Creator, null, 1, default))).Should().HaveCount(1);
    }

    [Fact]
    public async Task A_private_workspaces_tags_are_invisible_to_non_members()
    {
        var hidden = await CreateAsync(_private, "Secret plan");

        // Suggestions: 404 for the outsider, also when their role sees every workspace.
        await FluentActions.Invoking(() => As(Outsider, () => Tags().GetWorkspaceTagsAsync(_private, Outsider, null, 100, default)))
            .Should().ThrowAsync<KeyNotFoundException>();
        (await As(Assignee, () => Tags().GetWorkspaceTagsAsync(_private, Assignee, null, 100, default)))
            .Select(t => t.Name).Should().Equal("Secret plan");

        // The tag rows themselves are filtered.
        (await As(Outsider, () => _db.Tags.Select(t => t.Name).ToListAsync())).Should().NotContain("Secret plan");
        (await As(Outsider, () => _db.ActionItemTags.CountAsync())).Should().Be(0);

        // The filter by its id finds nothing for the outsider; the member finds the item.
        var tagId = hidden.Tags[0].Id;
        (await ListAsync(f => f.TagIds = [tagId], Outsider)).Items.Should().BeEmpty();
        (await ListAsync(f => f.TagIds = [tagId], Assignee)).Items.Select(i => i.Id).Should().Equal(hidden.Id);

        // My Actions options: an outsider assigned to a hidden item still sees nothing of it.
        var item = await _db.ActionItems.IgnoreQueryFilters().FirstAsync(a => a.Id == hidden.Id);
        _db.ActionItemAssignees.Add(new ActionItemAssignee { ActionItemId = item.Id, UserId = Outsider });
        await _db.SaveChangesAsync();
        (await As(Outsider, () => Tags().GetMyActionTagsAsync(Outsider, default))).Should().BeEmpty();
    }

    [Fact]
    public async Task Suggestions_follow_the_callers_workspaces()
    {
        await CreateAsync(_b, "beta-only");
        _visibleWorkspaces = [_a];                                    // a regular user who is in Alpha only

        await FluentActions.Invoking(() => As(Outsider, () => Tags().GetWorkspaceTagsAsync(_b, Outsider, null, 100, default)))
            .Should().ThrowAsync<KeyNotFoundException>();
        (await As(Outsider, () => Tags().GetWorkspaceTagsAsync(_a, Outsider, null, 100, default))).Should().BeEmpty();
        await FluentActions.Invoking(() => As(Outsider, () => Tags().GetWorkspaceTagsAsync(Guid.NewGuid(), Outsider, null, 100, default)))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task My_actions_options_are_my_items_tags_labelled_with_their_workspace()
    {
        await CreateAsync(_a, "Urgent", "Alpha only");
        await CreateAsync(_a, "urgent");
        await CreateAsync(_b, "Urgent");

        var options = await As(Assignee, () => Tags().GetMyActionTagsAsync(Assignee, default));

        options.Select(o => (o.WorkspaceTitle, o.Name, o.ItemCount)).Should().Equal(
            ("Alpha", "Alpha only", 1), ("Alpha", "Urgent", 2), ("Beta", "Urgent", 1));
        options.Select(o => o.Id).Should().OnlyHaveUniqueItems();
        (await As(Outsider, () => Tags().GetMyActionTagsAsync(Outsider, default))).Should().BeEmpty("nothing is assigned to them");
    }

    [Fact]
    public async Task A_stray_link_to_another_workspaces_tag_is_never_shown()
    {
        var item  = await CreateAsync(_a);
        var other = await CreateAsync(_b, "beta-name");
        _db.ActionItemTags.Add(new ActionItemTag { ActionItemId = item.Id, TagId = other.Tags[0].Id });   // bypassing the service
        await _db.SaveChangesAsync();

        (await As(Creator, () => Service().GetByIdAsync(item.Id, default)))!.Tags.Should().BeEmpty();
        (await ListAsync(f => f.WorkspaceId = _a)).Items.Single().Tags.Should().BeEmpty();
    }

    [Fact]
    public async Task Deleted_items_in_an_include_deleted_list_keep_their_tags()
    {
        var item = await CreateAsync(_a, "kept");
        var row  = await _db.ActionItems.FirstAsync(a => a.Id == item.Id);
        row.IsDeleted = true;
        await _db.SaveChangesAsync();

        (await ListAsync(f => { f.IncludeDeleted = true; f.WorkspaceId = _a; })).Items.Single().Tags
            .Select(t => t.Name).Should().Equal("kept");
    }

    [Fact]
    public async Task Replace_refuses_a_tag_of_another_workspace()
    {
        var other = await CreateAsync(_b, "beta");
        var item  = await _db.ActionItems.AsNoTracking().FirstAsync(a => a.WorkspaceId == _b);
        item.WorkspaceId = _a;
        var tag = await _db.Tags.IgnoreQueryFilters().AsNoTracking().FirstAsync(t => t.Id == other.Tags[0].Id);

        await FluentActions.Invoking(() => Tags().ReplaceItemTagsAsync(item, [tag]))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    // ── D7: migration pending ─────────────────────────────────────────────────

    [Fact]
    public async Task While_the_tag_tables_are_missing_items_read_and_save_without_tags()
    {
        var tagged = await CreateAsync(_a, "before");
        _schema.Available = false;

        // Reads work and carry no tags.
        (await As(Creator, () => Service().GetByIdAsync(tagged.Id, default)))!.Tags.Should().BeEmpty();
        (await ListAsync(f => { })).TotalCount.Should().Be(1);

        // Saves without tags work — also an empty list and a workspace move.
        var plain = await CreateAsync(_a);
        (await UpdateAsync(plain.Id, new ActionItemUpdateDto { Id = plain.Id, Title = "x", Tags = [] })).Title.Should().Be("x");
        (await UpdateAsync(plain.Id, new ActionItemUpdateDto { Id = plain.Id, WorkspaceId = _b })).WorkspaceId.Should().Be(_b);

        // Saves with tags and the tag filter: 503.
        await FluentActions.Invoking(() => CreateAsync(_a, "x")).Should().ThrowAsync<TagsUnavailableException>();
        await FluentActions.Invoking(() => UpdateAsync(plain.Id, new ActionItemUpdateDto { Id = plain.Id, Tags = ["x"] }))
            .Should().ThrowAsync<TagsUnavailableException>();
        await FluentActions.Invoking(() => ListAsync(f => f.TagIds = [tagged.Tags[0].Id]))
            .Should().ThrowAsync<TagsUnavailableException>()
            .WithMessage("Tags are not available until the database update for tags (Add_ActionItemTags) has been applied.");

        // Suggestions and options are empty.
        (await As(Creator, () => Tags().GetWorkspaceTagsAsync(_a, Creator, null, 100, default))).Should().BeEmpty();
        (await As(Assignee, () => Tags().GetMyActionTagsAsync(Assignee, default))).Should().BeEmpty();

        // Back on once the tables exist.
        _schema.Available = true;
        (await As(Creator, () => Service().GetByIdAsync(tagged.Id, default)))!.Tags.Select(t => t.Name).Should().Equal("before");
    }
}
