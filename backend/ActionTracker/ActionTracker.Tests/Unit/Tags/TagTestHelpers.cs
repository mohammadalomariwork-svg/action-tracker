using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.Tags.Interfaces;
using ActionTracker.Application.Features.Tags.Services;
using ActionTracker.Application.Helpers;

namespace ActionTracker.Tests.Unit.Tags;

/// <summary>A tag-table check with a fixed answer (WO-2026-11 A-5).</summary>
public sealed class FixedTagSchemaState : ITagSchemaState
{
    public bool Available { get; set; }
    public FixedTagSchemaState(bool available) => Available = available;
    public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(Available);
}

/// <summary>Every workspace visible (privileged role), or the given ones.</summary>
public sealed class FixedWorkspaceAccess : IWorkspaceAccessResolver
{
    private readonly List<Guid>? _ids;
    public FixedWorkspaceAccess(List<Guid>? ids = null) => _ids = ids;
    public Task<List<Guid>?> GetVisibleWorkspaceIdsAsync(string userId) => Task.FromResult(_ids);
    // WO-2026-11 P-2: no project roles; every workspace visible at workspace level.
    public Task<List<Guid>> GetProjectRoleProjectIdsAsync(string userId) => Task.FromResult(new List<Guid>());
    public Task<bool> IsWorkspaceLevelVisibleAsync(string? userId, Guid workspaceId) => Task.FromResult(true);
}

public static class TagTestHelpers
{
    /// <summary>The real tag service over <paramref name="db"/>; tag tables present unless told otherwise.</summary>
    public static IActionItemTagService NewTagService(
        IAppDbContext db, bool available = true, IWorkspaceAccessResolver? access = null) =>
        new ActionItemTagService(db, new FixedTagSchemaState(available), access ?? new FixedWorkspaceAccess());

    public static IActionItemTagService NewTagService(
        IAppDbContext db, ITagSchemaState schema, IWorkspaceAccessResolver? access = null) =>
        new ActionItemTagService(db, schema, access ?? new FixedWorkspaceAccess());
}
