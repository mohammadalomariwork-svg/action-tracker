using ActionTracker.API.Models;
using ActionTracker.Application.Features.Milestones.DTOs;
using ActionTracker.Application.Features.Milestones.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/milestones")]
[Authorize(AuthenticationSchemes = "LocalBearer,AzureAD")]
public class MilestonesController : ControllerBase
{
    private readonly IMilestoneService _service;
    private readonly ILogger<MilestonesController> _logger;

    public MilestonesController(IMilestoneService service, ILogger<MilestonesController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>Returns all milestones for a project, ordered by sequence.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<MilestoneResponseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByProject(Guid projectId, CancellationToken ct)
    {
        var milestones = await _service.GetByProjectAsync(projectId, ct);
        return Ok(ApiResponse<List<MilestoneResponseDto>>.Ok(milestones));
    }

    /// <summary>Returns a single milestone by ID.</summary>
    [HttpGet("{milestoneId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<MilestoneResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid projectId, Guid milestoneId, CancellationToken ct)
    {
        var milestone = await _service.GetByIdAsync(milestoneId, ct);
        if (milestone is null)
            return NotFound(ApiResponse<MilestoneResponseDto>.Fail($"Milestone {milestoneId} not found."));

        return Ok(ApiResponse<MilestoneResponseDto>.Ok(milestone));
    }

    /// <summary>Creates a new milestone for the project.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<MilestoneResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(Guid projectId, [FromBody] MilestoneCreateDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var created = await _service.CreateAsync(projectId, dto, ct);
            _logger.LogInformation("Milestone {Code} created for project {ProjectId}", created.MilestoneCode, projectId);
            return Created(string.Empty, ApiResponse<MilestoneResponseDto>.Ok(created));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<MilestoneResponseDto>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<MilestoneResponseDto>.Fail(ex.Message));
        }
    }

    /// <summary>Updates an existing milestone.</summary>
    [HttpPut("{milestoneId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<MilestoneResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid projectId, Guid milestoneId, [FromBody] MilestoneUpdateDto dto, CancellationToken ct)
    {
        try
        {
            var updated = await _service.UpdateAsync(projectId, milestoneId, dto, ct);
            return Ok(ApiResponse<MilestoneResponseDto>.Ok(updated));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<MilestoneResponseDto>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<MilestoneResponseDto>.Fail(ex.Message));
        }
    }

    /// <summary>Soft-deletes a milestone.</summary>
    [HttpDelete("{milestoneId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid projectId, Guid milestoneId, CancellationToken ct)
    {
        try
        {
            await _service.DeleteAsync(projectId, milestoneId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>Returns action-item stats for the project's milestones page.</summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApiResponse<MilestoneStatsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProjectStats(Guid projectId, CancellationToken ct)
    {
        var stats = await _service.GetProjectStatsAsync(projectId, ct);
        return Ok(ApiResponse<MilestoneStatsDto>.Ok(stats));
    }

    /// <summary>Locks milestone dates by saving baseline values.</summary>
    [HttpPost("baseline")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Baseline(Guid projectId, CancellationToken ct)
    {
        try
        {
            await _service.BaselineMilestonesAsync(projectId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
