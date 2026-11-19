using ActionTracker.Domain.Enums;

namespace ActionTracker.Domain.Entities;

/// <summary>History record for a single execution of a <see cref="DataSyncJob"/>.</summary>
public class DataSyncJobRun
{
    public Guid Id    { get; set; } = Guid.NewGuid();
    public Guid JobId { get; set; }
    public DataSyncJob Job { get; set; } = null!;

    public DateTime  StartedAt  { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }

    public DataSyncRunStatus Status  { get; set; } = DataSyncRunStatus.Running;
    public DataSyncTrigger   Trigger { get; set; } = DataSyncTrigger.Scheduled;

    /// <summary>User id when <see cref="Trigger"/> is Manual; null for scheduled runs.</summary>
    public string? TriggeredBy { get; set; }

    public int? RowsRead    { get; set; }
    public int? RowsWritten { get; set; }

    /// <summary>Success summary or the error message on failure.</summary>
    public string? Message { get; set; }
}
