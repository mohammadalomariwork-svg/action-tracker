using ActionTracker.Application.Common;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.Calendars.Services;
using ActionTracker.Application.Features.Notifications;
using ActionTracker.Application.Features.ProjectClassifications.Services;
using ActionTracker.Application.Features.Projects.Services;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using ActionTracker.Infrastructure.Data;
using ActionTracker.Tests.Unit.Tags;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ActionTracker.Tests.Unit.ProjectClassifications;

/// <summary>
/// Shared fixture for the WO-2026-11 P-1 tests: an InMemory context whose
/// query filters read a switchable current user, three workspaces (Alpha and
/// Beta open, Hidden private) and three users.
/// </summary>
public abstract class ClassificationTestBase : IDisposable
{
    protected const string Admin    = "user-admin";     // admin of every workspace
    protected const string Pm       = "user-pm";        // on the Project Managers list of Alpha only — no admin / member
    protected const string Outsider = "user-outsider";  // member of Alpha only — not of Hidden

    protected readonly AppDbContext Db;
    protected readonly Guid Alpha = Guid.NewGuid();
    protected readonly Guid Beta = Guid.NewGuid();
    protected readonly Guid Hidden = Guid.NewGuid();

    private string? _currentUserId = Admin;

    /// <summary>What <c>IWorkspaceAccessResolver</c> answers; null = every workspace (privileged role).</summary>
    protected List<Guid>? VisibleWorkspaces;

    protected ClassificationTestBase()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(() => _currentUserId);
        Db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            currentUser.Object);

        foreach (var id in new[] { Admin, Pm, Outsider })
            Db.Users.Add(new ApplicationUser { Id = id, UserName = id, Email = id + "@t", FirstName = id, LastName = "X", CreatedAt = DateTime.UtcNow });

        Workspace Ws(Guid id, string title, bool isPrivate, params string[] members)
        {
            var ws = new Workspace { Id = id, Title = title, OrganizationUnit = "O", IsPrivate = isPrivate, CreatedAt = DateTime.UtcNow };
            ws.Admins.Add(new WorkspaceAdmin { WorkspaceId = id, AdminUserId = Admin, AdminUserName = "a" });
            foreach (var m in members)
                ws.Members.Add(new WorkspaceMember { WorkspaceId = id, UserId = m, UserName = m });
            ws.ProjectManagers.Add(new WorkspaceProjectManager { WorkspaceId = id, UserId = Admin, UserName = "a" });
            return ws;
        }
        Db.Workspaces.AddRange(
            Ws(Alpha, "Alpha", false, Outsider),
            Ws(Beta, "Beta", false),
            Ws(Hidden, "Hidden", true));
        Db.WorkspaceProjectManagers.Add(new WorkspaceProjectManager { WorkspaceId = Alpha, UserId = Pm, UserName = "pm" });
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
    }

    public void Dispose() => Db.Dispose();

    protected ProjectClassificationService Classifications() =>
        new(Db, new FixedWorkspaceAccess(VisibleWorkspaces));

    protected ProjectService Projects()
    {
        var provider = new Mock<IServiceProvider>();
        var scope = new Mock<IServiceScope>();
        scope.SetupGet(s => s.ServiceProvider).Returns(provider.Object);
        var factory = new Mock<IServiceScopeFactory>();
        factory.Setup(f => f.CreateScope()).Returns(scope.Object);

        var calendars = new WorkCalendarService(Db, new FixedWorkspaceAccess(), new WorkCalendarCache(), NullLogger<WorkCalendarService>.Instance);
        return new ProjectService(Db, NullLogger<ProjectService>.Instance, Mock.Of<IEmailSender>(), Mock.Of<INotificationService>(),
            Options.Create(new AppSettings { FrontendBaseUrl = "http://localhost:4200" }), factory.Object, calendars, Classifications(),
            ActionTracker.Tests.Unit.ProjectPeople.ProjectPeopleTestHelpers.NewPeopleService(Db, factory.Object));
    }

    /// <summary>Runs as <paramref name="user"/> (the query filters read the current user) on a clean tracker.</summary>
    protected async Task<T> As<T>(string user, Func<Task<T>> act)
    {
        var previous = _currentUserId;
        _currentUserId = user;
        Db.ChangeTracker.Clear();
        try { return await act(); }
        finally { _currentUserId = previous; Db.ChangeTracker.Clear(); }
    }

    protected Task As(string user, Func<Task> act) =>
        As<bool>(user, async () => { await act(); return true; });

    /// <summary>Inserts a classification directly (system context), bypassing the service.</summary>
    protected async Task<WorkspaceProjectClassification> AddClassificationAsync(
        Guid workspaceId, string name, int sortOrder = 1, bool isActive = true, bool isDeleted = false)
    {
        var row = new WorkspaceProjectClassification
        {
            Id = Guid.NewGuid(), WorkspaceId = workspaceId, Name = name, NormalizedName = name.ToUpperInvariant(),
            SortOrder = sortOrder, IsActive = isActive, IsDeleted = isDeleted, CreatedAt = DateTime.UtcNow,
        };
        Db.WorkspaceProjectClassifications.Add(row);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return row;
    }

    /// <summary>Inserts a project directly, optionally classified, optionally soft-deleted.</summary>
    protected async Task<Project> AddProjectAsync(
        Guid workspaceId, Guid? classificationId = null, bool isDeleted = false,
        ProjectStatus status = ProjectStatus.Draft, string name = "Project")
    {
        var project = new Project
        {
            Id = Guid.NewGuid(), ProjectCode = "PRJ-" + Guid.NewGuid().ToString("N")[..8], Name = name + " " + Guid.NewGuid().ToString("N")[..4],
            WorkspaceId = workspaceId, ProjectType = ProjectType.Operational, Status = status, Priority = ActionPriority.Medium,
            ProjectManagerUserId = Admin, InternalClassificationId = classificationId, IsDeleted = isDeleted,
            PlannedStartDate = new DateTime(2030, 1, 7, 0, 0, 0, DateTimeKind.Utc),
            PlannedEndDate   = new DateTime(2030, 6, 28, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow,
        };
        project.Sponsors.Add(new ProjectSponsor { ProjectId = project.Id, UserId = Admin });
        Db.Projects.Add(project);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return project;
    }

    /// <summary>The stored row, whatever the filters say.</summary>
    protected Task<WorkspaceProjectClassification> RawClassificationAsync(Guid id) =>
        Db.WorkspaceProjectClassifications.IgnoreQueryFilters().AsNoTracking().FirstAsync(c => c.Id == id);

    protected Task<Project> RawProjectAsync(Guid id) =>
        Db.Projects.IgnoreQueryFilters().AsNoTracking().FirstAsync(p => p.Id == id);
}
