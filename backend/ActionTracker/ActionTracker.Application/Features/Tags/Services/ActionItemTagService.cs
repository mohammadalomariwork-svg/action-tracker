using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.ActionItems.DTOs;
using ActionTracker.Application.Features.Tags.DTOs;
using ActionTracker.Application.Features.Tags.Interfaces;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ActionTracker.Application.Features.Tags.Services;

/// <inheritdoc />
public sealed class ActionItemTagService : IActionItemTagService
{
    public const int MaxListLimit = 500;

    private readonly IAppDbContext _db;
    private readonly ITagSchemaState _schema;
    private readonly IWorkspaceAccessResolver _access;

    public ActionItemTagService(IAppDbContext db, ITagSchemaState schema, IWorkspaceAccessResolver access)
    {
        _db     = db;
        _schema = schema;
        _access = access;
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default) => _schema.IsAvailableAsync(ct);

    public async Task EnsureAvailableAsync(CancellationToken ct = default)
    {
        if (!await _schema.IsAvailableAsync(ct))
            throw new TagsUnavailableException();
    }

    // -------------------------------------------------------------------------
    // Save
    // -------------------------------------------------------------------------

    public async Task<IReadOnlyList<Tag>> ResolveAsync(
        Guid workspaceId, IReadOnlyList<string> cleanedNames, string? userId, CancellationToken ct = default)
    {
        if (cleanedNames.Count == 0) return [];
        await EnsureAvailableAsync(ct);

        var keys = cleanedNames.Select(TagRules.Normalize).Distinct().ToList();

        for (var attempt = 1; ; attempt++)
        {
            // Exact names in this one workspace only, so an item can never be
            // linked to another workspace's tag. IgnoreQueryFilters: the item
            // may be moving into a workspace whose tags the caller cannot list;
            // nothing is returned to the caller beyond the names they sent.
            var existing = await _db.Tags.IgnoreQueryFilters()
                .Where(t => t.WorkspaceId == workspaceId && keys.Contains(t.NormalizedName))
                .ToListAsync(ct);

            var byKey   = existing.ToDictionary(t => t.NormalizedName, StringComparer.Ordinal);
            var created = new List<Tag>();
            foreach (var name in cleanedNames)
            {
                var key = TagRules.Normalize(name);
                if (byKey.ContainsKey(key)) continue;
                var tag = new Tag
                {
                    Id              = Guid.NewGuid(),
                    WorkspaceId     = workspaceId,
                    Name            = name,
                    NormalizedName  = key,
                    CreatedAt       = DateTime.UtcNow,
                    CreatedByUserId = string.IsNullOrWhiteSpace(userId) ? null : userId,
                };
                byKey[key] = tag;
                created.Add(tag);
            }

            if (created.Count > 0)
            {
                _db.Tags.AddRange(created);
                try
                {
                    await _db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException) when (attempt == 1)
                {
                    // Someone created the same tag at the same moment (unique
                    // index on WorkspaceId + NormalizedName): drop ours and link
                    // theirs on the second pass.
                    foreach (var tag in created)
                        _db.Tags.Remove(tag);       // Added → Detached
                    continue;
                }
            }

            return cleanedNames.Select(n => byKey[TagRules.Normalize(n)]).Distinct().ToList();
        }
    }

    public async Task<IReadOnlyList<string>> GetItemTagNamesAsync(Guid actionItemId, CancellationToken ct = default)
    {
        if (!await _schema.IsAvailableAsync(ct)) return [];

        var names = await _db.ActionItemTags
            .Where(x => x.ActionItemId == actionItemId)
            .Select(x => x.Tag.Name)
            .ToListAsync(ct);
        return names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task ReplaceItemTagsAsync(ActionItem item, IReadOnlyList<Tag> tags, CancellationToken ct = default)
    {
        if (!await _schema.IsAvailableAsync(ct))
        {
            if (tags.Count > 0) throw new TagsUnavailableException();
            return;                                      // no tag tables, so no tags to remove
        }

        if (tags.Any(t => t.WorkspaceId != item.WorkspaceId))
            throw new InvalidOperationException("A tag must belong to the action item's workspace.");

        var current = await _db.ActionItemTags
            .Where(x => x.ActionItemId == item.Id)
            .ToListAsync(ct);

        var wanted = tags.Select(t => t.Id).ToHashSet();
        _db.ActionItemTags.RemoveRange(current.Where(c => !wanted.Contains(c.TagId)));

        var kept = current.Select(c => c.TagId).ToHashSet();
        foreach (var tag in tags.Where(t => !kept.Contains(t.Id)))
            _db.ActionItemTags.Add(new ActionItemTag { ActionItemId = item.Id, TagId = tag.Id });
    }

    // -------------------------------------------------------------------------
    // Read
    // -------------------------------------------------------------------------

    public async Task FillTagsAsync(IReadOnlyCollection<ActionItemResponseDto> items, CancellationToken ct = default)
    {
        if (items.Count == 0 || !await _schema.IsAvailableAsync(ct)) return;

        var ids = items.Select(i => i.Id).Distinct().ToList();

        // IgnoreQueryFilters so the items of an "include deleted" list keep
        // their tags; the ids come from a query that already applied the
        // visibility rules, and the private-workspace rule is applied again.
        var rows = await _db.ActionItemTags
            .IgnoreQueryFilters()
            .WhereWorkspaceVisibleTo(_db.CurrentUserId)
            .Where(x => ids.Contains(x.ActionItemId))
            .Select(x => new { x.ActionItemId, x.TagId, x.Tag.Name, x.Tag.WorkspaceId })
            .ToListAsync(ct);

        var byItem = rows.ToLookup(r => r.ActionItemId);
        foreach (var dto in items)
        {
            dto.Tags = byItem[dto.Id]
                // Never another workspace's name, whatever the link table holds.
                .Where(r => r.WorkspaceId == dto.WorkspaceId)
                .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .Select(r => new TagDto { Id = r.TagId, Name = r.Name })
                .ToList();
        }
    }

    public async Task<List<WorkspaceTagDto>> GetWorkspaceTagsAsync(
        Guid workspaceId, string userId, string? search, int limit, CancellationToken ct = default)
    {
        // Same visibility as the workspace itself: the private-workspace filter
        // and the caller's workspaces (null = privileged role, every workspace).
        var exists  = await _db.Workspaces.AnyAsync(w => w.Id == workspaceId, ct);
        var visible = await _access.GetVisibleWorkspaceIdsAsync(userId);
        if (!exists || (visible != null && !visible.Contains(workspaceId)))
            throw new KeyNotFoundException($"Workspace {workspaceId} not found.");

        if (!await _schema.IsAvailableAsync(ct)) return [];

        limit = Math.Clamp(limit, 1, MaxListLimit);
        var query = _db.Tags.Where(t => t.WorkspaceId == workspaceId);
        var key   = TagRules.Normalize(TagRules.Clean(search));
        if (key.Length > 0)
            query = query.Where(t => t.NormalizedName.Contains(key));

        var tags = await query
            .OrderBy(t => t.NormalizedName)
            .Take(limit)
            .Select(t => new { t.Id, t.Name })
            .ToListAsync(ct);

        var tagIds = tags.Select(t => t.Id).ToList();
        var counts = await _db.ActionItemTags
            .Where(x => tagIds.Contains(x.TagId))
            .GroupBy(x => x.TagId)
            .Select(g => new { TagId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(c => c.TagId, c => c.Count, ct);

        return tags
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .Select(t => new WorkspaceTagDto
            {
                Id        = t.Id,
                Name      = t.Name,
                ItemCount = counts.TryGetValue(t.Id, out var n) ? n : 0,
            })
            .ToList();
    }

    public async Task<List<MyActionTagOptionDto>> GetMyActionTagsAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || !await _schema.IsAvailableAsync(ct)) return [];

        // The same items as /my-actions (assigned to the caller, live, through
        // the private-workspace filters), so no option names a tag the caller
        // cannot already see on a row.
        var rows = await _db.ActionItemTags
            .Where(x => x.ActionItem.Assignees.Any(a => a.UserId == userId)
                        && x.Tag.WorkspaceId == x.ActionItem.WorkspaceId)
            .Select(x => new
            {
                x.TagId,
                x.Tag.Name,
                x.ActionItem.WorkspaceId,
                WorkspaceTitle = x.ActionItem.Workspace.Title,
            })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => r.TagId)
            .Select(g => new MyActionTagOptionDto
            {
                Id             = g.Key,
                Name           = g.First().Name,
                WorkspaceId    = g.First().WorkspaceId,
                WorkspaceTitle = g.First().WorkspaceTitle,
                ItemCount      = g.Count(),
            })
            .OrderBy(o => o.WorkspaceTitle, StringComparer.OrdinalIgnoreCase)
            .ThenBy(o => o.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
