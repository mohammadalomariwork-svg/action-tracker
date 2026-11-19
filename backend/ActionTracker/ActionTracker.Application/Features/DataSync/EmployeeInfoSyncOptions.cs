namespace ActionTracker.Application.Features.DataSync;

/// <summary>
/// Bound from the <c>DataSync:EmployeeInfo</c> configuration section.
/// The connection string carries credentials and must live only in the
/// git-ignored <c>appsettings.json</c> (or environment / secret store).
/// </summary>
public class EmployeeInfoSyncOptions
{
    public const string SectionName = "DataSync:EmployeeInfo";

    /// <summary>Full SQL Server connection string to the HR source database.</summary>
    public string SourceConnectionString { get; set; } = string.Empty;

    /// <summary>Schema-qualified source table. Default: dbo.ku_employee_info.</summary>
    public string SourceTable { get; set; } = "dbo.ku_employee_info";

    /// <summary>Schema-qualified target table in the application database.</summary>
    public string TargetTable { get; set; } = "dbo.ku_employee_info";

    /// <summary>
    /// Safety guard: abort when the source row count is below this fraction of
    /// the current target count (protects against an upstream ETL that emptied
    /// the table). 0 disables the check. Default 0.9.
    /// </summary>
    public double MinRowRatio { get; set; } = 0.9;

    /// <summary>Per-command timeout in seconds. Default 120.</summary>
    public int CommandTimeoutSeconds { get; set; } = 120;
}
