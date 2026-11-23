using ActionTracker.Application.Common;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.ActionItems.DTOs;
using ActionTracker.Application.Features.ActionItems.Services;
using ActionTracker.Application.Features.Calendars.Interfaces;
using ActionTracker.Application.Features.Dashboard;
using ActionTracker.Application.Features.Dashboard.Services;
using ActionTracker.Application.Features.Notifications;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using ActionTracker.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace ActionTracker.Tests.Unit;

/// <summary>
/// In-memory tests for the dashboard aggregations. Nothing here touches a
/// real database.
/// </summary>
public class DashboardAnalyticsServiceTests : IDisposable
{
    private const string Me    = "user-me";
    private const string Alice = "user-alice";
    private const string Bob   = "user-bob";

    private static readonly Guid Ws1 = Guid.NewGuid();
    private static readonly Guid Ws2 = Guid.NewGuid();

    private readonly AppDbContext _db;
    private readonly Mock<IWorkspaceAccessResolver> _workspaceAccess = new();
    private readonly Mock<IStrategicScopeService>   _strategicScope  = new();
    private readonly Mock<ICurrentUserService>      _currentUser     = new();
    private readonly DashboardAnalyticsService _service;

    private static DateTime Today => DateTime.UtcNow.Date;

    public DashboardAnalyticsServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _currentUser.SetupGet(c => c.UserId).Returns(Me);
        // null = "no restriction" (a privileged caller) unless a test overrides it
        _workspaceAccess.Setup(r => r.GetVisibleWorkspaceIdsAsync(It.IsAny<string>()))
            .ReturnsAsync((List<Guid>?)null);
        _strategicScope.Setup(s => s.GetVisibleOrgUnitIdsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<Guid>?)null);

        _service = new DashboardAnalyticsService(
            _db, _currentUser.Object, _workspaceAccess.Object, _strategicScope.Object,
            Mock.Of<ILogger<DashboardAnalyticsService>>());

        _db.Users.AddRange(
            MakeUser(Me, "Mona", "Me"), MakeUser(Alice, "Alice", "Smith"), MakeUser(Bob, "Bob", "Jones"));
        _db.Workspaces.AddRange(
            new Workspace { Id = Ws1, Title = "Workspace One", OrganizationUnit = "Org", CreatedAt = DateTime.UtcNow },
            new Workspace { Id = Ws2, Title = "Workspace Two", OrganizationUnit = "Org", CreatedAt = DateTime.UtcNow });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    // ── Seed helpers ─────────────────────────────────────────────────────────

    private static ApplicationUser MakeUser(string id, string first, string last) => new()
    {
        Id = id, UserName = $"{id}@test.com", Email = $"{id}@test.com",
        FirstName = first, LastName = last, IsActive = true, CreatedAt = DateTime.UtcNow,
    };

    private ActionItem AddItem(
        ActionStatus status, int dueInDays, string assignee = Alice, Guid? workspace = null,
        int createdDaysAgo = 1, ActionPriority priority = ActionPriority.Medium,
        bool escalated = false, Guid? projectId = null, int? updatedDaysAgo = null)
    {
        var item = new ActionItem
        {
            Id           = Guid.NewGuid(),
            ActionId     = "ACT-" + Guid.NewGuid().ToString("N")[..6],
            Title        = $"{status} due {dueInDays}",
            Description  = "x",
            WorkspaceId  = workspace ?? Ws1,
            ProjectId    = projectId,
            IsStandalone = projectId == null,
            Priority     = priority,
            Status       = status,
            DueDate      = Today.AddDays(dueInDays).AddHours(9),
            CreatedAt    = Today.AddDays(-createdDaysAgo).AddHours(9),
            UpdatedAt    = Today.AddDays(-(updatedDaysAgo ?? 0)).AddHours(9),
            IsEscalated  = escalated,
        };
        item.Assignees.Add(new ActionItemAssignee { ActionItemId = item.Id, UserId = assignee });
        _db.ActionItems.Add(item);
        return item;
    }

    private Project AddProject(
        string name, ProjectStatus status, int endsInDays, Guid? workspace = null,
        ProjectType type = ProjectType.Operational, Guid? objectiveId = null)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(), ProjectCode = "PRJ-" + name, Name = name,
            WorkspaceId = workspace ?? Ws1, ProjectType = type, Status = status,
            Priority = ActionPriority.Medium, ProjectManagerUserId = Alice,
            StrategicObjectiveId = objectiveId,
            PlannedStartDate = Today.AddDays(-30), PlannedEndDate = Today.AddDays(endsInDays),
        };
        _db.Projects.Add(project);
        return project;
    }

    private ActionItemService MakeActionItemService() => new(
        _db, Mock.Of<ILogger<ActionItemService>>(), Mock.Of<IEmailSender>(), Mock.Of<INotificationService>(),
        Options.Create(new AppSettings { FrontendBaseUrl = "http://localhost" }), Mock.Of<IServiceScopeFactory>(),
        Mock.Of<IWorkCalendarService>()); // list queries only — the working-day rule is never reached

    private async Task<int> ListCountAsync(Action<ActionItemFilterDto> configure)
    {
        var filter = new ActionItemFilterDto { PageSize = 100 };
        configure(filter);
        return (await MakeActionItemService().GetAllAsync(filter, CancellationToken.None)).TotalCount;
    }

    /// <summary>A spread of statuses, due dates and ages that lands items on both sides of every boundary.</summary>
    private void SeedMatrix()
    {
        var statuses = new[]
        {
            ActionStatus.ToDo, ActionStatus.InProgress, ActionStatus.InReview, ActionStatus.Done,
            ActionStatus.Overdue, ActionStatus.Deferred, ActionStatus.Cancelled,
        };
        var dueOffsets = new[] { -10, -1, 0, 1, 7, 8, 30, 31, 90 };
        var ages       = new[] { 0, 7, 8, 30, 31, 90, 91, 200 };

        var i = 0;
        foreach (var status in statuses)
            foreach (var due in dueOffsets)
                AddItem(status, due, createdDaysAgo: ages[i++ % ages.Length]);

        _db.SaveChanges();
    }

    // ── A chart segment and the list it drills down to must agree ────────────

    [Fact]
    public async Task DueBuckets_MatchTheListFilterForEveryBucket()
    {
        SeedMatrix();
        var dashboard = await _service.GetActionItemsAsync(CancellationToken.None);

        dashboard.ByDue.Sum(s => s.Count).Should().Be(dashboard.Open, "every open item falls in exactly one due window");
        foreach (var slice in dashboard.ByDue)
        {
            var listed = await ListCountAsync(f => f.DueBucket = slice.Key);
            listed.Should().Be(slice.Count, $"the '{slice.Key}' drill-down must list what the chart counted");
        }
    }

    [Fact]
    public async Task AgeBuckets_MatchTheListFilterForEveryBucket()
    {
        SeedMatrix();
        var dashboard = await _service.GetActionItemsAsync(CancellationToken.None);

        dashboard.ByAge.Sum(s => s.Count).Should().Be(dashboard.Open);
        foreach (var slice in dashboard.ByAge)
        {
            var listed = await ListCountAsync(f => f.AgeBucket = slice.Key);
            listed.Should().Be(slice.Count, $"the '{slice.Key}' drill-down must list what the chart counted");
        }
    }

    [Fact]
    public async Task OpenOverdueAndEscalatedTotals_MatchTheirListFilters()
    {
        SeedMatrix();
        AddItem(ActionStatus.InProgress, 5, escalated: true);
        _db.SaveChanges();

        var dashboard = await _service.GetActionItemsAsync(CancellationToken.None);

        (await ListCountAsync(f => f.OpenOnly = true)).Should().Be(dashboard.Open);
        (await ListCountAsync(f => f.OverdueOnly = true)).Should().Be(dashboard.Overdue);
        (await ListCountAsync(f => f.IsEscalated = true)).Should().Be(dashboard.Escalated).And.Be(1);
        foreach (var slice in dashboard.ByStatus)
            (await ListCountAsync(f => f.Status = (ActionStatus)int.Parse(slice.Key))).Should().Be(slice.Count);
    }

    [Fact]
    public async Task Overdue_CountsOpenItemsPastDueEvenWithoutTheOverdueStatus()
    {
        AddItem(ActionStatus.InProgress, -3);   // late, job has not flagged it yet
        AddItem(ActionStatus.Overdue, 2);       // flagged
        AddItem(ActionStatus.Done, -3);         // finished: never overdue
        AddItem(ActionStatus.Deferred, -3);     // parked: not open
        AddItem(ActionStatus.ToDo, 0);          // due today: not late yet
        _db.SaveChanges();

        var dashboard = await _service.GetActionItemsAsync(CancellationToken.None);

        dashboard.Overdue.Should().Be(2);
        dashboard.Open.Should().Be(3);
    }

    [Fact]
    public async Task UnknownBucketKey_IsIgnoredRatherThanReturningNothing()
    {
        AddItem(ActionStatus.ToDo, 3);
        _db.SaveChanges();

        (await ListCountAsync(f => f.DueBucket = "not-a-bucket")).Should().Be(1);
    }

    // ── Scoping ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ActionItems_AreLimitedToVisibleWorkspaces()
    {
        AddItem(ActionStatus.ToDo, 3, workspace: Ws1);
        AddItem(ActionStatus.ToDo, 3, workspace: Ws2);
        AddItem(ActionStatus.Done, 3, workspace: Ws2);
        _db.SaveChanges();

        _workspaceAccess.Setup(r => r.GetVisibleWorkspaceIdsAsync(Me)).ReturnsAsync(new List<Guid> { Ws1 });
        var scoped = await _service.GetActionItemsAsync(CancellationToken.None);
        scoped.Total.Should().Be(1);
        scoped.ByWorkspace.Should().ContainSingle().Which.WorkspaceId.Should().Be(Ws1);

        _workspaceAccess.Setup(r => r.GetVisibleWorkspaceIdsAsync(Me)).ReturnsAsync(new List<Guid>());
        var none = await _service.GetActionItemsAsync(CancellationToken.None);
        none.Total.Should().Be(0);
        none.Team.Should().BeEmpty();
    }

    [Fact]
    public async Task MyActions_CountsOnlyTheCallersItems_InAnyWorkspace()
    {
        AddItem(ActionStatus.InProgress, 3, assignee: Me, workspace: Ws1, priority: ActionPriority.Critical);
        AddItem(ActionStatus.Done, 3, assignee: Me, workspace: Ws2);
        AddItem(ActionStatus.ToDo, -2, assignee: Me, workspace: Ws2);
        AddItem(ActionStatus.ToDo, 3, assignee: Alice);
        _db.SaveChanges();

        // Even a caller who can see no workspace still sees their own assignments
        _workspaceAccess.Setup(r => r.GetVisibleWorkspaceIdsAsync(Me)).ReturnsAsync(new List<Guid>());

        var mine = await _service.GetMyActionsAsync(CancellationToken.None);

        mine.Total.Should().Be(3);
        mine.Open.Should().Be(2);
        mine.Completed.Should().Be(1);
        mine.Overdue.Should().Be(1);
        mine.DueThisWeek.Should().Be(1);
        mine.CompletionRate.Should().Be(33.3m);
        mine.ByPriority.Single(s => s.Key == ((int)ActionPriority.Critical).ToString()).Count.Should().Be(1);
        mine.ByPriority.Sum(s => s.Count).Should().Be(mine.Open, "priority is charted for open items only");
    }

    // ── Team activity ────────────────────────────────────────────────────────

    [Fact]
    public async Task TeamActivity_IsSortedByTaskCountDescending()
    {
        AddItem(ActionStatus.ToDo, 3, assignee: Bob);
        AddItem(ActionStatus.InProgress, 3, assignee: Alice);
        AddItem(ActionStatus.Done, 3, assignee: Alice);
        AddItem(ActionStatus.ToDo, -4, assignee: Alice);
        _db.SaveChanges();

        var team = await _service.GetTeamActivityAsync(CancellationToken.None);

        team.Select(m => m.UserId).Should().Equal(Alice, Bob);
        var alice = team[0];
        alice.UserName.Should().Be("Alice Smith");
        alice.Total.Should().Be(3);
        alice.Open.Should().Be(2);
        alice.InProgress.Should().Be(1);
        alice.Completed.Should().Be(1);
        alice.Overdue.Should().Be(1);
        alice.CompletionRate.Should().Be(33.3m);
    }

    // ── Projects ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Projects_GetProgressAndHealthFromTheirActionItemsAndMilestones()
    {
        var late      = AddProject("Late", ProjectStatus.Active, endsInDays: -5);
        var risky     = AddProject("Risky", ProjectStatus.Active, endsInDays: 40);
        var healthy   = AddProject("Healthy", ProjectStatus.Active, endsInDays: 40);
        var finished  = AddProject("Finished", ProjectStatus.Completed, endsInDays: -20);
        var milestone = AddProject("MilestoneOnly", ProjectStatus.Active, endsInDays: 40);
        AddProject("Draft", ProjectStatus.Draft, endsInDays: 60);

        AddItem(ActionStatus.Done, -1, projectId: late.Id);
        AddItem(ActionStatus.ToDo, 5, projectId: late.Id);
        AddItem(ActionStatus.InProgress, -2, projectId: risky.Id);      // overdue → at risk
        AddItem(ActionStatus.Done, 5, projectId: healthy.Id);
        AddItem(ActionStatus.Done, 5, projectId: healthy.Id);
        AddItem(ActionStatus.InProgress, 5, projectId: healthy.Id);
        AddItem(ActionStatus.InProgress, 5, projectId: healthy.Id);

        _db.Milestones.AddRange(
            new Milestone { Id = Guid.NewGuid(), MilestoneCode = "M1", Name = "Half", ProjectId = milestone.Id,
                Status = MilestoneStatus.InProgress, PlannedDueDate = Today.AddDays(10), CompletionPercentage = 40 },
            new Milestone { Id = Guid.NewGuid(), MilestoneCode = "M2", Name = "Done", ProjectId = milestone.Id,
                Status = MilestoneStatus.Completed, PlannedDueDate = Today.AddDays(-10), CompletionPercentage = 100 },
            new Milestone { Id = Guid.NewGuid(), MilestoneCode = "M3", Name = "Slipped", ProjectId = healthy.Id,
                Status = MilestoneStatus.InProgress, PlannedDueDate = Today.AddDays(-2), CompletionPercentage = 10 });
        _db.SaveChanges();

        var dashboard = await _service.GetProjectsAsync(CancellationToken.None);
        ProjectRowOf(late).Health.Should().Be(ProjectRules.HealthDelayed);
        ProjectRowOf(late).Progress.Should().Be(50m);
        ProjectRowOf(late).DaysToEnd.Should().Be(-5);
        ProjectRowOf(risky).Health.Should().Be(ProjectRules.HealthAtRisk);
        // A slipped milestone puts an otherwise healthy project at risk
        ProjectRowOf(healthy).Health.Should().Be(ProjectRules.HealthAtRisk);
        ProjectRowOf(healthy).Progress.Should().Be(50m);
        ProjectRowOf(finished).Health.Should().Be(ProjectRules.HealthCompleted);
        ProjectRowOf(finished).Progress.Should().Be(100m);
        // No action items → mean of its milestones
        ProjectRowOf(milestone).Progress.Should().Be(70m);
        ProjectRowOf(milestone).Health.Should().Be(ProjectRules.HealthOnTrack);

        dashboard.Total.Should().Be(6);
        dashboard.Active.Should().Be(4);
        dashboard.Completed.Should().Be(1);
        dashboard.Delayed.Should().Be(1);
        dashboard.AtRisk.Should().Be(2);
        dashboard.OnTrack.Should().Be(1);
        dashboard.MilestonesTotal.Should().Be(3);
        dashboard.MilestonesCompleted.Should().Be(1);
        dashboard.MilestonesDelayed.Should().Be(1);
        dashboard.MilestonesDueSoon.Should().Be(1);
        dashboard.Projects.First().Id.Should().Be(late.Id, "delayed projects are listed first");

        Application.Features.Dashboard.DTOs.ProjectRowDto ProjectRowOf(Project p) =>
            dashboard.Projects.Single(r => r.Id == p.Id);
    }

    [Fact]
    public async Task Projects_AreLimitedToVisibleWorkspaces()
    {
        AddProject("Mine", ProjectStatus.Active, 10, workspace: Ws1);
        AddProject("Theirs", ProjectStatus.Active, 10, workspace: Ws2);
        _db.SaveChanges();

        _workspaceAccess.Setup(r => r.GetVisibleWorkspaceIdsAsync(Me)).ReturnsAsync(new List<Guid> { Ws1 });

        var dashboard = await _service.GetProjectsAsync(CancellationToken.None);
        dashboard.Projects.Should().ContainSingle().Which.Name.Should().Be("Mine");
    }

    // ── Strategy ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Strategy_RollsKpiAchievementUpToObjectivesAndPlans()
    {
        var year  = DateTime.UtcNow.Year;
        var unitA = new OrgUnit { Id = Guid.NewGuid(), Name = "Unit A" };
        var unitB = new OrgUnit { Id = Guid.NewGuid(), Name = "Unit B" };
        _db.OrgUnits.AddRange(unitA, unitB);

        var plan     = new StrategicPlan { Id = Guid.NewGuid(), Name = "Plan A", OrgUnitId = unitA.Id,
            Status = StrategicPlanStatus.Approved, StartYear = year, DurationYears = 3 };
        var archived = new StrategicPlan { Id = Guid.NewGuid(), Name = "Old plan", OrgUnitId = unitA.Id,
            Status = StrategicPlanStatus.Archived, StartYear = year - 5, DurationYears = 3 };
        var other    = new StrategicPlan { Id = Guid.NewGuid(), Name = "Plan B", OrgUnitId = unitB.Id,
            Status = StrategicPlanStatus.Draft, StartYear = year, DurationYears = 3 };
        _db.StrategicPlans.AddRange(plan, archived, other);

        var objective = new StrategicObjective { Id = Guid.NewGuid(), ObjectiveCode = "SO-001", Statement = "Grow",
            OrgUnitId = unitA.Id, StrategicPlanId = plan.Id };
        var otherObjective = new StrategicObjective { Id = Guid.NewGuid(), ObjectiveCode = "SO-002", Statement = "Other",
            OrgUnitId = unitB.Id, StrategicPlanId = other.Id };
        _db.StrategicObjectives.AddRange(objective, otherObjective);

        var good   = new Kpi { Id = Guid.NewGuid(), KpiNumber = 1, Name = "Good",   StrategicObjectiveId = objective.Id, Period = MeasurementPeriod.Quarterly };
        var weak   = new Kpi { Id = Guid.NewGuid(), KpiNumber = 2, Name = "Weak",   StrategicObjectiveId = objective.Id, Period = MeasurementPeriod.Quarterly };
        var silent = new Kpi { Id = Guid.NewGuid(), KpiNumber = 3, Name = "Silent", StrategicObjectiveId = objective.Id, Period = MeasurementPeriod.Yearly };
        _db.Kpis.AddRange(good, weak, silent);

        var goodTarget = new KpiTarget { Id = Guid.NewGuid(), KpiId = good.Id, Year = year, Month = 3, Target = 100, Direction = KpiDirection.HigherIsBetter };
        var weakTarget = new KpiTarget { Id = Guid.NewGuid(), KpiId = weak.Id, Year = year, Month = 3, Target = 10,  Direction = KpiDirection.LowerIsBetter };
        var lastYear   = new KpiTarget { Id = Guid.NewGuid(), KpiId = silent.Id, Year = year - 1, Month = 12, Target = 5 };
        _db.KpiTargets.AddRange(goodTarget, weakTarget, lastYear);

        _db.KpiActuals.AddRange(
            new KpiActual { Id = Guid.NewGuid(), KpiTargetId = goodTarget.Id, Year = year, ActualValue = 95, Status = KpiActualStatus.Approved },
            new KpiActual { Id = Guid.NewGuid(), KpiTargetId = weakTarget.Id, Year = year, ActualValue = 20, Status = KpiActualStatus.Approved },
            // Not approved yet: counted as pending, but must not move achievement
            new KpiActual { Id = Guid.NewGuid(), KpiTargetId = goodTarget.Id, Year = year, ActualValue = 1, Status = KpiActualStatus.PendingApproval, PeriodIndex = 2 });

        var linked = AddProject("Strategic", ProjectStatus.Completed, -3, type: ProjectType.Strategic, objectiveId: objective.Id);
        AddProject("Unlinked", ProjectStatus.Active, 30);
        _db.SaveChanges();

        // Scope the caller to Unit A only
        _strategicScope.Setup(s => s.GetVisibleOrgUnitIdsAsync(Me, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { unitA.Id });

        var dashboard = await _service.GetStrategyAsync(null, CancellationToken.None);

        dashboard.Year.Should().Be(year);
        dashboard.PlanRows.Should().ContainSingle("archived plans and other org units are excluded")
            .Which.Name.Should().Be("Plan A");
        dashboard.Objectives.Should().Be(1);
        dashboard.Kpis.Should().Be(3);
        dashboard.KpisWithData.Should().Be(2);
        dashboard.ActualsPendingApproval.Should().Be(1);

        var rows = dashboard.KpiRows.ToDictionary(k => k.Name);
        rows["Good"].Achievement.Should().Be(95m);
        rows["Good"].Health.Should().Be(KpiAchievement.HealthOnTrack);
        rows["Weak"].Achievement.Should().Be(50m, "lower-is-better: target 10 against actual 20");
        rows["Weak"].Health.Should().Be(KpiAchievement.HealthOffTrack);
        rows["Silent"].Achievement.Should().BeNull();
        rows["Silent"].Health.Should().Be(KpiAchievement.HealthNoData);
        dashboard.KpiRows.First().Name.Should().Be("Weak", "the weakest KPI is listed first");

        dashboard.AvgAchievement.Should().Be(72.5m);
        var objectiveRow = dashboard.ObjectiveRows.Single();
        objectiveRow.AvgAchievement.Should().Be(72.5m);
        objectiveRow.KpisOnTrack.Should().Be(1);
        objectiveRow.KpisOffTrack.Should().Be(1);
        objectiveRow.KpisNoData.Should().Be(1);
        objectiveRow.ProjectCount.Should().Be(1);
        objectiveRow.ProjectsCompleted.Should().Be(1);
        objectiveRow.AvgProjectProgress.Should().Be(100m);

        dashboard.ProjectRows.Should().ContainSingle().Which.Id.Should().Be(linked.Id);
        dashboard.StrategicProjects.Should().Be(1);
        dashboard.StrategicProjectsCompleted.Should().Be(1);
    }

    [Fact]
    public async Task Strategy_ReturnsAnEmptyDashboardWhenNothingIsInScope()
    {
        _strategicScope.Setup(s => s.GetVisibleOrgUnitIdsAsync(Me, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());

        var dashboard = await _service.GetStrategyAsync(2030, CancellationToken.None);

        dashboard.Year.Should().Be(2030);
        dashboard.Plans.Should().Be(0);
        dashboard.AvgAchievement.Should().BeNull();
    }

    // ── Pure rules ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(80, 100, KpiDirection.HigherIsBetter, 80)]
    [InlineData(120, 100, KpiDirection.HigherIsBetter, 120)]
    [InlineData(5, 10, KpiDirection.LowerIsBetter, 200)]
    [InlineData(0, 10, KpiDirection.LowerIsBetter, 100)]
    [InlineData(10, 10, KpiDirection.OnTarget, 100)]
    [InlineData(12, 10, KpiDirection.OnTarget, 80)]
    public void KpiAchievement_RespectsDirection(double actual, double target, KpiDirection direction, double expected) =>
        KpiAchievement.Compute((decimal)actual, (decimal)target, direction).Should().Be((decimal)expected);

    [Fact]
    public void KpiAchievement_IsUndefinedWithoutAUsableTarget()
    {
        KpiAchievement.Compute(5, null, KpiDirection.HigherIsBetter).Should().BeNull();
        KpiAchievement.Compute(5, 0, KpiDirection.HigherIsBetter).Should().BeNull();
    }

    [Theory]
    [InlineData(90, KpiAchievement.HealthOnTrack)]
    [InlineData(89.9, KpiAchievement.HealthAtRisk)]
    [InlineData(60, KpiAchievement.HealthAtRisk)]
    [InlineData(59.9, KpiAchievement.HealthOffTrack)]
    public void KpiHealth_UsesTheNinetyAndSixtyBands(double achievement, string expected) =>
        KpiAchievement.Health((decimal)achievement).Should().Be(expected);
}
