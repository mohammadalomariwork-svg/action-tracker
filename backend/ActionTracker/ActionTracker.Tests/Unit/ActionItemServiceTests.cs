using ActionTracker.Application.Features.ActionItems.DTOs;
using ActionTracker.Application.Features.ActionItems.Services;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using ActionTracker.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace ActionTracker.Tests.Unit;

public class ActionItemServiceTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly ActionItemService _service;
    private readonly Mock<ILogger<ActionItemService>> _loggerMock;

    private const string UserId1 = "user-001";
    private const string UserId2 = "user-002";

    private static readonly Guid WorkspaceId = Guid.NewGuid();

    public ActionItemServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext  = new AppDbContext(options);
        _loggerMock = new Mock<ILogger<ActionItemService>>();
        _service    = new ActionItemService(_dbContext, _loggerMock.Object);
    }

    public void Dispose() => _dbContext.Dispose();

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static ApplicationUser MakeUser(
        string id, string firstName, string lastName, string email) =>
        new()
        {
            Id        = id,
            UserName  = email,
            Email     = email,
            FirstName = firstName,
            LastName  = lastName,
            IsActive  = true,
            CreatedAt = DateTime.UtcNow,
        };

    private static Workspace MakeWorkspace() => new()
    {
        Id               = WorkspaceId,
        Title            = "Test Workspace",
        OrganizationUnit = "Test Org",
        IsActive         = true,
        CreatedAt        = DateTime.UtcNow,
    };

    /// <summary>
    /// Creates an ActionItem with Guid PK and assigns it to the given user via junction table.
    /// </summary>
    private static ActionItem MakeItem(
        string         actionId,
        string         title,
        string         assigneeId,
        ActionStatus   status    = ActionStatus.ToDo,
        ActionPriority priority  = ActionPriority.Medium,
        DateTime?      dueDate   = null,
        bool           isDeleted = false)
    {
        var item = new ActionItem
        {
            Id          = Guid.NewGuid(),
            ActionId    = actionId,
            Title       = title,
            Description = "Test description",
            WorkspaceId = WorkspaceId,
            Priority    = priority,
            Status      = status,
            DueDate     = dueDate ?? DateTime.UtcNow.AddDays(7),
            Progress    = 0,
            IsDeleted   = isDeleted,
            CreatedAt   = DateTime.UtcNow,
        };

        item.Assignees.Add(new ActionItemAssignee
        {
            ActionItemId = item.Id,
            UserId       = assigneeId,
        });

        return item;
    }

    private async Task SeedUsersAndWorkspaceAsync()
    {
        _dbContext.Users.AddRange(
            MakeUser(UserId1, "Alice", "Smith", "alice@test.com"),
            MakeUser(UserId2, "Bob",   "Jones", "bob@test.com"));
        _dbContext.Workspaces.Add(MakeWorkspace());
        await _dbContext.SaveChangesAsync();
    }

    // -------------------------------------------------------------------------
    // 1. GetAllAsync — no filters returns only non-deleted items
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAllAsync_WithNoFilters_ReturnsAllActiveItems()
    {
        // Arrange
        await SeedUsersAndWorkspaceAsync();
        _dbContext.ActionItems.AddRange(
            MakeItem("ACT-001", "Fix Bug",      UserId1),
            MakeItem("ACT-002", "Write Docs",   UserId2),
            MakeItem("ACT-003", "Deleted Task", UserId1, isDeleted: true));
        await _dbContext.SaveChangesAsync();

        var filter = new ActionItemFilterDto();

        // Act
        var result = await _service.GetAllAsync(filter, CancellationToken.None);

        // Assert
        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Items.Should().NotContain(i => i.Title == "Deleted Task");
    }

    // -------------------------------------------------------------------------
    // 2. GetAllAsync — status filter returns only matching items
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAllAsync_WithStatusFilter_ReturnsFilteredItems()
    {
        // Arrange
        await SeedUsersAndWorkspaceAsync();
        _dbContext.ActionItems.AddRange(
            MakeItem("ACT-001", "Todo Task",        UserId1, ActionStatus.ToDo),
            MakeItem("ACT-002", "In-Progress Task", UserId1, ActionStatus.InProgress),
            MakeItem("ACT-003", "Done Task",        UserId2, ActionStatus.Done));
        await _dbContext.SaveChangesAsync();

        var filter = new ActionItemFilterDto { Status = ActionStatus.InProgress };

        // Act
        var result = await _service.GetAllAsync(filter, CancellationToken.None);

        // Assert
        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(i => i.Title == "In-Progress Task");
    }

    // -------------------------------------------------------------------------
    // 3. GetAllAsync — search term matches title (case-insensitive)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAllAsync_WithSearchTerm_ReturnsMatchingItems()
    {
        // Arrange
        await SeedUsersAndWorkspaceAsync();
        _dbContext.ActionItems.AddRange(
            MakeItem("ACT-001", "Deploy to Production", UserId1),
            MakeItem("ACT-002", "Update Unit Tests",    UserId1),
            MakeItem("ACT-003", "Review Pull Request",  UserId2));
        await _dbContext.SaveChangesAsync();

        var filter = new ActionItemFilterDto { SearchTerm = "production" };

        // Act
        var result = await _service.GetAllAsync(filter, CancellationToken.None);

        // Assert
        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(i => i.Title == "Deploy to Production");
    }

    // -------------------------------------------------------------------------
    // 4. GetByIdAsync — valid id returns mapped DTO with assignee details
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_WithValidId_ReturnsItem()
    {
        // Arrange
        await SeedUsersAndWorkspaceAsync();
        var entity = MakeItem("ACT-001", "Fix Login Bug", UserId1);
        _dbContext.ActionItems.Add(entity);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetByIdAsync(entity.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().Be("Fix Login Bug");
        result.ActionId.Should().Be("ACT-001");
        result.Assignees.Should().ContainSingle(a => a.FullName == "Alice Smith");
    }

    // -------------------------------------------------------------------------
    // 5. GetByIdAsync — invalid id returns null
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange — empty database

        // Act
        var result = await _service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // 6. CreateAsync — ActionId format increments from existing count
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_WithValidData_CreatesItemWithAutoId()
    {
        // Arrange
        await SeedUsersAndWorkspaceAsync();
        var existing1 = MakeItem("ACT-001", "Existing Item 1", UserId1);
        var existing2 = MakeItem("ACT-002", "Existing Item 2", UserId2);
        _dbContext.ActionItems.AddRange(existing1, existing2);
        await _dbContext.SaveChangesAsync();

        var dto = new ActionItemCreateDto
        {
            Title       = "New Action Item",
            WorkspaceId = WorkspaceId,
            AssigneeIds = new List<string> { UserId1 },
            Priority    = ActionPriority.High,
            Status      = ActionStatus.ToDo,
            DueDate     = DateTime.UtcNow.AddDays(14),
        };

        // Act
        var result = await _service.CreateAsync(dto, UserId1, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("New Action Item");
        result.ActionId.Should().Be("ACT-003");
        result.Assignees.Should().ContainSingle(a => a.FullName == "Alice Smith");
    }

    // -------------------------------------------------------------------------
    // 7. CreateAsync — first item in empty table gets ACT-001
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_FirstItem_GeneratesACT001Id()
    {
        // Arrange
        await SeedUsersAndWorkspaceAsync();
        var dto = new ActionItemCreateDto
        {
            Title       = "First Action",
            WorkspaceId = WorkspaceId,
            AssigneeIds = new List<string> { UserId1 },
            Priority    = ActionPriority.Low,
            Status      = ActionStatus.ToDo,
            DueDate     = DateTime.UtcNow.AddDays(7),
        };

        // Act
        var result = await _service.CreateAsync(dto, UserId1, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ActionId.Should().Be("ACT-001");
    }

    // -------------------------------------------------------------------------
    // 8. UpdateStatusAsync — setting Done auto-sets Progress to 100
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateStatusAsync_SetToDone_SetsProgressTo100()
    {
        // Arrange
        await SeedUsersAndWorkspaceAsync();
        var entity = MakeItem("ACT-001", "Almost Done", UserId1);
        _dbContext.ActionItems.Add(entity);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.UpdateStatusAsync(entity.Id, ActionStatus.Done, CancellationToken.None);

        // Assert
        var updated = await _dbContext.ActionItems.FindAsync(entity.Id);
        updated!.Status.Should().Be(ActionStatus.Done);
        updated.Progress.Should().Be(100);
    }

    // -------------------------------------------------------------------------
    // 9. DeleteAsync — sets IsDeleted = true (soft delete) without removing row
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_SoftDeletesItem()
    {
        // Arrange
        await SeedUsersAndWorkspaceAsync();
        var entity = MakeItem("ACT-001", "Task To Delete", UserId1);
        _dbContext.ActionItems.Add(entity);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.DeleteAsync(entity.Id, CancellationToken.None);

        // Assert — IgnoreQueryFilters bypasses the global IsDeleted filter so we
        // can confirm the row still exists in the store with IsDeleted = true
        var deleted = await _dbContext.ActionItems
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == entity.Id);

        deleted.Should().NotBeNull();
        deleted!.IsDeleted.Should().BeTrue();

        // Confirm the item is invisible through normal queries (filter applied)
        var visible = await _dbContext.ActionItems
            .FirstOrDefaultAsync(a => a.Id == entity.Id);

        visible.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // 10. ProcessOverdueItemsAsync — marks past-due, non-Done items as Overdue
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessOverdueItemsAsync_UpdatesOverdueItems()
    {
        // Arrange
        await SeedUsersAndWorkspaceAsync();
        var overdueA = MakeItem("ACT-001", "Overdue Task 1", UserId1, ActionStatus.ToDo,
            dueDate: DateTime.UtcNow.AddDays(-5));
        var overdueB = MakeItem("ACT-002", "Overdue Task 2", UserId2, ActionStatus.InProgress,
            dueDate: DateTime.UtcNow.AddDays(-1));
        var future   = MakeItem("ACT-003", "Future Task",    UserId1, ActionStatus.ToDo,
            dueDate: DateTime.UtcNow.AddDays(7));
        var done     = MakeItem("ACT-004", "Done Task",      UserId2, ActionStatus.Done,
            dueDate: DateTime.UtcNow.AddDays(-3));  // past due but already Done

        _dbContext.ActionItems.AddRange(overdueA, overdueB, future, done);
        await _dbContext.SaveChangesAsync();

        // Act
        var count = await _service.ProcessOverdueItemsAsync(CancellationToken.None);

        // Assert
        count.Should().Be(2);

        var updatedOverdueA = await _dbContext.ActionItems.FindAsync(overdueA.Id);
        var updatedOverdueB = await _dbContext.ActionItems.FindAsync(overdueB.Id);
        var updatedFuture   = await _dbContext.ActionItems.FindAsync(future.Id);
        var updatedDone     = await _dbContext.ActionItems.FindAsync(done.Id);

        updatedOverdueA!.Status.Should().Be(ActionStatus.Overdue);   // was ToDo + past due
        updatedOverdueB!.Status.Should().Be(ActionStatus.Overdue);   // was InProgress + past due
        updatedFuture!.Status.Should().Be(ActionStatus.ToDo);        // future — not changed
        updatedDone!.Status.Should().Be(ActionStatus.Done);          // Done — excluded from processing
    }
}
