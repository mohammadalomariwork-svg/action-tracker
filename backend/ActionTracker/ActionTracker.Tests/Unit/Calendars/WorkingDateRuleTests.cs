using ActionTracker.Application.Common;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.ActionItems.DTOs;
using ActionTracker.Application.Features.ActionItems.Services;
using ActionTracker.Application.Features.Calendars;
using ActionTracker.Application.Features.Calendars.DTOs;
using ActionTracker.Application.Features.Calendars.Services;
using ActionTracker.Application.Features.Milestones.DTOs;
using ActionTracker.Application.Features.Milestones.Services;
using ActionTracker.Application.Features.Notifications;
using ActionTracker.Application.Features.Projects.DTOs;
using ActionTracker.Application.Features.Projects.Services;
using ActionTracker.Application.Features.Workflow.DTOs;
using ActionTracker.Application.Features.Workflow.Interfaces;
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

namespace ActionTracker.Tests.Unit.Calendars;

/// <summary>
/// WO-2026-11 C-3: the workspace calendar setting and the working-day rule on
/// saved dates (Q7: start and end dates may not fall on a non-working day;
/// Q8: only dates that are created or changed are checked).
/// Calendar under test: the seeded default "KU Standard" (Mon–Fri) and a
/// "Six-day week" (Mon–Sat). Reference week: Mon 23 Nov … Sun 29 Nov 2026.
/// </summary>
public class WorkingDateRuleTests : IDisposable
{
    private const string UserId = "user-1";

    private static readonly DateTime Friday   = Utc(2026, 11, 27);
    private static readonly DateTime Saturday = Utc(2026, 11, 28);
    private static readonly DateTime Sunday   = Utc(2026, 11, 29);
    private static readonly DateTime Monday   = Utc(2026, 11, 30);
    private static readonly DateTime Tuesday  = Utc(2026, 12, 1);

    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly AppDbContext _db;
    private readonly WorkCalendarCache _cache = new();
    private readonly Mock<IWorkspaceAccessResolver> _workspaceAccess = new();
    private readonly WorkCalendarService _calendars;

    public WorkingDateRuleTests()
    {
        _db = NewContext(currentUserId: null);

        _workspaceAccess
            .Setup(r => r.GetVisibleWorkspaceIdsAsync(It.IsAny<string>()))
            .ReturnsAsync((List<Guid>?)null);

        _calendars = new WorkCalendarService(_db, _workspaceAccess.Object, _cache, NullLogger<WorkCalendarService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static DateTime Utc(int year, int month, int day) => new(year, month, day, 0, 0, 0, DateTimeKind.Utc);

    private AppDbContext NewContext(string? currentUserId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options;
        if (currentUserId is null)
            return new AppDbContext(options);

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(u => u.UserId).Returns(currentUserId);
        return new AppDbContext(options, currentUser.Object);
    }

    private Task SeedDefaultAsync() => WorkCalendarSeeder.SeedAsync(_db, NullLogger.Instance);

    private static List<WorkCalendarDayDto> Week(params DayOfWeek[] workingDays) =>
        Enum.GetValues<DayOfWeek>()
            .Select(d =>
            {
                var working = workingDays.Contains(d);
                return new WorkCalendarDayDto
                {
                    DayOfWeek    = d,
                    IsWorkingDay = working,
                    StartTime    = working ? new TimeOnly(8, 0) : null,
                    EndTime      = working ? new TimeOnly(17, 0) : null,
                };
            })
            .ToList();

    private Task<WorkCalendarDto> AddSixDayCalendarAsync(bool isActive = true) =>
        _calendars.CreateAsync(new CreateWorkCalendarDto
        {
            Name     = "Six-day week",
            IsActive = isActive,
            Days     = Week(DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                            DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday),
        }, UserId);

    private async Task<Workspace> AddWorkspaceAsync(Guid? calendarId = null, bool isPrivate = false)
    {
        var workspace = new Workspace
        {
            Id               = Guid.NewGuid(),
            Title            = "Workspace " + Guid.NewGuid().ToString("N")[..6],
            OrganizationUnit = "Org",
            CreatedAt        = DateTime.UtcNow,
            IsPrivate        = isPrivate,
            WorkCalendarId   = calendarId,
        };
        _db.Workspaces.Add(workspace);
        await _db.SaveChangesAsync();
        return workspace;
    }

    private async Task<ActionItem> AddActionItemAsync(
        Guid workspaceId, DateTime dueDate, DateTime? startDate = null, bool isStandalone = false)
    {
        var item = new ActionItem
        {
            Id              = Guid.NewGuid(),
            ActionId        = "ACT-" + Guid.NewGuid().ToString("N")[..4],
            Title           = "Existing item",
            WorkspaceId     = workspaceId,
            IsStandalone    = isStandalone,
            Status          = ActionStatus.ToDo,
            Priority        = ActionPriority.Medium,
            StartDate       = startDate,
            DueDate         = dueDate,
            CreatedByUserId = UserId,
            CreatedAt       = DateTime.UtcNow,
        };
        _db.ActionItems.Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    private async Task<Project> AddProjectAsync(Guid workspaceId, DateTime start, DateTime end)
    {
        var project = new Project
        {
            Id                   = Guid.NewGuid(),
            ProjectCode          = "PRJ-T-" + Guid.NewGuid().ToString("N")[..4],
            Name                 = "Existing project",
            WorkspaceId          = workspaceId,
            ProjectType          = ProjectType.Operational,
            Status               = ProjectStatus.Draft,
            Priority             = ActionPriority.Medium,
            ProjectManagerUserId = UserId,
            PlannedStartDate     = start,
            PlannedEndDate       = end,
            Currency             = "AED",
            CreatedBy            = UserId,
            CreatedAt            = DateTime.UtcNow,
        };
        _db.Projects.Add(project);
        _db.WorkspaceProjectManagers.Add(new WorkspaceProjectManager { WorkspaceId = workspaceId, UserId = UserId });
        await _db.SaveChangesAsync();
        return project;
    }

    private static IOptions<AppSettings> Settings() =>
        Options.Create(new AppSettings { FrontendBaseUrl = "http://localhost:4200" });

    /// <summary>
    /// Scope factory for the services' fire-and-forget notifications and the
    /// standalone edit path, which resolves the workflow service from a scope.
    /// Anything else resolves to null, so background notifications simply stop.
    /// </summary>
    private IServiceScopeFactory ScopeFactory(IActionItemWorkflowService? workflow = null)
    {
        var provider = new Mock<IServiceProvider>();
        if (workflow is not null)
            provider.Setup(p => p.GetService(typeof(IActionItemWorkflowService))).Returns(workflow);

        var scope = new Mock<IServiceScope>();
        scope.SetupGet(s => s.ServiceProvider).Returns(provider.Object);

        var factory = new Mock<IServiceScopeFactory>();
        factory.Setup(f => f.CreateScope()).Returns(scope.Object);
        return factory.Object;
    }

    private ActionItemWorkflowService NewWorkflowService() =>
        new(_db, Mock.Of<INotificationService>(), Mock.Of<IEmailSender>(), Settings(),
            ScopeFactory(), NullLogger<ActionItemWorkflowService>.Instance, _calendars);

    private ActionItemService NewActionItemService(IActionItemWorkflowService? workflow = null) =>
        new(_db, NullLogger<ActionItemService>.Instance, Mock.Of<IEmailSender>(), Mock.Of<INotificationService>(),
            Settings(), ScopeFactory(workflow), _calendars);

    private MilestoneService NewMilestoneService() =>
        new(_db, NullLogger<MilestoneService>.Instance, Mock.Of<IEmailSender>(), Mock.Of<INotificationService>(),
            Settings(), ScopeFactory(), _calendars);

    private ProjectService NewProjectService() =>
        new(_db, NullLogger<ProjectService>.Instance, Mock.Of<IEmailSender>(), Mock.Of<INotificationService>(),
            Settings(), ScopeFactory(), _calendars);

    private static ActionItemCreateDto NewActionItem(Guid workspaceId, DateTime dueDate, DateTime? startDate = null) => new()
    {
        Title       = "New item",
        WorkspaceId = workspaceId,
        Priority    = ActionPriority.Medium,
        Status      = ActionStatus.ToDo,
        StartDate   = startDate,
        DueDate     = dueDate,
    };

    // ── The rule ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task EnsureWorkingDays_RefusesASaturday_NamingTheDateTheCalendarAndTheNextWorkingDay()
    {
        await SeedDefaultAsync();
        var workspace = await AddWorkspaceAsync();

        var act = () => _calendars.EnsureWorkingDaysAsync(workspace.Id, new[] { WorkingDateCheck.New("Due date", Saturday) });

        var thrown = await act.Should().ThrowAsync<NonWorkingDateException>();
        thrown.Which.Message.Should().Be(
            "Due date 2026-11-28 (Saturday) is not a working day in the workspace calendar \"KU Standard\". " +
            "Next working day: 2026-11-30 (Monday).");
        thrown.Which.Dates.Should().ContainSingle()
            .Which.Should().Be(new NonWorkingDate("Due date", new DateOnly(2026, 11, 28), new DateOnly(2026, 11, 30)));
        thrown.Which.Should().BeAssignableTo<ArgumentException>("callers map ArgumentException to 400");
    }

    [Fact]
    public async Task EnsureWorkingDays_ListsEveryRefusedDate_AndAcceptsWorkingDays()
    {
        await SeedDefaultAsync();

        var act = () => _calendars.EnsureWorkingDaysAsync(null, new[]
        {
            WorkingDateCheck.New("Start date", Saturday),
            WorkingDateCheck.New("Due date",   Sunday),
        });

        var thrown = await act.Should().ThrowAsync<NonWorkingDateException>();
        thrown.Which.Dates.Select(d => d.Field).Should().Equal("Start date", "Due date");

        await _calendars.Invoking(c => c.EnsureWorkingDaysAsync(null, new[]
        {
            WorkingDateCheck.New("Start date", Friday),
            WorkingDateCheck.New("Due date",   Monday),
            WorkingDateCheck.New("Unset",      null),
        })).Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureWorkingDays_OnlyChecksDatesWhoseDayChanges()
    {
        await SeedDefaultAsync();

        // Same Saturday with another time of day: unchanged, not checked (Q8).
        await _calendars.Invoking(c => c.EnsureWorkingDaysAsync(null, new[]
        {
            WorkingDateCheck.Changed("Due date", Saturday.AddHours(9), Saturday),
        })).Should().NotThrowAsync();

        // Moved to Sunday: changed, checked.
        await _calendars.Invoking(c => c.EnsureWorkingDaysAsync(null, new[]
        {
            WorkingDateCheck.Changed("Due date", Sunday, Saturday),
        })).Should().ThrowAsync<NonWorkingDateException>();
    }

    [Fact]
    public async Task EnsureWorkingDays_FollowsTheWorkspaceCalendar()
    {
        await SeedDefaultAsync();
        var sixDay       = await AddSixDayCalendarAsync();
        var sixDayWs     = await AddWorkspaceAsync(sixDay.Id);
        var defaultWs    = await AddWorkspaceAsync();
        var saturdayOnly = new[] { WorkingDateCheck.New("Due date", Saturday) };

        await _calendars.Invoking(c => c.EnsureWorkingDaysAsync(sixDayWs.Id, saturdayOnly)).Should().NotThrowAsync();
        await _calendars.Invoking(c => c.EnsureWorkingDaysAsync(defaultWs.Id, saturdayOnly))
            .Should().ThrowAsync<NonWorkingDateException>();

        var sunday = () => _calendars.EnsureWorkingDaysAsync(sixDayWs.Id, new[] { WorkingDateCheck.New("Due date", Sunday) });
        (await sunday.Should().ThrowAsync<NonWorkingDateException>()).Which.CalendarName.Should().Be("Six-day week");
    }

    [Fact]
    public async Task EnsureWorkingDays_RefusesAHolidayOnAWeekday()
    {
        await SeedDefaultAsync();
        var calendar = (await _calendars.GetAllAsync()).Single();
        await _calendars.UpdateAsync(calendar.Id, new UpdateWorkCalendarDto
        {
            Name       = calendar.Name,
            TimeZoneId = calendar.TimeZoneId,
            IsActive   = true,
            Days       = calendar.Days,
            Exceptions = new List<WorkCalendarExceptionDto>
            {
                new() { Name = "National Day", StartDate = new DateOnly(2026, 12, 1), EndDate = new DateOnly(2026, 12, 2) },
            },
        }, UserId);

        var act = () => _calendars.EnsureWorkingDaysAsync(null, new[] { WorkingDateCheck.New("Due date", Tuesday) });

        (await act.Should().ThrowAsync<NonWorkingDateException>())
            .Which.Dates.Single().NextWorkingDay.Should().Be(new DateOnly(2026, 12, 3));
    }

    [Fact]
    public async Task EnsureWorkingDays_WithNoCalendarConfigured_DoesNotBlockTheSave()
    {
        await _calendars.Invoking(c => c.EnsureWorkingDaysAsync(Guid.NewGuid(), new[] { WorkingDateCheck.New("Due date", Saturday) }))
            .Should().NotThrowAsync();
    }

    // ── Workspace setting ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetWorkspaceCalendar_WithoutACalendarOfItsOwn_ReturnsTheDefaultAsInherited()
    {
        await SeedDefaultAsync();
        var workspace = await AddWorkspaceAsync();

        var result = await _calendars.GetWorkspaceCalendarAsync(workspace.Id);

        result.WorkspaceId.Should().Be(workspace.Id);
        result.WorkCalendarId.Should().BeNull();
        result.CalendarName.Should().Be("KU Standard");
        result.IsDefault.Should().BeTrue();
        result.IsInherited.Should().BeTrue();
        result.Days.Should().HaveCount(7);
        result.Days.Count(d => d.IsWorkingDay).Should().Be(5);
    }

    [Fact]
    public async Task SetWorkspaceCalendar_PointsTheWorkspaceAtTheCalendar_AndTheRuleFollowsAtOnce()
    {
        await SeedDefaultAsync();
        var sixDay    = await AddSixDayCalendarAsync();
        var workspace = await AddWorkspaceAsync();
        var saturday  = new[] { WorkingDateCheck.New("Due date", Saturday) };

        // Warm the cache with the default for this workspace first.
        await _calendars.Invoking(c => c.EnsureWorkingDaysAsync(workspace.Id, saturday))
            .Should().ThrowAsync<NonWorkingDateException>();

        var result = await _calendars.SetWorkspaceCalendarAsync(
            workspace.Id, new SetWorkspaceCalendarDto { WorkCalendarId = sixDay.Id }, UserId);

        result.WorkCalendarId.Should().Be(sixDay.Id);
        result.CalendarName.Should().Be("Six-day week");
        result.IsInherited.Should().BeFalse();
        (await _db.Workspaces.AsNoTracking().SingleAsync(w => w.Id == workspace.Id)).WorkCalendarId.Should().Be(sixDay.Id);
        await _calendars.Invoking(c => c.EnsureWorkingDaysAsync(workspace.Id, saturday)).Should().NotThrowAsync();

        // Back to the default.
        var reset = await _calendars.SetWorkspaceCalendarAsync(
            workspace.Id, new SetWorkspaceCalendarDto { WorkCalendarId = null }, UserId);

        reset.WorkCalendarId.Should().BeNull();
        reset.IsInherited.Should().BeTrue();
        await _calendars.Invoking(c => c.EnsureWorkingDaysAsync(workspace.Id, saturday))
            .Should().ThrowAsync<NonWorkingDateException>();
    }

    [Fact]
    public async Task SetWorkspaceCalendar_RefusesAnUnknownInactiveOrDeletedCalendar()
    {
        await SeedDefaultAsync();
        var inactive  = await AddSixDayCalendarAsync(isActive: false);
        var deleted   = await _calendars.CreateAsync(new CreateWorkCalendarDto { Name = "Old", Days = Week(DayOfWeek.Monday) }, UserId);
        await _calendars.SoftDeleteAsync(deleted.Id, UserId);
        var workspace = await AddWorkspaceAsync();

        foreach (var calendarId in new[] { Guid.NewGuid(), inactive.Id, deleted.Id, Guid.Empty })
        {
            await _calendars.Invoking(c => c.SetWorkspaceCalendarAsync(
                    workspace.Id, new SetWorkspaceCalendarDto { WorkCalendarId = calendarId }, UserId))
                .Should().ThrowAsync<ArgumentException>();
        }

        (await _db.Workspaces.AsNoTracking().SingleAsync(w => w.Id == workspace.Id)).WorkCalendarId.Should().BeNull();
    }

    [Fact]
    public async Task SetWorkspaceCalendar_WorkspaceOutsideTheCallersScope_IsNotFound()
    {
        await SeedDefaultAsync();
        var sixDay    = await AddSixDayCalendarAsync();
        var workspace = await AddWorkspaceAsync();
        _workspaceAccess
            .Setup(r => r.GetVisibleWorkspaceIdsAsync(UserId))
            .ReturnsAsync(new List<Guid> { Guid.NewGuid() });

        await _calendars.Invoking(c => c.SetWorkspaceCalendarAsync(
                workspace.Id, new SetWorkspaceCalendarDto { WorkCalendarId = sixDay.Id }, UserId))
            .Should().ThrowAsync<KeyNotFoundException>();

        (await _db.Workspaces.AsNoTracking().SingleAsync(w => w.Id == workspace.Id)).WorkCalendarId.Should().BeNull();
    }

    [Fact]
    public async Task GetWorkspaceCalendar_PrivateWorkspaceTheCallerIsNotPartOf_IsNotFound()
    {
        await SeedDefaultAsync();
        var workspace = await AddWorkspaceAsync(isPrivate: true);

        using var outsiderDb = NewContext("outsider");
        var asOutsider = new WorkCalendarService(outsiderDb, _workspaceAccess.Object, _cache, NullLogger<WorkCalendarService>.Instance);

        await asOutsider.Invoking(s => s.GetWorkspaceCalendarAsync(workspace.Id)).Should().ThrowAsync<KeyNotFoundException>();
        await _calendars.Invoking(s => s.GetWorkspaceCalendarAsync(Guid.NewGuid())).Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetWorkspaceCalendar_ReturnsOnlyExceptionsInTheWindow_AndRefusesABadWindow()
    {
        await SeedDefaultAsync();
        var calendar = (await _calendars.GetAllAsync()).Single();
        await _calendars.UpdateAsync(calendar.Id, new UpdateWorkCalendarDto
        {
            Name       = calendar.Name,
            TimeZoneId = calendar.TimeZoneId,
            IsActive   = true,
            Days       = calendar.Days,
            Exceptions = new List<WorkCalendarExceptionDto>
            {
                new() { Name = "Inside",   StartDate = new DateOnly(2026, 12, 2), EndDate = new DateOnly(2026, 12, 3) },
                new() { Name = "Overlaps", StartDate = new DateOnly(2026, 11, 30), EndDate = new DateOnly(2026, 12, 1) },
                new() { Name = "Outside",  StartDate = new DateOnly(2027, 6, 1),  EndDate = new DateOnly(2027, 6, 1) },
            },
        }, UserId);
        var workspace = await AddWorkspaceAsync();

        var result = await _calendars.GetWorkspaceCalendarAsync(workspace.Id, new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31));

        result.From.Should().Be(new DateOnly(2026, 12, 1));
        result.To.Should().Be(new DateOnly(2026, 12, 31));
        result.Exceptions.Select(e => e.Name).Should().Equal("Overlaps", "Inside");

        await _calendars.Invoking(c => c.GetWorkspaceCalendarAsync(workspace.Id, new DateOnly(2026, 12, 2), new DateOnly(2026, 12, 1)))
            .Should().ThrowAsync<ArgumentException>();
        // 3,660 days is the limit (same as the working-day search).
        await _calendars.Invoking(c => c.GetWorkspaceCalendarAsync(workspace.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1).AddDays(3659)))
            .Should().NotThrowAsync();
        await _calendars.Invoking(c => c.GetWorkspaceCalendarAsync(workspace.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1).AddDays(3660)))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetWorkspaceCalendarOptions_ListsActiveCalendars_DefaultFirst()
    {
        await SeedDefaultAsync();
        await AddSixDayCalendarAsync(isActive: false);
        await _calendars.CreateAsync(new CreateWorkCalendarDto { Name = "Alpha", Days = Week(DayOfWeek.Monday) }, UserId);

        var options = await _calendars.GetWorkspaceCalendarOptionsAsync();

        options.Select(o => o.Name).Should().Equal("KU Standard", "Alpha");
        options[0].IsDefault.Should().BeTrue();
        options[0].Days.Should().HaveCount(7);
    }

    // ── Action items ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ActionItemCreate_OnASaturday_IsRefused_AndNothingIsSaved()
    {
        await SeedDefaultAsync();
        var workspace = await AddWorkspaceAsync();

        await NewActionItemService().Invoking(s => s.CreateAsync(NewActionItem(workspace.Id, Saturday), UserId, default))
            .Should().ThrowAsync<NonWorkingDateException>();
        await NewActionItemService().Invoking(s => s.CreateAsync(NewActionItem(workspace.Id, Monday, startDate: Sunday), UserId, default))
            .Should().ThrowAsync<NonWorkingDateException>().WithMessage("Planned start 2026-11-29 (Sunday)*");

        (await _db.ActionItems.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ActionItemCreate_OnASaturday_SavesInAWorkspaceWhoseCalendarWorksSaturdays()
    {
        await SeedDefaultAsync();
        var sixDay    = await AddSixDayCalendarAsync();
        var workspace = await AddWorkspaceAsync(sixDay.Id);

        var created = await NewActionItemService().CreateAsync(NewActionItem(workspace.Id, Saturday), UserId, default);

        created.Should().NotBeNull();
        (await _db.ActionItems.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ActionItemUpdate_LegacyItemOnAWeekend_StillSavesOtherFields()
    {
        await SeedDefaultAsync();
        var workspace = await AddWorkspaceAsync();
        var item      = await AddActionItemAsync(workspace.Id, dueDate: Saturday, startDate: Sunday);

        // The form sends every field back, dates included, unchanged.
        await NewActionItemService().UpdateAsync(item.Id, new ActionItemUpdateDto
        {
            Title     = "Renamed",
            StartDate = Sunday,
            DueDate   = Saturday,
        }, UserId, default);

        var saved = await _db.ActionItems.AsNoTracking().SingleAsync(a => a.Id == item.Id);
        saved.Title.Should().Be("Renamed");
        saved.DueDate.Should().Be(Saturday);
    }

    [Fact]
    public async Task ActionItemUpdate_ChangingADateToASaturday_IsRefused()
    {
        await SeedDefaultAsync();
        var workspace = await AddWorkspaceAsync();
        var item      = await AddActionItemAsync(workspace.Id, dueDate: Friday);

        await NewActionItemService().Invoking(s => s.UpdateAsync(item.Id, new ActionItemUpdateDto { Title = "x", DueDate = Saturday }, UserId, default))
            .Should().ThrowAsync<NonWorkingDateException>();

        var saved = await _db.ActionItems.AsNoTracking().SingleAsync(a => a.Id == item.Id);
        saved.DueDate.Should().Be(Friday);
        saved.Title.Should().Be("Existing item", "the whole save is refused");
    }

    [Fact]
    public async Task StandaloneItemUpdate_ToASaturday_IsRefused_InsteadOfBeingDroppedSilently()
    {
        await SeedDefaultAsync();
        var workspace = await AddWorkspaceAsync();
        var item      = await AddActionItemAsync(workspace.Id, dueDate: Friday, isStandalone: true);
        var service   = NewActionItemService(NewWorkflowService());

        await service.Invoking(s => s.UpdateAsync(item.Id, new ActionItemUpdateDto { DueDate = Saturday }, UserId, default))
            .Should().ThrowAsync<NonWorkingDateException>();

        (await _db.ActionItemWorkflowRequests.CountAsync()).Should().Be(0);

        // A working day still becomes a date-change request, as before.
        await service.UpdateAsync(item.Id, new ActionItemUpdateDto { DueDate = Monday }, UserId, default);

        var request = await _db.ActionItemWorkflowRequests.SingleAsync();
        request.RequestedNewDueDate.Should().Be(Monday);
        (await _db.ActionItems.AsNoTracking().SingleAsync(a => a.Id == item.Id)).DueDate.Should().Be(Friday);
    }

    [Fact]
    public async Task DateChangeRequest_ForASaturday_IsRefused()
    {
        await SeedDefaultAsync();
        var workspace = await AddWorkspaceAsync();
        var item      = await AddActionItemAsync(workspace.Id, dueDate: Friday, isStandalone: true);

        var act = () => NewWorkflowService().CreateDateChangeRequestAsync(new CreateDateChangeRequestDto
        {
            ActionItemId = item.Id,
            NewDueDate   = Saturday,
            Reason       = "Need more time",
        }, UserId);

        await act.Should().ThrowAsync<NonWorkingDateException>().WithMessage("Requested planned end 2026-11-28 (Saturday)*");
        (await _db.ActionItemWorkflowRequests.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DateChangeRequest_KeepingTheCurrentWeekendStartDate_OnlyChecksTheChangedDueDate()
    {
        await SeedDefaultAsync();
        var workspace = await AddWorkspaceAsync();
        var item      = await AddActionItemAsync(workspace.Id, dueDate: Friday, startDate: Sunday, isStandalone: true);

        await NewWorkflowService().CreateDateChangeRequestAsync(new CreateDateChangeRequestDto
        {
            ActionItemId = item.Id,
            NewStartDate = Sunday,
            NewDueDate   = Monday,
            Reason       = "Move the due date",
        }, UserId);

        (await _db.ActionItemWorkflowRequests.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ApprovingARequest_StillAppliesItsDates_EvenWhenTheyAreNoLongerWorkingDays()
    {
        await SeedDefaultAsync();
        var sixDay    = await AddSixDayCalendarAsync();
        var workspace = await AddWorkspaceAsync(sixDay.Id);
        var item      = await AddActionItemAsync(workspace.Id, dueDate: Friday, isStandalone: true);
        var workflow  = NewWorkflowService();

        // Requested while Saturday was a working day…
        var request = await workflow.CreateDateChangeRequestAsync(new CreateDateChangeRequestDto
        {
            ActionItemId = item.Id, NewDueDate = Saturday, Reason = "Weekend shift",
        }, UserId);

        // …then the workspace moves to the Mon–Fri default before review.
        await _calendars.SetWorkspaceCalendarAsync(workspace.Id, new SetWorkspaceCalendarDto { WorkCalendarId = null }, UserId);

        await workflow.ReviewRequestAsync(request.Id, new ReviewWorkflowRequestDto { IsApproved = true }, UserId);

        (await _db.ActionItems.AsNoTracking().SingleAsync(a => a.Id == item.Id)).DueDate.Should().Be(Saturday);
    }

    // ── Milestones ────────────────────────────────────────────────────────────

    [Fact]
    public async Task MilestoneCreate_OnASaturday_IsRefused()
    {
        await SeedDefaultAsync();
        var workspace = await AddWorkspaceAsync();
        var project   = await AddProjectAsync(workspace.Id, Friday, Tuesday);

        var act = () => NewMilestoneService().CreateAsync(project.Id, new MilestoneCreateDto
        {
            Name = "Kick-off", SequenceOrder = 1, PlannedStartDate = Friday, PlannedDueDate = Saturday,
        }, default);

        await act.Should().ThrowAsync<NonWorkingDateException>().WithMessage("Planned due date 2026-11-28 (Saturday)*");
        (await _db.Milestones.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task MilestoneCreate_OnASaturday_SavesInAWorkspaceWhoseCalendarWorksSaturdays()
    {
        await SeedDefaultAsync();
        var sixDay    = await AddSixDayCalendarAsync();
        var workspace = await AddWorkspaceAsync(sixDay.Id);
        var project   = await AddProjectAsync(workspace.Id, Friday, Tuesday);

        await NewMilestoneService().CreateAsync(project.Id, new MilestoneCreateDto
        {
            Name = "Kick-off", SequenceOrder = 1, PlannedStartDate = Friday, PlannedDueDate = Saturday,
        }, default);

        (await _db.Milestones.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task MilestoneUpdate_LegacyWeekendDatesUnchanged_Saves_ButChangingToASundayIsRefused()
    {
        await SeedDefaultAsync();
        var workspace = await AddWorkspaceAsync();
        var project   = await AddProjectAsync(workspace.Id, Friday, Tuesday);
        var milestone = new Milestone
        {
            Id = Guid.NewGuid(), ProjectId = project.Id, MilestoneCode = "MS-T-1", Name = "Legacy",
            SequenceOrder = 1, PlannedStartDate = Saturday, PlannedDueDate = Monday, CreatedAt = DateTime.UtcNow,
        };
        _db.Milestones.Add(milestone);
        await _db.SaveChangesAsync();

        await NewMilestoneService().UpdateAsync(project.Id, milestone.Id, new MilestoneUpdateDto
        {
            Name = "Renamed", SequenceOrder = 1, PlannedStartDate = Saturday, PlannedDueDate = Monday,
        }, default);
        (await _db.Milestones.AsNoTracking().SingleAsync()).Name.Should().Be("Renamed");

        await NewMilestoneService().Invoking(s => s.UpdateAsync(project.Id, milestone.Id, new MilestoneUpdateDto
        {
            Name = "Renamed", SequenceOrder = 1, PlannedStartDate = Saturday, PlannedDueDate = Sunday.AddDays(7),
        }, default)).Should().ThrowAsync<NonWorkingDateException>();
    }

    // ── Projects ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProjectCreate_OnASaturday_IsRefused()
    {
        await SeedDefaultAsync();
        var workspace = await AddWorkspaceAsync();
        _db.WorkspaceProjectManagers.Add(new WorkspaceProjectManager { WorkspaceId = workspace.Id, UserId = UserId });
        await _db.SaveChangesAsync();

        var act = () => NewProjectService().CreateAsync(new ProjectCreateDto
        {
            Name = "New project", WorkspaceId = workspace.Id, ProjectType = ProjectType.Operational,
            ProjectManagerUserId = UserId, SponsorUserIds = new List<string> { UserId },
            PlannedStartDate = Saturday, PlannedEndDate = Tuesday,
        }, UserId, default);

        await act.Should().ThrowAsync<NonWorkingDateException>().WithMessage("Planned start date 2026-11-28 (Saturday)*");
        (await _db.Projects.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ProjectUpdate_LegacyWeekendDatesUnchanged_Saves_ButChangingTheEndToASundayIsRefused()
    {
        await SeedDefaultAsync();
        var workspace = await AddWorkspaceAsync();
        var project   = await AddProjectAsync(workspace.Id, start: Saturday, end: Tuesday);
        _db.ProjectSponsors.Add(new ProjectSponsor { ProjectId = project.Id, UserId = UserId });
        await _db.SaveChangesAsync();

        ProjectUpdateDto Update(DateTime end) => new()
        {
            Name = "Renamed", ProjectType = ProjectType.Operational, Status = ProjectStatus.Draft,
            Priority = ActionPriority.Medium, ProjectManagerUserId = UserId,
            SponsorUserIds = new List<string> { UserId },
            PlannedStartDate = Saturday, PlannedEndDate = end,
        };

        await NewProjectService().UpdateAsync(project.Id, Update(Tuesday), default);
        (await _db.Projects.AsNoTracking().SingleAsync()).Name.Should().Be("Renamed");

        await NewProjectService().Invoking(s => s.UpdateAsync(project.Id, Update(Sunday.AddDays(7)), default))
            .Should().ThrowAsync<NonWorkingDateException>().WithMessage("Planned end date 2026-12-06 (Sunday)*");
    }
}
