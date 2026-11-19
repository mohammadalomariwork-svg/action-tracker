namespace ActionTracker.Application.Features.DataSync.DTOs;

public class DataSyncJobRunDto
{
    public Guid      Id          { get; set; }
    public Guid      JobId       { get; set; }
    public DateTime  StartedAt   { get; set; }
    public DateTime? FinishedAt  { get; set; }
    public long?     DurationMs  { get; set; }
    public string    Status      { get; set; } = string.Empty;
    public string    Trigger     { get; set; } = string.Empty;
    public string?   TriggeredBy { get; set; }
    public int?      RowsRead    { get; set; }
    public int?      RowsWritten { get; set; }
    public string?   Message     { get; set; }
}
