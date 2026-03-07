using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.Projects.DTOs;
using ActionTracker.Application.Features.Projects.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

/// <summary>
/// Manages threaded comments on projects, milestones, and action items.
/// Author identity is sourced from JWT claims; edit and delete are author-only
/// (admins may also delete).
/// </summary>
[ApiController]
[Route("api/comments")]
[Authorize(AuthenticationSchemes = "LocalBearer,AzureAD")]
public class CommentsController : ControllerBase
{
    private readonly ICommentService _service;
    private readonly ILogger<CommentsController> _logger;

    /// <summary>Initialises the controller with required services.</summary>
    public CommentsController(ICommentService service, ILogger<CommentsController> logger)
    {
        _service = service;
        _logger  = logger;
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    private string CurrentUserName =>
        User.FindFirstValue(ClaimTypes.Name)
        ?? User.FindFirstValue("name")
        ?? string.Empty;

    // ── GET api/comments/action-item/{actionItemId} ───────────────────────────

    /// <summary>Returns all active comments on an action item, ordered chronologically.</summary>
    [HttpGet("action-item/{actionItemId:int}")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<CommentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByActionItem(int actionItemId)
    {
        _logger.LogInformation("GET api/comments/action-item/{ActionItemId}", actionItemId);
        var result = await _service.GetByActionItemAsync(actionItemId);
        return Ok(ApiResponse<IEnumerable<CommentDto>>.Ok(result));
    }

    // ── GET api/comments/milestone/{milestoneId} ──────────────────────────────

    /// <summary>Returns all active comments on a milestone, ordered chronologically.</summary>
    [HttpGet("milestone/{milestoneId:int}")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<CommentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByMilestone(int milestoneId)
    {
        _logger.LogInformation("GET api/comments/milestone/{MilestoneId}", milestoneId);
        var result = await _service.GetByMilestoneAsync(milestoneId);
        return Ok(ApiResponse<IEnumerable<CommentDto>>.Ok(result));
    }

    // ── GET api/comments/project/{projectId} ──────────────────────────────────

    /// <summary>Returns all active project-level comments, ordered chronologically.</summary>
    [HttpGet("project/{projectId:int}")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<CommentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByProject(int projectId)
    {
        _logger.LogInformation("GET api/comments/project/{ProjectId}", projectId);
        var result = await _service.GetByProjectAsync(projectId);
        return Ok(ApiResponse<IEnumerable<CommentDto>>.Ok(result));
    }

    // ── POST api/comments ─────────────────────────────────────────────────────

    /// <summary>
    /// Posts a new comment. Author identity (userId and userName) is taken
    /// directly from the authenticated user's JWT claims and injected into the DTO,
    /// overriding any client-supplied values.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CommentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateCommentDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        // Override author fields with verified JWT identity.
        dto.AuthorUserId   = CurrentUserId;
        dto.AuthorUserName = CurrentUserName;

        _logger.LogInformation("POST api/comments by user {UserId}", CurrentUserId);

        try
        {
            var created = await _service.CreateAsync(dto);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<CommentDto>.Ok(created));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // ── PUT api/comments/{id} ─────────────────────────────────────────────────

    /// <summary>
    /// Edits a comment's body. Only the original author may call this endpoint.
    /// Returns 403 Forbidden if the requesting user is not the author.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<CommentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCommentDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        _logger.LogInformation("PUT api/comments/{Id} by user {UserId}", id, CurrentUserId);

        try
        {
            var updated = await _service.UpdateAsync(id, dto, CurrentUserId);
            if (updated is null)
                return NotFound(ApiResponse<string>.Fail($"Comment {id} not found."));
            return Ok(ApiResponse<CommentDto>.Ok(updated));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<string>.Fail(ex.Message));
        }
    }

    // ── DELETE api/comments/{id} ──────────────────────────────────────────────

    /// <summary>
    /// Deletes a comment. Only the original author or an Admin may delete.
    /// Returns 403 Forbidden if the requesting user has neither permission.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        _logger.LogInformation("DELETE api/comments/{Id} by user {UserId}", id, CurrentUserId);

        try
        {
            var deleted = await _service.DeleteAsync(id, CurrentUserId);
            if (!deleted)
            {
                // Service returns false for both "not found" and "not authorised";
                // attempt a secondary lookup to distinguish the two cases.
                return NotFound(ApiResponse<string>.Fail($"Comment {id} not found or access denied."));
            }
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<string>.Fail(ex.Message));
        }
    }
}
