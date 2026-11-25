using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.Documents;
using ActionTracker.Application.Features.Documents.DTOs;
using ActionTracker.Application.Features.Documents.Interfaces;
using ActionTracker.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

/// <summary>
/// Polymorphic documents for <c>ActionItem</c>, <c>ActionItemSubItem</c>,
/// <c>ActionItemUpdate</c>, <c>Project</c>, <c>Milestone</c>, <c>ProjectRisk</c>,
/// <c>KpiTarget</c> and <c>KpiActual</c> (any other type 400). Since WO-2026-11 A-4
/// every route checks that the caller can see the owning record — 404 when it
/// is hidden, also for a download or delete by document id. The three
/// action-item types also need <c>ActionItems.View</c> to read and
/// <c>ActionItems.Edit</c> to write (403); because one route serves every type,
/// those two policies are evaluated here and passed on rather than declared
/// with <c>[Authorize(Policy = …)]</c>. 409 when the record is locked (Done /
/// Cancelled action item, its sub-items, a submitted action update).
/// </summary>
[ApiController]
[Route("api/documents")]
[Authorize(AuthenticationSchemes = "LocalBearer,AzureAD")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _service;
    private readonly IAuthorizationService _authorization;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        IDocumentService service, IAuthorizationService authorization, ILogger<DocumentsController> logger)
    {
        _service       = service;
        _authorization = authorization;
        _logger        = logger;
    }

    private string CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

    /// <summary>The caller with the two action-item permissions resolved (used only for the action-item types).</summary>
    private async Task<DocumentCaller> CallerAsync() => new(
        CurrentUserId,
        (await _authorization.AuthorizeAsync(User, PermissionPolicies.ActionItemsView)).Succeeded,
        (await _authorization.AuthorizeAsync(User, PermissionPolicies.ActionItemsEdit)).Succeeded);

    // -------------------------------------------------------------------------
    // GET api/documents?entityType=ActionItem&entityId={guid}
    // -------------------------------------------------------------------------

    /// <summary>Documents of a record the caller can see; each carries <c>canDelete</c>.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<DocumentResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetByEntity(
        [FromQuery] string entityType, [FromQuery] Guid entityId, CancellationToken ct) =>
        Run(async () => Ok(ApiResponse<List<DocumentResponseDto>>.Ok(
            await _service.GetByEntityAsync(entityType, entityId, await CallerAsync(), ct))));

    // -------------------------------------------------------------------------
    // POST api/documents
    // -------------------------------------------------------------------------

    /// <summary>Uploads a file (multipart, max 10 MB; PDF / DOC / DOCX / XLS / XLSX / PPT / PPTX).</summary>
    [HttpPost]
    [RequestSizeLimit(11 * 1024 * 1024)] // slightly above 10 MB to account for multipart overhead
    [ProducesResponseType(typeof(ApiResponse<DocumentResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> Upload(
        [FromForm] string entityType,
        [FromForm] Guid entityId,
        [FromForm] string name,
        IFormFile file,
        CancellationToken ct) =>
        Run(async () =>
        {
            var doc = await _service.UploadAsync(entityType, entityId, name, file, await CallerAsync(), ct);
            return Created(string.Empty, ApiResponse<DocumentResponseDto>.Ok(doc));
        });

    // -------------------------------------------------------------------------
    // GET api/documents/{id}/download
    // -------------------------------------------------------------------------

    [HttpGet("{id:guid}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Download(Guid id, CancellationToken ct) =>
        Run(async () =>
        {
            var doc = await _service.DownloadAsync(id, await CallerAsync(), ct);
            return File(doc.Content, doc.ContentType, doc.FileName);
        });

    // -------------------------------------------------------------------------
    // DELETE api/documents/{id}
    // -------------------------------------------------------------------------

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        Run(async () =>
        {
            await _service.DeleteAsync(id, await CallerAsync(), ct);
            return NoContent();
        });

    /// <summary>Maps the service's error contract; messages are returned so the user sees the reason.</summary>
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
        catch (DocumentLockedException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            _logger.LogInformation("Document request refused: {Reason}", ex.Message);
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
