namespace ActionTracker.Application.Features.DataSync.DTOs;

/// <summary>Result of a read-only connectivity / schema probe against a job's source and target.</summary>
public class DataSyncProbeResultDto
{
    public bool    SourceReachable { get; set; }
    public int?    SourceRowCount  { get; set; }
    public string? SourceError     { get; set; }

    public bool    TargetReachable { get; set; }
    public int?    TargetRowCount  { get; set; }
    public string? TargetError     { get; set; }

    /// <summary>Columns present in the source but missing in the target (ignored during copy).</summary>
    public List<string> SourceOnlyColumns { get; set; } = new();

    /// <summary>Columns present in the target but missing in the source (left NULL / default).</summary>
    public List<string> TargetOnlyColumns { get; set; } = new();

    /// <summary>Number of columns that will actually be copied.</summary>
    public int CommonColumnCount { get; set; }

    public long ElapsedMs { get; set; }
}
