using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ActionTracker.Application.Features.Projects.DTOs;

namespace ActionTracker.Application.Features.Projects.Interfaces;

/// <summary>
/// Defines the application-level operations for managing threaded comments on
/// action items, milestones, and projects.
/// </summary>
public interface ICommentService
{
    /// <summary>
    /// Returns all comments posted on the specified action item, ordered by
    /// creation time ascending.
    /// </summary>
    /// <param name="actionItemId">Primary key of the action item.</param>
    Task<IEnumerable<CommentDto>> GetByActionItemAsync(Guid actionItemId);

    /// <summary>
    /// Returns all comments posted on the specified milestone, ordered by
    /// creation time ascending.
    /// </summary>
    /// <param name="milestoneId">Primary key of the milestone.</param>
    Task<IEnumerable<CommentDto>> GetByMilestoneAsync(Guid milestoneId);

    /// <summary>
    /// Returns all comments posted directly on the specified project (not on
    /// nested milestones or action items), ordered by creation time ascending.
    /// </summary>
    /// <param name="projectId">Primary key of the project.</param>
    Task<IEnumerable<CommentDto>> GetByProjectAsync(Guid projectId);

    /// <summary>
    /// Posts a new comment.  Exactly one of the target ID fields
    /// (<c>ActionItemId</c>, <c>MilestoneId</c>, <c>ProjectId</c>) in
    /// <paramref name="dto"/> must be non-null — enforced at this layer.
    /// </summary>
    /// <param name="dto">Comment content and target-entity reference.</param>
    /// <returns>The newly created comment.</returns>
    Task<CommentDto> CreateAsync(CreateCommentDto dto);

    /// <summary>
    /// Edits the text body of an existing comment.
    /// Only the comment's original author may perform this operation.
    /// </summary>
    /// <param name="id">Primary key of the comment to edit.</param>
    /// <param name="dto">New content for the comment.</param>
    /// <param name="requestingUserId">
    /// AspNetUsers.Id of the user making the request; must match the comment's
    /// <c>AuthorUserId</c>.
    /// </param>
    /// <returns>
    /// The updated comment, or <c>null</c> if not found or not authorised.
    /// </returns>
    Task<CommentDto?> UpdateAsync(Guid id, UpdateCommentDto dto, string requestingUserId);

    /// <summary>
    /// Deletes a comment.  Only the original author or a workspace admin may
    /// perform this operation.
    /// </summary>
    /// <param name="id">Primary key of the comment to delete.</param>
    /// <param name="requestingUserId">
    /// AspNetUsers.Id of the user making the request.
    /// </param>
    /// <returns>
    /// <c>true</c> if the comment was found and deleted; <c>false</c> if not
    /// found or not authorised.
    /// </returns>
    Task<bool> DeleteAsync(Guid id, string requestingUserId);
}
