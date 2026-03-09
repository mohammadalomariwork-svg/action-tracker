using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.Comments.DTOs;
using ActionTracker.Application.Features.Comments.Interfaces;
using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Application.Features.Comments.Services;

public class CommentService : ICommentService
{
    private readonly IAppDbContext _dbContext;
    private readonly ILogger<CommentService> _logger;

    public CommentService(IAppDbContext dbContext, ILogger<CommentService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<List<CommentResponseDto>> GetByEntityAsync(
        string entityType, Guid entityId, CancellationToken ct)
    {
        return await _dbContext.Comments
            .Where(c => c.RelatedEntityType == entityType && c.RelatedEntityId == entityId)
            .Include(c => c.Author)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CommentResponseDto
            {
                Id = c.Id,
                RelatedEntityType = c.RelatedEntityType,
                RelatedEntityId = c.RelatedEntityId,
                Content = c.Content,
                AuthorUserId = c.AuthorUserId,
                AuthorName = c.Author.FirstName + " " + c.Author.LastName,
                IsHighImportance = c.IsHighImportance,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
            })
            .ToListAsync(ct);
    }

    public async Task<CommentResponseDto> AddAsync(
        string entityType, Guid entityId, CreateCommentDto dto, string userId, CancellationToken ct)
    {
        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            RelatedEntityType = entityType,
            RelatedEntityId = entityId,
            Content = dto.Content.Trim(),
            AuthorUserId = userId,
            IsHighImportance = dto.IsHighImportance,
            CreatedAt = DateTime.UtcNow,
        };

        _dbContext.Comments.Add(comment);
        await _dbContext.SaveChangesAsync(ct);

        var saved = await _dbContext.Comments
            .Include(c => c.Author)
            .FirstAsync(c => c.Id == comment.Id, ct);

        _logger.LogInformation(
            "Comment {CommentId} added for {EntityType} {EntityId}",
            comment.Id, entityType, entityId);

        return new CommentResponseDto
        {
            Id = saved.Id,
            RelatedEntityType = saved.RelatedEntityType,
            RelatedEntityId = saved.RelatedEntityId,
            Content = saved.Content,
            AuthorUserId = saved.AuthorUserId,
            AuthorName = saved.Author?.FullName ?? string.Empty,
            IsHighImportance = saved.IsHighImportance,
            CreatedAt = saved.CreatedAt,
            UpdatedAt = saved.UpdatedAt,
        };
    }

    public async Task<CommentResponseDto> UpdateAsync(
        Guid commentId, UpdateCommentDto dto, string userId, CancellationToken ct)
    {
        var comment = await _dbContext.Comments
            .FirstOrDefaultAsync(c => c.Id == commentId, ct)
            ?? throw new KeyNotFoundException($"Comment {commentId} not found.");

        if (comment.AuthorUserId != userId)
            throw new UnauthorizedAccessException("You can only edit your own comments.");

        comment.Content = dto.Content.Trim();
        comment.IsHighImportance = dto.IsHighImportance;
        comment.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        var saved = await _dbContext.Comments
            .Include(c => c.Author)
            .FirstAsync(c => c.Id == commentId, ct);

        return new CommentResponseDto
        {
            Id = saved.Id,
            RelatedEntityType = saved.RelatedEntityType,
            RelatedEntityId = saved.RelatedEntityId,
            Content = saved.Content,
            AuthorUserId = saved.AuthorUserId,
            AuthorName = saved.Author?.FullName ?? string.Empty,
            IsHighImportance = saved.IsHighImportance,
            CreatedAt = saved.CreatedAt,
            UpdatedAt = saved.UpdatedAt,
        };
    }

    public async Task DeleteAsync(Guid commentId, string userId, CancellationToken ct)
    {
        var comment = await _dbContext.Comments
            .FirstOrDefaultAsync(c => c.Id == commentId, ct)
            ?? throw new KeyNotFoundException($"Comment {commentId} not found.");

        if (comment.AuthorUserId != userId)
            throw new UnauthorizedAccessException("You can only delete your own comments.");

        _dbContext.Comments.Remove(comment);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Comment {CommentId} deleted by user {UserId}", commentId, userId);
    }
}
