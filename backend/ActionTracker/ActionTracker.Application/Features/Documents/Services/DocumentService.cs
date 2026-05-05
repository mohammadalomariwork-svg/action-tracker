using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.Documents.DTOs;
using ActionTracker.Application.Features.Documents.Interfaces;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Application.Features.Documents.Services;

public class DocumentService : IDocumentService
{
    private readonly IAppDbContext _dbContext;
    private readonly ILogger<DocumentService> _logger;
    private readonly IStrategicScopeService _scopeService;

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
        IStrategicScopeService   scopeService)
    {
        _dbContext    = dbContext;
        _logger       = logger;
        _scopeService = scopeService;
    }

    /// <summary>
    /// For document operations that target an entity guarded by the strategic
    /// scope (currently <c>KpiTarget</c>), throws <see cref="UnauthorizedAccessException"/>
    /// when the caller is out of scope. Returns silently for any other entity type.
    /// </summary>
    private async Task EnsureScopedWriteAsync(
        string entityType, Guid entityId, string userId, CancellationToken ct)
    {
        if (!string.Equals(entityType, "KpiTarget", StringComparison.OrdinalIgnoreCase))
            return;

        var orgUnitId = await _dbContext.KpiTargets
            .Where(t => t.Id == entityId)
            .Select(t => (Guid?)t.Kpi!.StrategicObjective!.OrgUnitId)
            .FirstOrDefaultAsync(ct);

        if (orgUnitId is null) return; // KpiTarget missing — let downstream handle.

        await _scopeService.EnsureCanWriteAsync(userId, orgUnitId.Value, ct);
    }

    public async Task<List<DocumentResponseDto>> GetByEntityAsync(
        string entityType, Guid entityId, CancellationToken ct)
    {
        return await _dbContext.Documents
            .Where(d => d.RelatedEntityType == entityType && d.RelatedEntityId == entityId)
            .Include(d => d.UploadedBy)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new DocumentResponseDto
            {
                Id                = d.Id,
                Name              = d.Name,
                FileName          = d.FileName,
                ContentType       = d.ContentType,
                FileSize          = d.FileSize,
                RelatedEntityType = d.RelatedEntityType,
                RelatedEntityId   = d.RelatedEntityId,
                UploadedByUserId  = d.UploadedByUserId,
                UploadedByName    = d.UploadedBy.FirstName + " " + d.UploadedBy.LastName,
                CreatedAt         = d.CreatedAt,
            })
            .ToListAsync(ct);
    }

    public async Task<DocumentResponseDto> UploadAsync(
        string entityType, Guid entityId, string name,
        IFormFile file, string userId, CancellationToken ct)
    {
        await EnsureScopedWriteAsync(entityType, entityId, userId, ct);

        if (file.Length == 0)
            throw new ArgumentException("File is empty.");

        if (file.Length > MaxFileSize)
            throw new ArgumentException("File size exceeds the 10 MB limit.");

        var ext = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(ext))
            throw new ArgumentException($"File type '{ext}' is not allowed. Allowed: PDF, DOC, DOCX, XLS, XLSX, PPT, PPTX.");

        if (!AllowedContentTypes.Contains(file.ContentType))
            throw new ArgumentException($"Content type '{file.ContentType}' is not allowed.");

        if (MaxDocumentsPerEntity.TryGetValue(entityType, out var maxCount))
        {
            var existingCount = await _dbContext.Documents
                .CountAsync(d => d.RelatedEntityType == entityType && d.RelatedEntityId == entityId, ct);
            if (existingCount >= maxCount)
                throw new ArgumentException(
                    $"Maximum of {maxCount} files per {entityType} reached. Delete an existing file before uploading another.");
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
            RelatedEntityType = entityType,
            RelatedEntityId   = entityId,
            UploadedByUserId  = userId,
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
            doc.Id, entityType, entityId);

        return new DocumentResponseDto
        {
            Id                = saved.Id,
            Name              = saved.Name,
            FileName          = saved.FileName,
            ContentType       = saved.ContentType,
            FileSize          = saved.FileSize,
            RelatedEntityType = saved.RelatedEntityType,
            RelatedEntityId   = saved.RelatedEntityId,
            UploadedByUserId  = saved.UploadedByUserId,
            UploadedByName    = saved.UploadedBy?.FullName ?? string.Empty,
            CreatedAt         = saved.CreatedAt,
        };
    }

    public async Task<DocumentDownloadDto> DownloadAsync(Guid documentId, CancellationToken ct)
    {
        var doc = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId, ct)
            ?? throw new KeyNotFoundException($"Document {documentId} not found.");

        return new DocumentDownloadDto
        {
            FileName    = doc.FileName,
            ContentType = doc.ContentType,
            Content     = doc.Content,
        };
    }

    public async Task DeleteAsync(Guid documentId, string userId, CancellationToken ct)
    {
        var doc = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId, ct)
            ?? throw new KeyNotFoundException($"Document {documentId} not found.");

        await EnsureScopedWriteAsync(doc.RelatedEntityType, doc.RelatedEntityId, userId, ct);

        _dbContext.Documents.Remove(doc);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Document {DocId} deleted by user {UserId}", documentId, userId);
    }
}
