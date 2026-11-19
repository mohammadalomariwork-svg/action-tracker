namespace ActionTracker.Domain.Enums;

/// <summary>Outcome of a single data-sync job execution.</summary>
public enum DataSyncRunStatus
{
    Running   = 0,
    Succeeded = 1,
    Failed    = 2,
}
