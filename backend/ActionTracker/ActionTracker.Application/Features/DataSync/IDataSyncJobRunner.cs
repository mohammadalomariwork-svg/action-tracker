namespace ActionTracker.Application.Features.DataSync;

/// <summary>
/// Entry point invoked by the scheduler. Loads the job row, records a
/// <c>DataSyncJobRun</c>, resolves the matching <see cref="IDataSyncJobHandler"/>,
/// executes it and persists the outcome.
/// </summary>
public interface IDataSyncJobRunner
{
    Task RunAsync(string jobKey, string? triggeredByUserId, CancellationToken cancellationToken);
}
