using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.Projects.DTOs;
using ActionTracker.Application.Features.Projects.Interfaces;
using ActionTracker.Application.Features.Projects.Models;
using ActionTracker.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Application.Features.Projects.Services;

/// <summary>
/// Application service for managing threaded comments on projects, milestones,
/// and action items.
/// Enforces authorship rules on edits and deletes; workspace admins may delete
/// any comment via <see cref="UserManager{TUser}"/> role check.
/// </summary>
public class CommentService : ICommentService
{
    private readonly IAppDbContext _db;
    private readonly ILogger<CommentService> _logger;
    private readonly UserManager<ApplicationUser> _userManager;

    /// <summary>Initialises the service with its required dependencies.</summary>
    public CommentService(
        IAppDbContext db,
        ILogger<CommentService> logger,
        UserManager<ApplicationUser> userManager)
    {
        _db          = db;
        _logger      = logger;
        _userManager = userManager;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IEnumerable<CommentDto>> GetByActionItemAsync(Guid actionItemId)
    {
        try
        {
            var comments = await _db.Comments
                .Where(c => c.ActionItemId == actionItemId && c.IsActive)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();

            return comments.Select(MapToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving comments for action item {ActionItemId}.", actionItemId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<CommentDto>> GetByMilestoneAsync(Guid milestoneId)
    {
        try
        {
            var comments = await _db.Comments
                .Where(c => c.MilestoneId == milestoneId && c.IsActive)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();

            return comments.Select(MapToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving comments for milestone {MilestoneId}.", milestoneId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<CommentDto>> GetByProjectAsync(Guid projectId)
    {
        try
        {
            var comments = await _db.Comments
                .Where(c => c.ProjectId == projectId && c.IsActive)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();

            return comments.Select(MapToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving comments for project {ProjectId}.", projectId);
            throw;
        }
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// Thrown when the number of non-null target IDs
    /// (<c>ActionItemId</c>, <c>MilestoneId</c>, <c>ProjectId</c>) is not
    /// exactly one.
    /// </exception>
    public async Task<CommentDto> CreateAsync(CreateCommentDto dto)
    {
        // Enforce exactly-one-target rule.
        int targetCount = (dto.ActionItemId.HasValue ? 1 : 0)
                        + (dto.MilestoneId.HasValue  ? 1 : 0)
                        + (dto.ProjectId.HasValue    ? 1 : 0);

        if (targetCount != 1)
            throw new ArgumentException(
                "Exactly one of ActionItemId, MilestoneId, or ProjectId must be provided.",
                nameof(dto));

        try
        {
            var comment = new Comment
            {
                Content        = dto.Content,
                AuthorUserId   = dto.AuthorUserId,
                AuthorUserName = dto.AuthorUserName,
                ActionItemId   = dto.ActionItemId,
                MilestoneId    = dto.MilestoneId,
                ProjectId      = dto.ProjectId,
                IsEdited       = false,
                IsActive       = true,
                CreatedAt      = DateTime.UtcNow
            };

            _db.Comments.Add(comment);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Created comment {Id} by user {UserId}.",
                comment.Id, dto.AuthorUserId);

            return MapToDto(comment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating comment by user {UserId}.", dto.AuthorUserId);
            throw;
        }
    }

    /// <inheritdoc/>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown when <paramref name="requestingUserId"/> does not match the
    /// comment's <c>AuthorUserId</c>.
    /// </exception>
    public async Task<CommentDto?> UpdateAsync(Guid id, UpdateCommentDto dto, string requestingUserId)
    {
        try
        {
            var comment = await _db.Comments
                .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

            if (comment is null) return null;

            if (comment.AuthorUserId != requestingUserId)
                throw new UnauthorizedAccessException(
                    $"User '{requestingUserId}' is not the author of comment {id} and cannot edit it.");

            comment.Content   = dto.Content;
            comment.IsEdited  = true;
            comment.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            _logger.LogInformation("User {UserId} edited comment {Id}.", requestingUserId, id);
            return MapToDto(comment);
        }
        catch (UnauthorizedAccessException)
        {
            throw; // Re-throw business-rule exceptions without wrapping.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating comment {Id}.", id);
            throw;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Deletion is permitted when the requesting user is the comment's original
    /// author OR when the user holds the <c>Admin</c> role (checked via
    /// <see cref="UserManager{TUser}.IsInRoleAsync"/>).
    /// </remarks>
    public async Task<bool> DeleteAsync(Guid id, string requestingUserId)
    {
        try
        {
            var comment = await _db.Comments
                .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

            if (comment is null) return false;

            // Allow if the user is the author.
            bool isAuthor = comment.AuthorUserId == requestingUserId;

            // Allow if the user is an admin.
            bool isAdmin = false;
            if (!isAuthor)
            {
                var user = await _userManager.FindByIdAsync(requestingUserId);
                if (user is not null)
                    isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            }

            if (!isAuthor && !isAdmin)
            {
                _logger.LogWarning(
                    "User {UserId} attempted to delete comment {Id} without permission.",
                    requestingUserId, id);
                return false;
            }

            comment.IsActive  = false;
            comment.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _logger.LogInformation("User {UserId} deleted comment {Id}.", requestingUserId, id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting comment {Id}.", id);
            throw;
        }
    }

    // ── Private mapping ───────────────────────────────────────────────────────

    /// <summary>Maps a <see cref="Comment"/> entity to its DTO.</summary>
    private static CommentDto MapToDto(Comment c) => new()
    {
        Id             = c.Id,
        Content        = c.Content,
        AuthorUserId   = c.AuthorUserId,
        AuthorUserName = c.AuthorUserName,
        ActionItemId   = c.ActionItemId,
        MilestoneId    = c.MilestoneId,
        ProjectId      = c.ProjectId,
        CreatedAt      = c.CreatedAt,
        UpdatedAt      = c.UpdatedAt,
        IsEdited       = c.IsEdited
    };
}
