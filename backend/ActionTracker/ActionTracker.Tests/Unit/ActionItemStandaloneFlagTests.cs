using ActionTracker.Application.Common;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.ActionItems;
using ActionTracker.Application.Features.ActionItems.DTOs;
using ActionTracker.Application.Features.ActionItems.Services;
using ActionTracker.Application.Features.Calendars.Services;
using ActionTracker.Application.Features.Notifications;
using ActionTracker.Application.Features.Workflow.Interfaces;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using ActionTracker.Infrastructure.Data;
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
/// WO-2026-11 A-1b: <see cref="ActionItem.IsStandalone"/> follows the project
/// link. The client's value is ignored; the flag changes only when a save
/// changes <see cref="ActionItem.ProjectId"/>.
/// </summary>
public class ActionItemStandaloneFlagTests : IDisposable
{
    private const string UserId = "user-1";

    private static readonly DateTime Today = ActionItemActualDates.Today(null, DateTime.UtcNow);

    private readonly AppDbContext _db;
    private readonly WorkCalendarService _calendars;
    private readonly Mock<IActionItemWorkflowService> _workflow = new();

    public ActionItemStandaloneFlagTests()
    {
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var access = new Mock<IWorkspaceAccessResolver>();
        access.Setup(r => r.GetVisibleWorkspaceIdsAsync(It.IsAny<string>())).ReturnsAsync((List<Guid>?)null);
        _calendars = new WorkCalendarService(_db, access.Object, new WorkCalendarCache(), NullLogger<WorkCalendarService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

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

    private ActionItemService NewService() =>
        new(_db, NullLogger<ActionItemService>.Instance, Mock.Of<IEmailSender>(), Mock.Of<INotificationService>(),
            Options.Create(new AppSettings { FrontendBaseUrl = "http://localhost:4200" }), ScopeFactory(), _calendars, TagTestHelpers.NewTagService(_db));

    private async Task<Workspace> WorkspaceAsync()
    {
        var existing = await _db.Workspaces.FirstOrDefaultAsync();
        if (existing is not null) return existing;

        var workspace = new Workspace
        {
            Id = Guid.NewGuid(), Title = "Workspace", OrganizationUnit = "Org", CreatedAt = DateTime.UtcNow,
        };
        _db.Workspaces.Add(workspace);
        await _db.SaveChangesAsync();
        return workspace;
    }

    private async Task<Project> AddProjectAsync(ProjectStatus status)
    {
        var workspace = await WorkspaceAsync();
        var project = new Project
        {
            Id = Guid.NewGuid(), ProjectCode = "PRJ-" + Guid.NewGuid().ToString("N")[..4], Name = "Project",
            WorkspaceId = workspace.Id, ProjectType = ProjectType.Operational, Status = status,
            Priority = ActionPriority.Medium, ProjectManagerUserId = UserId,
            PlannedStartDate = Today.AddDays(-7), PlannedEndDate = Today.AddDays(30),
            Currency = "AED", CreatedBy = UserId, CreatedAt = DateTime.UtcNow,
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        return project;
    }

    private async Task<ActionItem> AddItemAsync(Guid? projectId, bool isStandalone, ActionStatus status = ActionStatus.ToDo)
    {
        var workspace = await WorkspaceAsync();
        var item = new ActionItem
        {
            Id              = Guid.NewGuid(),
            ActionId        = "ACT-" + Guid.NewGuid().ToString("N")[..4],
            Title           = "Item",
            Description     = "Description",
            WorkspaceId     = workspace.Id,
            ProjectId       = projectId,
            IsStandalone    = isStandalone,
            Status          = status,
            Priority        = ActionPriority.Medium,
            DueDate         = Today.AddDays(10),
            CreatedByUserId = UserId,
            CreatedAt       = DateTime.UtcNow,
        };
        item.Assignees.Add(new ActionItemAssignee { ActionItemId = item.Id, UserId = UserId });
        _db.ActionItems.Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    private async Task<ActionItem> ReloadAsync(Guid id)
    {
        _db.ChangeTracker.Clear();
        return await _db.ActionItems.IgnoreQueryFilters().FirstAsync(a => a.Id == id);
    }

    /// <summary>
    /// The PUT body that My Actions, workspace detail and the action form sent
    /// on every edit before A-1b: every field of the form with
    /// <c>isStandalone: true</c> and no <c>projectId</c>, dates unchanged.
    /// </summary>
    private static ActionItemUpdateDto EditScreenPayload(ActionItem item, bool? isStandalone) => new()
    {
        Id           = item.Id,
        Title        = item.Title + " (edited)",
        Description  = item.Description,
        WorkspaceId  = item.WorkspaceId,
        IsStandalone = isStandalone,
        AssigneeIds  = new List<string> { UserId },
        Priority     = item.Priority,
        Status       = item.Status,
        StartDate    = item.StartDate,
        DueDate      = item.DueDate,
        Progress     = item.Progress,
        IsEscalated  = false,
    };

    // ── Reproduction: an edit used to detach a project-linked item ───────────

    [Theory]
    [InlineData(ProjectStatus.Draft)]
    [InlineData(ProjectStatus.Active)]
    public async Task EditWithIsStandaloneTrue_KeepsAProjectLinkedItemLinked(ProjectStatus projectStatus)
    {
        var project = await AddProjectAsync(projectStatus);
        var item    = await AddItemAsync(project.Id, isStandalone: false);

        var dto = await NewService().UpdateAsync(item.Id, EditScreenPayload(item, isStandalone: true), UserId, default);

        dto.IsStandalone.Should().BeFalse();
        var saved = await ReloadAsync(item.Id);
        saved.IsStandalone.Should().BeFalse("the item is still linked to its project");
        saved.ProjectId.Should().Be(project.Id);
        saved.Title.Should().Be("Item (edited)", "the rest of the edit is applied");
    }

    [Fact]
    public async Task EditWithIsStandaloneFalse_KeepsAStandaloneItemStandalone()
    {
        var item = await AddItemAsync(projectId: null, isStandalone: true);

        await NewService().UpdateAsync(item.Id, EditScreenPayload(item, isStandalone: false), UserId, default);

        (await ReloadAsync(item.Id)).IsStandalone.Should().BeTrue();
    }

    [Fact]
    public async Task EditWithoutIsStandalone_KeepsTheFlag()
    {
        var project = await AddProjectAsync(ProjectStatus.Draft);
        var item    = await AddItemAsync(project.Id, isStandalone: false);

        await NewService().UpdateAsync(item.Id, EditScreenPayload(item, isStandalone: null), UserId, default);

        (await ReloadAsync(item.Id)).IsStandalone.Should().BeFalse();
    }

    // ── The flag changes only with the project link ───────────────────────────

    [Fact]
    public async Task LinkingAStandaloneItemToAProject_MakesItProjectLinked()
    {
        var project = await AddProjectAsync(ProjectStatus.Draft);
        var item    = await AddItemAsync(projectId: null, isStandalone: true);

        await NewService().UpdateAsync(item.Id,
            new ActionItemUpdateDto { Id = item.Id, ProjectId = project.Id, IsStandalone = true }, UserId, default);

        var saved = await ReloadAsync(item.Id);
        saved.ProjectId.Should().Be(project.Id);
        saved.IsStandalone.Should().BeFalse("a sent isStandalone: true does not win over the project link");
    }

    [Fact]
    public async Task ClearingTheProjectLink_MakesTheItemStandalone()
    {
        var project = await AddProjectAsync(ProjectStatus.Draft);
        var item    = await AddItemAsync(project.Id, isStandalone: false);

        await NewService().UpdateAsync(item.Id,
            new ActionItemUpdateDto { Id = item.Id, ProjectId = Guid.Empty, IsStandalone = false }, UserId, default);

        var saved = await ReloadAsync(item.Id);
        saved.ProjectId.Should().BeNull();
        saved.IsStandalone.Should().BeTrue();
    }

    [Fact]
    public async Task SendingTheSameProjectAgain_KeepsTheStoredFlag()
    {
        // A row that is already inconsistent (linked, flagged standalone) is not
        // repaired on the side by an edit that re-sends its own project.
        var project = await AddProjectAsync(ProjectStatus.Draft);
        var item    = await AddItemAsync(project.Id, isStandalone: true);

        await NewService().UpdateAsync(item.Id,
            new ActionItemUpdateDto { Id = item.Id, ProjectId = project.Id, IsStandalone = false, Title = "Renamed" },
            UserId, default);

        var saved = await ReloadAsync(item.Id);
        saved.IsStandalone.Should().BeTrue();
        saved.Title.Should().Be("Renamed");
    }

    [Fact]
    public async Task AnUnrelatedEdit_LeavesAnInconsistentRowAsItIs()
    {
        var project = await AddProjectAsync(ProjectStatus.Draft);
        var item    = await AddItemAsync(project.Id, isStandalone: true);

        await NewService().UpdateAsync(item.Id,
            new ActionItemUpdateDto { Id = item.Id, Priority = ActionPriority.High }, UserId, default);

        (await ReloadAsync(item.Id)).IsStandalone.Should().BeTrue();
    }

    [Fact]
    public async Task ADoneItem_AcceptsActualDatesEvenWhenIsStandaloneIsSent()
    {
        // The field is ignored, so it does not turn an actual-dates-only save
        // into a refused edit of a Done item (A-1).
        var project = await AddProjectAsync(ProjectStatus.Active);
        var item    = await AddItemAsync(project.Id, isStandalone: false, ActionStatus.Done);

        var dto = await NewService().UpdateAsync(item.Id,
            new ActionItemUpdateDto { Id = item.Id, IsStandalone = true, ActualEndDate = Today }, UserId, default);

        dto.ActualEndDate.Should().Be(Today);
        (await ReloadAsync(item.Id)).IsStandalone.Should().BeFalse();
    }

    // ── Create ────────────────────────────────────────────────────────────────

    private async Task<ActionItemResponseDto> CreateAsync(Guid? projectId, bool isStandalone)
    {
        var workspace = await WorkspaceAsync();
        return await NewService().CreateAsync(new ActionItemCreateDto
        {
            Title        = "New",
            WorkspaceId  = workspace.Id,
            ProjectId    = projectId,
            IsStandalone = isStandalone,
            AssigneeIds  = new List<string> { UserId },
            Priority     = ActionPriority.Medium,
            DueDate      = Today.AddDays(10),
        }, UserId, default);
    }

    [Fact]
    public async Task Create_WithAProject_IsProjectLinked_WhateverIsStandaloneSays()
    {
        var project = await AddProjectAsync(ProjectStatus.Draft);

        var created = await CreateAsync(project.Id, isStandalone: true);

        created.IsStandalone.Should().BeFalse();
        (await ReloadAsync(created.Id)).IsStandalone.Should().BeFalse();
    }

    [Fact]
    public async Task Create_WithoutAProject_IsStandalone_WhateverIsStandaloneSays()
    {
        var created = await CreateAsync(projectId: null, isStandalone: false);

        created.IsStandalone.Should().BeTrue();
    }

    // ── The rule ──────────────────────────────────────────────────────────────

    [Fact]
    public void ForProject_TreatsNullAndEmptyAsNoProject()
    {
        ActionItemStandalone.ForProject(null).Should().BeTrue();
        ActionItemStandalone.ForProject(Guid.Empty).Should().BeTrue();
        ActionItemStandalone.ForProject(Guid.NewGuid()).Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AfterUpdate_KeepsTheStoredFlag_WhenTheLinkIsUnchanged(bool stored)
    {
        var projectId = Guid.NewGuid();
        ActionItemStandalone.AfterUpdate(stored, projectId, projectId).Should().Be(stored);
        ActionItemStandalone.AfterUpdate(stored, null, null).Should().Be(stored);
    }

    [Fact]
    public void AfterUpdate_FollowsANewLink()
    {
        ActionItemStandalone.AfterUpdate(true, null, Guid.NewGuid()).Should().BeFalse();
        ActionItemStandalone.AfterUpdate(false, Guid.NewGuid(), null).Should().BeTrue();
        ActionItemStandalone.AfterUpdate(true, Guid.NewGuid(), Guid.NewGuid()).Should().BeFalse();
    }
}
