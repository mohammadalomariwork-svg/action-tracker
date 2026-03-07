using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.Projects.DTOs;
using ActionTracker.Application.Features.Projects.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

/// <summary>
/// Manages project budget records and associated contracts.
/// Budget operations use upsert semantics (create or update keyed by ProjectId).
/// </summary>
[ApiController]
[Route("api/budget")]
[Authorize(AuthenticationSchemes = "LocalBearer,AzureAD")]
public class BudgetController : ControllerBase
{
    private readonly IBudgetService _service;
    private readonly ILogger<BudgetController> _logger;

    /// <summary>Initialises the controller with required services.</summary>
    public BudgetController(IBudgetService service, ILogger<BudgetController> logger)
    {
        _service = service;
        _logger  = logger;
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    // ── GET api/budget/project/{projectId} ────────────────────────────────────

    /// <summary>
    /// Returns the budget record for the given project, or 404 when none has
    /// been set up yet.
    /// </summary>
    [HttpGet("project/{projectId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ProjectBudgetDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByProject(Guid projectId)
    {
        _logger.LogInformation("GET api/budget/project/{ProjectId}", projectId);
        var result = await _service.GetByProjectAsync(projectId);
        if (result is null)
            return NotFound(ApiResponse<string>.Fail($"No budget found for project {projectId}."));
        return Ok(ApiResponse<ProjectBudgetDto>.Ok(result));
    }

    // ── POST api/budget ───────────────────────────────────────────────────────

    /// <summary>
    /// Creates or fully replaces the budget for a project (upsert).
    /// Restricted to Admin and Manager roles.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(ApiResponse<ProjectBudgetDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateOrUpdate([FromBody] CreateUpdateBudgetDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        _logger.LogInformation(
            "POST api/budget for project {ProjectId} by user {UserId}", dto.ProjectId, CurrentUserId);

        var result = await _service.CreateOrUpdateAsync(dto);
        return Ok(ApiResponse<ProjectBudgetDto>.Ok(result));
    }

    // ── GET api/budget/project/{projectId}/contracts ──────────────────────────

    /// <summary>Returns all active contracts associated with the given project.</summary>
    [HttpGet("project/{projectId:guid}/contracts")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ContractDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetContracts(Guid projectId)
    {
        _logger.LogInformation("GET api/budget/project/{ProjectId}/contracts", projectId);
        var result = await _service.GetContractsByProjectAsync(projectId);
        return Ok(ApiResponse<IEnumerable<ContractDto>>.Ok(result));
    }

    // ── POST api/budget/contracts ─────────────────────────────────────────────

    /// <summary>Creates a new contract record on a project. Restricted to Admin and Manager roles.</summary>
    [HttpPost("contracts")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(ApiResponse<ContractDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateContract([FromBody] CreateContractDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        _logger.LogInformation(
            "POST api/budget/contracts for project {ProjectId} by user {UserId}",
            dto.ProjectId, CurrentUserId);

        var created = await _service.CreateContractAsync(dto);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<ContractDto>.Ok(created));
    }

    // ── PUT api/budget/contracts/{id} ─────────────────────────────────────────

    /// <summary>Updates a contract record. Restricted to Admin and Manager roles.</summary>
    [HttpPut("contracts/{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(ApiResponse<ContractDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateContract(Guid id, [FromBody] UpdateContractDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("Invalid request data."));

        _logger.LogInformation(
            "PUT api/budget/contracts/{Id} by user {UserId}", id, CurrentUserId);

        var updated = await _service.UpdateContractAsync(id, dto);
        if (updated is null)
            return NotFound(ApiResponse<string>.Fail($"Contract {id} not found."));
        return Ok(ApiResponse<ContractDto>.Ok(updated));
    }

    // ── DELETE api/budget/contracts/{id} ──────────────────────────────────────

    /// <summary>
    /// Soft-deletes a contract record. Restricted to Admin and Manager roles.
    /// </summary>
    [HttpDelete("contracts/{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteContract(Guid id)
    {
        _logger.LogInformation(
            "DELETE api/budget/contracts/{Id} by user {UserId}", id, CurrentUserId);

        var deleted = await _service.DeleteContractAsync(id);
        if (!deleted)
            return NotFound(ApiResponse<string>.Fail($"Contract {id} not found."));
        return NoContent();
    }
}
