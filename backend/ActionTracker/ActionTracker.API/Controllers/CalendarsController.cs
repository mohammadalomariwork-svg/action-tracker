using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.Calendars.DTOs;
using ActionTracker.Application.Features.Calendars.Interfaces;
using ActionTracker.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

/// <summary>
/// Admin API for working calendars (weekly pattern + holidays / exceptions).
/// Rule violations — duplicate name, touching the default calendar, a calendar
/// still used by a workspace — return 409 Conflict.
/// </summary>
[ApiController]
[Route("api/calendars")]
[Authorize(AuthenticationSchemes = "LocalBearer,AzureAD")]
public class CalendarsController : ControllerBase
{
    private readonly IWorkCalendarService _service;
    private readonly ILogger<CalendarsController> _logger;

    public CalendarsController(IWorkCalendarService service, ILogger<CalendarsController> logger)
    {
        _service = service;
        _logger  = logger;
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    /// <summary>Returns all calendars, default first. Pass includeDeleted=true to also list soft-deleted ones.</summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.CalendarsView)]
    [ProducesResponseType(typeof(ApiResponse<List<WorkCalendarDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] bool includeDeleted = false, CancellationToken ct = default)
    {
        var calendars = await _service.GetAllAsync(includeDeleted, ct);
        return Ok(ApiResponse<List<WorkCalendarDto>>.Ok(calendars));
    }

    /// <summary>Returns a single calendar with its weekly pattern and exceptions.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.CalendarsView)]
    [ProducesResponseType(typeof(ApiResponse<WorkCalendarDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<WorkCalendarDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var calendar = await _service.GetByIdAsync(id, ct);
        if (calendar is null)
            return NotFound(ApiResponse<WorkCalendarDto>.Fail($"Calendar {id} not found."));

        return Ok(ApiResponse<WorkCalendarDto>.Ok(calendar));
    }

    /// <summary>
    /// Returns the workspaces using a calendar. Workspaces the caller cannot
    /// see are reported only as a count.
    /// </summary>
    [HttpGet("{id:guid}/usage")]
    [Authorize(Policy = PermissionPolicies.CalendarsView)]
    [ProducesResponseType(typeof(ApiResponse<WorkCalendarUsageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<WorkCalendarUsageDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUsage(Guid id, CancellationToken ct)
    {
        try
        {
            var usage = await _service.GetUsageAsync(id, CurrentUserId, ct);
            return Ok(ApiResponse<WorkCalendarUsageDto>.Ok(usage));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<WorkCalendarUsageDto>.Fail(ex.Message));
        }
    }

    /// <summary>Creates a calendar.</summary>
    [HttpPost]
    [Authorize(Policy = PermissionPolicies.CalendarsCreate)]
    [ProducesResponseType(typeof(ApiResponse<WorkCalendarDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<WorkCalendarDto>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateWorkCalendarDto dto, CancellationToken ct)
    {
        try
        {
            var created = await _service.CreateAsync(dto, CurrentUserId, ct);
            return Created(string.Empty, ApiResponse<WorkCalendarDto>.Ok(created));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<WorkCalendarDto>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<WorkCalendarDto>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Updates a calendar, its weekly pattern and its full exception list.
    /// Deactivating the default calendar or one a workspace uses returns 409.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.CalendarsEdit)]
    [ProducesResponseType(typeof(ApiResponse<WorkCalendarDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<WorkCalendarDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<WorkCalendarDto>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWorkCalendarDto dto, CancellationToken ct)
    {
        try
        {
            var updated = await _service.UpdateAsync(id, dto, CurrentUserId, ct);
            return Ok(ApiResponse<WorkCalendarDto>.Ok(updated));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<WorkCalendarDto>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<WorkCalendarDto>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<WorkCalendarDto>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Soft-deletes a calendar. Returns 409 for the default calendar and for a
    /// calendar a workspace still uses.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.CalendarsDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await _service.SoftDeleteAsync(id, CurrentUserId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogInformation("Calendar {Id} delete refused: {Reason}", id, ex.Message);
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>Restores a soft-deleted calendar. Returns 409 when its name is now taken.</summary>
    [HttpPatch("{id:guid}/restore")]
    [Authorize(Policy = PermissionPolicies.CalendarsEdit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct)
    {
        try
        {
            await _service.RestoreAsync(id, CurrentUserId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Makes this calendar the default; the previous default loses the flag.
    /// Returns 409 for an inactive calendar.
    /// </summary>
    [HttpPut("{id:guid}/default")]
    [Authorize(Policy = PermissionPolicies.CalendarsEdit)]
    [ProducesResponseType(typeof(ApiResponse<WorkCalendarDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<WorkCalendarDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<WorkCalendarDto>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetDefault(Guid id, CancellationToken ct)
    {
        try
        {
            var calendar = await _service.SetDefaultAsync(id, CurrentUserId, ct);
            return Ok(ApiResponse<WorkCalendarDto>.Ok(calendar));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<WorkCalendarDto>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<WorkCalendarDto>.Fail(ex.Message));
        }
    }
}
