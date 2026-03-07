using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.Projects.DTOs;
using ActionTracker.Application.Features.Projects.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

/// <summary>
/// Manages document uploads, listings, downloads, and deletions for both
/// projects and action items.
/// File bytes are transported as <c>multipart/form-data</c>; metadata is
/// supplied as additional form fields.
/// </summary>
[ApiController]
[Route("api/documents")]
[Authorize(AuthenticationSchemes = "LocalBearer,AzureAD")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _service;
    private readonly ILogger<DocumentsController> _logger;

    /// <summary>Initialises the controller with required services.</summary>
    public DocumentsController(IDocumentService service, ILogger<DocumentsController> logger)
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

    // ── GET api/documents/project/{projectId} ────────────────────────────────

    /// <summary>Returns all active documents attached to the given project.</summary>
    [HttpGet("project/{projectId:int}")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<DocumentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByProject(int projectId)
    {
        _logger.LogInformation("GET api/documents/project/{ProjectId}", projectId);
        var result = await _service.GetByProjectAsync(projectId);
        return Ok(ApiResponse<IEnumerable<DocumentDto>>.Ok(result));
    }

    // ── GET api/documents/action-item/{actionItemId} ─────────────────────────

    /// <summary>Returns all active documents attached to the given action item.</summary>
    [HttpGet("action-item/{actionItemId:int}")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<DocumentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByActionItem(int actionItemId)
    {
        _logger.LogInformation("GET api/documents/action-item/{ActionItemId}", actionItemId);
        var result = await _service.GetByActionItemAsync(actionItemId);
        return Ok(ApiResponse<IEnumerable<DocumentDto>>.Ok(result));
    }

    // ── POST api/documents/project ────────────────────────────────────────────

    /// <summary>
    /// Uploads a document and attaches it to a project.
    /// Accepts <c>multipart/form-data</c>: the file in the <c>file</c> field
    /// and metadata fields matching <see cref="UploadDocumentDto"/>.
    /// Max 20 MB; allowed types: pdf, docx, xlsx, pptx, jpg, png, txt.
    /// </summary>
    [HttpPost("project")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<DocumentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadProjectDocument(
        [FromForm] UploadDocumentDto dto,
        IFormFile file)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        // Inject authenticated uploader identity.
        dto.UploadedByUserId   = CurrentUserId;
        dto.UploadedByUserName = CurrentUserName;

        _logger.LogInformation(
            "POST api/documents/project for project {ProjectId} by user {UserId}",
            dto.ProjectId, CurrentUserId);

        try
        {
            var created = await _service.UploadProjectDocumentAsync(dto, file);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<DocumentDto>.Ok(created));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // ── POST api/documents/action-item ────────────────────────────────────────

    /// <summary>
    /// Uploads a document and attaches it to an action item.
    /// Same constraints as the project upload endpoint.
    /// </summary>
    [HttpPost("action-item")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<DocumentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadActionDocument(
        [FromForm] UploadDocumentDto dto,
        IFormFile file)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        dto.UploadedByUserId   = CurrentUserId;
        dto.UploadedByUserName = CurrentUserName;

        _logger.LogInformation(
            "POST api/documents/action-item for action {ActionItemId} by user {UserId}",
            dto.ActionItemId, CurrentUserId);

        try
        {
            var created = await _service.UploadActionDocumentAsync(dto, file);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<DocumentDto>.Ok(created));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // ── GET api/documents/{id}/download ──────────────────────────────────────

    /// <summary>
    /// Downloads a document as a file stream.
    /// Pass <c>?type=project</c> or <c>?type=action</c> to select the document
    /// table.  Returns a <see cref="FileContentResult"/> with the original file
    /// name and MIME type.
    /// </summary>
    [HttpGet("{id:int}/download")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(int id, [FromQuery] string type = "project")
    {
        bool isProjectDocument = type.Equals("project", StringComparison.OrdinalIgnoreCase);

        if (!isProjectDocument && !type.Equals("action", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse<string>.Fail("Query param 'type' must be 'project' or 'action'."));

        _logger.LogInformation("GET api/documents/{Id}/download?type={Type}", id, type);

        try
        {
            var (bytes, contentType, fileName) = await _service.DownloadAsync(id, isProjectDocument);
            return File(bytes, contentType, fileName);
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
    }

    // ── DELETE api/documents/project/{id} ────────────────────────────────────

    /// <summary>
    /// Soft-deletes a project document and removes the physical file.
    /// Only the uploader or an Admin may delete.
    /// </summary>
    [HttpDelete("project/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProjectDocument(int id)
    {
        _logger.LogInformation(
            "DELETE api/documents/project/{Id} by user {UserId}", id, CurrentUserId);

        var deleted = await _service.DeleteProjectDocumentAsync(id, CurrentUserId);
        if (!deleted)
            return NotFound(ApiResponse<string>.Fail(
                $"Project document {id} not found or access denied."));
        return NoContent();
    }

    // ── DELETE api/documents/action-item/{id} ────────────────────────────────

    /// <summary>
    /// Soft-deletes an action-item document and removes the physical file.
    /// Only the uploader or an Admin may delete.
    /// </summary>
    [HttpDelete("action-item/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteActionDocument(int id)
    {
        _logger.LogInformation(
            "DELETE api/documents/action-item/{Id} by user {UserId}", id, CurrentUserId);

        var deleted = await _service.DeleteActionDocumentAsync(id, CurrentUserId);
        if (!deleted)
            return NotFound(ApiResponse<string>.Fail(
                $"Action document {id} not found or access denied."));
        return NoContent();
    }
}
