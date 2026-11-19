using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.DataSync;
using ActionTracker.Application.Features.DataSync.DTOs;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Infrastructure.DataSync;

public sealed class DataSyncService : IDataSyncService
{
    private static readonly TimeSpan RunningWindow = TimeSpan.FromMinutes(30);

    private readonly IAppDbContext _db;
    private readonly IDataSyncScheduler _scheduler;
    private readonly IReadOnlyDictionary<string, IDataSyncJobHandler> _handlers;
    private readonly ILogger<DataSyncService> _logger;

    public DataSyncService(
        IAppDbContext db,
        IDataSyncScheduler scheduler,
        IEnumerable<IDataSyncJobHandler> handlers,
        ILogger<DataSyncService> logger)
    {
        _db        = db;
        _scheduler = scheduler;
        _handlers  = handlers.ToDictionary(h => h.JobKey, h => h, StringComparer.OrdinalIgnoreCase);
        _logger    = logger;
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<DataSyncJobDto>> GetJobsAsync(CancellationToken ct = default)
    {
        var jobs = await _db.DataSyncJobs.AsNoTracking().OrderBy(j => j.Name).ToListAsync(ct);
        var running = await RunningJobIdsAsync(ct);
        return jobs.Select(j => ToDto(j, running.Contains(j.Id))).ToList();
    }

    public async Task<DataSyncJobDto?> GetJobAsync(Guid id, CancellationToken ct = default)
    {
        var job = await _db.DataSyncJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id, ct);
        if (job is null) return null;
        var running = await RunningJobIdsAsync(ct);
        return ToDto(job, running.Contains(job.Id));
    }

    public async Task<IReadOnlyList<DataSyncJobRunDto>> GetRunsAsync(Guid id, int take = 25, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);
        var runs = await _db.DataSyncJobRuns.AsNoTracking()
            .Where(r => r.JobId == id)
            .OrderByDescending(r => r.StartedAt)
            .Take(take)
            .ToListAsync(ct);

        return runs.Select(r => new DataSyncJobRunDto
        {
            Id          = r.Id,
            JobId       = r.JobId,
            StartedAt   = r.StartedAt,
            FinishedAt  = r.FinishedAt,
            DurationMs  = r.FinishedAt.HasValue ? (long)(r.FinishedAt.Value - r.StartedAt).TotalMilliseconds : null,
            Status      = r.Status.ToString(),
            Trigger     = r.Trigger.ToString(),
            TriggeredBy = r.TriggeredBy,
            RowsRead    = r.RowsRead,
            RowsWritten = r.RowsWritten,
            Message     = r.Message,
        }).ToList();
    }

    public async Task<DataSyncProbeResultDto> ProbeAsync(Guid id, CancellationToken ct = default)
    {
        var job = await _db.DataSyncJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id, ct)
                  ?? throw new KeyNotFoundException("Data sync job not found.");

        if (!_handlers.TryGetValue(job.JobKey, out var handler))
            throw new InvalidOperationException($"No handler is registered for job '{job.JobKey}'.");

        return await handler.ProbeAsync(ct);
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    public async Task<DataSyncJobDto> UpdateJobAsync(Guid id, UpdateDataSyncJobDto dto, string? userId, CancellationToken ct = default)
    {
        var job = await _db.DataSyncJobs.FirstOrDefaultAsync(j => j.Id == id, ct)
                  ?? throw new KeyNotFoundException("Data sync job not found.");

        var cron = (dto.CronExpression ?? string.Empty).Trim();
        var tz   = string.IsNullOrWhiteSpace(dto.TimeZoneId) ? null : dto.TimeZoneId.Trim();

        _scheduler.ValidateCron(cron);       // throws ArgumentException
        _scheduler.ValidateTimeZone(tz);     // throws ArgumentException

        job.CronExpression = cron;
        job.TimeZoneId     = tz;
        job.IsEnabled      = dto.IsEnabled;
        job.UpdatedAt      = DateTime.UtcNow;
        job.UpdatedBy      = userId;

        await _db.SaveChangesAsync(ct);
        Register(job);

        _logger.LogInformation("DataSync: job '{JobKey}' updated by {User}: cron='{Cron}', tz='{Tz}', enabled={Enabled}.",
            job.JobKey, userId ?? "(unknown)", cron, tz ?? "UTC", job.IsEnabled);

        var running = await RunningJobIdsAsync(ct);
        return ToDto(job, running.Contains(job.Id));
    }

    public async Task<DataSyncJobDto> TriggerAsync(Guid id, string? userId, CancellationToken ct = default)
    {
        var job = await _db.DataSyncJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id, ct)
                  ?? throw new KeyNotFoundException("Data sync job not found.");

        if (!_handlers.ContainsKey(job.JobKey))
            throw new InvalidOperationException($"No handler is registered for job '{job.JobKey}'.");

        var running = await RunningJobIdsAsync(ct);
        if (running.Contains(job.Id))
            throw new InvalidOperationException("This job is already running. Wait for it to finish before starting another run.");

        var hangfireId = _scheduler.TriggerNow(job.JobKey, string.IsNullOrWhiteSpace(userId) ? "manual" : userId);
        _logger.LogInformation("DataSync: job '{JobKey}' manually triggered by {User} (hangfire id {HangfireId}).",
            job.JobKey, userId ?? "(unknown)", hangfireId);

        return ToDto(job, isRunning: true);
    }

    public async Task EnsureJobsRegisteredAsync(CancellationToken ct = default)
    {
        var existing = await _db.DataSyncJobs.ToListAsync(ct);
        var added = 0;

        foreach (var handler in _handlers.Values)
        {
            if (existing.Any(j => string.Equals(j.JobKey, handler.JobKey, StringComparison.OrdinalIgnoreCase)))
                continue;

            var job = new DataSyncJob
            {
                JobKey         = handler.JobKey,
                Name           = handler.DefaultName,
                Description    = handler.DefaultDescription,
                CronExpression = handler.DefaultCron,
                TimeZoneId     = null,
                IsEnabled      = true,
                CreatedAt      = DateTime.UtcNow,
            };
            _db.DataSyncJobs.Add(job);
            existing.Add(job);
            added++;
        }

        if (added > 0)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("DataSync: seeded {Count} new job definition(s).", added);
        }

        foreach (var job in existing)
            Register(job);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Pushes a job's DB state to the scheduler (add/update when enabled, remove otherwise).</summary>
    private void Register(DataSyncJob job)
    {
        try
        {
            if (job.IsEnabled && _handlers.ContainsKey(job.JobKey))
                _scheduler.AddOrUpdate(job.JobKey, job.CronExpression, job.TimeZoneId);
            else
                _scheduler.Remove(job.JobKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DataSync: failed to register job '{JobKey}' with the scheduler.", job.JobKey);
            throw;
        }
    }

    private async Task<HashSet<Guid>> RunningJobIdsAsync(CancellationToken ct)
    {
        var since = DateTime.UtcNow - RunningWindow;
        var ids = await _db.DataSyncJobRuns.AsNoTracking()
            .Where(r => r.Status == DataSyncRunStatus.Running && r.StartedAt >= since)
            .Select(r => r.JobId)
            .Distinct()
            .ToListAsync(ct);
        return ids.ToHashSet();
    }

    private DataSyncJobDto ToDto(DataSyncJob j, bool isRunning)
    {
        _handlers.TryGetValue(j.JobKey, out var handler);

        DateTime? next = null;
        if (j.IsEnabled && handler is not null)
        {
            try { next = _scheduler.GetNextExecutionUtc(j.JobKey); }
            catch (Exception ex) { _logger.LogWarning(ex, "DataSync: could not read next execution for '{JobKey}'.", j.JobKey); }
        }

        return new DataSyncJobDto
        {
            Id                  = j.Id,
            JobKey              = j.JobKey,
            Name                = j.Name,
            Description         = j.Description,
            CronExpression      = j.CronExpression,
            TimeZoneId          = j.TimeZoneId,
            IsEnabled           = j.IsEnabled,
            ScheduleDescription = CronDescriber.Describe(j.CronExpression),
            NextRunAt           = next,
            IsRunning           = isRunning,
            LastRunAt           = j.LastRunAt,
            LastRunStatus       = j.LastRunStatus?.ToString(),
            LastRunMessage      = j.LastRunMessage,
            LastRunRowCount     = j.LastRunRowCount,
            LastRunDurationMs   = j.LastRunDurationMs,
            SourceSummary       = handler?.SourceSummary,
            TargetSummary       = handler?.TargetSummary,
            HasHandler          = handler is not null,
            CreatedAt           = j.CreatedAt,
            UpdatedAt           = j.UpdatedAt,
            UpdatedBy           = j.UpdatedBy,
        };
    }
}
