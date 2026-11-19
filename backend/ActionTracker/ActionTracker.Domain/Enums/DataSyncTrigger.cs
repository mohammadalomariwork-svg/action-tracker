namespace ActionTracker.Domain.Enums;

/// <summary>How a data-sync run was started.</summary>
public enum DataSyncTrigger
{
    Scheduled = 0,
    Manual    = 1,
}
