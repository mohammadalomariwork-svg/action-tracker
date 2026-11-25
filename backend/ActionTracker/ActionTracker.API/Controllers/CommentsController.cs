using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.Comments.DTOs;
using ActionTracker.Application.Features.Comments.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

/// <summary>
/// Shared comments on projects, milestones and risks
/// (<c>entityType</c> = <c>Project</c>, <c>Milestone</c>, <c>ProjectRisk</c>).
/// Every route first checks that the caller can see the parent record — the
/// same rule as the parent's own GET by id (deleted, or in a private workspace
/// the caller is not part of → 404). Any other entity type is 400.
/// <c>ActionItem</c> comments are read-only here since WO-2026-11 A-3: posting,
/// editing or deleting one is 400 — action items use Action Updates
/// (<c>/api/action-items/{id}/updates</c>).
/// </summary>
[ApiController]
[Route("api/comments")]
[Authorize(AuthenticationSchemes = "LocalBearer,AzureAD")]
public class CommentsController : ControllerBase
{
    private readonly ICommentService _service;
    private readonly ILogger<CommentsController> _logger;

    public CommentsController(ICommentService service, ILogger<CommentsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    private string CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

    // -------------------------------------------------------------------------
    // GET api/comments?entityType=Project&entityId={guid}
    // -------------------------------------------------------------------------

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<CommentResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetByEntity(
        [FromQuery] string entityType, [FromQuery] Guid entityId, CancellationToken ct) =>
        Run(async () => Ok(ApiResponse<List<CommentResponseDto>>.Ok(
            await _service.GetByEntityAsync(entityType, entityId, ct))));

    // -------------------------------------------------------------------------
    // POST api/comments?entityType=Project&entityId={guid}
    // -------------------------------------------------------------------------

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CommentResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Add(
        [FromQuery] string entityType,
        [FromQuery] Guid entityId,
        [FromBody] CreateCommentDto dto,
        CancellationToken ct) =>
        Run(async () =>
        {
            var comment = await _service.AddAsync(entityType, entityId, dto, CurrentUserId, ct);
            return Created(string.Empty, ApiResponse<CommentResponseDto>.Ok(comment));
        });

    // -------------------------------------------------------------------------
    // PUT api/comments/{id}
    // -------------------------------------------------------------------------

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CommentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Update(
        Guid id, [FromBody] UpdateCommentDto dto, CancellationToken ct) =>
        Run(async () => Ok(ApiResponse<CommentResponseDto>.Ok(
            await _service.UpdateAsync(id, dto, CurrentUserId, ct))));

    // -------------------------------------------------------------------------
    // DELETE api/comments/{id}
    // -------------------------------------------------------------------------

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        Run(async () =>
        {
            await _service.DeleteAsync(id, CurrentUserId, ct);
            return NoContent();
        });

    /// <summary>Maps the service's error contract (messages returned so the caller sees the reason).</summary>
    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            _logger.LogInformation("Comment request refused: {Reason}", ex.Message);
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
