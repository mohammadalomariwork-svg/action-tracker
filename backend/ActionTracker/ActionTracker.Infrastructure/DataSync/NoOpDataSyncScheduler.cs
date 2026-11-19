using ActionTracker.Application.Features.DataSync;

namespace ActionTracker.Infrastructure.DataSync;

/// <summary>
/// Scheduler used in the <c>Testing</c> environment so the integration test
/// host never opens a Hangfire connection to a real SQL Server. Validation
/// still behaves like the real scheduler; scheduling calls are recorded but
/// nothing executes.
/// </summary>
public sealed class NoOpDataSyncScheduler : IDataSyncScheduler
{
    private readonly Dictionary<string, (string Cron, string? Tz)> _registered = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, (string Cron, string? Tz)> Registered => _registered;

    public void AddOrUpdate(string jobKey, string cronExpression, string? timeZoneId)
    {
        ValidateCron(cronExpression);
        ValidateTimeZone(timeZoneId);
        _registered[jobKey] = (cronExpression, timeZoneId);
    }

    public void Remove(string jobKey) => _registered.Remove(jobKey);

    public string TriggerNow(string jobKey, string? triggeredByUserId) => Guid.NewGuid().ToString("N");

    public DateTime? GetNextExecutionUtc(string jobKey) => null;

    public void ValidateCron(string cronExpression) => CronValidation.Validate(cronExpression);

    public void ValidateTimeZone(string? timeZoneId) => CronValidation.ResolveTimeZone(timeZoneId);
}
