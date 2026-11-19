using System.Diagnostics;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.DataSync;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Infrastructure.DataSync;

/// <summary>
/// Executes one data-sync job and records the outcome. Invoked by Hangfire
/// through <see cref="DataSyncHangfireJob"/>; never called from controllers.
/// </summary>
public sealed class DataSyncJobRunner : IDataSyncJobRunner
{
    /// <summary>A "Running" row older than this is considered orphaned (app crashed mid-run).</summary>
    private static readonly TimeSpan StaleRunThreshold = TimeSpan.FromMinutes(30);

    private readonly IAppDbContext _db;
    private readonly IEnumerable<IDataSyncJobHandler> _handlers;
    private readonly ILogger<DataSyncJobRunner> _logger;

    public DataSyncJobRunner(
        IAppDbContext db,
        IEnumerable<IDataSyncJobHandler> handlers,
        ILogger<DataSyncJobRunner> logger)
    {
        _db       = db;
        _handlers = handlers;
        _logger   = logger;
    }

    public async Task RunAsync(string jobKey, string? triggeredByUserId, CancellationToken ct)
    {
        var job = await _db.DataSyncJobs.FirstOrDefaultAsync(j => j.JobKey == jobKey, ct);
        if (job is null)
        {
            _logger.LogWarning("DataSync: job '{JobKey}' has no configuration row; skipping.", jobKey);
            return;
        }

        var isManual = !string.IsNullOrEmpty(triggeredByUserId);
        if (!job.IsEnabled && !isManual)
        {
            _logger.LogInformation("DataSync: job '{JobKey}' is disabled; scheduled run skipped.", jobKey);
            return;
        }

        var now = DateTime.UtcNow;

        // Mark orphaned runs so the UI does not show them as running forever.
        var stale = await _db.DataSyncJobRuns
            .Where(r => r.JobId == job.Id && r.Status == DataSyncRunStatus.Running && r.StartedAt < now - StaleRunThreshold)
            .ToListAsync(ct);
        foreach (var s in stale)
        {
            s.Status     = DataSyncRunStatus.Failed;
            s.FinishedAt = now;
            s.Message    = "Interrupted: the application stopped before this run finished.";
        }

        var alreadyRunning = await _db.DataSyncJobRuns
            .AnyAsync(r => r.JobId == job.Id && r.Status == DataSyncRunStatus.Running && r.StartedAt >= now - StaleRunThreshold, ct);
        if (alreadyRunning)
        {
            _logger.LogWarning("DataSync: job '{JobKey}' is already running; this invocation was skipped.", jobKey);
            if (stale.Count > 0) await _db.SaveChangesAsync(ct);
            return;
        }

        var run = new DataSyncJobRun
        {
            JobId       = job.Id,
            StartedAt   = now,
            Status      = DataSyncRunStatus.Running,
            Trigger     = isManual ? DataSyncTrigger.Manual : DataSyncTrigger.Scheduled,
            TriggeredBy = triggeredByUserId,
        };
        _db.DataSyncJobRuns.Add(run);
        await _db.SaveChangesAsync(ct);

        var handler = _handlers.FirstOrDefault(h => string.Equals(h.JobKey, jobKey, StringComparison.OrdinalIgnoreCase));
        var sw = Stopwatch.StartNew();

        try
        {
            if (handler is null)
                throw new InvalidOperationException($"No handler is registered for job key '{jobKey}'.");

            _logger.LogInformation("DataSync: starting '{JobKey}' ({Trigger}).", jobKey, run.Trigger);
            var result = await handler.ExecuteAsync(ct);
            sw.Stop();

            run.Status      = DataSyncRunStatus.Succeeded;
            run.RowsRead    = result.RowsRead;
            run.RowsWritten = result.RowsWritten;
            run.Message     = Truncate(result.Message, 4000);
        }
        catch (Exception ex)
        {
            sw.Stop();
            run.Status  = DataSyncRunStatus.Failed;
            run.Message = Truncate(ex.Message, 4000);
            _logger.LogError(ex, "DataSync: job '{JobKey}' failed after {Elapsed}ms.", jobKey, sw.ElapsedMilliseconds);
        }
        finally
        {
            run.FinishedAt = DateTime.UtcNow;

            job.LastRunAt         = run.StartedAt;
            job.LastRunStatus     = run.Status;
            job.LastRunMessage    = Truncate(run.Message, 2000);
            job.LastRunRowCount   = run.RowsWritten;
            job.LastRunDurationMs = sw.ElapsedMilliseconds;

            // Use CancellationToken.None so a cancelled job still records its outcome.
            await _db.SaveChangesAsync(CancellationToken.None);
        }
    }

    private static string? Truncate(string? value, int max)
        => value is null || value.Length <= max ? value : value[..max];
}
