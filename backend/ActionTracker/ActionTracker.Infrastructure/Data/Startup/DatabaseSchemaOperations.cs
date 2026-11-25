using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ActionTracker.Infrastructure.Data.Startup;

/// <summary>
/// The database operations <see cref="DatabaseStartup"/> needs, behind an
/// interface so its decisions can be unit-tested without a SQL Server.
/// </summary>
public interface IDatabaseSchemaOperations
{
    /// <summary>False for the EF InMemory provider used by the integration-test host.</summary>
    bool IsRelational { get; }

    Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken ct);

    Task MigrateAsync(CancellationToken ct);

    Task EnsureCreatedAsync(CancellationToken ct);

    /// <summary>
    /// The tables (<c>dbo.Table</c>) and columns (<c>dbo.Table.Column</c>) that
    /// the given model entity types map to and the database does not have.
    /// </summary>
    Task<IReadOnlyList<string>> FindMissingSchemaObjectsAsync(IReadOnlyCollection<Type> entityTypes, CancellationToken ct);

    /// <summary>True for "Invalid object name" (208) / "Invalid column name" (207).</summary>
    bool IsMissingSchemaError(Exception exception);
}

/// <summary><see cref="IDatabaseSchemaOperations"/> over <see cref="AppDbContext"/> (SQL Server).</summary>
public sealed class EfDatabaseSchemaOperations : IDatabaseSchemaOperations
{
    private const int InvalidColumnName = 207;
    private const int InvalidObjectName = 208;

    private readonly AppDbContext _db;

    public EfDatabaseSchemaOperations(AppDbContext db) => _db = db;

    public bool IsRelational => _db.Database.IsRelational();

    public async Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken ct) =>
        (await _db.Database.GetPendingMigrationsAsync(ct)).ToList();

    public Task MigrateAsync(CancellationToken ct) => _db.Database.MigrateAsync(ct);

    public Task EnsureCreatedAsync(CancellationToken ct) => _db.Database.EnsureCreatedAsync(ct);

    public async Task<IReadOnlyList<string>> FindMissingSchemaObjectsAsync(
        IReadOnlyCollection<Type> entityTypes, CancellationToken ct)
    {
        if (!IsRelational || entityTypes.Count == 0)
            return [];

        var rows = await _db.Database
            .SqlQueryRaw<SchemaColumnRow>(
                "SELECT TABLE_SCHEMA AS [Schema], TABLE_NAME AS [Table], COLUMN_NAME AS [Column] " +
                "FROM INFORMATION_SCHEMA.COLUMNS")
            .ToListAsync(ct);

        return FindMissing(_db.Model, entityTypes, rows.Select(r => (r.Schema, r.Table, r.Column)));
    }

    /// <summary>
    /// Pure part of <see cref="FindMissingSchemaObjectsAsync"/>: compares the
    /// tables and columns <paramref name="entityTypes"/> map to in
    /// <paramref name="model"/> with the columns the database reports.
    /// </summary>
    public static IReadOnlyList<string> FindMissing(
        IModel model,
        IEnumerable<Type> entityTypes,
        IEnumerable<(string Schema, string Table, string Column)> existingColumns)
    {
        var existing = existingColumns
            .GroupBy(r => (r.Schema.ToUpperInvariant(), r.Table.ToUpperInvariant()))
            .ToDictionary(
                g => g.Key,
                g => g.Select(r => r.Column).ToHashSet(StringComparer.OrdinalIgnoreCase));

        var missing = new List<string>();
        foreach (var clrType in entityTypes.Distinct())
        {
            var entityType = model.FindEntityType(clrType)
                ?? throw new ArgumentException($"{clrType.Name} is not an entity type of the model.", nameof(entityTypes));

            var table = entityType.GetTableName();
            if (table is null) continue;                       // keyless / view-mapped
            var modelSchema = entityType.GetSchema();          // null = the default schema
            var schema = modelSchema ?? "dbo";

            if (!existing.TryGetValue((schema.ToUpperInvariant(), table.ToUpperInvariant()), out var columns))
            {
                missing.Add($"{schema}.{table}");
                continue;
            }

            // The store object must carry the model's own schema (null here):
            // with "dbo" GetColumnName finds no mapping and returns null.
            var store = StoreObjectIdentifier.Table(table, modelSchema);
            foreach (var property in entityType.GetProperties())
            {
                var column = property.GetColumnName(store);
                if (column is not null && !columns.Contains(column))
                    missing.Add($"{schema}.{table}.{column}");
            }
        }

        return missing.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public bool IsMissingSchemaError(Exception exception)
    {
        for (var ex = exception; ex is not null; ex = ex.InnerException)
        {
            if (ex is SqlException sql && sql.Errors.Cast<SqlError>()
                    .Any(e => e.Number is InvalidColumnName or InvalidObjectName))
                return true;
        }
        return false;
    }

    internal sealed class SchemaColumnRow
    {
        public string Schema { get; set; } = string.Empty;
        public string Table { get; set; } = string.Empty;
        public string Column { get; set; } = string.Empty;
    }
}
