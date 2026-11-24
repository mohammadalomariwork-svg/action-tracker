using ActionTracker.Application.Common;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.ActionItems;
using ActionTracker.Application.Features.ActionItems.DTOs;
using ActionTracker.Application.Features.ActionItems.Services;
using ActionTracker.Application.Features.Calendars.Services;
using ActionTracker.Application.Features.Notifications;
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

namespace ActionTracker.Tests.Unit;

/// <summary>
/// WO-2026-11 A-1: actual start / actual end on action items (Q9).
/// The rule itself (<see cref="ActionItemActualDates"/>) and every path that
/// changes an action item's status: create, PUT, PATCH /status, the approved
/// status-change request, ApplyApprovedStatusChangeAsync and the overdue job.
/// No calendar is seeded unless a test says so, so "today" is the UTC day and
/// planned dates are not checked against a calendar.
/// </summary>
public class ActionItemActualDatesTests : IDisposable
{
    private const string UserId = "user-1";

    private static readonly DateTime Today     = ActionItemActualDates.Today(null, DateTime.UtcNow);
    private static readonly DateTime Yesterday = Today.AddDays(-1);
    private static readonly DateTime LastWeek  = Today.AddDays(-7);

    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly AppDbContext _db;
    private readonly WorkCalendarService _calendars;
    private readonly Mock<IActionItemWorkflowService> _workflow = new();

    public ActionItemActualDatesTests()
    {
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options);

        var access = new Mock<IWorkspaceAccessResolver>();
        access.Setup(r => r.GetVisibleWorkspaceIdsAsync(It.IsAny<string>())).ReturnsAsync((List<Guid>?)null);
        _calendars = new WorkCalendarService(_db, access.Object, new WorkCalendarCache(), NullLogger<WorkCalendarService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static DateTime Utc(int year, int month, int day, int hour = 0, int minute = 0) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    private IServiceScopeFactory ScopeFactory()
    {
        var provider = new Mock<IServiceProvider>();
        provider.Setup(p => p.GetService(typeof(IActionItemWorkflowService))).Returns(_workflow.Object);
        var scope = new Mock<IServiceScope>();
        scope.SetupGet(s => s.ServiceProvider).Returns(provider.Object);
        var factory = new Mock<IServiceScopeFactory>();
        factory.Setup(f => f.CreateScope()).Returns(scope.Object);
        return factory.Object;
    }

    private static IOptions<AppSettings> Settings() =>
        Options.Create(new AppSettings { FrontendBaseUrl = "http://localhost:4200" });

    private ActionItemService NewService() =>
        new(_db, NullLogger<ActionItemService>.Instance, Mock.Of<IEmailSender>(), Mock.Of<INotificationService>(),
            Settings(), ScopeFactory(), _calendars);

    private ActionItemWorkflowService NewWorkflowService() =>
        new(_db, Mock.Of<INotificationService>(), Mock.Of<IEmailSender>(), Settings(),
            ScopeFactory(), NullLogger<ActionItemWorkflowService>.Instance, _calendars);

    private async Task<Workspace> AddWorkspaceAsync()
    {
        var workspace = new Workspace
        {
            Id = Guid.NewGuid(), Title = "Workspace", OrganizationUnit = "Org", CreatedAt = DateTime.UtcNow,
        };
        _db.Workspaces.Add(workspace);
        await _db.SaveChangesAsync();
        return workspace;
    }

    private async Task<ActionItem> AddItemAsync(
        ActionStatus status,
        bool isStandalone = false,
        DateTime? dueDate = null,
        DateTime? actualStart = null,
        DateTime? actualEnd = null,
        Guid? projectId = null)
    {
        var workspace = await _db.Workspaces.FirstOrDefaultAsync() ?? await AddWorkspaceAsync();
        var item = new ActionItem
        {
            Id              = Guid.NewGuid(),
            ActionId        = "ACT-" + Guid.NewGuid().ToString("N")[..4],
            Title           = "Item",
            Description     = "",
            WorkspaceId     = workspace.Id,
            ProjectId       = projectId,
            IsStandalone    = isStandalone,
            Status          = status,
            Priority        = ActionPriority.Medium,
            DueDate         = dueDate ?? Today.AddDays(10),
            ActualStartDate = actualStart,
            ActualEndDate   = actualEnd,
            Progress        = status == ActionStatus.Done ? 100 : 0,
            CreatedByUserId = UserId,
            CreatedAt       = DateTime.UtcNow,
        };
        _db.ActionItems.Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    private async Task<ActionItem> ReloadAsync(Guid id)
    {
        _db.ChangeTracker.Clear();
        return await _db.ActionItems.IgnoreQueryFilters().FirstAsync(a => a.Id == id);
    }

    private static (DateTime? Start, DateTime? End) Resolve(
        ActionStatus? previous, ActionStatus requested, ActionStatus? resulting = null,
        DateTime? currentStart = null, DateTime? currentEnd = null,
        DateTime? suppliedStart = null, bool clearStart = false, DateTime? suppliedEnd = null) =>
        ActionItemActualDates.Resolve(previous, requested, resulting ?? requested,
            currentStart, currentEnd, suppliedStart, clearStart, suppliedEnd, Today);

    // ── Today ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Today_IsTheCalendarDayInTheCalendarTimeZone()
    {
        // 21:30 UTC on 24 Nov is 01:30 on 25 Nov in Dubai (UTC+4).
        var today = ActionItemActualDates.Today("Asia/Dubai", Utc(2026, 11, 24, 21, 30));

        today.Should().Be(Utc(2026, 11, 25));
        today.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Not/AZone")]
    public void Today_FallsBackToTheUtcDay(string? timeZoneId) =>
        ActionItemActualDates.Today(timeZoneId, Utc(2026, 11, 24, 21, 30)).Should().Be(Utc(2026, 11, 24));

    // ── The rule ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ActionStatus.ToDo)]
    [InlineData(ActionStatus.InReview)]
    [InlineData(ActionStatus.Overdue)]
    [InlineData(ActionStatus.Done)]
    [InlineData(null)]
    public void MovingIntoInProgress_SetsTheActualStartToToday(ActionStatus? previous) =>
        Resolve(previous, ActionStatus.InProgress).Should().Be(((DateTime?)Today, (DateTime?)null));

    [Fact]
    public void MovingIntoInProgressAgain_KeepsTheFirstActualStart() =>
        Resolve(ActionStatus.InReview, ActionStatus.InProgress, currentStart: LastWeek).Start.Should().Be(LastWeek);

    [Fact]
    public void StayingInProgress_DoesNotSetAnActualStart() =>
        Resolve(ActionStatus.InProgress, ActionStatus.InProgress).Start.Should().BeNull();

    [Fact]
    public void InProgressStoredAsOverdue_StillSetsTheActualStart() =>
        Resolve(ActionStatus.ToDo, ActionStatus.InProgress, ActionStatus.Overdue).Start.Should().Be(Today);

    [Fact]
    public void MovingIntoDone_SetsTheActualEnd_AndLeavesAnEmptyActualStartEmpty() =>
        Resolve(ActionStatus.ToDo, ActionStatus.Done).Should().Be(((DateTime?)null, (DateTime?)Today));

    [Fact]
    public void MovingIntoDone_UsesASuppliedActualEnd() =>
        Resolve(ActionStatus.InProgress, ActionStatus.Done, currentStart: LastWeek, suppliedEnd: Yesterday)
            .Should().Be(((DateTime?)LastWeek, (DateTime?)Yesterday));

    [Fact]
    public void MovingIntoDone_ReplacesAStaleActualEnd() =>
        Resolve(ActionStatus.InProgress, ActionStatus.Done, currentEnd: LastWeek).End.Should().Be(Today);

    [Theory]
    [InlineData(ActionStatus.InProgress)]
    [InlineData(ActionStatus.ToDo)]
    [InlineData(ActionStatus.Overdue)]
    public void LeavingDone_ClearsTheActualEnd_AndKeepsTheActualStart(ActionStatus to) =>
        Resolve(ActionStatus.Done, to, currentStart: LastWeek, currentEnd: Yesterday)
            .Should().Be(((DateTime?)LastWeek, (DateTime?)null));

    [Fact]
    public void StayingDone_KeepsTheActualEnd_UnlessOneIsSupplied()
    {
        Resolve(ActionStatus.Done, ActionStatus.Done, currentEnd: LastWeek).End.Should().Be(LastWeek);
        Resolve(ActionStatus.Done, ActionStatus.Done, currentEnd: LastWeek, suppliedEnd: Yesterday).End.Should().Be(Yesterday);
    }

    [Fact]
    public void ClearStart_ClearsTheActualStart() =>
        Resolve(ActionStatus.InProgress, ActionStatus.InProgress, currentStart: LastWeek, clearStart: true)
            .Start.Should().BeNull();

    [Fact]
    public void SuppliedValues_AreStoredAsTheCalendarDay() =>
        Resolve(ActionStatus.InProgress, ActionStatus.InProgress, suppliedStart: Yesterday.AddHours(15).AddMinutes(45))
            .Start.Should().Be(Yesterday);

    [Fact]
    public void AnActualStartAfterToday_IsRefused() =>
        FluentActions.Invoking(() => Resolve(ActionStatus.ToDo, ActionStatus.ToDo, suppliedStart: Today.AddDays(1)))
            .Should().Throw<ArgumentException>().WithMessage("Actual start (*) cannot be after today (*).");

    [Fact]
    public void AnActualEndAfterToday_IsRefused() =>
        FluentActions.Invoking(() => Resolve(ActionStatus.Done, ActionStatus.Done, suppliedEnd: Today.AddDays(1)))
            .Should().Throw<ArgumentException>().WithMessage("Actual end (*) cannot be after today (*).");

    [Theory]
    [InlineData(ActionStatus.ToDo)]
    [InlineData(ActionStatus.InProgress)]
    [InlineData(ActionStatus.Cancelled)]
    public void AnActualEnd_IsRefusedUnlessTheItemIsDone(ActionStatus status) =>
        FluentActions.Invoking(() => Resolve(ActionStatus.ToDo, status, suppliedEnd: Yesterday))
            .Should().Throw<ArgumentException>().WithMessage("Actual end can only be set when the action item is Done.");

    [Fact]
    public void AnActualEndBeforeTheActualStart_IsRefused() =>
        FluentActions.Invoking(() => Resolve(ActionStatus.Done, ActionStatus.Done, currentStart: Yesterday, suppliedEnd: LastWeek))
            .Should().Throw<ArgumentException>().WithMessage("Actual end (*) cannot be before actual start (*).");

    [Fact]
    public void SettingAndClearingTheActualStartTogether_IsRefused() =>
        FluentActions.Invoking(() => Resolve(ActionStatus.ToDo, ActionStatus.ToDo, suppliedStart: Yesterday, clearStart: true))
            .Should().Throw<ArgumentException>();

    [Fact]
    public void AnAutomaticActualEndBeforeTheActualStart_IsMovedUpNotRefused() =>
        // Only possible when the time zone changed under the item: never block a status change on it.
        Resolve(ActionStatus.InProgress, ActionStatus.Done, currentStart: Today.AddDays(1))
            .End.Should().Be(Today.AddDays(1));

    // ── Create ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ActionStatus.ToDo,       false, false)]
    [InlineData(ActionStatus.InProgress, true,  false)]
    [InlineData(ActionStatus.Done,       false, true)]
    public async Task Create_StampsTheActualDatesForTheInitialStatus(ActionStatus status, bool started, bool finished)
    {
        var workspace = await AddWorkspaceAsync();

        var created = await NewService().CreateAsync(new ActionItemCreateDto
        {
            Title = "New", WorkspaceId = workspace.Id, Priority = ActionPriority.Medium,
            Status = status, DueDate = Today.AddDays(5), AssigneeIds = new() { UserId },
        }, UserId, default);

        created.ActualStartDate.Should().Be(started ? Today : null);
        created.ActualEndDate.Should().Be(finished ? Today : null);
    }

    // ── PATCH /status ─────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateStatus_InProgressThenDoneThenReopened_FollowsTheRule()
    {
        var item = await AddItemAsync(ActionStatus.ToDo);
        var service = NewService();

        await service.UpdateStatusAsync(item.Id, ActionStatus.InProgress, default);
        (await ReloadAsync(item.Id)).ActualStartDate.Should().Be(Today);

        await service.UpdateStatusAsync(item.Id, ActionStatus.Done, default);
        var done = await ReloadAsync(item.Id);
        done.ActualStartDate.Should().Be(Today);
        done.ActualEndDate.Should().Be(Today);

        await service.UpdateStatusAsync(item.Id, ActionStatus.InProgress, default);
        var reopened = await ReloadAsync(item.Id);
        reopened.Status.Should().Be(ActionStatus.InProgress);
        reopened.ActualStartDate.Should().Be(Today);
        reopened.ActualEndDate.Should().BeNull();
    }

    [Fact]
    public async Task UpdateStatus_InProgressOnAPastDueItem_IsStoredAsOverdue_AndStillRecordsTheStart()
    {
        var item = await AddItemAsync(ActionStatus.ToDo, dueDate: LastWeek);

        await NewService().UpdateStatusAsync(item.Id, ActionStatus.InProgress, default);

        var stored = await ReloadAsync(item.Id);
        stored.Status.Should().Be(ActionStatus.Overdue);
        stored.ActualStartDate.Should().Be(Today);
    }

    [Fact]
    public async Task UpdateStatus_KeepsTheFirstActualStart()
    {
        var item = await AddItemAsync(ActionStatus.InReview, actualStart: LastWeek);

        await NewService().UpdateStatusAsync(item.Id, ActionStatus.InProgress, default);

        (await ReloadAsync(item.Id)).ActualStartDate.Should().Be(LastWeek);
    }

    [Fact]
    public async Task UpdateStatus_UsesTodayInTheWorkspaceCalendarTimeZone()
    {
        await WorkCalendarSeeder.SeedAsync(_db, NullLogger.Instance);   // KU Standard, Asia/Dubai
        var item = await AddItemAsync(ActionStatus.ToDo);

        await NewService().UpdateStatusAsync(item.Id, ActionStatus.InProgress, default);

        (await ReloadAsync(item.Id)).ActualStartDate.Should().Be(ActionItemActualDates.Today("Asia/Dubai", DateTime.UtcNow));
    }

    // ── PUT ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_MovingIntoDone_WithASuppliedActualEnd_StoresIt()
    {
        var item = await AddItemAsync(ActionStatus.InProgress, actualStart: LastWeek);

        var dto = await NewService().UpdateAsync(item.Id,
            new ActionItemUpdateDto { Id = item.Id, Status = ActionStatus.Done, ActualEndDate = Yesterday }, UserId, default);

        dto.ActualStartDate.Should().Be(LastWeek);
        dto.ActualEndDate.Should().Be(Yesterday);
        dto.ActualEndVarianceDays.Should().Be((int)(Yesterday - item.DueDate.Date).TotalDays);
    }

    [Fact]
    public async Task Update_ActualStart_OnAnOpenItem_IsSavedWithoutChangingTheStatus()
    {
        var item = await AddItemAsync(ActionStatus.ToDo);

        var dto = await NewService().UpdateAsync(item.Id,
            new ActionItemUpdateDto { Id = item.Id, ActualStartDate = Yesterday }, UserId, default);

        dto.Status.Should().Be(ActionStatus.ToDo);
        dto.ActualStartDate.Should().Be(Yesterday);
    }

    [Fact]
    public async Task Update_ClearActualStartDate_ClearsIt()
    {
        var item = await AddItemAsync(ActionStatus.InProgress, actualStart: LastWeek);

        var dto = await NewService().UpdateAsync(item.Id,
            new ActionItemUpdateDto { Id = item.Id, ClearActualStartDate = true }, UserId, default);

        dto.ActualStartDate.Should().BeNull();
    }

    [Fact]
    public async Task Update_ARefusedActualDate_SavesNothing()
    {
        var item = await AddItemAsync(ActionStatus.ToDo);

        var act = () => NewService().UpdateAsync(item.Id,
            new ActionItemUpdateDto { Id = item.Id, Title = "Renamed", ActualStartDate = Today.AddDays(1) }, UserId, default);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("Actual start*");
        var stored = await ReloadAsync(item.Id);
        stored.Title.Should().Be("Item");
        stored.ActualStartDate.Should().BeNull();
    }

    [Fact]
    public async Task Update_AnActualEndOnAnOpenItem_IsRefused()
    {
        var item = await AddItemAsync(ActionStatus.InProgress);

        var act = () => NewService().UpdateAsync(item.Id,
            new ActionItemUpdateDto { Id = item.Id, ActualEndDate = Yesterday }, UserId, default);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("Actual end can only be set*");
    }

    [Fact]
    public async Task Update_DoneItem_AcceptsAPayloadWithOnlyActualDates()
    {
        var item = await AddItemAsync(ActionStatus.Done, actualEnd: Today);

        var dto = await NewService().UpdateAsync(item.Id,
            new ActionItemUpdateDto { Id = item.Id, ActualStartDate = LastWeek, ActualEndDate = Yesterday }, UserId, default);

        dto.Status.Should().Be(ActionStatus.Done);
        dto.ActualStartDate.Should().Be(LastWeek);
        dto.ActualEndDate.Should().Be(Yesterday);
    }

    [Fact]
    public async Task Update_DoneItem_RefusesAnyOtherField_AndAnEmptyPayload()
    {
        var item = await AddItemAsync(ActionStatus.Done, actualEnd: Today);
        var service = NewService();

        await service.Invoking(s => s.UpdateAsync(item.Id,
                new ActionItemUpdateDto { Id = item.Id, Title = "Renamed", ActualEndDate = Yesterday }, UserId, default))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("Completed action items cannot be edited.");
        await service.Invoking(s => s.UpdateAsync(item.Id,
                new ActionItemUpdateDto { Id = item.Id, Status = ActionStatus.InProgress }, UserId, default))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("Completed action items cannot be edited.");
        await service.Invoking(s => s.UpdateAsync(item.Id, new ActionItemUpdateDto { Id = item.Id }, UserId, default))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("Completed action items cannot be edited.");

        (await ReloadAsync(item.Id)).ActualEndDate.Should().Be(Today);
    }

    [Fact]
    public async Task Update_ProjectItemWithFrozenDates_AcceptsActualDates_ButStillRefusesPlannedDates()
    {
        var workspace = await AddWorkspaceAsync();
        var project = new Project
        {
            Id = Guid.NewGuid(), ProjectCode = "PRJ-A1", Name = "Active project", WorkspaceId = workspace.Id,
            ProjectType = ProjectType.Operational, Status = ProjectStatus.Active, Priority = ActionPriority.Medium,
            ProjectManagerUserId = UserId, PlannedStartDate = LastWeek, PlannedEndDate = Today.AddDays(30),
            Currency = "AED", CreatedBy = UserId, CreatedAt = DateTime.UtcNow,
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        var item = await AddItemAsync(ActionStatus.InProgress, projectId: project.Id);
        var service = NewService();

        var dto = await service.UpdateAsync(item.Id,
            new ActionItemUpdateDto { Id = item.Id, ActualStartDate = LastWeek }, UserId, default);
        dto.ActualStartDate.Should().Be(LastWeek);

        await service.Invoking(s => s.UpdateAsync(item.Id,
                new ActionItemUpdateDto { Id = item.Id, DueDate = item.DueDate.AddDays(1) }, UserId, default))
            .Should().ThrowAsync<ArgumentException>().WithMessage("Action item dates cannot be changed*");
    }

    [Fact]
    public async Task Update_StandaloneItem_SavesActualDatesDirectly_WithoutADateChangeRequest()
    {
        var item = await AddItemAsync(ActionStatus.InProgress, isStandalone: true);

        var dto = await NewService().UpdateAsync(item.Id,
            new ActionItemUpdateDto { Id = item.Id, ActualStartDate = LastWeek }, UserId, default);

        dto.ActualStartDate.Should().Be(LastWeek);
        _workflow.Verify(w => w.CreateDateChangeRequestAsync(It.IsAny<CreateDateChangeRequestDto>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Update_StandaloneItemToDone_SendsTheStatusForApproval_AndRecordsNoActualEndYet()
    {
        var item = await AddItemAsync(ActionStatus.InProgress, isStandalone: true);

        var dto = await NewService().UpdateAsync(item.Id,
            new ActionItemUpdateDto { Id = item.Id, Status = ActionStatus.Done }, UserId, default);

        dto.Status.Should().Be(ActionStatus.InProgress);
        dto.ActualEndDate.Should().BeNull();
        _workflow.Verify(w => w.CreateStatusChangeRequestAsync(
            It.Is<CreateStatusChangeRequestDto>(r => r.NewStatus == ActionStatus.Done), UserId), Times.Once);
    }

    [Fact]
    public async Task Update_StandaloneItemToDone_WithAnActualEnd_IsRefusedBeforeAnyRequestIsCreated()
    {
        var item = await AddItemAsync(ActionStatus.InProgress, isStandalone: true);

        var act = () => NewService().UpdateAsync(item.Id,
            new ActionItemUpdateDto { Id = item.Id, Status = ActionStatus.Done, ActualEndDate = Yesterday }, UserId, default);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("Actual end can only be set*");
        _workflow.Verify(w => w.CreateStatusChangeRequestAsync(It.IsAny<CreateStatusChangeRequestDto>(), It.IsAny<string>()), Times.Never);
    }

    // ── Approval, the unused bypass, and the overdue job ──────────────────────

    [Fact]
    public async Task ApprovedStatusChangeToDone_RecordsTheActualEnd()
    {
        var item = await AddItemAsync(ActionStatus.InProgress, isStandalone: true, actualStart: LastWeek);
        var request = new ActionItemWorkflowRequest
        {
            Id = Guid.NewGuid(), ActionItemId = item.Id, RequestType = WorkflowRequestType.StatusChangeRequest,
            RequestedNewStatus = ActionStatus.Done, CurrentStatus = ActionStatus.InProgress,
            RequestedByUserId = "requester", Reason = "Finished",
        };
        _db.ActionItemWorkflowRequests.Add(request);
        await _db.SaveChangesAsync();

        await NewWorkflowService().ReviewRequestAsync(request.Id, new ReviewWorkflowRequestDto { IsApproved = true }, UserId);

        var stored = await ReloadAsync(item.Id);
        stored.Status.Should().Be(ActionStatus.Done);
        stored.ActualStartDate.Should().Be(LastWeek);
        stored.ActualEndDate.Should().Be(Today);
    }

    [Fact]
    public async Task RejectedStatusChange_LeavesTheActualDatesAlone()
    {
        var item = await AddItemAsync(ActionStatus.InProgress, isStandalone: true, actualStart: LastWeek);
        var request = new ActionItemWorkflowRequest
        {
            Id = Guid.NewGuid(), ActionItemId = item.Id, RequestType = WorkflowRequestType.StatusChangeRequest,
            RequestedNewStatus = ActionStatus.Done, CurrentStatus = ActionStatus.InProgress,
            RequestedByUserId = "requester", Reason = "Finished",
        };
        _db.ActionItemWorkflowRequests.Add(request);
        await _db.SaveChangesAsync();

        await NewWorkflowService().ReviewRequestAsync(request.Id, new ReviewWorkflowRequestDto { IsApproved = false }, UserId);

        var stored = await ReloadAsync(item.Id);
        stored.Status.Should().Be(ActionStatus.InProgress);
        stored.ActualEndDate.Should().BeNull();
    }

    [Fact]
    public async Task ApplyApprovedStatusChange_FollowsTheSameRule()
    {
        var item = await AddItemAsync(ActionStatus.ToDo);
        var service = NewService();

        await service.ApplyApprovedStatusChangeAsync(item.Id, ActionStatus.Done);
        (await ReloadAsync(item.Id)).ActualEndDate.Should().Be(Today);

        await service.ApplyApprovedStatusChangeAsync(item.Id, ActionStatus.InProgress);
        var reopened = await ReloadAsync(item.Id);
        reopened.ActualStartDate.Should().Be(Today);
        reopened.ActualEndDate.Should().BeNull();
    }

    [Fact]
    public async Task ProcessOverdue_LeavesTheActualDatesAlone()
    {
        var started = await AddItemAsync(ActionStatus.InProgress, dueDate: LastWeek, actualStart: LastWeek.AddDays(-3));
        var notStarted = await AddItemAsync(ActionStatus.ToDo, dueDate: LastWeek);
        var done = await AddItemAsync(ActionStatus.Done, dueDate: LastWeek, actualEnd: Yesterday);

        (await NewService().ProcessOverdueItemsAsync(default)).Should().Be(2);

        var s = await ReloadAsync(started.Id);
        s.Status.Should().Be(ActionStatus.Overdue);
        s.ActualStartDate.Should().Be(LastWeek.AddDays(-3));
        (await ReloadAsync(notStarted.Id)).ActualStartDate.Should().BeNull();
        var d = await ReloadAsync(done.Id);
        d.Status.Should().Be(ActionStatus.Done);
        d.ActualEndDate.Should().Be(Yesterday);
    }

    // ── Response ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(3, 3)]
    [InlineData(0, 0)]
    [InlineData(-2, -2)]
    public void Variance_IsActualEndMinusPlannedEnd_InCalendarDays(int offset, int expected)
    {
        var dto = new ActionItemResponseDto { DueDate = Utc(2026, 11, 20), ActualEndDate = Utc(2026, 11, 20).AddDays(offset) };
        dto.ActualEndVarianceDays.Should().Be(expected);
        new ActionItemResponseDto { DueDate = Utc(2026, 11, 20) }.ActualEndVarianceDays.Should().BeNull();
    }
}
