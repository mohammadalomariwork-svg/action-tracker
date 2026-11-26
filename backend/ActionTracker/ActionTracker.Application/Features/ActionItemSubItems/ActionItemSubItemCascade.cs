using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Helpers;
using Microsoft.EntityFrameworkCore;

namespace ActionTracker.Application.Features.ActionItemSubItems;

/// <summary>
/// Soft delete and restore of sub-items together with their parent action
/// items (WO-2026-11 A-2, decision D2). Deleting a parent soft-deletes its
/// live sub-items and marks them <c>DeletedWithParent</c>; restoring the parent
/// restores exactly those, so a sub-item deleted on its own stays deleted.
/// Used by <c>ActionItemService</c> (one item) and <c>ProjectService</c> (all
/// items of a project). Changes are tracked only — the caller saves.
/// </summary>
public static class ActionItemSubItemCascade
{
    /// <summary>Marks the live sub-items of <paramref name="parentIds"/> as deleted with their parent.</summary>
    public static async Task<int> DeleteWithParentsAsync(
        IAppDbContext db, IReadOnlyCollection<Guid> parentIds, CancellationToken ct)
    {
        if (parentIds.Count == 0) return 0;

        // IgnoreQueryFilters: the parent may already be flagged deleted in the
        // same unit of work. The private-workspace rule is re-applied.
        var subItems = await db.ActionItemSubItems
            .IgnoreQueryFilters()
            .WhereWorkspaceVisibleTo(db)
            .Where(s => parentIds.Contains(s.ActionItemId) && !s.IsDeleted)
            .ToListAsync(ct);

        foreach (var s in subItems)
        {
            s.IsDeleted         = true;
            s.DeletedWithParent = true;
        }
        return subItems.Count;
    }

    /// <summary>Restores the sub-items that were deleted together with <paramref name="parentIds"/>.</summary>
    public static async Task<int> RestoreWithParentsAsync(
        IAppDbContext db, IReadOnlyCollection<Guid> parentIds, CancellationToken ct)
    {
        if (parentIds.Count == 0) return 0;

        var subItems = await db.ActionItemSubItems
            .IgnoreQueryFilters()
            .WhereWorkspaceVisibleTo(db)
            .Where(s => parentIds.Contains(s.ActionItemId) && s.IsDeleted && s.DeletedWithParent)
            .ToListAsync(ct);

        foreach (var s in subItems)
        {
            s.IsDeleted         = false;
            s.DeletedWithParent = false;
        }
        return subItems.Count;
    }
}
