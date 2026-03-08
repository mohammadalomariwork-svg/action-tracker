using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.Documents.DTOs;
using ActionTracker.Application.Features.Documents.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize(AuthenticationSchemes = "LocalBearer,AzureAD")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _service;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(IDocumentService service, ILogger<DocumentsController> logger)
    {
        _service = service;
        _logger  = logger;
    }

    // -------------------------------------------------------------------------
    // GET api/documents?entityType=ActionItem&entityId={guid}
    // -------------------------------------------------------------------------

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<DocumentResponseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByEntity(
        [FromQuery] string entityType, [FromQuery] Guid entityId, CancellationToken ct)
    {
        var docs = await _service.GetByEntityAsync(entityType, entityId, ct);
        return Ok(ApiResponse<List<DocumentResponseDto>>.Ok(docs));
    }

    // -------------------------------------------------------------------------
    // POST api/documents
    // -------------------------------------------------------------------------

    [HttpPost]
    [RequestSizeLimit(11 * 1024 * 1024)] // slightly above 10 MB to account for multipart overhead
    [ProducesResponseType(typeof(ApiResponse<DocumentResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload(
        [FromForm] string entityType,
        [FromForm] Guid entityId,
        [FromForm] string name,
        IFormFile file,
        CancellationToken ct)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            var doc = await _service.UploadAsync(entityType, entityId, name, file, userId, ct);
            return Created(string.Empty, ApiResponse<DocumentResponseDto>.Ok(doc));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<DocumentResponseDto>.Fail(ex.Message));
        }
    }

    // -------------------------------------------------------------------------
    // GET api/documents/{id}/download
    // -------------------------------------------------------------------------

    [HttpGet("{id:guid}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        try
        {
            var doc = await _service.DownloadAsync(id, ct);
            return File(doc.Content, doc.ContentType, doc.FileName);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // -------------------------------------------------------------------------
    // DELETE api/documents/{id}
    // -------------------------------------------------------------------------

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
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
    }
}
