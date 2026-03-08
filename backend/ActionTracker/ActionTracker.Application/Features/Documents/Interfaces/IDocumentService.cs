using ActionTracker.Application.Features.Documents.DTOs;
using Microsoft.AspNetCore.Http;

namespace ActionTracker.Application.Features.Documents.Interfaces;

public interface IDocumentService
{
    Task<List<DocumentResponseDto>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken ct);
    Task<DocumentResponseDto> UploadAsync(string entityType, Guid entityId, string name, IFormFile file, string userId, CancellationToken ct);
    Task<DocumentDownloadDto> DownloadAsync(Guid documentId, CancellationToken ct);
    Task DeleteAsync(Guid documentId, string userId, CancellationToken ct);
}
