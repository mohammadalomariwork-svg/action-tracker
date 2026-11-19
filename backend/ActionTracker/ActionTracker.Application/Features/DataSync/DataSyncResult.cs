namespace ActionTracker.Application.Features.DataSync;

/// <summary>Outcome returned by an <see cref="IDataSyncJobHandler"/> on success.</summary>
public sealed record DataSyncResult(int RowsRead, int RowsWritten, string Message);
