using ActionTracker.Application.Features.DataSync;
using Hangfire;

namespace ActionTracker.Infrastructure.DataSync;

/// <summary>
/// The concrete type Hangfire serialises into its job table. Kept separate
/// from <see cref="IDataSyncJobRunner"/> so the Hangfire filter attributes
/// (which must sit on the invoked method) stay out of the Application layer.
/// </summary>
public sealed class DataSyncHangfireJob
{
    private readonly IDataSyncJobRunner _runner;

    public DataSyncHangfireJob(IDataSyncJobRunner runner) => _runner = runner;

    /// <summary>
    /// One data-sync job at a time across the whole server farm; a second
    /// invocation waits up to 10 minutes for the lock before failing.
    /// No automatic retries: every attempt is recorded as its own run row and
    /// the recurring schedule will try again at the next tick.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    [AutomaticRetry(Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    [JobDisplayName("Data sync: {0}")]
    public Task Run(string jobKey, string? triggeredByUserId, CancellationToken cancellationToken)
        => _runner.RunAsync(jobKey, triggeredByUserId, cancellationToken);
}
