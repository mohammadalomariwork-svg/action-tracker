using System.Data;
using System.Diagnostics;
using ActionTracker.Application.Features.DataSync;
using ActionTracker.Application.Features.DataSync.DTOs;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActionTracker.Infrastructure.DataSync;

/// <summary>
/// Copies <c>ku_employee_info</c> from the HR source database (KU_BOT_PROD)
/// into the application database.
/// <para>
/// Strategy: full refresh. The source table has no primary key, so an
/// incremental merge is not reliable. The row count is small (~1.7k), so the
/// whole copy is read into memory, then the target is cleared and re-filled
/// with <see cref="SqlBulkCopy"/> inside a single transaction. Readers never
/// observe an empty table.
/// </para>
/// <para>
/// Only columns that exist on both sides (matched by name, case-insensitive)
/// are copied; computed / identity target columns are skipped. A row-count
/// guard aborts the run if the source shrank suspiciously.
/// </para>
/// </summary>
public sealed class EmployeeInfoSyncJobHandler : IDataSyncJobHandler
{
    public const string Key = "employee-info-sync";

    private readonly EmployeeInfoSyncOptions _options;
    private readonly string _targetConnectionString;
    private readonly ILogger<EmployeeInfoSyncJobHandler> _logger;

    public EmployeeInfoSyncJobHandler(
        IOptions<EmployeeInfoSyncOptions> options,
        IConfiguration configuration,
        ILogger<EmployeeInfoSyncJobHandler> logger)
    {
        _options = options.Value;
        _targetConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");
        _logger = logger;
    }

    // ── Metadata ─────────────────────────────────────────────────────────────

    public string JobKey             => Key;
    public string DefaultName        => "HR Employee Info Sync";
    public string DefaultDescription =>
        "Copies the ku_employee_info table from the HR source database (KU_BOT_PROD) into " +
        "Action Center. Full refresh inside a single transaction; used for supervisor lookups " +
        "and the user picker.";
    public string DefaultCron        => "0 * * * *"; // hourly

    public string SourceSummary => Describe(_options.SourceConnectionString, _options.SourceTable);
    public string TargetSummary => Describe(_targetConnectionString, _options.TargetTable);

    // ── Execute ──────────────────────────────────────────────────────────────

    public async Task<DataSyncResult> ExecuteAsync(CancellationToken ct)
    {
        EnsureConfigured();

        var sw          = Stopwatch.StartNew();
        var sourceTable = SqlIdentifier.SplitTableName(_options.SourceTable);
        var targetTable = SqlIdentifier.SplitTableName(_options.TargetTable);
        var timeout     = Math.Max(30, _options.CommandTimeoutSeconds);

        // 1. Discover writable target columns
        await using var target = new SqlConnection(_targetConnectionString);
        await target.OpenAsync(ct);
        var targetColumns = await GetColumnsAsync(target, targetTable, writableOnly: true, timeout, ct);
        if (targetColumns.Count == 0)
            throw new InvalidOperationException($"Target table {SqlIdentifier.Quote(targetTable)} was not found or has no writable columns.");

        // 2. Discover source columns and compute the intersection
        await using var source = new SqlConnection(_options.SourceConnectionString);
        await source.OpenAsync(ct);
        var sourceColumns = await GetColumnsAsync(source, sourceTable, writableOnly: false, timeout, ct);
        if (sourceColumns.Count == 0)
            throw new InvalidOperationException($"Source table {SqlIdentifier.Quote(sourceTable)} was not found or is not accessible.");

        var common = targetColumns
            .Where(t => sourceColumns.Contains(t, StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (common.Count == 0)
            throw new InvalidOperationException("Source and target tables share no column names; nothing to copy.");

        // 3. Read the whole source table into memory (small table, ~1.7k rows)
        var selectSql = $"SELECT {string.Join(", ", common.Select(SqlIdentifier.Quote))} FROM {SqlIdentifier.Quote(sourceTable)} WITH (NOLOCK)";
        var data = new DataTable();
        await using (var cmd = new SqlCommand(selectSql, source) { CommandTimeout = timeout })
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            data.Load(reader);
        }
        var rowsRead = data.Rows.Count;
        if (rowsRead == 0)
            throw new InvalidOperationException("Source table returned 0 rows; target left untouched.");

        // 4. Replace target contents atomically
        await using var tx = (SqlTransaction)await target.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            var existing = await ScalarIntAsync(target, tx, $"SELECT COUNT(*) FROM {SqlIdentifier.Quote(targetTable)}", timeout, ct);

            if (_options.MinRowRatio > 0 && existing > 0 && rowsRead < existing * _options.MinRowRatio)
            {
                throw new InvalidOperationException(
                    $"Safety guard: source has {rowsRead:N0} rows but target has {existing:N0} " +
                    $"(below {_options.MinRowRatio:P0} threshold). Target left untouched.");
            }

            await using (var del = new SqlCommand($"DELETE FROM {SqlIdentifier.Quote(targetTable)}", target, tx) { CommandTimeout = timeout })
            {
                await del.ExecuteNonQueryAsync(ct);
            }

            using (var bulk = new SqlBulkCopy(target, SqlBulkCopyOptions.KeepNulls, tx)
            {
                DestinationTableName = SqlIdentifier.Quote(targetTable),
                BulkCopyTimeout      = timeout,
                BatchSize            = 1000,
            })
            {
                foreach (var col in common)
                    bulk.ColumnMappings.Add(col, col);

                await bulk.WriteToServerAsync(data, ct);
            }

            var written = await ScalarIntAsync(target, tx, $"SELECT COUNT(*) FROM {SqlIdentifier.Quote(targetTable)}", timeout, ct);
            if (written != rowsRead)
                throw new InvalidOperationException($"Row count mismatch after copy: read {rowsRead:N0}, wrote {written:N0}. Rolled back.");

            await tx.CommitAsync(ct);
            sw.Stop();

            var message = $"Copied {written:N0} rows ({common.Count} columns) from {SourceSummary} in {sw.Elapsed.TotalSeconds:0.0}s" +
                          (existing != written ? $" (previously {existing:N0} rows)." : ".");
            _logger.LogInformation("EmployeeInfoSync: {Message}", message);
            return new DataSyncResult(rowsRead, written, message);
        }
        catch
        {
            try { await tx.RollbackAsync(CancellationToken.None); } catch { /* connection may already be gone */ }
            throw;
        }
    }

    // ── Probe ────────────────────────────────────────────────────────────────

    public async Task<DataSyncProbeResultDto> ProbeAsync(CancellationToken ct)
    {
        var sw     = Stopwatch.StartNew();
        var result = new DataSyncProbeResultDto();
        var timeout = 30;

        List<string> sourceCols = new();
        List<string> targetCols = new();

        try
        {
            EnsureConfigured();
            var sourceTable = SqlIdentifier.SplitTableName(_options.SourceTable);
            await using var source = new SqlConnection(_options.SourceConnectionString);
            await source.OpenAsync(ct);
            sourceCols = await GetColumnsAsync(source, sourceTable, writableOnly: false, timeout, ct);
            result.SourceRowCount  = await ScalarIntAsync(source, null, $"SELECT COUNT(*) FROM {SqlIdentifier.Quote(sourceTable)} WITH (NOLOCK)", timeout, ct);
            result.SourceReachable = true;
        }
        catch (Exception ex)
        {
            result.SourceError = ex.Message;
        }

        try
        {
            var targetTable = SqlIdentifier.SplitTableName(_options.TargetTable);
            await using var target = new SqlConnection(_targetConnectionString);
            await target.OpenAsync(ct);
            targetCols = await GetColumnsAsync(target, targetTable, writableOnly: true, timeout, ct);
            result.TargetRowCount  = await ScalarIntAsync(target, null, $"SELECT COUNT(*) FROM {SqlIdentifier.Quote(targetTable)}", timeout, ct);
            result.TargetReachable = true;
        }
        catch (Exception ex)
        {
            result.TargetError = ex.Message;
        }

        if (result.SourceReachable && result.TargetReachable)
        {
            var cmp = StringComparer.OrdinalIgnoreCase;
            result.SourceOnlyColumns = sourceCols.Where(s => !targetCols.Contains(s, cmp)).OrderBy(s => s).ToList();
            result.TargetOnlyColumns = targetCols.Where(t => !sourceCols.Contains(t, cmp)).OrderBy(s => s).ToList();
            result.CommonColumnCount = targetCols.Count(t => sourceCols.Contains(t, cmp));
        }

        sw.Stop();
        result.ElapsedMs = sw.ElapsedMilliseconds;
        return result;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void EnsureConfigured()
    {
        var cs = _options.SourceConnectionString;
        if (string.IsNullOrWhiteSpace(cs) || cs.Contains("<USERNAME>", StringComparison.OrdinalIgnoreCase) || cs.Contains("<PASSWORD>", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Source connection string is not configured. Set DataSync:EmployeeInfo:SourceConnectionString in appsettings.json.");
    }

    private static async Task<List<string>> GetColumnsAsync(
        SqlConnection conn, (string Schema, string Table) table, bool writableOnly, int timeout, CancellationToken ct)
    {
        const string sql = @"
SELECT c.name
FROM   sys.columns c
JOIN   sys.tables  t ON t.object_id = c.object_id
JOIN   sys.schemas s ON s.schema_id = t.schema_id
WHERE  s.name = @schema AND t.name = @table
  AND (@writableOnly = 0 OR (c.is_computed = 0 AND c.is_identity = 0 AND c.generated_always_type = 0))
ORDER BY c.column_id;";

        var list = new List<string>();
        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = timeout };
        cmd.Parameters.AddWithValue("@schema", table.Schema);
        cmd.Parameters.AddWithValue("@table", table.Table);
        cmd.Parameters.AddWithValue("@writableOnly", writableOnly ? 1 : 0);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(reader.GetString(0));
        return list;
    }

    private static async Task<int> ScalarIntAsync(SqlConnection conn, SqlTransaction? tx, string sql, int timeout, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(sql, conn, tx) { CommandTimeout = timeout };
        var value = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(value);
    }

    private static string Describe(string connectionString, string table)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return "Not configured";
        try
        {
            var b = new SqlConnectionStringBuilder(connectionString);
            return $"{b.DataSource} / {b.InitialCatalog} / {table}";
        }
        catch
        {
            return $"(invalid connection string) / {table}";
        }
    }
}
