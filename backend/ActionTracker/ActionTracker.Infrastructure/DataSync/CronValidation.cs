namespace ActionTracker.Infrastructure.DataSync;

/// <summary>Shared cron / time-zone validation used by every <c>IDataSyncScheduler</c> implementation.</summary>
public static class CronValidation
{
    /// <summary>Throws <see cref="ArgumentException"/> when the expression is not a valid 5- or 6-field cron.</summary>
    public static void Validate(string cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
            throw new ArgumentException("Cron expression is required.");

        var fields = cronExpression.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length is not (5 or 6))
            throw new ArgumentException("Cron expression must have 5 fields (minute hour day month weekday) or 6 with seconds.");

        try
        {
            var format = fields.Length == 6 ? Cronos.CronFormat.IncludeSeconds : Cronos.CronFormat.Standard;
            Cronos.CronExpression.Parse(cronExpression.Trim(), format);
        }
        catch (Cronos.CronFormatException ex)
        {
            throw new ArgumentException($"Invalid cron expression: {ex.Message}", ex);
        }
    }

    /// <summary>Null / blank resolves to UTC. Throws <see cref="ArgumentException"/> for unknown ids.</summary>
    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId)) return TimeZoneInfo.Utc;
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new ArgumentException($"Unknown time zone '{timeZoneId}'.", ex);
        }
    }
}
