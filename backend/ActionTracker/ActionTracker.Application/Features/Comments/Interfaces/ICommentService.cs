using ActionTracker.Application.Features.Comments.DTOs;

namespace ActionTracker.Application.Features.Comments.Interfaces;

public interface ICommentService
{
    Task<List<CommentResponseDto>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken ct);
    Task<CommentResponseDto> AddAsync(string entityType, Guid entityId, CreateCommentDto dto, string userId, CancellationToken ct);
    Task<CommentResponseDto> UpdateAsync(Guid commentId, UpdateCommentDto dto, string userId, CancellationToken ct);
    Task DeleteAsync(Guid commentId, string userId, CancellationToken ct);
}
