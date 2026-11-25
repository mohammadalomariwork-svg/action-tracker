using ActionTracker.Application.Features.Documents.DTOs;
using Microsoft.AspNetCore.Http;

namespace ActionTracker.Application.Features.Documents.Interfaces;

/// <summary>
/// Polymorphic documents. Every call checks that <see cref="DocumentCaller"/>
/// can see the owning record (WO-2026-11 A-4): <see cref="KeyNotFoundException"/>
/// (404) when hidden, unknown or missing; <see cref="UnauthorizedAccessException"/>
/// (403) for a missing permission, delete right or KPI org-unit scope;
/// <see cref="DocumentLockedException"/> (409) on a locked record;
/// <see cref="ArgumentException"/> (400) for an unsupported type, a file rule or the cap.
/// </summary>
public interface IDocumentService
{
    Task<List<DocumentResponseDto>> GetByEntityAsync(string entityType, Guid entityId, DocumentCaller caller, CancellationToken ct);
    Task<DocumentResponseDto> UploadAsync(string entityType, Guid entityId, string name, IFormFile file, DocumentCaller caller, CancellationToken ct);
    Task<DocumentDownloadDto> DownloadAsync(Guid documentId, DocumentCaller caller, CancellationToken ct);
    Task DeleteAsync(Guid documentId, DocumentCaller caller, CancellationToken ct);
}
