using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Infrastructure.Data.Startup;

/// <summary>
/// Startup database step (WO-2026-11 A-3, fix 0). With
/// <c>Database:MigrateOnStartup</c> = true (the default when the key is
/// missing) pending migrations are applied, as before. With false nothing is
/// applied: each pending migration is named in a warning and the API still
/// starts. While migrations are pending, <see cref="RunSeederAsync"/> skips a
/// seeder whose tables or columns are missing instead of letting it crash the
/// start.
/// </summary>
public sealed class DatabaseStartup
{
    public const string MigrateOnStartupKey = "Database:MigrateOnStartup";

    private readonly IDatabaseSchemaOperations _schema;
    private readonly ILogger _logger;

    private DatabaseStartup(
        IDatabaseSchemaOperations schema, ILogger logger, bool migrated, IReadOnlyList<string> pending)
    {
        _schema            = schema;
        _logger            = logger;
        Migrated           = migrated;
        PendingMigrations  = pending;
    }

    /// <summary>True when this start applied migrations (setting on, relational database).</summary>
    public bool Migrated { get; }

    /// <summary>Migrations the database still lacks after this start (empty when the schema is current).</summary>
    public IReadOnlyList<string> PendingMigrations { get; }

    public bool IsSchemaCurrent => PendingMigrations.Count == 0;

    /// <summary>Reads <see cref="MigrateOnStartupKey"/>; missing or empty means true.</summary>
    public static bool ReadMigrateOnStartup(IConfiguration configuration) =>
        configuration.GetValue(MigrateOnStartupKey, true);

    public static async Task<DatabaseStartup> InitializeAsync(
        IDatabaseSchemaOperations schema, bool migrateOnStartup, ILogger logger, CancellationToken ct = default)
    {
        // The integration-test host swaps SQL Server for the EF InMemory
        // provider, which has no migrations — create its schema from the model.
        if (!schema.IsRelational)
        {
            await schema.EnsureCreatedAsync(ct);
            return new DatabaseStartup(schema, logger, migrated: false, pending: []);
        }

        var pending = await schema.GetPendingMigrationsAsync(ct);

        if (migrateOnStartup)
        {
            if (pending.Count > 0)
                logger.LogInformation(
                    "{Key} is true: applying {Count} pending migration(s): {Migrations}.",
                    MigrateOnStartupKey, pending.Count, string.Join(", ", pending));

            await schema.MigrateAsync(ct);
            return new DatabaseStartup(schema, logger, migrated: true, pending: []);
        }

        if (pending.Count == 0)
        {
            logger.LogInformation(
                "{Key} is false: no migration applied; the database schema is up to date (no pending migrations).",
                MigrateOnStartupKey);
        }
        else
        {
            logger.LogWarning(
                "{Key} is false: {Count} pending migration(s) were NOT applied: {Migrations}. " +
                "The API is running on the older schema, so features that need these migrations fail until " +
                "they are applied (dotnet ef database update, or one start with {Key} = true). " +
                "Startup seeders whose tables or columns are missing are skipped.",
                MigrateOnStartupKey, pending.Count, string.Join(", ", pending), MigrateOnStartupKey);
        }

        return new DatabaseStartup(schema, logger, migrated: false, pending: pending);
    }

    /// <summary>
    /// Runs a startup seeder. When the schema is current it simply runs (an
    /// error propagates, as before). While migrations are pending the seeder is
    /// skipped with a warning when a table or column of <paramref name="requires"/>
    /// is missing, or when it fails on a missing table / column anyway.
    /// Returns false when skipped.
    /// </summary>
    public async Task<bool> RunSeederAsync(
        string seederName, IReadOnlyCollection<Type> requires, Func<Task> seed, CancellationToken ct = default)
    {
        if (IsSchemaCurrent)
        {
            await seed();
            return true;
        }

        var missing = await _schema.FindMissingSchemaObjectsAsync(requires, ct);
        if (missing.Count > 0)
        {
            _logger.LogWarning(
                "{Seeder} skipped: the database is missing {Missing} ({Count} pending migration(s), {Key} is false). " +
                "It runs at the next start after the migrations are applied.",
                seederName, string.Join(", ", missing), PendingMigrations.Count, MigrateOnStartupKey);
            return false;
        }

        try
        {
            await seed();
            return true;
        }
        catch (Exception ex) when (_schema.IsMissingSchemaError(ex))
        {
            _logger.LogWarning(ex,
                "{Seeder} skipped: it needs a table or column that a pending migration adds ({Count} pending migration(s), {Key} is false). " +
                "It runs at the next start after the migrations are applied.",
                seederName, PendingMigrations.Count, MigrateOnStartupKey);
            return false;
        }
    }
}
