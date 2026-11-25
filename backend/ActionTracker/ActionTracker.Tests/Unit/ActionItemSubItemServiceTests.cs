using ActionTracker.Application.Common;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.ActionItems.DTOs;
using ActionTracker.Application.Features.ActionItems.Services;
using ActionTracker.Application.Features.ActionItemSubItems;
using ActionTracker.Application.Features.ActionItemSubItems.DTOs;
using ActionTracker.Application.Features.ActionItemSubItems.Services;
using ActionTracker.Application.Features.Calendars;
using ActionTracker.Application.Features.Calendars.Services;
using ActionTracker.Application.Features.Dashboard.Services;
using ActionTracker.Application.Features.Notifications;
using ActionTracker.Application.Features.Notifications.DTOs;
using ActionTracker.Application.Features.Projects.Services;
using ActionTracker.Application.Features.Workflow.Interfaces;
using ActionTracker.Application.Features.Workspaces.Interfaces;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using ActionTracker.Infrastructure.Data;
using ActionTracker.Infrastructure.Data.Seeders;
using ActionTracker.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ActionTracker.Tests.Unit.Tags;

namespace ActionTracker.Tests.Unit;

/// <summary>
/// WO-2026-11 A-2: sub-items on action items — the service rules (dates inside
/// the parent's planned dates, working days, assignee pool, Done / Cancelled
/// parent, who may tick a sub-item), delete / restore with the parent and its
/// project, private-workspace visibility (incl. the IgnoreQueryFilters paths),
/// notifications, the My Actions query, and that sub-items never change an
/// action-item count. The seeded "KU Standard" calendar (Mon–Fri) applies.
/// </summary>
public class ActionItemSubItemServiceTests : IDisposable
{
    private const string Editor   = "user-editor";    // workspace admin; creates the item
    private const string Alice    = "user-alice";     // workspace member
    private const string Bob      = "user-bob";       // workspace member
    private const string Outsider = "user-outsider";  // in no workspace list

    // 2030-06-03 is a Monday; 06-08 / 06-09 are a weekend.
    private static readonly DateTime Monday    = D(2030, 6, 3);
    private static readonly DateTime Tuesday   = D(2030, 6, 4);
    private static readonly DateTime Friday    = D(2030, 6, 7);
    private static readonly DateTime Saturday  = D(2030, 6, 8);
    private static readonly DateTime NextMon   = D(2030, 6, 10);
    private static readonly DateTime ParentEnd = D(2030, 6, 28);   // Friday

    private readonly AppDbContext _db;
    private string? _currentUserId = Editor;
    private readonly Guid _workspaceId = Guid.NewGuid();
    private readonly WorkCalendarService _calendars;
    private readonly Mock<IWorkspaceService> _workspaces = new();
    private readonly Mock<IWorkflowNotificationHelper> _notify = new();

    public ActionItemSubItemServiceTests()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(() => _currentUserId);
        _db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            currentUser.Object);

        var access = new Mock<IWorkspaceAccessResolver>();
        access.Setup(r => r.GetVisibleWorkspaceIdsAsync(It.IsAny<string>())).ReturnsAsync((List<Guid>?)null);
        _calendars = new WorkCalendarService(_db, access.Object, new WorkCalendarCache(), NullLogger<WorkCalendarService>.Instance);

        // The workspace's assignee pool: admins + members + PMs (here Editor, Alice, Bob).
        _workspaces.Setup(w => w.GetAssignableUsersAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new[] { Editor, Alice, Bob }.Select(id => new AssignableUserDto { UserId = id, UserName = id }).ToList());

        _db.Users.AddRange(User(Editor, "Erin", "Editor"), User(Alice, "Alice", "Smith"),
                           User(Bob, "Bob", "Jones"), User(Outsider, "Olly", "Outsider"));
        var workspace = new Workspace
        {
            Id = _workspaceId, Title = "Sub-item workspace", OrganizationUnit = "Org", CreatedAt = DateTime.UtcNow,
        };
        workspace.Admins.Add(new WorkspaceAdmin { WorkspaceId = _workspaceId, AdminUserId = Editor, AdminUserName = "Erin Editor" });
        workspace.Members.Add(new WorkspaceMember { WorkspaceId = _workspaceId, UserId = Alice, UserName = "Alice Smith" });
        workspace.Members.Add(new WorkspaceMember { WorkspaceId = _workspaceId, UserId = Bob, UserName = "Bob Jones" });
        _db.Workspaces.Add(workspace);
        _db.SaveChanges();
        WorkCalendarSeeder.SeedAsync(_db, NullLogger.Instance).GetAwaiter().GetResult();
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static DateTime D(int y, int m, int d) => new(y, m, d, 0, 0, 0, DateTimeKind.Utc);

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

    private ActionItemSubItemService Service() =>
        new(_db, _calendars, _workspaces.Object, ScopeFactory(), NullLogger<ActionItemSubItemService>.Instance);

    private static IOptions<AppSettings> Settings() =>
        Options.Create(new AppSettings { FrontendBaseUrl = "http://localhost:4200" });

    private ActionItemService ActionItems() =>
        new(_db, NullLogger<ActionItemService>.Instance, Mock.Of<IEmailSender>(), Mock.Of<INotificationService>(),
            Settings(), Mock.Of<IServiceScopeFactory>(), _calendars, TagTestHelpers.NewTagService(_db));

    private async Task<ActionItem> AddParentAsync(
        ActionStatus status = ActionStatus.ToDo, DateTime? start = null, bool noStart = false,
        Guid? projectId = null, params string[] assignees)
    {
        var item = new ActionItem
        {
            Id = Guid.NewGuid(), ActionId = "ACT-" + Guid.NewGuid().ToString("N")[..5], Title = "Parent",
            Description = "", WorkspaceId = _workspaceId, ProjectId = projectId, IsStandalone = projectId is null,
            Status = status, Priority = ActionPriority.Medium,
            StartDate = noStart ? null : start ?? Monday, DueDate = ParentEnd,
            CreatedByUserId = Editor, CreatedAt = DateTime.UtcNow,
        };
        foreach (var a in assignees.DefaultIfEmpty(Editor))
            item.Assignees.Add(new ActionItemAssignee { ActionItemId = item.Id, UserId = a });
        _db.ActionItems.Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    /// <summary>A sub-item written straight to the database (bypasses the service rules).</summary>
    private async Task<ActionItemSubItem> AddSubItemAsync(
        Guid parentId, DateTime end, DateTime? start = null, int order = 1, bool isDone = false, params string[] assignees)
    {
        var s = new ActionItemSubItem
        {
            Id = Guid.NewGuid(), ActionItemId = parentId, Title = "Seeded " + order, StartDate = start, EndDate = end,
            SortOrder = order, IsDone = isDone, CreatedAt = DateTime.UtcNow, CreatedByUserId = Editor,
        };
        foreach (var a in assignees)
            s.Assignees.Add(new ActionItemSubItemAssignee { SubItemId = s.Id, UserId = a });
        _db.ActionItemSubItems.Add(s);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return s;
    }

    private static CreateActionItemSubItemDto NewDto(
        DateTime end, DateTime? start = null, string title = "Step", params string[] assignees) => new()
    {
        Title = title, Description = "  details  ", StartDate = start, EndDate = end, AssigneeIds = assignees.ToList(),
    };

    private static UpdateActionItemSubItemDto UpdateDto(ActionItemSubItem s, string? title = null,
        DateTime? end = null, DateTime? start = null, bool keepStart = true, params string[] assignees) => new()
    {
        Title = title ?? s.Title, Description = s.Description, StartDate = keepStart ? start ?? s.StartDate : start,
        EndDate = end ?? s.EndDate, AssigneeIds = assignees.ToList(),
    };

    private ActionItemSubItem Raw(Guid id)
    {
        _db.ChangeTracker.Clear();
        return _db.ActionItemSubItems.IgnoreQueryFilters().Include(s => s.Assignees).First(s => s.Id == id);
    }

    /// <summary>Notifications run fire-and-forget; wait for the mocked helper to be reached.</summary>
    private async Task WaitForNotificationAsync(int count = 1)
    {
        for (var i = 0; i < 100 && _notify.Invocations.Count < count; i++)
            await Task.Delay(20);
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_stores_the_sub_item_at_the_end_with_its_assignees()
    {
        var parent = await AddParentAsync();
        var service = Service();

        var first  = await service.CreateAsync(parent.Id, NewDto(Friday, Monday, "  First  ", Alice), Editor);
        var second = await service.CreateAsync(parent.Id, NewDto(NextMon, null, "Second", Alice, Bob, Alice), Editor);

        first.Title.Should().Be("First");
        first.Description.Should().Be("details");
        first.SortOrder.Should().Be(1);
        first.StartDate.Should().Be(Monday);
        first.EndDate.Should().Be(Friday);
        first.IsDone.Should().BeFalse();
        first.Assignees.Select(a => a.UserId).Should().Equal(Alice);
        second.SortOrder.Should().Be(2);
        second.Assignees.Select(a => a.UserId).Should().BeEquivalentTo(new[] { Alice, Bob });

        var list = await service.GetForActionItemAsync(parent.Id);
        list.Items.Select(i => i.Title).Should().Equal("First", "Second");
        list.TotalCount.Should().Be(2);
        list.DoneCount.Should().Be(0);
        list.ParentAcceptsChanges.Should().BeTrue();
        list.ParentStartDate.Should().Be(Monday);
        list.ParentDueDate.Should().Be(ParentEnd);
    }

    [Theory]
    [InlineData(2030, 6, 28, 2030, 7, 1, "Sub-item end 2030-07-01 is after the action item's planned end 2030-06-28.")]
    [InlineData(2030, 5, 31, 2030, 6, 4, "Sub-item start 2030-05-31 is before the action item's planned start 2030-06-03.")]
    [InlineData(2030, 6, 11, 2030, 6, 10, "Sub-item end 2030-06-10 is before the sub-item start 2030-06-11.")]
    public async Task Create_refuses_dates_outside_the_parent_and_saves_nothing(
        int sy, int sm, int sd, int ey, int em, int ed, string message)
    {
        var parent = await AddParentAsync();

        var act = () => Service().CreateAsync(parent.Id, NewDto(D(ey, em, ed), D(sy, sm, sd)), Editor);

        (await act.Should().ThrowAsync<ArgumentException>()).Which.Message.Should().Be(message);
        _db.ActionItemSubItems.IgnoreQueryFilters().Should().BeEmpty();
    }

    [Fact]
    public async Task Create_parent_without_planned_start_accepts_any_start_up_to_its_end()
    {
        var parent = await AddParentAsync(noStart: true);

        var created = await Service().CreateAsync(parent.Id, NewDto(ParentEnd, D(2030, 1, 7)), Editor);

        created.StartDate.Should().Be(D(2030, 1, 7));
    }

    [Fact]
    public async Task Create_refuses_a_non_working_day_with_the_calendar_message()
    {
        var parent = await AddParentAsync();

        var act = () => Service().CreateAsync(parent.Id, NewDto(Saturday, Monday), Editor);

        var ex = (await act.Should().ThrowAsync<NonWorkingDateException>()).Which;
        ex.Message.Should().Contain("Sub-item end 2030-06-08 (Saturday) is not a working day")
            .And.Contain("Next working day: 2030-06-10 (Monday).");
        _db.ActionItemSubItems.IgnoreQueryFilters().Should().BeEmpty();
    }

    [Fact]
    public async Task Create_refuses_an_assignee_outside_the_workspace_pool()
    {
        var parent = await AddParentAsync();

        var act = () => Service().CreateAsync(parent.Id, NewDto(Friday, null, "Step", Alice, Outsider), Editor);

        (await act.Should().ThrowAsync<ArgumentException>()).Which.Message.Should().Contain("Not allowed: Olly Outsider.");
        _db.ActionItemSubItems.IgnoreQueryFilters().Should().BeEmpty();
    }

    [Fact]
    public async Task Create_on_a_missing_or_deleted_parent_is_not_found()
    {
        var parent = await AddParentAsync();
        parent.IsDeleted = true;
        await _db.SaveChangesAsync();

        await FluentActions.Invoking(() => Service().CreateAsync(parent.Id, NewDto(Friday), Editor))
            .Should().ThrowAsync<KeyNotFoundException>();
        await FluentActions.Invoking(() => Service().GetForActionItemAsync(Guid.NewGuid()))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Done / Cancelled parent ───────────────────────────────────────────────

    [Theory]
    [InlineData(ActionStatus.Done)]
    [InlineData(ActionStatus.Cancelled)]
    public async Task Every_write_on_a_Done_or_Cancelled_parent_is_refused(ActionStatus status)
    {
        var parent = await AddParentAsync(status);
        var sub = await AddSubItemAsync(parent.Id, Friday, null, 1, false, Alice);
        var service = Service();

        await FluentActions.Invoking(() => service.CreateAsync(parent.Id, NewDto(Friday), Editor))
            .Should().ThrowAsync<SubItemParentLockedException>();
        await FluentActions.Invoking(() => service.UpdateAsync(parent.Id, sub.Id, UpdateDto(sub, "Renamed"), Editor))
            .Should().ThrowAsync<SubItemParentLockedException>();
        await FluentActions.Invoking(() => service.DeleteAsync(parent.Id, sub.Id, Editor))
            .Should().ThrowAsync<SubItemParentLockedException>();
        await FluentActions.Invoking(() => service.SetDoneAsync(parent.Id, sub.Id, true, Editor, canEdit: true))
            .Should().ThrowAsync<SubItemParentLockedException>();
        await FluentActions.Invoking(() => service.SetDoneAsync(parent.Id, sub.Id, true, Alice, canEdit: false))
            .Should().ThrowAsync<SubItemParentLockedException>();
        await FluentActions.Invoking(() => service.ReorderAsync(parent.Id, new[] { sub.Id }, Editor))
            .Should().ThrowAsync<SubItemParentLockedException>();

        var stored = Raw(sub.Id);
        stored.Title.Should().Be("Seeded 1");
        stored.IsDone.Should().BeFalse();
        stored.IsDeleted.Should().BeFalse();

        var list = await service.GetForActionItemAsync(parent.Id);   // reads still work
        list.ParentAcceptsChanges.Should().BeFalse();
        list.Items.Should().ContainSingle();
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_with_unchanged_dates_does_not_apply_the_date_rules()
    {
        // A sub-item already outside the parent's range and on a Saturday
        // (e.g. the parent's dates moved later) can still be renamed.
        var parent = await AddParentAsync();
        var sub = await AddSubItemAsync(parent.Id, D(2030, 7, 6), D(2030, 5, 30));

        var updated = await Service().UpdateAsync(parent.Id, sub.Id, UpdateDto(sub, "Renamed"), Editor);

        updated.Title.Should().Be("Renamed");
        updated.IsOutsideParentDates.Should().BeTrue();
        Raw(sub.Id).UpdatedByUserId.Should().Be(Editor);
    }

    [Fact]
    public async Task Update_with_a_changed_date_checks_the_range_and_the_calendar()
    {
        var parent = await AddParentAsync();
        var sub = await AddSubItemAsync(parent.Id, Friday, Monday);
        var service = Service();

        await FluentActions.Invoking(() => service.UpdateAsync(parent.Id, sub.Id, UpdateDto(sub, end: D(2030, 7, 1)), Editor))
            .Should().ThrowAsync<ArgumentException>().WithMessage("Sub-item end 2030-07-01 is after*");
        await FluentActions.Invoking(() => service.UpdateAsync(parent.Id, sub.Id, UpdateDto(sub, end: Saturday), Editor))
            .Should().ThrowAsync<NonWorkingDateException>();

        var updated = await service.UpdateAsync(parent.Id, sub.Id, UpdateDto(sub, end: NextMon, start: Tuesday), Editor);
        updated.StartDate.Should().Be(Tuesday);
        updated.EndDate.Should().Be(NextMon);

        // Removing the start date is a change too, and allowed.
        var noStart = await service.UpdateAsync(parent.Id, sub.Id, UpdateDto(sub, end: NextMon, start: null, keepStart: false), Editor);
        noStart.StartDate.Should().BeNull();
    }

    [Fact]
    public async Task Update_keeps_an_assignee_who_left_the_pool_but_refuses_a_new_outsider()
    {
        var parent = await AddParentAsync();
        // Outsider was assigned before leaving the workspace.
        var sub = await AddSubItemAsync(parent.Id, Friday, null, 1, false, Outsider, Alice);
        var service = Service();

        var kept = await service.UpdateAsync(parent.Id, sub.Id, UpdateDto(sub, assignees: new[] { Outsider, Bob }), Editor);
        kept.Assignees.Select(a => a.UserId).Should().BeEquivalentTo(new[] { Outsider, Bob });

        var removed = await service.UpdateAsync(parent.Id, sub.Id, UpdateDto(sub, assignees: new[] { Bob }), Editor);
        removed.Assignees.Select(a => a.UserId).Should().Equal(Bob);

        await FluentActions.Invoking(() => service.UpdateAsync(parent.Id, sub.Id, UpdateDto(sub, assignees: new[] { Bob, Outsider }), Editor))
            .Should().ThrowAsync<ArgumentException>().WithMessage("*Not allowed: Olly Outsider.*");
    }

    // ── Done flag ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetDone_records_and_clears_the_date_and_user()
    {
        var parent = await AddParentAsync();
        var sub = await AddSubItemAsync(parent.Id, Friday, null, 1, false, Alice);
        var service = Service();

        var done = await service.SetDoneAsync(parent.Id, sub.Id, true, Editor, canEdit: true);
        done.IsDone.Should().BeTrue();
        done.DoneByUserId.Should().Be(Editor);
        done.DoneByName.Should().Be("Erin Editor");
        done.DoneAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        (await service.GetForActionItemAsync(parent.Id)).DoneCount.Should().Be(1);

        var undone = await service.SetDoneAsync(parent.Id, sub.Id, false, Editor, canEdit: true);
        undone.IsDone.Should().BeFalse();
        undone.DoneAt.Should().BeNull();
        undone.DoneByUserId.Should().BeNull();

        // The parent's progress is not recalculated (Q10).
        _db.ChangeTracker.Clear();
        (await _db.ActionItems.FirstAsync(a => a.Id == parent.Id)).Progress.Should().Be(0);
    }

    [Fact]
    public async Task SetDone_is_allowed_for_the_sub_item_assignee_without_edit_permission_only()
    {
        var parent = await AddParentAsync();
        var sub = await AddSubItemAsync(parent.Id, Friday, null, 1, false, Alice);
        var service = Service();

        _currentUserId = Bob;
        await FluentActions.Invoking(() => service.SetDoneAsync(parent.Id, sub.Id, true, Bob, canEdit: false))
            .Should().ThrowAsync<UnauthorizedAccessException>();
        Raw(sub.Id).IsDone.Should().BeFalse();

        _currentUserId = Alice;
        (await service.SetDoneAsync(parent.Id, sub.Id, true, Alice, canEdit: false)).IsDone.Should().BeTrue();
        (await service.SetDoneAsync(parent.Id, sub.Id, false, Alice, canEdit: false)).IsDone.Should().BeFalse();
    }

    // ── Reorder / delete ──────────────────────────────────────────────────────

    [Fact]
    public async Task Reorder_needs_every_sub_item_once()
    {
        var parent = await AddParentAsync();
        var a = await AddSubItemAsync(parent.Id, Friday, null, 1);
        var b = await AddSubItemAsync(parent.Id, Friday, null, 2);
        var c = await AddSubItemAsync(parent.Id, Friday, null, 3);
        var service = Service();

        var list = await service.ReorderAsync(parent.Id, new[] { c.Id, a.Id, b.Id }, Editor);
        list.Items.Select(i => i.Id).Should().Equal(c.Id, a.Id, b.Id);
        list.Items.Select(i => i.SortOrder).Should().Equal(1, 2, 3);

        await FluentActions.Invoking(() => service.ReorderAsync(parent.Id, new[] { c.Id, a.Id }, Editor))
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => service.ReorderAsync(parent.Id, new[] { c.Id, a.Id, Guid.NewGuid() }, Editor))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Delete_soft_deletes_and_keeps_the_order_dense()
    {
        var parent = await AddParentAsync();
        var a = await AddSubItemAsync(parent.Id, Friday, null, 1);
        var b = await AddSubItemAsync(parent.Id, Friday, null, 2);
        var c = await AddSubItemAsync(parent.Id, Friday, null, 3);
        var service = Service();

        await service.DeleteAsync(parent.Id, b.Id, Editor);

        var stored = Raw(b.Id);
        stored.IsDeleted.Should().BeTrue();
        stored.DeletedWithParent.Should().BeFalse();
        var list = await service.GetForActionItemAsync(parent.Id);
        list.Items.Select(i => i.Id).Should().Equal(a.Id, c.Id);
        list.Items.Select(i => i.SortOrder).Should().Equal(1, 2);
        await FluentActions.Invoking(() => service.DeleteAsync(parent.Id, b.Id, Editor)).Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Delete / restore with the parent (D2) ────────────────────────────────

    [Fact]
    public async Task Deleting_the_parent_soft_deletes_its_sub_items_and_restoring_brings_back_only_those()
    {
        var parent = await AddParentAsync();
        var kept     = await AddSubItemAsync(parent.Id, Friday, null, 1);
        var deleted  = await AddSubItemAsync(parent.Id, Friday, null, 2);
        await Service().DeleteAsync(parent.Id, deleted.Id, Editor);   // deleted on its own first

        await ActionItems().DeleteAsync(parent.Id, CancellationToken.None);

        Raw(kept.Id).Should().Match<ActionItemSubItem>(s => s.IsDeleted && s.DeletedWithParent);
        Raw(deleted.Id).Should().Match<ActionItemSubItem>(s => s.IsDeleted && !s.DeletedWithParent);
        _db.ActionItemSubItems.Should().BeEmpty();

        await ActionItems().RestoreAsync(parent.Id, CancellationToken.None);

        Raw(kept.Id).Should().Match<ActionItemSubItem>(s => !s.IsDeleted && !s.DeletedWithParent);
        Raw(deleted.Id).IsDeleted.Should().BeTrue();
        (await Service().GetForActionItemAsync(parent.Id)).Items.Select(i => i.Id).Should().Equal(kept.Id);
    }

    [Fact]
    public async Task Deleting_and_restoring_the_project_cascades_to_sub_items()
    {
        var project = new Project
        {
            Id = Guid.NewGuid(), ProjectCode = "PRJ-A2", Name = "A-2", WorkspaceId = _workspaceId,
            ProjectType = ProjectType.Operational, Status = ProjectStatus.Draft, Priority = ActionPriority.Medium,
            ProjectManagerUserId = Editor, PlannedStartDate = Monday, PlannedEndDate = ParentEnd, CreatedAt = DateTime.UtcNow,
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        var parent = await AddParentAsync(projectId: project.Id);
        var sub = await AddSubItemAsync(parent.Id, Friday);
        var projects = new ProjectService(_db, NullLogger<ProjectService>.Instance, Mock.Of<IEmailSender>(),
            Mock.Of<INotificationService>(), Settings(), Mock.Of<IServiceScopeFactory>(), _calendars);

        await projects.DeleteAsync(project.Id, CancellationToken.None);
        Raw(sub.Id).Should().Match<ActionItemSubItem>(s => s.IsDeleted && s.DeletedWithParent);

        await projects.RestoreAsync(project.Id, CancellationToken.None);
        Raw(sub.Id).Should().Match<ActionItemSubItem>(s => !s.IsDeleted && !s.DeletedWithParent);
    }

    // ── Private workspaces ────────────────────────────────────────────────────

    [Fact]
    public async Task Private_workspace_sub_items_and_assignees_are_invisible_to_non_members()
    {
        var parent = await AddParentAsync();
        var sub = await AddSubItemAsync(parent.Id, Friday, null, 1, false, Alice);
        var ws = await _db.Workspaces.FirstAsync(w => w.Id == _workspaceId);
        ws.IsPrivate = true;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        var service = Service();

        // A member sees them.
        _currentUserId = Alice;
        (await service.GetForActionItemAsync(parent.Id)).Items.Should().ContainSingle();
        _db.ActionItemSubItemAssignees.Should().ContainSingle();

        // A non-member sees nothing, through every read and write.
        _currentUserId = Outsider;
        _db.ActionItemSubItems.Should().BeEmpty();
        _db.ActionItemSubItemAssignees.Should().BeEmpty();
        await FluentActions.Invoking(() => service.GetForActionItemAsync(parent.Id)).Should().ThrowAsync<KeyNotFoundException>();
        await FluentActions.Invoking(() => service.CreateAsync(parent.Id, NewDto(Friday), Outsider)).Should().ThrowAsync<KeyNotFoundException>();
        await FluentActions.Invoking(() => service.UpdateAsync(parent.Id, sub.Id, UpdateDto(sub, "x"), Outsider)).Should().ThrowAsync<KeyNotFoundException>();
        await FluentActions.Invoking(() => service.SetDoneAsync(parent.Id, sub.Id, true, Outsider, canEdit: true)).Should().ThrowAsync<KeyNotFoundException>();
        await FluentActions.Invoking(() => service.DeleteAsync(parent.Id, sub.Id, Outsider)).Should().ThrowAsync<KeyNotFoundException>();
        await FluentActions.Invoking(() => service.ReorderAsync(parent.Id, new[] { sub.Id }, Outsider)).Should().ThrowAsync<KeyNotFoundException>();

        // ...including the IgnoreQueryFilters paths, which re-apply the rule.
        _db.ActionItemSubItems.IgnoreQueryFilters().WhereWorkspaceVisibleTo(Outsider).Should().BeEmpty();
        (await ActionItemSubItemCascade.DeleteWithParentsAsync(_db, new[] { parent.Id }, CancellationToken.None)).Should().Be(0);
        _db.ChangeTracker.Clear();

        // ...and the counts on the item lists.
        var items = await ActionItems().GetAllAsync(new ActionItemFilterDto { PageSize = 100 }, CancellationToken.None);
        items.Items.Should().BeEmpty();
        Raw(sub.Id).IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Sub_items_of_a_private_workspace_parent_are_not_listed_for_an_assignee_who_left_it()
    {
        var parent = await AddParentAsync();
        await AddSubItemAsync(parent.Id, Friday, null, 1, false, Outsider);
        var ws = await _db.Workspaces.FirstAsync(w => w.Id == _workspaceId);
        ws.IsPrivate = true;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        _currentUserId = Outsider;
        var mine = await ActionItems().GetAssignedViaSubItemsAsync(Outsider, new ActionItemFilterDto(), CancellationToken.None);

        mine.TotalCount.Should().Be(0);
    }

    // ── Dates moved under the sub-items (D3) ──────────────────────────────────

    [Fact]
    public async Task A_parent_date_change_is_not_blocked_and_the_list_flags_sub_items_left_outside()
    {
        var parent = await AddParentAsync();
        var inside  = await AddSubItemAsync(parent.Id, Friday, Monday, 1);
        var outside = await AddSubItemAsync(parent.Id, D(2030, 6, 27), D(2030, 6, 24), 2);

        // Project-less item: the PUT turns the date change into a request; apply it as an approval would.
        await ActionItems().ApplyApprovedDateChangeAsync(parent.Id, Monday, D(2030, 6, 14));

        var list = await Service().GetForActionItemAsync(parent.Id);
        list.ParentDueDate.Should().Be(D(2030, 6, 14));
        list.Items.Single(i => i.Id == inside.Id).IsOutsideParentDates.Should().BeFalse();
        list.Items.Single(i => i.Id == outside.Id).IsOutsideParentDates.Should().BeTrue();
    }

    // ── Notifications ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Assignment_notifies_only_users_newly_added_by_someone_else()
    {
        var parent = await AddParentAsync();
        var service = Service();

        var created = await service.CreateAsync(parent.Id, NewDto(Friday, null, "Step", Alice, Editor), Editor);
        await WaitForNotificationAsync();
        _notify.Verify(n => n.NotifySubItemAssignedAsync(created.Id,
            It.Is<IReadOnlyCollection<string>>(ids => ids.OrderBy(x => x).SequenceEqual(new[] { Alice, Editor }.OrderBy(x => x))),
            Editor), Times.Once);

        // Only the actor added: nothing to send. Re-saving the same people: nothing new.
        _notify.Invocations.Clear();
        await service.CreateAsync(parent.Id, NewDto(Friday, null, "Mine", Editor), Editor);
        var stored = Raw(created.Id);
        await service.UpdateAsync(parent.Id, created.Id, UpdateDto(stored, "Renamed", assignees: new[] { Alice, Editor }), Editor);
        await Task.Delay(200);
        _notify.Invocations.Should().BeEmpty();

        // Bob added on update: only Bob is passed on.
        await service.UpdateAsync(parent.Id, created.Id, UpdateDto(stored, assignees: new[] { Alice, Editor, Bob }), Editor);
        await WaitForNotificationAsync();
        _notify.Verify(n => n.NotifySubItemAssignedAsync(created.Id,
            It.Is<IReadOnlyCollection<string>>(ids => ids.SequenceEqual(new[] { Bob })), Editor), Times.Once);
    }

    [Fact]
    public async Task Completion_notifies_when_ticked_but_not_when_unticked()
    {
        var parent = await AddParentAsync();
        var sub = await AddSubItemAsync(parent.Id, Friday, null, 1, false, Alice);
        var service = Service();

        await service.SetDoneAsync(parent.Id, sub.Id, true, Alice, canEdit: false);
        await WaitForNotificationAsync();
        _notify.Verify(n => n.NotifySubItemCompletedAsync(sub.Id, Alice), Times.Once);

        _notify.Invocations.Clear();
        await service.SetDoneAsync(parent.Id, sub.Id, false, Alice, canEdit: false);
        await service.SetDoneAsync(parent.Id, sub.Id, false, Alice, canEdit: false);   // no change
        await Task.Delay(200);
        _notify.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task Helper_assignment_skips_the_actor_and_inactive_users()
    {
        _db.Users.Add(User("user-gone", "Gone", "User", active: false));
        await _db.SaveChangesAsync();
        var parent = await AddParentAsync();
        var sub = await AddSubItemAsync(parent.Id, Friday, null, 1, false, Alice, Editor, "user-gone");
        var notifications = new Mock<INotificationService>();
        var email = new Mock<IEmailSender>();
        var helper = new WorkflowNotificationHelper(_db, notifications.Object, email.Object, Settings(),
            NullLogger<WorkflowNotificationHelper>.Instance);

        await helper.NotifySubItemAssignedAsync(sub.Id, new[] { Alice, Editor, "user-gone" }, Editor);

        notifications.Verify(n => n.CreateAsync(It.Is<CreateNotificationDto>(d =>
            d.UserId == Alice && d.Type == "ActionItem" && d.ActionType == "SubItemAssigned"
            && d.RelatedEntityId == parent.Id && d.Url == $"/actions/{parent.Id}/view"
            && d.Message.Contains("Erin Editor assigned you to the sub-item \"Seeded 1\""))), Times.Once);
        notifications.Verify(n => n.CreateAsync(It.IsAny<CreateNotificationDto>()), Times.Once);
        email.Verify(e => e.SendEmailAsync("ActionItem.SubItemAssigned",
            It.Is<Dictionary<string, string>>(p => p["SubItemTitle"] == "Seeded 1" && p["ActorName"] == "Erin Editor"
                && p["ItemUrl"] == $"http://localhost:4200/actions/{parent.Id}/view"),
            It.Is<List<string>>(r => r.SequenceEqual(new[] { Alice + "@test" })),
            "ActionItem", parent.Id, Editor), Times.Once);
        email.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Helper_completion_goes_to_creator_parent_assignees_and_other_sub_item_assignees_not_the_actor()
    {
        var parent = await AddParentAsync(assignees: new[] { Bob, Editor });   // creator: Editor
        var sub = await AddSubItemAsync(parent.Id, Friday, null, 1, true, Alice, Outsider);
        await AddSubItemAsync(parent.Id, Friday, null, 2, false);
        var notifications = new Mock<INotificationService>();
        var email = new Mock<IEmailSender>();
        var helper = new WorkflowNotificationHelper(_db, notifications.Object, email.Object, Settings(),
            NullLogger<WorkflowNotificationHelper>.Instance);

        await helper.NotifySubItemCompletedAsync(sub.Id, Alice);

        var notified = notifications.Invocations.Select(i => ((CreateNotificationDto)i.Arguments[0]).UserId).ToList();
        notified.Should().BeEquivalentTo(new[] { Editor, Bob, Outsider });
        notifications.Invocations.Select(i => (CreateNotificationDto)i.Arguments[0])
            .Should().OnlyContain(d => d.ActionType == "SubItemCompleted" && d.Message.EndsWith("(1 of 2 done)"));
        email.Verify(e => e.SendEmailAsync("ActionItem.SubItemCompleted",
            It.Is<Dictionary<string, string>>(p => p["DoneCount"] == "1" && p["TotalCount"] == "2"),
            It.IsAny<List<string>>(), "ActionItem", parent.Id, Alice), Times.Exactly(3));
    }

    // ── Sub-item e-mails: user text is HTML-encoded (follow-up to A-3) ───────

    private const string HostileSubTitle    = "Don't <b>panic</b> & \"go\"";
    private const string HostileSubTitleEnc = "Don&#39;t &lt;b&gt;panic&lt;/b&gt; &amp; &quot;go&quot;";

    /// <summary>A parent + sub-item whose titles and workspace name carry HTML, and an actor whose name does.</summary>
    private async Task<(ActionItem Parent, ActionItemSubItem Sub)> AddHostileSubItemAsync(bool isDone)
    {
        _db.Users.Add(User("user-mallory", "<img src=x onerror=alert(1)>", "O'Brien"));
        (await _db.Workspaces.FirstAsync(w => w.Id == _workspaceId)).Title = "R&D <Lab>";
        var parent = await AddParentAsync(assignees: new[] { Alice });
        var tracked = await _db.ActionItems.FirstAsync(a => a.Id == parent.Id);
        tracked.Title = "Plan <script>x()</script>";
        await _db.SaveChangesAsync();
        var sub = await AddSubItemAsync(parent.Id, Friday, null, 1, isDone, Bob);
        var subTracked = await _db.ActionItemSubItems.FirstAsync(s => s.Id == sub.Id);
        subTracked.Title = HostileSubTitle;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return (parent, sub);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Helper_sub_item_emails_html_encode_titles_and_names(bool completed)
    {
        var (parent, sub) = await AddHostileSubItemAsync(isDone: completed);
        var notifications = new Mock<INotificationService>();
        var email = new Mock<IEmailSender>();
        var helper = new WorkflowNotificationHelper(_db, notifications.Object, email.Object, Settings(),
            NullLogger<WorkflowNotificationHelper>.Instance);

        if (completed) await helper.NotifySubItemCompletedAsync(sub.Id, "user-mallory");
        else           await helper.NotifySubItemAssignedAsync(sub.Id, new[] { Bob }, "user-mallory");

        var sent = email.Invocations.Where(i => i.Method.Name == nameof(IEmailSender.SendEmailAsync)).ToList();
        sent.Should().NotBeEmpty();
        foreach (var call in sent)
        {
            call.Arguments[0].Should().Be(completed ? "ActionItem.SubItemCompleted" : "ActionItem.SubItemAssigned");
            var p = (Dictionary<string, string>)call.Arguments[1];
            p["SubItemTitle"].Should().Be(HostileSubTitleEnc);
            p["Title"].Should().Be("Plan &lt;script&gt;x()&lt;/script&gt;");
            p["ActorName"].Should().Be("&lt;img src=x onerror=alert(1)&gt; O&#39;Brien");
            p["WorkspaceName"].Should().Be("R&amp;D &lt;Lab&gt;");
            p.Values.Should().NotContain(v => v.Contains('<') || v.Contains('>') || v.Contains('"'),
                "every value goes into the HTML body as it is");
        }
        // In-app messages are text rendered by Angular (escaped there) — left as they are.
        notifications.Invocations.Select(i => ((CreateNotificationDto)i.Arguments[0]).Message)
            .Should().OnlyContain(m => m.Contains(HostileSubTitle));
    }

    [Fact]
    public async Task Sub_item_email_subject_reads_as_plain_text_and_the_body_stays_encoded()
    {
        var (parent, sub) = await AddHostileSubItemAsync(isDone: true);
        var loaded = await _db.ActionItemSubItems.AsNoTracking()
            .Include(s => s.ActionItem).ThenInclude(a => a.Workspace)
            .FirstAsync(s => s.Id == sub.Id);
        var placeholders = WorkflowNotificationHelper.BuildSubItemPlaceholders(
            loaded, "Erin <Editor>", "Bob O'Neil", "http://localhost:4200/actions/1/view?a=1&b=2",
            new Dictionary<string, string> { ["DoneCount"] = "1", ["TotalCount"] = "2" });

        foreach (var key in new[] { "ActionItem.SubItemAssigned", "ActionItem.SubItemCompleted" })
        {
            var template = EmailTemplateSeeder.SeedTemplates().Single(t => t.TemplateKey == key);

            // Every placeholder the template uses is supplied.
            var used = System.Text.RegularExpressions.Regex.Matches(template.Subject + template.HtmlBody, @"\{\{([^}]+)\}\}")
                .Select(m => m.Groups[1].Value).ToHashSet();
            used.Should().BeSubsetOf(placeholders.Keys.ToHashSet(), key);

            var subject = EmailSender.ResolveSubject(template.Subject, placeholders);
            subject.Should().Contain(HostileSubTitle, "a subject is plain text: the title reads as typed")
                .And.Contain($"({parent.ActionId})").And.NotContain("&amp;").And.NotContain("&#39;");

            var body = EmailSender.ResolvePlaceholders(template.HtmlBody, placeholders);
            body.Should().Contain(HostileSubTitleEnc).And.NotContain("<b>panic</b>").And.NotContain("<script>")
                .And.Contain("Hi Bob O&#39;Neil").And.Contain("href=\"http://localhost:4200/actions/1/view?a=1&amp;b=2\"");
        }
    }

    // ── My Actions and counts: sub-items are never action items ──────────────

    [Fact]
    public async Task Assigned_via_sub_items_lists_parents_where_the_user_is_only_a_sub_item_assignee()
    {
        var onlySub   = await AddParentAsync(assignees: new[] { Editor });
        var both      = await AddParentAsync(assignees: new[] { Alice });
        var deletedSub = await AddParentAsync(assignees: new[] { Editor });
        await AddSubItemAsync(onlySub.Id, Friday, null, 1, true, Alice);
        await AddSubItemAsync(onlySub.Id, Friday, null, 2, false, Alice);
        await AddSubItemAsync(onlySub.Id, Friday, null, 3, false, Bob);
        await AddSubItemAsync(both.Id, Friday, null, 1, false, Alice);
        var gone = await AddSubItemAsync(deletedSub.Id, Friday, null, 1, false, Alice);
        await Service().DeleteAsync(deletedSub.Id, gone.Id, Editor);

        _currentUserId = Alice;
        var mine = await ActionItems().GetAssignedViaSubItemsAsync(
            Alice, new ActionItemFilterDto { AssigneeId = Bob, IncludeDeleted = true }, CancellationToken.None);

        mine.TotalCount.Should().Be(1);
        var row = mine.Items.Single();
        row.Id.Should().Be(onlySub.Id);
        row.SubItemCount.Should().Be(3);
        row.SubItemDoneCount.Should().Be(1);
        row.MySubItemCount.Should().Be(2);

        // The usual lists carry the counts too, without MySubItemCount.
        var all = await ActionItems().GetAllAsync(new ActionItemFilterDto { PageSize = 100 }, CancellationToken.None);
        all.Items.Single(i => i.Id == onlySub.Id).Should().Match<ActionItemResponseDto>(i =>
            i.SubItemCount == 3 && i.SubItemDoneCount == 1 && i.MySubItemCount == null);
        all.Items.Single(i => i.Id == deletedSub.Id).SubItemCount.Should().Be(0);
        (await ActionItems().GetByIdAsync(onlySub.Id, CancellationToken.None))!.SubItemCount.Should().Be(3);
    }

    [Fact]
    public async Task Sub_item_assignments_change_no_action_item_count()
    {
        // Alice is a parent assignee of one item and a sub-item assignee of another.
        var own   = await AddParentAsync(assignees: new[] { Alice });
        var other = await AddParentAsync(ActionStatus.InProgress, assignees: new[] { Bob });
        await AddSubItemAsync(other.Id, Friday, null, 1, false, Alice);
        await AddSubItemAsync(other.Id, Friday, null, 2, true, Alice);
        _ = own;

        var stats = await ActionItems().GetMyStatsAsync(Alice, CancellationToken.None);
        stats.TotalCount.Should().Be(1);

        var myActions = await ActionItems().GetAllAsync(new ActionItemFilterDto { AssigneeId = Alice }, CancellationToken.None);
        myActions.TotalCount.Should().Be(1);

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(Alice);
        var access = new Mock<IWorkspaceAccessResolver>();
        access.Setup(r => r.GetVisibleWorkspaceIdsAsync(It.IsAny<string>())).ReturnsAsync((List<Guid>?)null);
        var scope = new Mock<IStrategicScopeService>();
        var dashboards = new DashboardAnalyticsService(_db, currentUser.Object, access.Object, scope.Object,
            NullLogger<DashboardAnalyticsService>.Instance);

        _currentUserId = Alice;
        (await dashboards.GetMyActionsAsync(CancellationToken.None)).Total.Should().Be(1);
        var team = await dashboards.GetTeamActivityAsync(CancellationToken.None);
        team.Single(t => t.UserId == Alice).Total.Should().Be(1);
        (await dashboards.GetActionItemsAsync(CancellationToken.None)).Total.Should().Be(2);
    }
}
