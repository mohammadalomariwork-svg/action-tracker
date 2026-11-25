using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.Documents.DTOs;
using ActionTracker.Application.Features.Documents.Interfaces;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Constants;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Application.Features.Documents.Services;

/// <summary>
/// Polymorphic documents (<c>/api/documents</c>, and the KPI-actual evidence
/// routes). Since WO-2026-11 A-4 every list, upload, download and delete first
/// resolves the owning record and checks that the caller can see it — a hidden
/// or unknown parent is 404, also for a download by document id. Checks run in
/// this order: unsupported type (400) → parent hidden (404) → permission (403)
/// → locked (409) → file rules and caps (400).
/// <list type="bullet">
/// <item><b>ActionItem / ActionItemSubItem / ActionItemUpdate</b> — visibility through the
/// query filters (deleted, private workspace; someone else's draft update);
/// <c>ActionItems.View</c> to read, <c>ActionItems.Edit</c> to write (from <see cref="DocumentCaller"/>);
/// locked (409) while the action item is Done / Cancelled (item and sub-item files)
/// and once an update is submitted; a draft update's files only by its author;
/// a file on an item / sub-item can be deleted by its uploader, the item's creator
/// or assignees, the sub-item's assignees, or an Admin; at most 10 files per record.</item>
/// <item><b>Project / Milestone / ProjectRisk</b> — visibility through the query filters,
/// as <c>/api/comments</c>; no permission policy (unchanged).</item>
/// <item><b>KpiTarget / KpiActual</b> — the record must exist (draft targets included) and be in
/// the caller's strategic read scope (only a Strategy Editor is limited to an
/// org-unit subtree), else 404 on read; writes keep the org-unit check (403).
/// Limits and file types unchanged.</item>
/// </list>
/// </summary>
public class DocumentService : IDocumentService
{
    private readonly IAppDbContext _dbContext;
    private readonly ILogger<DocumentService> _logger;
    private readonly IStrategicScopeService _scopeService;
    private readonly IUserRoleService _roles;

    private const long MaxFileSize = 10 * 1024 * 1024; // 10 MB

    /// <summary>
    /// Per-entity-type upload caps. When an entity type appears here, no more
    /// than the listed number of documents may be attached to a single owning
    /// record. Entity types not listed have no cap.
    /// </summary>
    private static readonly Dictionary<string, int> MaxDocumentsPerEntity =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["KpiTarget"] = 10,
            ["KpiActual"] = 10,
            // WO-2026-11 A-4 (Q13)
            [DocumentEntityTypes.ActionItem]        = 10,
            [DocumentEntityTypes.ActionItemSubItem] = 10,
            [DocumentEntityTypes.ActionItemUpdate]  = 10,
        };

    /// <summary>How the cap message names the new types (the KPI messages keep the type name, as before).</summary>
    private static readonly Dictionary<string, string> CapLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        [DocumentEntityTypes.ActionItem]        = "action item",
        [DocumentEntityTypes.ActionItemSubItem] = "sub-item",
        [DocumentEntityTypes.ActionItemUpdate]  = "action update",
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-powerpoint",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
    };

    public DocumentService(
        IAppDbContext            dbContext,
        ILogger<DocumentService> logger,
        IStrategicScopeService   scopeService,
        IUserRoleService         roles)
    {
        _dbContext    = dbContext;
        _logger       = logger;
        _scopeService = scopeService;
        _roles        = roles;
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public async Task<List<DocumentResponseDto>> GetByEntityAsync(
        string entityType, Guid entityId, DocumentCaller caller, CancellationToken ct)
    {
        var parent = await ResolveVisibleParentAsync(RequireType(entityType), entityId, caller, ct);
        EnsureCanRead(parent, caller);

        var docs = await _dbContext.Documents
            .Where(d => d.RelatedEntityType == parent.Type && d.RelatedEntityId == entityId)
            .Include(d => d.UploadedBy)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);

        var isAdmin = await IsAdminIfNeededAsync(parent, caller, ct);
        return docs.Select(d => ToDto(d, CanDelete(parent, d, caller, isAdmin))).ToList();
    }

    public async Task<DocumentResponseDto> UploadAsync(
        string entityType, Guid entityId, string name,
        IFormFile file, DocumentCaller caller, CancellationToken ct)
    {
        var parent = await ResolveVisibleParentAsync(RequireType(entityType), entityId, caller, ct);
        EnsureCanWrite(parent, caller);
        EnsureNotLocked(parent);
        await EnsureKpiScopedWriteAsync(parent, caller.UserId, ct);

        if (file.Length == 0)
            throw new ArgumentException("File is empty.");

        if (file.Length > MaxFileSize)
            throw new ArgumentException("File size exceeds the 10 MB limit.");

        var ext = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(ext))
            throw new ArgumentException($"File type '{ext}' is not allowed. Allowed: PDF, DOC, DOCX, XLS, XLSX, PPT, PPTX.");

        if (!AllowedContentTypes.Contains(file.ContentType))
            throw new ArgumentException($"Content type '{file.ContentType}' is not allowed.");

        if (MaxDocumentsPerEntity.TryGetValue(parent.Type, out var maxCount))
        {
            var existingCount = await _dbContext.Documents
                .CountAsync(d => d.RelatedEntityType == parent.Type && d.RelatedEntityId == entityId, ct);
            if (existingCount >= maxCount)
                throw new ArgumentException(
                    $"Maximum of {maxCount} files per {(CapLabels.TryGetValue(parent.Type, out var label) ? label : parent.Type)} reached. " +
                    "Delete an existing file before uploading another.");
        }

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);

        var doc = new Document
        {
            Id                = Guid.NewGuid(),
            Name              = name.Trim(),
            FileName          = file.FileName,
            ContentType       = file.ContentType,
            FileSize          = file.Length,
            Content           = ms.ToArray(),
            RelatedEntityType = parent.Type,
            RelatedEntityId   = entityId,
            UploadedByUserId  = caller.UserId,
            CreatedAt         = DateTime.UtcNow,
        };

        _dbContext.Documents.Add(doc);
        await _dbContext.SaveChangesAsync(ct);

        // Fetch with uploader name
        var saved = await _dbContext.Documents
            .Include(d => d.UploadedBy)
            .FirstAsync(d => d.Id == doc.Id, ct);

        _logger.LogInformation(
            "Document {DocId} uploaded for {EntityType} {EntityId}",
            doc.Id, parent.Type, entityId);

        var isAdmin = await IsAdminIfNeededAsync(parent, caller, ct);
        return ToDto(saved, CanDelete(parent, saved, caller, isAdmin));
    }

    public async Task<DocumentDownloadDto> DownloadAsync(Guid documentId, DocumentCaller caller, CancellationToken ct)
    {
        var doc = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId, ct)
            ?? throw new KeyNotFoundException($"Document {documentId} not found.");

        var parent = await ResolveVisibleParentOfDocumentAsync(doc, caller, ct);
        EnsureCanRead(parent, caller);

        return new DocumentDownloadDto
        {
            FileName    = doc.FileName,
            ContentType = doc.ContentType,
            Content     = doc.Content,
        };
    }

    public async Task DeleteAsync(Guid documentId, DocumentCaller caller, CancellationToken ct)
    {
        var doc = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId, ct)
            ?? throw new KeyNotFoundException($"Document {documentId} not found.");

        var parent = await ResolveVisibleParentOfDocumentAsync(doc, caller, ct);
        EnsureCanWrite(parent, caller);
        EnsureNotLocked(parent);
        await EnsureKpiScopedWriteAsync(parent, caller.UserId, ct);

        if (!CanDelete(parent, doc, caller, await IsAdminIfNeededAsync(parent, caller, ct)))
            throw new UnauthorizedAccessException(parent.Type == DocumentEntityTypes.ActionItemUpdate
                ? "Only the author of a draft action update can remove its files."
                : "Only the person who uploaded this file, the action item's creator or assignees"
                  + (parent.Type == DocumentEntityTypes.ActionItemSubItem ? ", the sub-item's assignees" : string.Empty)
                  + " or an Admin can delete it.");

        _dbContext.Documents.Remove(doc);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Document {DocId} deleted by user {UserId}", documentId, caller.UserId);
    }

    // ── Parent resolution ────────────────────────────────────────────────────

    /// <summary>What the rules need to know about a visible owning record.</summary>
    private sealed record Parent(string Type, Guid Id)
    {
        /// <summary>The action item's status (action-item types).</summary>
        public ActionStatus? ItemStatus { get; init; }

        /// <summary>Item creator, item assignees and (sub-item) sub-item assignees — may delete any file.</summary>
        public IReadOnlyCollection<string> DeleteRightsUserIds { get; init; } = Array.Empty<string>();

        public ActionItemUpdateStatus? UpdateStatus { get; init; }
        public string? UpdateAuthorId { get; init; }

        /// <summary>The org unit of a KPI record (scope check on write).</summary>
        public Guid? KpiOrgUnitId { get; init; }
    }

    private static string RequireType(string? entityType) =>
        DocumentEntityTypes.Normalize(entityType)
        ?? throw new ArgumentException(
            $"Documents are not available for entity type '{entityType}'. Supported types: {string.Join(", ", DocumentEntityTypes.All)}.");

    private Task<Parent> ResolveVisibleParentOfDocumentAsync(Document doc, DocumentCaller caller, CancellationToken ct)
    {
        // A stored type outside the list has no parent to check: not found.
        var type = DocumentEntityTypes.Normalize(doc.RelatedEntityType)
                   ?? throw new KeyNotFoundException($"Document {doc.Id} not found.");
        return ResolveVisibleParentAsync(type, doc.RelatedEntityId, caller, ct, notFound: $"Document {doc.Id} not found.");
    }

    /// <summary>
    /// The owning record when the caller can see it — the same rule as that
    /// record's own pages — else <see cref="KeyNotFoundException"/> (404).
    /// </summary>
    private async Task<Parent> ResolveVisibleParentAsync(
        string type, Guid id, DocumentCaller caller, CancellationToken ct, string? notFound = null)
    {
        Parent? parent = type switch
        {
            DocumentEntityTypes.ActionItem        => await LoadActionItemParentAsync(id, ct),
            DocumentEntityTypes.ActionItemSubItem => await LoadSubItemParentAsync(id, ct),
            DocumentEntityTypes.ActionItemUpdate  => await LoadUpdateParentAsync(id, ct),

            DocumentEntityTypes.Project => await _dbContext.Projects.AnyAsync(p => p.Id == id, ct)
                ? new Parent(type, id) : null,
            DocumentEntityTypes.Milestone => await _dbContext.Milestones.AnyAsync(m => m.Id == id, ct)
                ? new Parent(type, id) : null,
            DocumentEntityTypes.ProjectRisk => await _dbContext.ProjectRisks.AnyAsync(r => r.Id == id, ct)
                ? new Parent(type, id) : null,

            DocumentEntityTypes.KpiTarget or DocumentEntityTypes.KpiActual => await LoadKpiParentAsync(type, id, ct),

            _ => null,
        };

        if (parent is not null && DocumentEntityTypes.IsKpiType(type) && !await InKpiReadScopeAsync(parent, caller.UserId, ct))
            parent = null;

        return parent ?? throw new KeyNotFoundException(notFound ?? $"{type} {id} not found.");
    }

    /// <summary>Through the action-item query filter (deleted, private workspace).</summary>
    private async Task<Parent?> LoadActionItemParentAsync(Guid id, CancellationToken ct)
    {
        var row = await _dbContext.ActionItems
            .Where(a => a.Id == id)
            .Select(a => new { a.Status, a.CreatedByUserId, Assignees = a.Assignees.Select(x => x.UserId).ToList() })
            .FirstOrDefaultAsync(ct);
        if (row is null) return null;

        var rights = row.Assignees;
        if (row.CreatedByUserId is not null) rights.Add(row.CreatedByUserId);
        return new Parent(DocumentEntityTypes.ActionItem, id) { ItemStatus = row.Status, DeleteRightsUserIds = rights };
    }

    /// <summary>Through the sub-item query filter (sub-item or parent deleted, private workspace).</summary>
    private async Task<Parent?> LoadSubItemParentAsync(Guid id, CancellationToken ct)
    {
        var row = await _dbContext.ActionItemSubItems
            .Where(s => s.Id == id)
            .Select(s => new
            {
                s.ActionItem.Status,
                s.ActionItem.CreatedByUserId,
                ParentAssignees = s.ActionItem.Assignees.Select(a => a.UserId).ToList(),
                SubAssignees    = s.Assignees.Select(a => a.UserId).ToList(),
            })
            .FirstOrDefaultAsync(ct);
        if (row is null) return null;

        var rights = row.ParentAssignees.Concat(row.SubAssignees).ToList();
        if (row.CreatedByUserId is not null) rights.Add(row.CreatedByUserId);
        return new Parent(DocumentEntityTypes.ActionItemSubItem, id) { ItemStatus = row.Status, DeleteRightsUserIds = rights };
    }

    /// <summary>Through the update query filter: someone else's draft is not found, Admin included.</summary>
    private async Task<Parent?> LoadUpdateParentAsync(Guid id, CancellationToken ct)
    {
        var row = await _dbContext.ActionItemUpdates
            .Where(u => u.Id == id)
            .Select(u => new { u.Status, u.AuthorUserId })
            .FirstOrDefaultAsync(ct);
        return row is null ? null
            : new Parent(DocumentEntityTypes.ActionItemUpdate, id) { UpdateStatus = row.Status, UpdateAuthorId = row.AuthorUserId };
    }

    /// <summary>
    /// KPI records: drafts (CR-001-G) count as existing, so the query filters
    /// are bypassed and the deletion flags checked explicitly.
    /// </summary>
    private async Task<Parent?> LoadKpiParentAsync(string type, Guid id, CancellationToken ct)
    {
        Guid? orgUnitId = type == DocumentEntityTypes.KpiTarget
            ? await _dbContext.KpiTargets.IgnoreQueryFilters()
                .Where(t => t.Id == id && !t.Kpi!.IsDeleted && !t.Kpi.StrategicObjective!.IsDeleted)
                .Select(t => (Guid?)t.Kpi!.StrategicObjective!.OrgUnitId)
                .FirstOrDefaultAsync(ct)
            : await _dbContext.KpiActuals.IgnoreQueryFilters()
                .Where(a => a.Id == id && !a.IsDeleted && !a.KpiTarget!.Kpi!.IsDeleted
                            && !a.KpiTarget.Kpi.StrategicObjective!.IsDeleted)
                .Select(a => (Guid?)a.KpiTarget!.Kpi!.StrategicObjective!.OrgUnitId)
                .FirstOrDefaultAsync(ct);
        return orgUnitId is null ? null : new Parent(type, id) { KpiOrgUnitId = orgUnitId };
    }

    /// <summary>The KPI pages' own read rule: only a Strategy Editor is limited (to an org-unit subtree).</summary>
    private async Task<bool> InKpiReadScopeAsync(Parent parent, string userId, CancellationToken ct)
    {
        if (parent.KpiOrgUnitId is null) return true;
        var visible = await _scopeService.GetVisibleOrgUnitIdsAsync(userId, ct);
        return visible is null || visible.Contains(parent.KpiOrgUnitId.Value);
    }

    // ── Rules ────────────────────────────────────────────────────────────────

    private static void EnsureCanRead(Parent parent, DocumentCaller caller)
    {
        if (DocumentEntityTypes.IsActionItemType(parent.Type) && !caller.CanViewActionItems)
            throw new UnauthorizedAccessException("You need the ActionItems.View permission to see these files.");
    }

    private static void EnsureCanWrite(Parent parent, DocumentCaller caller)
    {
        if (DocumentEntityTypes.IsActionItemType(parent.Type) && !caller.CanEditActionItems)
            throw new UnauthorizedAccessException("You need the ActionItems.Edit permission to add or remove these files.");
    }

    /// <summary>The 409 rules (WO-2026-11 A-4, D1 and Q11).</summary>
    private static void EnsureNotLocked(Parent parent)
    {
        var lockReason = LockReason(parent);
        if (lockReason is not null) throw new DocumentLockedException(lockReason);
    }

    public static bool ItemStatusLocksFiles(ActionStatus status) =>
        status is ActionStatus.Done or ActionStatus.Cancelled;

    private static string? LockReason(Parent parent) => parent.Type switch
    {
        DocumentEntityTypes.ActionItem when parent.ItemStatus is { } s && ItemStatusLocksFiles(s) =>
            $"Files cannot be added to or removed from an action item that is {Label(s)}. Add them to an action update instead.",
        DocumentEntityTypes.ActionItemSubItem when parent.ItemStatus is { } s && ItemStatusLocksFiles(s) =>
            $"Files on sub-items cannot be added or removed while the action item is {Label(s)}.",
        DocumentEntityTypes.ActionItemUpdate when parent.UpdateStatus == ActionItemUpdateStatus.Submitted =>
            "This action update has been submitted. Its files cannot be changed.",
        _ => null,
    };

    private static string Label(ActionStatus status) => status == ActionStatus.Done ? "Done" : "Cancelled";

    private async Task EnsureKpiScopedWriteAsync(Parent parent, string userId, CancellationToken ct)
    {
        // Unchanged KPI evidence rule: a Strategy Editor writes only inside their org units (403).
        if (DocumentEntityTypes.IsKpiType(parent.Type) && parent.KpiOrgUnitId is { } orgUnitId)
            await _scopeService.EnsureCanWriteAsync(userId, orgUnitId, ct);
    }

    /// <summary>Admin is looked up only where it matters (item and sub-item files).</summary>
    private async Task<bool> IsAdminIfNeededAsync(Parent parent, DocumentCaller caller, CancellationToken ct) =>
        parent.Type is DocumentEntityTypes.ActionItem or DocumentEntityTypes.ActionItemSubItem
        && caller.CanEditActionItems
        && await _roles.IsInRoleAsync(caller.UserId, AppRoles.Admin, ct);

    /// <summary>
    /// Whether the caller may delete <paramref name="doc"/> (D4). Locks and the
    /// Edit permission are part of it, so the UI can show the button exactly when
    /// the delete would succeed. KPI types: the org-unit write scope is not
    /// evaluated here (the delete still enforces it with 403, as before).
    /// </summary>
    private static bool CanDelete(Parent parent, Document doc, DocumentCaller caller, bool isAdmin)
    {
        if (DocumentEntityTypes.IsActionItemType(parent.Type) && !caller.CanEditActionItems) return false;
        if (LockReason(parent) is not null) return false;

        return parent.Type switch
        {
            DocumentEntityTypes.ActionItem or DocumentEntityTypes.ActionItemSubItem =>
                isAdmin
                || string.Equals(doc.UploadedByUserId, caller.UserId, StringComparison.Ordinal)
                || parent.DeleteRightsUserIds.Contains(caller.UserId, StringComparer.Ordinal),
            DocumentEntityTypes.ActionItemUpdate =>
                string.Equals(parent.UpdateAuthorId, caller.UserId, StringComparison.Ordinal),
            _ => true,
        };
    }

    private static DocumentResponseDto ToDto(Document d, bool canDelete) => new()
    {
        Id                = d.Id,
        Name              = d.Name,
        FileName          = d.FileName,
        ContentType       = d.ContentType,
        FileSize          = d.FileSize,
        RelatedEntityType = d.RelatedEntityType,
        RelatedEntityId   = d.RelatedEntityId,
        UploadedByUserId  = d.UploadedByUserId,
        UploadedByName    = d.UploadedBy is null ? string.Empty : (d.UploadedBy.FirstName + " " + d.UploadedBy.LastName).Trim(),
        CreatedAt         = d.CreatedAt,
        CanDelete         = canDelete,
    };
}
