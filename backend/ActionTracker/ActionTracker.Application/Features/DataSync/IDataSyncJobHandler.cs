using ActionTracker.Application.Features.DataSync.DTOs;

namespace ActionTracker.Application.Features.DataSync;

/// <summary>
/// One implementation per sync job. Registered in DI and resolved by
/// <see cref="JobKey"/> when the scheduler fires or an admin triggers a run.
/// Implementations must be idempotent and safe to re-run.
/// </summary>
public interface IDataSyncJobHandler
{
    /// <summary>Stable key matching <c>DataSyncJob.JobKey</c>.</summary>
    string JobKey { get; }

    /// <summary>Display name used when the DB row is first seeded.</summary>
    string DefaultName { get; }

    /// <summary>Description used when the DB row is first seeded.</summary>
    string DefaultDescription { get; }

    /// <summary>Cron used when the DB row is first seeded.</summary>
    string DefaultCron { get; }

    /// <summary>Non-secret description of the source (server / db / table) for the admin UI.</summary>
    string SourceSummary { get; }

    /// <summary>Non-secret description of the target for the admin UI.</summary>
    string TargetSummary { get; }

    /// <summary>Performs the sync. Throw to mark the run as failed.</summary>
    Task<DataSyncResult> ExecuteAsync(CancellationToken cancellationToken);

    /// <summary>Read-only connectivity / schema probe used by the "Test connection" button.</summary>
    Task<DataSyncProbeResultDto> ProbeAsync(CancellationToken cancellationToken);
}
