using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Entities;
using ActionTracker.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ActionTracker.Infrastructure.Services;

public class OrgUnitScopeResolver : IOrgUnitScopeResolver
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public OrgUnitScopeResolver(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db          = db;
        _userManager = userManager;
    }

    // ─────────────────────────────────────────────────────────────────────────

    public async Task<List<Guid>> GetUserOrgUnitIdsAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user?.OrgUnitId is null)
            return new List<Guid>();

        // Scope workspaces to the user's Level-2 ancestor (college / division)
        // and every org unit that descends from it.
        //
        // Org-unit level conventions:
        //   Level 1 = institution root  (only one)
        //   Level 2 = college / top-level division
        //   Level 3+ = departments, sections, labs …
        //
        // For a user at level 3+ we walk UP the parent chain until we reach
        // level 2 and use that unit as the scope root.  Users already at
        // level 1 or 2 use their own unit directly.
        var scopeRootId = await ResolveLevel2AncestorAsync(user.OrgUnitId.Value);

        var result = new List<Guid> { scopeRootId };
        result.AddRange(await GetDescendantOrgUnitIdsAsync(scopeRootId));

        return result;
    }

    /// <summary>
    /// Walks up the org-unit hierarchy from <paramref name="orgUnitId"/> and
    /// returns the ID of the Level-2 ancestor.  If the unit is already at
    /// level ≤ 2 it is returned as-is.  Falls back to the original ID when
    /// no Level-2 ancestor can be found.
    /// </summary>
    private async Task<Guid> ResolveLevel2AncestorAsync(Guid orgUnitId)
    {
        // Load the entire (non-deleted) org-unit map in one round-trip so we
        // can traverse the parent chain in memory without N+1 queries.
        var units = await _db.OrgUnits
            .Where(o => !o.IsDeleted)
            .Select(o => new { o.Id, o.Level, o.ParentId })
            .ToListAsync();

        var lookup = units.ToDictionary(o => o.Id);

        if (!lookup.TryGetValue(orgUnitId, out var current))
            return orgUnitId; // unknown unit — return original as fallback

        // Already at level 1 or 2: use as-is
        if (current.Level <= 2)
            return current.Id;

        // Walk up the parent chain until we reach level 2 (or run out of parents)
        while (current.Level > 2 && current.ParentId is not null)
        {
            if (!lookup.TryGetValue(current.ParentId.Value, out var parent))
                break;
            current = parent;
        }

        // If we landed on level 2 (or the closest ancestor we could reach), use it
        return current.Level <= 2 ? current.Id : orgUnitId;
    }

    public async Task<bool> IsOrgUnitVisibleToUserAsync(string userId, Guid orgUnitId)
    {
        var visible = await GetUserOrgUnitIdsAsync(userId);
        return visible.Contains(orgUnitId);
    }

    public async Task<List<Guid>> GetDescendantOrgUnitIdsAsync(Guid rootOrgUnitId)
    {
        // Load the full (non-deleted) org unit parent map once, then traverse
        // iteratively in memory. This avoids N+1 queries and SQL recursion.
        var parentMap = await _db.OrgUnits
            .Where(o => !o.IsDeleted && o.ParentId != null)
            .Select(o => new { o.Id, ParentId = o.ParentId!.Value })
            .ToListAsync();

        // Build a children lookup: parentId → list of child IDs
        var childrenOf = new Dictionary<Guid, List<Guid>>();
        foreach (var row in parentMap)
        {
            if (!childrenOf.TryGetValue(row.ParentId, out var list))
            {
                list = new List<Guid>();
                childrenOf[row.ParentId] = list;
            }
            list.Add(row.Id);
        }

        // Iterative BFS with a visited set to defend against cycles in data
        var descendants = new List<Guid>();
        var visited     = new HashSet<Guid> { rootOrgUnitId };
        var queue       = new Queue<Guid>();

        if (childrenOf.TryGetValue(rootOrgUnitId, out var firstLevel))
        {
            foreach (var id in firstLevel)
                queue.Enqueue(id);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (!visited.Add(current))   // already seen — skip (cycle defence)
                continue;

            descendants.Add(current);

            if (childrenOf.TryGetValue(current, out var children))
            {
                foreach (var child in children)
                {
                    if (!visited.Contains(child))
                        queue.Enqueue(child);
                }
            }
        }

        return descendants;
    }
}
