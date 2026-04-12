using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.ProjectRisks.DTOs;
using ActionTracker.Application.Features.ProjectRisks.Interfaces;
using ActionTracker.Application.Helpers;
using ActionTracker.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/risks")]
[Authorize(AuthenticationSchemes = "LocalBearer,AzureAD")]
public class ProjectRisksController : ControllerBase
{
    private readonly IProjectRiskService _service;
    private readonly ILogger<ProjectRisksController> _logger;

    public ProjectRisksController(IProjectRiskService service, ILogger<ProjectRisksController> logger)
    {
        _service = service;
        _logger  = logger;
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    private string CurrentUserDisplayName =>
        User.FindFirstValue("displayName")
        ?? User.FindFirstValue(ClaimTypes.Email)
        ?? string.Empty;

    /// <summary>Returns paginated risks for a project.</summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.ProjectsView)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ProjectRiskSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByProject(
        Guid projectId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? rating = null,
        [FromQuery] string? category = null,
        CancellationToken ct = default)
    {
        var result = await _service.GetByProjectAsync(projectId, page, pageSize, status, rating, category, ct);
        return Ok(ApiResponse<PagedResult<ProjectRiskSummaryDto>>.Ok(result));
    }

    /// <summary>Returns a single risk by ID.</summary>
    [HttpGet("{riskId:guid}")]
    [Authorize(Policy = PermissionPolicies.ProjectsView)]
    [ProducesResponseType(typeof(ApiResponse<ProjectRiskDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid projectId, Guid riskId, CancellationToken ct)
    {
        var risk = await _service.GetByIdAsync(riskId, ct);
        if (risk is null)
            return NotFound(ApiResponse<ProjectRiskDto>.Fail($"Risk {riskId} not found."));

        return Ok(ApiResponse<ProjectRiskDto>.Ok(risk));
    }

    /// <summary>Returns risk stats summary for a project.</summary>
    [HttpGet("stats")]
    [Authorize(Policy = PermissionPolicies.ProjectsView)]
    [ProducesResponseType(typeof(ApiResponse<ProjectRiskStatsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats(Guid projectId, CancellationToken ct)
    {
        var stats = await _service.GetStatsAsync(projectId, ct);
        return Ok(ApiResponse<ProjectRiskStatsDto>.Ok(stats));
    }

    /// <summary>Creates a new risk for the project.</summary>
    [HttpPost]
    [Authorize(Policy = PermissionPolicies.ProjectsEdit)]
    [ProducesResponseType(typeof(ApiResponse<ProjectRiskDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(Guid projectId, [FromBody] CreateProjectRiskDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        dto.ProjectId = projectId;

        try
        {
            var created = await _service.CreateAsync(dto, CurrentUserId, CurrentUserDisplayName, ct);
            _logger.LogInformation("Risk {Code} created for project {ProjectId}", created.RiskCode, projectId);
            return Created(string.Empty, ApiResponse<ProjectRiskDto>.Ok(created));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<ProjectRiskDto>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<ProjectRiskDto>.Fail(ex.Message));
        }
    }

    /// <summary>Updates an existing risk.</summary>
    [HttpPut("{riskId:guid}")]
    [Authorize(Policy = PermissionPolicies.ProjectsEdit)]
    [ProducesResponseType(typeof(ApiResponse<ProjectRiskDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid projectId, Guid riskId, [FromBody] UpdateProjectRiskDto dto, CancellationToken ct)
    {
        try
        {
            var updated = await _service.UpdateAsync(riskId, dto, ct);
            return Ok(ApiResponse<ProjectRiskDto>.Ok(updated));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<ProjectRiskDto>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<ProjectRiskDto>.Fail(ex.Message));
        }
    }

    /// <summary>Soft-deletes a risk.</summary>
    [HttpDelete("{riskId:guid}")]
    [Authorize(Policy = PermissionPolicies.ProjectsDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid projectId, Guid riskId, CancellationToken ct)
    {
        try
        {
            await _service.SoftDeleteAsync(riskId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>Restores a soft-deleted risk.</summary>
    [HttpPatch("{riskId:guid}/restore")]
    [Authorize(Policy = PermissionPolicies.ProjectsEdit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Restore(Guid projectId, Guid riskId, CancellationToken ct)
    {
        try
        {
            await _service.RestoreAsync(riskId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
