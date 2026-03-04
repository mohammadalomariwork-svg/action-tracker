using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.ActionItems.DTOs;
using ActionTracker.Application.Features.ActionItems.Interfaces;
using ActionTracker.Application.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

[ApiController]
[Route("api/action-items")]
[Authorize(AuthenticationSchemes = "LocalBearer,AzureAD")]
public class ActionItemsController : ControllerBase
{
    private readonly IActionItemService _service;
    private readonly ILogger<ActionItemsController> _logger;

    public ActionItemsController(IActionItemService service, ILogger<ActionItemsController> logger)
    {
        _service = service;
        _logger  = logger;
    }

    // -------------------------------------------------------------------------
    // GET api/action-items
    // -------------------------------------------------------------------------

    /// <summary>Returns a paginated, filtered list of action items.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ActionItemResponseDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<ActionItemResponseDto>>>> GetAll(
        [FromQuery] ActionItemFilterDto filter, CancellationToken ct)
    {
        var result = await _service.GetAllAsync(filter, ct);
        return Ok(ApiResponse<PagedResult<ActionItemResponseDto>>.Ok(result));
    }

    // -------------------------------------------------------------------------
    // GET api/action-items/{id}
    // -------------------------------------------------------------------------

    /// <summary>Returns a single action item by integer ID, or 404 if not found.</summary>
    [HttpGet("{id:int}", Name = nameof(GetById))]
    [ProducesResponseType(typeof(ApiResponse<ActionItemResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ActionItemResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ActionItemResponseDto>>> GetById(
        int id, CancellationToken ct)
    {
        var item = await _service.GetByIdAsync(id, ct);

        if (item is null)
        {
            _logger.LogWarning("ActionItem {Id} not found", id);
            return NotFound(ApiResponse<ActionItemResponseDto>.Fail($"ActionItem {id} not found."));
        }

        return Ok(ApiResponse<ActionItemResponseDto>.Ok(item));
    }

    // -------------------------------------------------------------------------
    // POST api/action-items
    // -------------------------------------------------------------------------

    /// <summary>Creates a new action item. ActionId is auto-generated (ACT-001 format).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ActionItemResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ActionItemResponseDto>>> Create(
        [FromBody] ActionItemCreateDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId  = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var created = await _service.CreateAsync(dto, userId, ct);

        _logger.LogInformation("ActionItem {ActionId} created", created.ActionId);

        return CreatedAtRoute(
            nameof(GetById),
            new { id = created.Id },
            ApiResponse<ActionItemResponseDto>.Ok(created));
    }

    // -------------------------------------------------------------------------
    // PUT api/action-items/{id}
    // -------------------------------------------------------------------------

    /// <summary>Updates an existing action item. Only supplied fields are changed.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<ActionItemResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ActionItemResponseDto>>> Update(
        int id, [FromBody] ActionItemUpdateDto dto, CancellationToken ct)
    {
        try
        {
            var updated = await _service.UpdateAsync(id, dto, ct);
            return Ok(ApiResponse<ActionItemResponseDto>.Ok(updated));
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "ActionItem {Id} not found for update", id);
            return NotFound(ApiResponse<ActionItemResponseDto>.Fail(ex.Message));
        }
    }

    // -------------------------------------------------------------------------
    // DELETE api/action-items/{id}   [Admin, Manager only]
    // -------------------------------------------------------------------------

    /// <summary>Soft-deletes an action item. Restricted to Admin and Manager roles.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            await _service.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "ActionItem {Id} not found for deletion", id);
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // -------------------------------------------------------------------------
    // PATCH api/action-items/{id}/status
    // -------------------------------------------------------------------------

    /// <summary>Updates the status of an action item. Completing an item sets Progress to 100.</summary>
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(typeof(ApiResponse<ActionItemResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ActionItemResponseDto>>> UpdateStatus(
        int id, [FromBody] UpdateStatusRequest request, CancellationToken ct)
    {
        try
        {
            await _service.UpdateStatusAsync(id, request.Status, ct);

            // Re-fetch to return the full updated DTO
            var item = await _service.GetByIdAsync(id, ct);
            return Ok(ApiResponse<ActionItemResponseDto>.Ok(item!));
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "ActionItem {Id} not found for status update", id);
            return NotFound(ApiResponse<ActionItemResponseDto>.Fail(ex.Message));
        }
    }

    // -------------------------------------------------------------------------
    // POST api/action-items/process-overdue   [Admin only]
    // -------------------------------------------------------------------------

    /// <summary>
    /// Scans all non-completed items past their due date and marks them Overdue.
    /// Restricted to Admin role.
    /// </summary>
    [HttpPost("process-overdue")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<int>>> ProcessOverdue(CancellationToken ct)
    {
        var count = await _service.ProcessOverdueItemsAsync(ct);

        _logger.LogInformation("ProcessOverdue completed: {Count} items marked as Overdue", count);

        return Ok(ApiResponse<int>.Ok(count, $"{count} item(s) marked as Overdue."));
    }
}
