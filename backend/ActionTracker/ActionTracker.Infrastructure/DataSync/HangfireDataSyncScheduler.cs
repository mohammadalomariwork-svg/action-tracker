using ActionTracker.Application.Features.DataSync;
using Hangfire;
using Hangfire.Storage;

namespace ActionTracker.Infrastructure.DataSync;

/// <summary>Hangfire-backed implementation of <see cref="IDataSyncScheduler"/>.</summary>
public sealed class HangfireDataSyncScheduler : IDataSyncScheduler
{
    private readonly IRecurringJobManager  _recurring;
    private readonly IBackgroundJobClient  _client;
    private readonly JobStorage            _storage;

    public HangfireDataSyncScheduler(
        IRecurringJobManager recurring,
        IBackgroundJobClient client,
        JobStorage storage)
    {
        _recurring = recurring;
        _client    = client;
        _storage   = storage;
    }

    public void AddOrUpdate(string jobKey, string cronExpression, string? timeZoneId)
    {
        CronValidation.Validate(cronExpression);
        var tz = CronValidation.ResolveTimeZone(timeZoneId);

        _recurring.AddOrUpdate<DataSyncHangfireJob>(
            recurringJobId: jobKey,
            methodCall:     j => j.Run(jobKey, null, CancellationToken.None),
            cronExpression: cronExpression,
            options:        new RecurringJobOptions { TimeZone = tz });
    }

    public void Remove(string jobKey) => _recurring.RemoveIfExists(jobKey);

    public string TriggerNow(string jobKey, string? triggeredByUserId)
        => _client.Enqueue<DataSyncHangfireJob>(j => j.Run(jobKey, triggeredByUserId, CancellationToken.None));

    public DateTime? GetNextExecutionUtc(string jobKey)
    {
        using var connection = _storage.GetConnection();
        var job = connection.GetRecurringJobs(new[] { jobKey }).FirstOrDefault();
        return job?.NextExecution;
    }

    public void ValidateCron(string cronExpression) => CronValidation.Validate(cronExpression);

    public void ValidateTimeZone(string? timeZoneId) => CronValidation.ResolveTimeZone(timeZoneId);
}
