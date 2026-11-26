using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.ProjectClassifications.DTOs;
using ActionTracker.Application.Features.ProjectClassifications.Interfaces;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ActionTracker.Application.Features.ProjectClassifications.Services;

/// <summary>
/// <see cref="IProjectClassificationService"/> (WO-2026-11 P-1). Classifications
/// are read through their query filter (soft delete + private workspace), so a
/// private workspace's names never reach a non-member; the usage counts read
/// projects without filters on purpose, so soft-deleted projects also block a
/// delete (decision 3) — only the numbers leave the service.
/// </summary>
public class ProjectClassificationService : IProjectClassificationService
{
    private readonly IAppDbContext _db;
    private readonly IWorkspaceAccessResolver _access;

    public ProjectClassificationService(IAppDbContext db, IWorkspaceAccessResolver access)
    {
        _db     = db;
        _access = access;
    }

    // ── Reads ───────────────────────────────────────────────────────────────

    public async Task<List<ProjectClassificationDto>> GetForWorkspaceAsync(
        Guid workspaceId, string userId, CancellationToken ct = default)
    {
        await EnsureCanReadAsync(workspaceId, userId, ct);
        return await ListAsync(workspaceId, ct);
    }

    // ── Writes ──────────────────────────────────────────────────────────────

    public async Task<ProjectClassificationDto> CreateAsync(
        Guid workspaceId, CreateProjectClassificationDto dto, string userId, CancellationToken ct = default)
    {
        await EnsureCanWriteAsync(workspaceId, userId, ct);

        var name = ProjectClassificationRules.CleanOrThrow(dto.Name);
        var key  = ProjectClassificationRules.Normalize(name);
        await EnsureNameFreeAsync(workspaceId, key, name, exceptId: null, ct);

        var last = await _db.WorkspaceProjectClassifications
            .Where(c => c.WorkspaceId == workspaceId)
            .MaxAsync(c => (int?)c.SortOrder, ct) ?? 0;

        var row = new WorkspaceProjectClassification
        {
            Id              = Guid.NewGuid(),
            WorkspaceId     = workspaceId,
            Name            = name,
            NormalizedName  = key,
            SortOrder       = last + 1,
            IsActive        = true,
            CreatedAt       = DateTime.UtcNow,
            CreatedByUserId = NullIfEmpty(userId),
        };
        _db.WorkspaceProjectClassifications.Add(row);
        await SaveAsync(row, ct);

        return (await ListAsync(workspaceId, ct)).Single(c => c.Id == row.Id);
    }

    public async Task<ProjectClassificationDto> UpdateAsync(
        Guid workspaceId, Guid classificationId, UpdateProjectClassificationDto dto, string userId, CancellationToken ct = default)
    {
        await EnsureCanWriteAsync(workspaceId, userId, ct);
        var row = await FindAsync(workspaceId, classificationId, ct);

        var name = ProjectClassificationRules.CleanOrThrow(dto.Name);
        var key  = ProjectClassificationRules.Normalize(name);
        if (key != row.NormalizedName)
            await EnsureNameFreeAsync(workspaceId, key, name, exceptId: row.Id, ct);

        if (row.Name != name || row.IsActive != dto.IsActive)
        {
            row.Name            = name;
            row.NormalizedName  = key;
            row.IsActive        = dto.IsActive;
            row.UpdatedAt       = DateTime.UtcNow;
            row.UpdatedByUserId = NullIfEmpty(userId);
            await SaveAsync(row, ct);
        }

        return (await ListAsync(workspaceId, ct)).Single(c => c.Id == row.Id);
    }

    public async Task<List<ProjectClassificationDto>> ReorderAsync(
        Guid workspaceId, ReorderProjectClassificationsDto dto, string userId, CancellationToken ct = default)
    {
        await EnsureCanWriteAsync(workspaceId, userId, ct);

        var rows = await _db.WorkspaceProjectClassifications
            .Where(c => c.WorkspaceId == workspaceId)
            .ToListAsync(ct);

        var ids = dto.ClassificationIds ?? new List<Guid>();
        var byId = rows.ToDictionary(r => r.Id);
        if (ids.Count != rows.Count || ids.Distinct().Count() != ids.Count || !ids.All(byId.ContainsKey))
            throw new ArgumentException(
                "The new order must list every classification of this workspace exactly once.");

        var now = DateTime.UtcNow;
        for (var i = 0; i < ids.Count; i++)
        {
            var row = byId[ids[i]];
            if (row.SortOrder == i + 1) continue;
            row.SortOrder       = i + 1;
            row.UpdatedAt       = now;
            row.UpdatedByUserId = NullIfEmpty(userId);
        }
        await _db.SaveChangesAsync(ct);

        return await ListAsync(workspaceId, ct);
    }

    public async Task DeleteAsync(Guid workspaceId, Guid classificationId, string userId, CancellationToken ct = default)
    {
        await EnsureCanWriteAsync(workspaceId, userId, ct);
        var row = await FindAsync(workspaceId, classificationId, ct);

        var usage = await UsageAsync(new[] { row.Id }, ct);
        usage.TryGetValue(row.Id, out var counts);
        if (counts.Live + counts.Deleted > 0)
            throw new ProjectClassificationConflictException(InUseMessage(row.Name, counts.Live, counts.Deleted));

        var now = DateTime.UtcNow;
        row.IsDeleted       = true;
        row.UpdatedAt       = now;
        row.UpdatedByUserId = NullIfEmpty(userId);

        // Keep the remaining order dense (1..n).
        var remaining = await _db.WorkspaceProjectClassifications
            .Where(c => c.WorkspaceId == workspaceId && c.Id != row.Id)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.NormalizedName)
            .ToListAsync(ct);
        for (var i = 0; i < remaining.Count; i++)
        {
            if (remaining[i].SortOrder == i + 1) continue;
            remaining[i].SortOrder = i + 1;
            remaining[i].UpdatedAt = now;
            remaining[i].UpdatedByUserId = NullIfEmpty(userId);
        }

        await _db.SaveChangesAsync(ct);
    }

    // ── Rule for a project's classification ─────────────────────────────────

    public async Task EnsureAssignableAsync(
        Guid workspaceId, Guid requestedId, Guid? currentId, CancellationToken ct = default)
    {
        // Keeping what the project has is always fine, inactive or not.
        if (currentId.HasValue && requestedId == currentId.Value)
            return;

        // Through the query filter: a deleted one, or one of a private
        // workspace the caller is not part of, is simply not found.
        var row = await _db.WorkspaceProjectClassifications
            .Where(c => c.Id == requestedId)
            .Select(c => new { c.WorkspaceId, c.Name, c.IsActive })
            .FirstOrDefaultAsync(ct);

        if (row is null || row.WorkspaceId != workspaceId)
            throw new ArgumentException(
                "The chosen internal classification does not belong to this project's workspace.");

        if (!row.IsActive)
            throw new ArgumentException(
                $"The internal classification \"{row.Name}\" is inactive and cannot be chosen.");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Read access: the workspace passes the private-workspace filter, and the
    /// caller sees it through the resolver or is on its Project Managers list
    /// (decision 2 — they classify their own projects).
    /// </summary>
    private async Task EnsureCanReadAsync(Guid workspaceId, string userId, CancellationToken ct)
    {
        // P-2: workspace-level rule — the Workspace filter also lets project people's workspaces through.
        if (!await _db.Workspaces.WhereWorkspaceLevelVisibleTo(_db.CurrentUserId).AnyAsync(w => w.Id == workspaceId, ct))
            throw NotFound(workspaceId);

        var visible = await _access.GetVisibleWorkspaceIdsAsync(userId);
        if (visible is null || visible.Contains(workspaceId))
            return;

        var onPmList = !string.IsNullOrEmpty(userId) && await _db.WorkspaceProjectManagers
            .AnyAsync(p => p.WorkspaceId == workspaceId && p.UserId == userId, ct);
        if (!onPmList)
            throw NotFound(workspaceId);
    }

    /// <summary>Write access: the private-workspace filter, then the resolver (as the calendar setting).</summary>
    private async Task EnsureCanWriteAsync(Guid workspaceId, string userId, CancellationToken ct)
    {
        // P-2: workspace-level rule — the Workspace filter also lets project people's workspaces through.
        if (!await _db.Workspaces.WhereWorkspaceLevelVisibleTo(_db.CurrentUserId).AnyAsync(w => w.Id == workspaceId, ct))
            throw NotFound(workspaceId);

        var visible = await _access.GetVisibleWorkspaceIdsAsync(userId);
        if (visible != null && !visible.Contains(workspaceId))
            throw NotFound(workspaceId);
    }

    private async Task<WorkspaceProjectClassification> FindAsync(Guid workspaceId, Guid classificationId, CancellationToken ct) =>
        await _db.WorkspaceProjectClassifications
            .FirstOrDefaultAsync(c => c.Id == classificationId && c.WorkspaceId == workspaceId, ct)
        ?? throw new KeyNotFoundException($"Classification {classificationId} not found in this workspace.");

    private async Task EnsureNameFreeAsync(Guid workspaceId, string key, string name, Guid? exceptId, CancellationToken ct)
    {
        var taken = await _db.WorkspaceProjectClassifications
            .AnyAsync(c => c.WorkspaceId == workspaceId && c.NormalizedName == key
                        && (exceptId == null || c.Id != exceptId), ct);
        if (taken)
            throw new ProjectClassificationConflictException(ProjectClassificationRules.DuplicateName(name));
    }

    /// <summary>
    /// Saves <paramref name="row"/>; a clash on the unique name index (another
    /// request took the name at the same moment) becomes the duplicate-name 409.
    /// Any other database error is rethrown.
    /// </summary>
    private async Task SaveAsync(WorkspaceProjectClassification row, CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            var taken = await _db.WorkspaceProjectClassifications
                .AsNoTracking()
                .AnyAsync(c => c.WorkspaceId == row.WorkspaceId && c.NormalizedName == row.NormalizedName
                            && c.Id != row.Id, ct);
            if (!taken) throw;
            throw new ProjectClassificationConflictException(ProjectClassificationRules.DuplicateName(row.Name));
        }
    }

    private async Task<List<ProjectClassificationDto>> ListAsync(Guid workspaceId, CancellationToken ct)
    {
        var rows = await _db.WorkspaceProjectClassifications
            .AsNoTracking()
            .Where(c => c.WorkspaceId == workspaceId)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.NormalizedName)
            .Select(c => new { c.Id, c.WorkspaceId, c.Name, c.SortOrder, c.IsActive })
            .ToListAsync(ct);

        var usage = await UsageAsync(rows.Select(r => r.Id).ToList(), ct);

        return rows.Select(r =>
        {
            usage.TryGetValue(r.Id, out var counts);
            return new ProjectClassificationDto
            {
                Id                  = r.Id,
                WorkspaceId         = r.WorkspaceId,
                Name                = r.Name,
                SortOrder           = r.SortOrder,
                IsActive            = r.IsActive,
                ProjectCount        = counts.Live,
                DeletedProjectCount = counts.Deleted,
            };
        }).ToList();
    }

    /// <summary>
    /// Projects per classification, live and soft-deleted. Ignores the query
    /// filters on purpose: a soft-deleted project still holds its
    /// classification and must block a delete. Only counts leave here.
    /// </summary>
    private async Task<Dictionary<Guid, (int Live, int Deleted)>> UsageAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new();

        var rows = await _db.Projects
            .IgnoreQueryFilters()
            .Where(p => p.InternalClassificationId != null && ids.Contains(p.InternalClassificationId.Value))
            .GroupBy(p => new { Id = p.InternalClassificationId!.Value, p.IsDeleted })
            .Select(g => new { g.Key.Id, g.Key.IsDeleted, Count = g.Count() })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => r.Id)
            .ToDictionary(
                g => g.Key,
                g => (g.Where(r => !r.IsDeleted).Sum(r => r.Count), g.Where(r => r.IsDeleted).Sum(r => r.Count)));
    }

    private static string InUseMessage(string name, int live, int deleted)
    {
        static string Projects(int n) => n == 1 ? "1 project" : $"{n} projects";
        var usedBy = (live, deleted) switch
        {
            (> 0, 0)  => Projects(live),
            (0, > 0)  => deleted == 1 ? "1 deleted project" : $"{deleted} deleted projects",
            _         => $"{Projects(live + deleted)} ({deleted} of them deleted)",
        };
        return $"\"{name}\" is used by {usedBy} and cannot be deleted. " +
               "Change the classification of those projects first, or deactivate it instead.";
    }

    private static KeyNotFoundException NotFound(Guid workspaceId) =>
        new($"Workspace {workspaceId} not found.");

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
