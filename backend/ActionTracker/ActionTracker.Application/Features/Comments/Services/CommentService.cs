using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.Comments.DTOs;
using ActionTracker.Application.Features.Comments.Interfaces;
using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Application.Features.Comments.Services;

/// <summary>
/// The shared, polymorphic comments (<c>/api/comments</c>) on projects,
/// milestones and risks.
/// <para>
/// Parent visibility (WO-2026-11 A-3, fix 3): every read and write first loads
/// the owning record through the EF query filters — the same rule as that
/// record's own GET by id (soft delete + private workspace). A parent the
/// caller cannot see is "not found" (404), so private-workspace comments can
/// neither be read nor written by non-members. Only <see cref="SupportedEntityTypes"/>
/// are served; any other type is refused (400).
/// </para>
/// <para>
/// Action items moved to Action Updates (A-3): their old comments here stay
/// readable, but new comments, edits and deletes for <c>ActionItem</c> are
/// refused (400) — use <c>/api/action-items/{id}/updates</c>.
/// </para>
/// </summary>
public class CommentService : ICommentService
{
    public const string ActionItemType  = "ActionItem";
    public const string ProjectType     = "Project";
    public const string MilestoneType   = "Milestone";
    public const string ProjectRiskType = "ProjectRisk";

    /// <summary>The entity types <c>/api/comments</c> serves.</summary>
    public static readonly IReadOnlyList<string> SupportedEntityTypes =
        new[] { ActionItemType, ProjectType, MilestoneType, ProjectRiskType };

    public const string ActionItemWritesRefusedMessage =
        "Comments on action items have been replaced by Action Updates. " +
        "Use /api/action-items/{id}/updates to add an update.";

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
        var type = RequireSupportedType(entityType);
        await EnsureParentVisibleAsync(type, entityId, ct);

        return await _dbContext.Comments
            .Where(c => c.RelatedEntityType == type && c.RelatedEntityId == entityId)
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
        var type = RequireSupportedType(entityType);
        await EnsureParentVisibleAsync(type, entityId, ct);
        RefuseActionItemWrite(type);

        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            RelatedEntityType = type,
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
            comment.Id, type, entityId);

        return ToDto(saved);
    }

    public async Task<CommentResponseDto> UpdateAsync(
        Guid commentId, UpdateCommentDto dto, string userId, CancellationToken ct)
    {
        var comment = await LoadVisibleCommentAsync(commentId, ct);
        RefuseActionItemWrite(comment.RelatedEntityType);

        if (comment.AuthorUserId != userId)
            throw new UnauthorizedAccessException("You can only edit your own comments.");

        comment.Content = dto.Content.Trim();
        comment.IsHighImportance = dto.IsHighImportance;
        comment.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        var saved = await _dbContext.Comments
            .Include(c => c.Author)
            .FirstAsync(c => c.Id == commentId, ct);

        return ToDto(saved);
    }

    public async Task DeleteAsync(Guid commentId, string userId, CancellationToken ct)
    {
        var comment = await LoadVisibleCommentAsync(commentId, ct);
        RefuseActionItemWrite(comment.RelatedEntityType);

        if (comment.AuthorUserId != userId)
            throw new UnauthorizedAccessException("You can only delete your own comments.");

        _dbContext.Comments.Remove(comment);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Comment {CommentId} deleted by user {UserId}", commentId, userId);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>The canonical spelling of a supported type (case-insensitive), or null.</summary>
    public static string? NormalizeEntityType(string? entityType) =>
        SupportedEntityTypes.FirstOrDefault(t => string.Equals(t, entityType?.Trim(), StringComparison.OrdinalIgnoreCase));

    private static string RequireSupportedType(string? entityType) =>
        NormalizeEntityType(entityType)
        ?? throw new ArgumentException(
            $"Comments are not available for entity type '{entityType}'. Supported types: {string.Join(", ", SupportedEntityTypes)}.");

    private static void RefuseActionItemWrite(string entityType)
    {
        if (string.Equals(entityType, ActionItemType, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(ActionItemWritesRefusedMessage);
    }

    /// <summary>
    /// The comment, when its parent is visible to the caller. A comment whose
    /// parent is hidden, deleted or of an unsupported type is "not found", so
    /// its existence is not revealed.
    /// </summary>
    private async Task<Comment> LoadVisibleCommentAsync(Guid commentId, CancellationToken ct)
    {
        var comment = await _dbContext.Comments.FirstOrDefaultAsync(c => c.Id == commentId, ct);
        var type = NormalizeEntityType(comment?.RelatedEntityType);
        if (comment is null || type is null || !await IsParentVisibleAsync(type, comment.RelatedEntityId, ct))
            throw new KeyNotFoundException($"Comment {commentId} not found.");
        return comment;
    }

    private async Task EnsureParentVisibleAsync(string type, Guid entityId, CancellationToken ct)
    {
        if (!await IsParentVisibleAsync(type, entityId, ct))
            throw new KeyNotFoundException($"{type} {entityId} not found.");
    }

    /// <summary>Through the query filters: deleted, or private to a workspace the caller is not part of → false.</summary>
    private Task<bool> IsParentVisibleAsync(string type, Guid entityId, CancellationToken ct) => type switch
    {
        ActionItemType  => _dbContext.ActionItems.AnyAsync(a => a.Id == entityId, ct),
        ProjectType     => _dbContext.Projects.AnyAsync(p => p.Id == entityId, ct),
        MilestoneType   => _dbContext.Milestones.AnyAsync(m => m.Id == entityId, ct),
        ProjectRiskType => _dbContext.ProjectRisks.AnyAsync(r => r.Id == entityId, ct),
        _               => Task.FromResult(false),
    };

    private static CommentResponseDto ToDto(Comment saved) => new()
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
