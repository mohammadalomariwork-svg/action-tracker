using ActionTracker.Application.Features.DataSync.DTOs;

namespace ActionTracker.Application.Features.DataSync;

/// <summary>Admin-facing operations for the Data Sync page.</summary>
public interface IDataSyncService
{
    Task<IReadOnlyList<DataSyncJobDto>> GetJobsAsync(CancellationToken ct = default);
    Task<DataSyncJobDto?> GetJobAsync(Guid id, CancellationToken ct = default);

    /// <summary>Updates schedule / enabled flag and re-registers the job with the scheduler.</summary>
    Task<DataSyncJobDto> UpdateJobAsync(Guid id, UpdateDataSyncJobDto dto, string? userId, CancellationToken ct = default);

    /// <summary>Enqueues an immediate run. Throws <see cref="InvalidOperationException"/> if one is already running.</summary>
    Task<DataSyncJobDto> TriggerAsync(Guid id, string? userId, CancellationToken ct = default);

    Task<IReadOnlyList<DataSyncJobRunDto>> GetRunsAsync(Guid id, int take = 25, CancellationToken ct = default);

    Task<DataSyncProbeResultDto> ProbeAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Ensures a DB row exists for every registered handler, then pushes every
    /// row's schedule to the scheduler. Called once at startup.
    /// </summary>
    Task EnsureJobsRegisteredAsync(CancellationToken ct = default);
}
