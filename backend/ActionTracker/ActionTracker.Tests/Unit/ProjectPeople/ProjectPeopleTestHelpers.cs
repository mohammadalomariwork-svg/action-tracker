using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.ProjectPeople.Interfaces;
using ActionTracker.Application.Features.ProjectPeople.Services;
using ActionTracker.Application.Features.Workspaces.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ActionTracker.Tests.Unit.ProjectPeople;

public static class ProjectPeopleTestHelpers
{
    /// <summary>
    /// The real people service over <paramref name="db"/> (WO-2026-11 P-2). The
    /// workspace service is only used by the assignee picker; notifications run in
    /// the background through <paramref name="scopeFactory"/> and never fail a save.
    /// </summary>
    public static IProjectPeopleService NewPeopleService(
        IAppDbContext db, IServiceScopeFactory? scopeFactory = null, IWorkspaceService? workspaces = null) =>
        new ProjectPeopleService(
            db,
            new ProjectAccessService(db),
            workspaces ?? Mock.Of<IWorkspaceService>(),
            scopeFactory ?? Mock.Of<IServiceScopeFactory>(),
            NullLogger<ProjectPeopleService>.Instance);
}
