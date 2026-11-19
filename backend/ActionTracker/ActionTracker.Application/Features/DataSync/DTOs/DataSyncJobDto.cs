namespace ActionTracker.Application.Features.DataSync.DTOs;

public class DataSyncJobDto
{
    public Guid    Id             { get; set; }
    public string  JobKey         { get; set; } = string.Empty;
    public string  Name           { get; set; } = string.Empty;
    public string  Description    { get; set; } = string.Empty;
    public string  CronExpression { get; set; } = string.Empty;
    public string? TimeZoneId     { get; set; }
    public bool    IsEnabled      { get; set; }

    /// <summary>Human-readable description of the cron (e.g. "Every hour").</summary>
    public string? ScheduleDescription { get; set; }

    /// <summary>Next scheduled execution (UTC) as reported by the scheduler; null when disabled.</summary>
    public DateTime? NextRunAt { get; set; }

    /// <summary>True while a run is currently in progress.</summary>
    public bool IsRunning { get; set; }

    public DateTime? LastRunAt         { get; set; }
    public string?   LastRunStatus     { get; set; }
    public string?   LastRunMessage    { get; set; }
    public int?      LastRunRowCount   { get; set; }
    public long?     LastRunDurationMs { get; set; }

    /// <summary>Non-secret summary of where the data comes from (server / database / table).</summary>
    public string? SourceSummary { get; set; }
    public string? TargetSummary { get; set; }

    /// <summary>False when the handler for this JobKey is not registered in the running build.</summary>
    public bool HasHandler { get; set; }

    public DateTime  CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string?   UpdatedBy { get; set; }
}
