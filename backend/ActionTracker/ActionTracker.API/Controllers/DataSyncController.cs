using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.DataSync;
using ActionTracker.Application.Features.DataSync.DTOs;
using ActionTracker.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

/// <summary>
/// Admin Panel → Data Sync. Lists the scheduled sync jobs, lets an admin
/// change the schedule / enabled flag, trigger an immediate run, probe
/// connectivity, and inspect run history.
/// </summary>
[ApiController]
[Route("api/data-sync")]
[Authorize(Policy = PermissionPolicies.DataSyncView)]
public class DataSyncController : ControllerBase
{
    private readonly IDataSyncService _service;
    private readonly ILogger<DataSyncController> _logger;

    public DataSyncController(IDataSyncService service, ILogger<DataSyncController> logger)
    {
        _service = service;
        _logger  = logger;
    }

    /// <summary>All configured sync jobs with last-run summary and next scheduled run.</summary>
    [HttpGet("jobs")]
    public async Task<IActionResult> GetJobs(CancellationToken ct)
    {
        var jobs = await _service.GetJobsAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<DataSyncJobDto>>.Ok(jobs));
    }

    [HttpGet("jobs/{id:guid}")]
    public async Task<IActionResult> GetJob(Guid id, CancellationToken ct)
    {
        var job = await _service.GetJobAsync(id, ct);
        return job is null
            ? NotFound(ApiResponse<string>.Fail("Data sync job not found."))
            : Ok(ApiResponse<DataSyncJobDto>.Ok(job));
    }

    /// <summary>Recent run history, newest first.</summary>
    [HttpGet("jobs/{id:guid}/runs")]
    public async Task<IActionResult> GetRuns(Guid id, [FromQuery] int take = 25, CancellationToken ct = default)
    {
        var runs = await _service.GetRunsAsync(id, take, ct);
        return Ok(ApiResponse<IReadOnlyList<DataSyncJobRunDto>>.Ok(runs));
    }

    /// <summary>Update schedule / enabled flag. Re-registers the job with the scheduler.</summary>
    [HttpPut("jobs/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.DataSyncEdit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDataSyncJobDto dto, CancellationToken ct)
    {
        if (dto is null)
            return BadRequest(ApiResponse<string>.Fail("Request body is required."));
        if (string.IsNullOrWhiteSpace(dto.CronExpression))
            return BadRequest(ApiResponse<string>.Fail("Cron expression is required."));

        try
        {
            var result = await _service.UpdateJobAsync(id, dto, CurrentUserId(), ct);
            return Ok(ApiResponse<DataSyncJobDto>.Ok(result, "Data sync job saved."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
    }

    /// <summary>Enqueue an immediate run of the job (runs in the background).</summary>
    [HttpPost("jobs/{id:guid}/run")]
    [Authorize(Policy = PermissionPolicies.DataSyncEdit)]
    public async Task<IActionResult> Run(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _service.TriggerAsync(id, CurrentUserId(), ct);
            return Accepted(ApiResponse<DataSyncJobDto>.Ok(result, "Sync started. Refresh in a few seconds to see the result."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<string>.Fail(ex.Message));
        }
    }

    /// <summary>Read-only connectivity and schema check of source and target.</summary>
    [HttpPost("jobs/{id:guid}/probe")]
    [Authorize(Policy = PermissionPolicies.DataSyncEdit)]
    public async Task<IActionResult> Probe(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _service.ProbeAsync(id, ct);
            return Ok(ApiResponse<DataSyncProbeResultDto>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
    }

    private string? CurrentUserId()
        => User.FindFirst(ClaimTypes.NameIdentifier)?.Value
           ?? User.FindFirst("oid")?.Value
           ?? User.Identity?.Name;
}
