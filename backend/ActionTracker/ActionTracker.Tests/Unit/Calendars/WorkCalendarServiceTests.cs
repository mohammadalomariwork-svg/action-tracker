using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.Calendars.DTOs;
using ActionTracker.Application.Features.Calendars.Services;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Entities;
using ActionTracker.Infrastructure.Data;
using ActionTracker.Infrastructure.Data.Seeders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ActionTracker.Tests.Unit.Calendars;

/// <summary>
/// Calendar rules: exactly one default, the delete / deactivate guards
/// (default calendar, calendar in use — including by a private workspace the
/// caller cannot see), name uniqueness, input validation, cache invalidation,
/// and the startup seeder.
/// </summary>
public class WorkCalendarServiceTests : IDisposable
{
    private const string AdminId = "admin-1";

    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly AppDbContext _db;
    private readonly WorkCalendarCache _cache = new();
    private readonly Mock<IWorkspaceAccessResolver> _workspaceAccess = new();
    private readonly WorkCalendarService _sut;

    public WorkCalendarServiceTests()
    {
        _db = NewContext(currentUserId: null);

        // Unrestricted workspace visibility unless a test says otherwise.
        _workspaceAccess
            .Setup(r => r.GetVisibleWorkspaceIdsAsync(It.IsAny<string>()))
            .ReturnsAsync((List<Guid>?)null);

        _sut = NewService(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>A context on the shared in-memory database, optionally acting as a signed-in user.</summary>
    private AppDbContext NewContext(string? currentUserId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        if (currentUserId is null)
            return new AppDbContext(options);

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(u => u.UserId).Returns(currentUserId);
        return new AppDbContext(options, currentUser.Object);
    }

    private WorkCalendarService NewService(AppDbContext db) =>
        new(db, _workspaceAccess.Object, _cache, NullLogger<WorkCalendarService>.Instance);

    private static List<WorkCalendarDayDto> StandardWeek() =>
        Enum.GetValues<DayOfWeek>()
            .Select(d =>
            {
                var working = d is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
                return new WorkCalendarDayDto
                {
                    DayOfWeek    = d,
                    IsWorkingDay = working,
                    StartTime    = working ? new TimeOnly(8, 0) : null,
                    EndTime      = working ? new TimeOnly(17, 0) : null,
                };
            })
            .ToList();

    private static CreateWorkCalendarDto NewCalendar(string name) =>
        new() { Name = name, Days = StandardWeek() };

    private static UpdateWorkCalendarDto AsUpdate(WorkCalendarDto calendar) => new()
    {
        Name        = calendar.Name,
        Description = calendar.Description,
        TimeZoneId  = calendar.TimeZoneId,
        IsActive    = calendar.IsActive,
        Days        = calendar.Days,
        Exceptions  = calendar.Exceptions,
    };

    private Task SeedDefaultAsync() =>
        WorkCalendarSeeder.SeedAsync(_db, NullLogger.Instance);

    private async Task<Workspace> AddWorkspaceAsync(Guid? calendarId, bool isPrivate = false, bool isActive = true)
    {
        var workspace = new Workspace
        {
            Id               = Guid.NewGuid(),
            Title            = "Workspace " + Guid.NewGuid().ToString("N")[..6],
            OrganizationUnit = "Org",
            CreatedAt        = DateTime.UtcNow,
            IsPrivate        = isPrivate,
            IsActive         = isActive,
            WorkCalendarId   = calendarId,
        };
        _db.Workspaces.Add(workspace);
        await _db.SaveChangesAsync();
        return workspace;
    }

    private Task<int> DefaultCountAsync() => _db.WorkCalendars.CountAsync(c => c.IsDefault);

    // ── Seeder ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Seeder_OnADatabaseWithNoCalendar_InsertsKuStandardAsDefault()
    {
        await SeedDefaultAsync();

        var calendar = await _db.WorkCalendars.Include(c => c.Days).SingleAsync();
        calendar.Name.Should().Be("KU Standard");
        calendar.IsDefault.Should().BeTrue();
        calendar.IsActive.Should().BeTrue();
        calendar.TimeZoneId.Should().Be("Asia/Dubai");
        calendar.Days.Should().HaveCount(7);

        foreach (var day in calendar.Days)
        {
            var weekend = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            day.IsWorkingDay.Should().Be(!weekend, $"{day.DayOfWeek}");
            day.StartTime.Should().Be(weekend ? (TimeOnly?)null : new TimeOnly(8, 0));
            day.EndTime.Should().Be(weekend ? (TimeOnly?)null : new TimeOnly(17, 0));
        }
    }

    [Fact]
    public async Task Seeder_RunTwice_InsertsOnlyOnce()
    {
        await SeedDefaultAsync();
        await SeedDefaultAsync();

        (await _db.WorkCalendars.CountAsync()).Should().Be(1);
        (await DefaultCountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Seeder_WhenCalendarsExistButNoneIsDefault_PromotesOneInsteadOfInserting()
    {
        _db.WorkCalendars.Add(new WorkCalendar { Name = "Existing", IsDefault = false, IsActive = true });
        await _db.SaveChangesAsync();

        await SeedDefaultAsync();

        var calendar = await _db.WorkCalendars.SingleAsync();
        calendar.Name.Should().Be("Existing");
        calendar.IsDefault.Should().BeTrue();
    }

    // ── Create / validation ───────────────────────────────────────────────────

    [Fact]
    public async Task Create_StoresSevenDays_AndIsNotDefaultWhenADefaultExists()
    {
        await SeedDefaultAsync();

        var created = await _sut.CreateAsync(NewCalendar("Site Office"), AdminId);

        created.IsDefault.Should().BeFalse();
        created.Days.Should().HaveCount(7);
        created.Days.Select(d => d.DayOfWeek).Should().BeInAscendingOrder();
        created.TimeZoneId.Should().Be("Asia/Dubai");
        created.CreatedBy.Should().Be(AdminId);
        (await DefaultCountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Create_DuplicateName_IsRefused_IgnoringCase()
    {
        await SeedDefaultAsync();

        var act = () => _sut.CreateAsync(NewCalendar("ku standard"), AdminId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already exists*");
    }

    [Fact]
    public async Task Create_NameOfADeletedCalendar_IsAllowed()
    {
        await SeedDefaultAsync();
        var first = await _sut.CreateAsync(NewCalendar("Site Office"), AdminId);
        await _sut.SoftDeleteAsync(first.Id, AdminId);

        var second = await _sut.CreateAsync(NewCalendar("Site Office"), AdminId);

        second.Id.Should().NotBe(first.Id);
    }

    [Fact]
    public async Task Create_WorkingDayWithoutHours_IsRefused()
    {
        var dto = NewCalendar("Broken");
        dto.Days.Single(d => d.DayOfWeek == DayOfWeek.Monday).StartTime = null;

        var act = () => _sut.CreateAsync(dto, AdminId);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Monday*");
    }

    [Fact]
    public async Task Create_WorkingDayEndingBeforeItStarts_IsRefused()
    {
        var dto = NewCalendar("Broken");
        var tuesday = dto.Days.Single(d => d.DayOfWeek == DayOfWeek.Tuesday);
        tuesday.StartTime = new TimeOnly(17, 0);
        tuesday.EndTime   = new TimeOnly(8, 0);

        var act = () => _sut.CreateAsync(dto, AdminId);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Tuesday*");
    }

    [Fact]
    public async Task Create_WithoutAllSevenDays_IsRefused()
    {
        var dto = NewCalendar("Broken");
        dto.Days.RemoveAt(0);

        var act = () => _sut.CreateAsync(dto, AdminId);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*seven*");
    }

    [Fact]
    public async Task Create_ExceptionEndingBeforeItStarts_IsRefused()
    {
        var dto = NewCalendar("Broken");
        dto.Exceptions.Add(new WorkCalendarExceptionDto
        {
            Name = "Backwards", StartDate = new DateOnly(2026, 12, 3), EndDate = new DateOnly(2026, 12, 2),
        });

        var act = () => _sut.CreateAsync(dto, AdminId);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*end date*");
    }

    [Fact]
    public async Task Create_UnknownTimeZone_IsRefused()
    {
        var dto = NewCalendar("Broken");
        dto.TimeZoneId = "Mars/Olympus_Mons";

        var act = () => _sut.CreateAsync(dto, AdminId);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*time zone*");
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_AddsEditsAndRemovesExceptions()
    {
        await SeedDefaultAsync();
        var dto = NewCalendar("Site Office");
        dto.Exceptions.Add(new WorkCalendarExceptionDto
        {
            Name = "National Day", StartDate = new DateOnly(2026, 12, 2), EndDate = new DateOnly(2026, 12, 3),
        });
        dto.Exceptions.Add(new WorkCalendarExceptionDto
        {
            Name = "To be removed", StartDate = new DateOnly(2026, 12, 10), EndDate = new DateOnly(2026, 12, 10),
        });
        var created = await _sut.CreateAsync(dto, AdminId);

        var update = AsUpdate(created);
        update.Exceptions = new List<WorkCalendarExceptionDto>
        {
            // kept and renamed
            new()
            {
                Id = created.Exceptions.Single(e => e.Name == "National Day").Id,
                Name = "UAE National Day", StartDate = new DateOnly(2026, 12, 2), EndDate = new DateOnly(2026, 12, 3),
            },
            // new
            new() { Name = "New Year", StartDate = new DateOnly(2027, 1, 1), EndDate = new DateOnly(2027, 1, 1) },
        };

        var updated = await _sut.UpdateAsync(created.Id, update, AdminId);

        updated.Exceptions.Select(e => e.Name).Should().Equal("UAE National Day", "New Year");
        updated.Days.Should().HaveCount(7);
        updated.UpdatedBy.Should().Be(AdminId);
        (await _db.WorkCalendarExceptions.CountAsync(e => e.WorkCalendarId == created.Id)).Should().Be(2);
    }

    [Fact]
    public async Task Update_DeactivatingTheDefaultCalendar_IsRefused()
    {
        await SeedDefaultAsync();
        var standard = (await _sut.GetAllAsync()).Single();

        var update = AsUpdate(standard);
        update.IsActive = false;
        var act = () => _sut.UpdateAsync(standard.Id, update, AdminId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*default calendar*");
        (await _db.WorkCalendars.SingleAsync()).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Update_DeactivatingACalendarInUse_IsRefused()
    {
        await SeedDefaultAsync();
        var created = await _sut.CreateAsync(NewCalendar("Site Office"), AdminId);
        await AddWorkspaceAsync(created.Id);

        var update = AsUpdate(created);
        update.IsActive = false;
        var act = () => _sut.UpdateAsync(created.Id, update, AdminId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*used by 1 workspace*");
    }

    // ── Delete guard ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_CalendarUsedByAWorkspace_IsRefused()
    {
        await SeedDefaultAsync();
        var created = await _sut.CreateAsync(NewCalendar("Site Office"), AdminId);
        await AddWorkspaceAsync(created.Id);

        var act = () => _sut.SoftDeleteAsync(created.Id, AdminId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*used by 1 workspace*");
        (await _db.WorkCalendars.AnyAsync(c => c.Id == created.Id)).Should().BeTrue("the calendar must survive");
    }

    [Fact]
    public async Task Delete_CalendarUsedOnlyByAnArchivedWorkspace_IsStillRefused()
    {
        await SeedDefaultAsync();
        var created = await _sut.CreateAsync(NewCalendar("Site Office"), AdminId);
        await AddWorkspaceAsync(created.Id, isActive: false);

        var act = () => _sut.SoftDeleteAsync(created.Id, AdminId);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Delete_CalendarUsedOnlyByAPrivateWorkspaceTheCallerCannotSee_IsStillRefused()
    {
        await SeedDefaultAsync();
        var created = await _sut.CreateAsync(NewCalendar("Site Office"), AdminId);
        await AddWorkspaceAsync(created.Id, isPrivate: true); // no admins, no members

        // Act as a signed-in admin who is not part of the private workspace.
        await using var asAdmin = NewContext(AdminId);
        (await asAdmin.Workspaces.CountAsync()).Should().Be(0, "the private workspace is hidden from this user");

        var act = () => NewService(asAdmin).SoftDeleteAsync(created.Id, AdminId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*used by 1 workspace*");
    }

    [Fact]
    public async Task Delete_TheDefaultCalendar_IsRefused()
    {
        await SeedDefaultAsync();
        var standard = (await _sut.GetAllAsync()).Single();

        var act = () => _sut.SoftDeleteAsync(standard.Id, AdminId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*default calendar*");
        (await DefaultCountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Delete_UnusedCalendar_SoftDeletes_AndRestoreBringsItBack()
    {
        await SeedDefaultAsync();
        var created = await _sut.CreateAsync(NewCalendar("Site Office"), AdminId);

        await _sut.SoftDeleteAsync(created.Id, AdminId);

        (await _sut.GetByIdAsync(created.Id)).Should().BeNull();
        (await _sut.GetAllAsync()).Should().NotContain(c => c.Id == created.Id);
        (await _sut.GetAllAsync(includeDeleted: true)).Should().Contain(c => c.Id == created.Id && c.IsDeleted);
        (await _db.WorkCalendars.IgnoreQueryFilters().AnyAsync(c => c.Id == created.Id))
            .Should().BeTrue("the row is kept, only flagged");

        await _sut.RestoreAsync(created.Id, AdminId);

        var restored = await _sut.GetByIdAsync(created.Id);
        restored.Should().NotBeNull();
        restored!.Days.Should().HaveCount(7);
    }

    [Fact]
    public async Task Restore_WhenTheNameIsNowTaken_IsRefused()
    {
        await SeedDefaultAsync();
        var first = await _sut.CreateAsync(NewCalendar("Site Office"), AdminId);
        await _sut.SoftDeleteAsync(first.Id, AdminId);
        await _sut.CreateAsync(NewCalendar("Site Office"), AdminId);

        var act = () => _sut.RestoreAsync(first.Id, AdminId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already named*");
    }

    [Fact]
    public async Task Delete_UnknownCalendar_ThrowsNotFound()
    {
        var act = () => _sut.SoftDeleteAsync(Guid.NewGuid(), AdminId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Default calendar ──────────────────────────────────────────────────────

    [Fact]
    public async Task SetDefault_MovesTheFlag_SoThereIsStillExactlyOneDefault()
    {
        await SeedDefaultAsync();
        var standard = (await _sut.GetAllAsync()).Single();
        var created  = await _sut.CreateAsync(NewCalendar("Site Office"), AdminId);

        var result = await _sut.SetDefaultAsync(created.Id, AdminId);

        result.IsDefault.Should().BeTrue();
        (await DefaultCountAsync()).Should().Be(1);
        (await _sut.GetByIdAsync(standard.Id))!.IsDefault.Should().BeFalse();
        (await _sut.GetDefaultAsync()).Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task SetDefault_OnTheCurrentDefault_ChangesNothing()
    {
        await SeedDefaultAsync();
        var standard = (await _sut.GetAllAsync()).Single();

        var result = await _sut.SetDefaultAsync(standard.Id, AdminId);

        result.IsDefault.Should().BeTrue();
        (await DefaultCountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task SetDefault_OnAnInactiveCalendar_IsRefused()
    {
        await SeedDefaultAsync();
        var dto = NewCalendar("Dormant");
        dto.IsActive = false;
        var created = await _sut.CreateAsync(dto, AdminId);

        var act = () => _sut.SetDefaultAsync(created.Id, AdminId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*inactive*");
        (await _sut.GetDefaultAsync()).Name.Should().Be("KU Standard");
    }

    // ── Resolution + cache ────────────────────────────────────────────────────

    [Fact]
    public async Task GetForWorkspace_UsesTheWorkspaceCalendar_OrTheDefaultWhenItHasNone()
    {
        await SeedDefaultAsync();
        var created = await _sut.CreateAsync(NewCalendar("Site Office"), AdminId);
        var withOwn = await AddWorkspaceAsync(created.Id);
        var without = await AddWorkspaceAsync(calendarId: null);

        (await _sut.GetForWorkspaceAsync(withOwn.Id)).Name.Should().Be("Site Office");
        (await _sut.GetForWorkspaceAsync(without.Id)).Name.Should().Be("KU Standard");
        (await _sut.GetForWorkspaceAsync(Guid.NewGuid())).Name.Should().Be("KU Standard", "unknown workspace falls back");
    }

    [Fact]
    public async Task GetForWorkspace_IsCached_AndASaveInvalidatesTheCache()
    {
        await SeedDefaultAsync();
        var created   = await _sut.CreateAsync(NewCalendar("Site Office"), AdminId);
        var workspace = await AddWorkspaceAsync(created.Id);
        var holiday   = new DateOnly(2026, 12, 2); // a Wednesday

        var before = await _sut.GetForWorkspaceAsync(workspace.Id);
        before.IsWorkingDay(holiday).Should().BeTrue();
        (await _sut.GetForWorkspaceAsync(workspace.Id)).Should().BeSameAs(before, "the second call is served from the cache");

        var update = AsUpdate(created);
        update.Exceptions.Add(new WorkCalendarExceptionDto { Name = "National Day", StartDate = holiday, EndDate = holiday });
        await _sut.UpdateAsync(created.Id, update, AdminId);

        var after = await _sut.GetForWorkspaceAsync(workspace.Id);
        after.Should().NotBeSameAs(before);
        after.IsWorkingDay(holiday).Should().BeFalse("the new holiday must apply immediately");
    }

    // ── Usage ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetUsage_NamesVisibleWorkspaces_AndOnlyCountsHiddenOnes()
    {
        await SeedDefaultAsync();
        var created = await _sut.CreateAsync(NewCalendar("Site Office"), AdminId);
        var open    = await AddWorkspaceAsync(created.Id);
        var secret  = await AddWorkspaceAsync(created.Id, isPrivate: true);

        await using var asAdmin = NewContext(AdminId);
        var usage = await NewService(asAdmin).GetUsageAsync(created.Id, AdminId);

        usage.TotalWorkspaceCount.Should().Be(2);
        usage.Workspaces.Select(w => w.Id).Should().Equal(open.Id);
        usage.Workspaces.Should().NotContain(w => w.Id == secret.Id || w.Title == secret.Title);
        usage.HiddenWorkspaceCount.Should().Be(1);
        usage.InheritingWorkspaceCount.Should().Be(0);
    }

    [Fact]
    public async Task GetUsage_ForANonPrivilegedCaller_NamesOnlyTheirOwnWorkspaces()
    {
        await SeedDefaultAsync();
        var created = await _sut.CreateAsync(NewCalendar("Site Office"), AdminId);
        var mine    = await AddWorkspaceAsync(created.Id);
        await AddWorkspaceAsync(created.Id); // someone else's

        _workspaceAccess
            .Setup(r => r.GetVisibleWorkspaceIdsAsync("ws-admin"))
            .ReturnsAsync(new List<Guid> { mine.Id });

        var usage = await _sut.GetUsageAsync(created.Id, "ws-admin");

        usage.Workspaces.Select(w => w.Id).Should().Equal(mine.Id);
        usage.TotalWorkspaceCount.Should().Be(2);
        usage.HiddenWorkspaceCount.Should().Be(1);
    }

    [Fact]
    public async Task GetUsage_ForTheDefaultCalendar_CountsWorkspacesThatInheritIt()
    {
        await SeedDefaultAsync();
        var standard = (await _sut.GetAllAsync()).Single();
        await AddWorkspaceAsync(calendarId: null);
        await AddWorkspaceAsync(calendarId: null);

        var usage = await _sut.GetUsageAsync(standard.Id, AdminId);

        usage.IsDefault.Should().BeTrue();
        usage.TotalWorkspaceCount.Should().Be(0);
        usage.InheritingWorkspaceCount.Should().Be(2);
    }
}
