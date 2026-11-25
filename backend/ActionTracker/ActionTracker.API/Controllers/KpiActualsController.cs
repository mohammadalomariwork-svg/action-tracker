using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.Documents;
using ActionTracker.Application.Features.Documents.DTOs;
using ActionTracker.Application.Features.Documents.Interfaces;
using ActionTracker.Application.Features.KpiActuals.DTOs;
using ActionTracker.Application.Features.KpiActuals.Interfaces;
using ActionTracker.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

[ApiController]
[Route("api/kpis/actuals")]
[Authorize(Policy = "LocalOrAzureAD")]
public class KpiActualsController : ControllerBase
{
    private readonly IKpiActualService _service;
    private readonly IDocumentService _documents;
    private readonly ILogger<KpiActualsController> _logger;

    public KpiActualsController(
        IKpiActualService service,
        IDocumentService documents,
        ILogger<KpiActualsController> logger)
    {
        _service = service;
        _documents = documents;
        _logger = logger;
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    // ── CRUD ────────────────────────────────────────────────────────────────

    [HttpGet("by-kpi/{kpiId:guid}")]
    [Authorize(Policy = PermissionPolicies.KpiActualsView)]
    public async Task<IActionResult> ListByKpi(Guid kpiId, [FromQuery] int year, CancellationToken ct = default)
    {
        try
        {
            var rows = await _service.ListByKpiAsync(kpiId, year, CurrentUserId, ct);
            return Ok(ApiResponse<List<KpiActualDto>>.Ok(rows));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse<string>.Fail(ex.Message)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<string>.Fail(ex.Message)); }
    }

    [HttpGet("grid")]
    [Authorize(Policy = PermissionPolicies.KpiActualsView)]
    public async Task<IActionResult> GetGrid(
        [FromQuery] Guid kpiTargetId, [FromQuery] int year, CancellationToken ct = default)
    {
        try
        {
            var grid = await _service.GetGridAsync(kpiTargetId, year, CurrentUserId, ct);
            return Ok(ApiResponse<KpiActualGridResponseDto>.Ok(grid));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse<string>.Fail(ex.Message)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<string>.Fail(ex.Message)); }
    }

    [HttpGet("history")]
    [Authorize(Policy = PermissionPolicies.KpiActualsView)]
    public async Task<IActionResult> GetHistory(
        [FromQuery] Guid kpiTargetId,
        [FromQuery] int periodType,
        [FromQuery] int periodIndex,
        [FromQuery] int year,
        CancellationToken ct = default)
    {
        try
        {
            var rows = await _service.GetHistoryAsync(kpiTargetId, periodType, periodIndex, year, CurrentUserId, ct);
            return Ok(ApiResponse<List<KpiActualDto>>.Ok(rows));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse<string>.Fail(ex.Message)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<string>.Fail(ex.Message)); }
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.KpiActualsView)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var dto = await _service.GetByIdAsync(id, CurrentUserId, ct);
        if (dto is null) return NotFound(ApiResponse<string>.Fail("KPI actual not found."));
        return Ok(ApiResponse<KpiActualDto>.Ok(dto));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.KpiActualsCreate)]
    public async Task<IActionResult> Create([FromBody] CreateKpiActualRequestDto dto, CancellationToken ct = default)
    {
        try
        {
            var result = await _service.CreateAsync(dto, CurrentUserId, ct);
            return Ok(ApiResponse<KpiActualDto>.Ok(result));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse<string>.Fail(ex.Message)); }
        catch (ArgumentException ex) { return BadRequest(ApiResponse<string>.Fail(ex.Message)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<string>.Fail(ex.Message)); }
        catch (InvalidOperationException ex) { return Conflict(ApiResponse<string>.Fail(ex.Message)); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.KpiActualsEdit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateKpiActualRequestDto dto, CancellationToken ct = default)
    {
        try
        {
            var result = await _service.UpdateAsync(id, dto, CurrentUserId, ct);
            return Ok(ApiResponse<KpiActualDto>.Ok(result));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse<string>.Fail(ex.Message)); }
        catch (ArgumentException ex) { return BadRequest(ApiResponse<string>.Fail(ex.Message)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<string>.Fail(ex.Message)); }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.KpiActualsEdit)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        try
        {
            await _service.DeleteAsync(id, CurrentUserId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse<string>.Fail(ex.Message)); }
        catch (ArgumentException ex) { return BadRequest(ApiResponse<string>.Fail(ex.Message)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<string>.Fail(ex.Message)); }
    }

    // ── Documents (evidence) ────────────────────────────────────────────────

    [HttpGet("{id:guid}/documents")]
    [Authorize(Policy = PermissionPolicies.KpiActualsView)]
    public async Task<IActionResult> ListDocuments(Guid id, CancellationToken ct = default)
    {
        try
        {
            var docs = await _documents.GetByEntityAsync("KpiActual", id, DocumentCaller.WithoutActionItemRights(CurrentUserId), ct);
            return Ok(ApiResponse<List<DocumentResponseDto>>.Ok(docs));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse<string>.Fail(ex.Message)); }
    }

    [HttpPost("{id:guid}/documents")]
    [Authorize(Policy = PermissionPolicies.KpiActualsEdit)]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> UploadDocument(
        Guid id,
        [FromForm] string name,
        IFormFile file,
        CancellationToken ct = default)
    {
        try
        {
            var doc = await _documents.UploadAsync("KpiActual", id, name, file, DocumentCaller.WithoutActionItemRights(CurrentUserId), ct);
            return Ok(ApiResponse<DocumentResponseDto>.Ok(doc));
        }
        catch (ArgumentException ex)
        {
            // 10-doc cap surfaces as ArgumentException — return 409 to match the
            // KpiTarget cap behaviour described in CR-001-F §9.
            if (ex.Message.Contains("Maximum of", StringComparison.OrdinalIgnoreCase))
                return Conflict(ApiResponse<string>.Fail(ex.Message));
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<string>.Fail(ex.Message)); }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse<string>.Fail(ex.Message)); }
    }

    [HttpDelete("{id:guid}/documents/{documentId:guid}")]
    [Authorize(Policy = PermissionPolicies.KpiActualsEdit)]
    public async Task<IActionResult> DeleteDocument(Guid id, Guid documentId, CancellationToken ct = default)
    {
        try
        {
            await _documents.DeleteAsync(documentId, DocumentCaller.WithoutActionItemRights(CurrentUserId), ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse<string>.Fail(ex.Message)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<string>.Fail(ex.Message)); }
    }
}
