namespace ActionTracker.Application.Features.DataSync.DTOs;

/// <summary>Admin-editable fields of a data-sync job.</summary>
public class UpdateDataSyncJobDto
{
    public string  CronExpression { get; set; } = string.Empty;
    public string? TimeZoneId     { get; set; }
    public bool    IsEnabled      { get; set; }
}
