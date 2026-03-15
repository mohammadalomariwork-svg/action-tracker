using ActionTracker.Domain.Entities;
using ActionTracker.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ActionTracker.Application.Helpers;

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

        var rootId = user.OrgUnitId.Value;

        var result = new List<Guid> { rootId };
        result.AddRange(await GetDescendantOrgUnitIdsAsync(rootId));

        return result;
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
