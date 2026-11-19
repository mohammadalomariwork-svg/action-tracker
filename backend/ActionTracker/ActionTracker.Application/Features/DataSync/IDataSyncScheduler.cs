namespace ActionTracker.Application.Features.DataSync;

/// <summary>
/// Thin abstraction over the background scheduler (Hangfire in Infrastructure)
/// so the Application layer never references the scheduling library.
/// </summary>
public interface IDataSyncScheduler
{
    /// <summary>Creates or replaces the recurring schedule for a job.</summary>
    void AddOrUpdate(string jobKey, string cronExpression, string? timeZoneId);

    /// <summary>Removes the recurring schedule (no-op if absent).</summary>
    void Remove(string jobKey);

    /// <summary>Enqueues an immediate one-off execution. Returns the scheduler's job id.</summary>
    string TriggerNow(string jobKey, string? triggeredByUserId);

    /// <summary>Next scheduled execution in UTC, or null when the job is not scheduled.</summary>
    DateTime? GetNextExecutionUtc(string jobKey);

    /// <summary>Throws <see cref="ArgumentException"/> when the cron expression is not valid.</summary>
    void ValidateCron(string cronExpression);

    /// <summary>Resolves a time-zone id; throws <see cref="ArgumentException"/> when unknown.</summary>
    void ValidateTimeZone(string? timeZoneId);
}
