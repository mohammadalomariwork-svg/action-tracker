using ActionTracker.Domain.Enums;

namespace ActionTracker.Domain.Entities;

/// <summary>
/// Admin-configurable definition of a recurring data-sync job.
/// One row per job; the <see cref="JobKey"/> maps to a registered
/// <c>IDataSyncJobHandler</c> in Infrastructure and doubles as the
/// Hangfire recurring-job id.
/// </summary>
public class DataSyncJob
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Stable machine key, e.g. "employee-info-sync".</summary>
    public string JobKey { get; set; } = string.Empty;

    public string Name        { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>Standard 5-field cron expression (minute hour dom month dow).</summary>
    public string CronExpression { get; set; } = "0 * * * *";

    /// <summary>Windows / IANA time-zone id used to evaluate the cron. Null = UTC.</summary>
    public string? TimeZoneId { get; set; }

    public bool IsEnabled { get; set; } = true;

    // ── Last-run summary (denormalised for fast list rendering) ──────────────
    public DateTime?          LastRunAt         { get; set; }
    public DataSyncRunStatus? LastRunStatus     { get; set; }
    public string?            LastRunMessage    { get; set; }
    public int?               LastRunRowCount   { get; set; }
    public long?              LastRunDurationMs { get; set; }

    public DateTime  CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string?   UpdatedBy { get; set; }

    public ICollection<DataSyncJobRun> Runs { get; set; } = new List<DataSyncJobRun>();
}
