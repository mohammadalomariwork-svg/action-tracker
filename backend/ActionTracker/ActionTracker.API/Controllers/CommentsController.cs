using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.Comments.DTOs;
using ActionTracker.Application.Features.Comments.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

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

    // -------------------------------------------------------------------------
    // GET api/comments?entityType=Project&entityId={guid}
    // -------------------------------------------------------------------------

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<CommentResponseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByEntity(
        [FromQuery] string entityType, [FromQuery] Guid entityId, CancellationToken ct)
    {
        var comments = await _service.GetByEntityAsync(entityType, entityId, ct);
        return Ok(ApiResponse<List<CommentResponseDto>>.Ok(comments));
    }

    // -------------------------------------------------------------------------
    // POST api/comments?entityType=Project&entityId={guid}
    // -------------------------------------------------------------------------

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CommentResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Add(
        [FromQuery] string entityType,
        [FromQuery] Guid entityId,
        [FromBody] CreateCommentDto dto,
        CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var comment = await _service.AddAsync(entityType, entityId, dto, userId, ct);
        return Created(string.Empty, ApiResponse<CommentResponseDto>.Ok(comment));
    }

    // -------------------------------------------------------------------------
    // PUT api/comments/{id}
    // -------------------------------------------------------------------------

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CommentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateCommentDto dto, CancellationToken ct)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            var comment = await _service.UpdateAsync(id, dto, userId, ct);
            return Ok(ApiResponse<CommentResponseDto>.Ok(comment));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(ex.Message));
        }
    }

    // -------------------------------------------------------------------------
    // DELETE api/comments/{id}
    // -------------------------------------------------------------------------

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            await _service.DeleteAsync(id, userId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(ex.Message));
        }
    }
}
