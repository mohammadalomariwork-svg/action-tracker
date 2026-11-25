using ActionTracker.Domain.Entities;
using ActionTracker.Infrastructure.Data;
using ActionTracker.Infrastructure.Data.Startup;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Tests.Unit;

/// <summary>
/// WO-2026-11 A-3, fix 0: the <c>Database:MigrateOnStartup</c> switch.
/// On (default) → pending migrations are applied at startup, as before.
/// Off → nothing is applied, every pending migration is named in a warning,
/// startup carries on, and a seeder that needs a missing table / column is
/// skipped with a warning instead of crashing the start.
/// </summary>
public class DatabaseStartupTests
{
    private const string Pending1 = "20261125000000_Add_Something";
    private const string Pending2 = "20261126000000_Add_SomethingElse";

    // ── Reading the setting ──────────────────────────────────────────────────

    [Theory]
    [InlineData(null, true)]       // key missing → default on
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    public void Setting_defaults_to_true_and_reads_an_explicit_value(string? value, bool expected)
    {
        var values = new Dictionary<string, string?>();
        if (value is not null) values[DatabaseStartup.MigrateOnStartupKey] = value;
        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        DatabaseStartup.ReadMigrateOnStartup(config).Should().Be(expected);
    }

    // ── Setting on ───────────────────────────────────────────────────────────

    [Fact]
    public async Task On_applies_pending_migrations_and_runs_seeders_unchecked()
    {
        var schema = new FakeSchema { Pending = [Pending1, Pending2] };
        var log = new ListLogger();

        var startup = await DatabaseStartup.InitializeAsync(schema, migrateOnStartup: true, log);

        schema.MigrateCalls.Should().Be(1);
        startup.Migrated.Should().BeTrue();
        startup.IsSchemaCurrent.Should().BeTrue();
        log.Entries.Should().NotContain(e => e.Level == LogLevel.Warning);
        log.Entries.Should().Contain(e => e.Level == LogLevel.Information
            && e.Message.Contains(Pending1) && e.Message.Contains(Pending2));

        var ran = false;
        (await startup.RunSeederAsync("S", [typeof(WorkCalendar)], () => { ran = true; return Task.CompletedTask; }))
            .Should().BeTrue();
        ran.Should().BeTrue();
        schema.MissingChecks.Should().Be(0);   // schema is current: no check needed
    }

    [Fact]
    public async Task On_a_missing_schema_error_from_a_seeder_still_fails_the_start()
    {
        var schema = new FakeSchema();
        var startup = await DatabaseStartup.InitializeAsync(schema, migrateOnStartup: true, new ListLogger());

        var act = () => startup.RunSeederAsync("S", [typeof(WorkCalendar)],
            () => throw new FakeMissingSchemaException());

        await act.Should().ThrowAsync<FakeMissingSchemaException>();
    }

    // ── Setting off ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Off_applies_nothing_and_names_every_pending_migration_in_a_warning()
    {
        var schema = new FakeSchema { Pending = [Pending1, Pending2] };
        var log = new ListLogger();

        var startup = await DatabaseStartup.InitializeAsync(schema, migrateOnStartup: false, log);

        schema.MigrateCalls.Should().Be(0);
        schema.EnsureCreatedCalls.Should().Be(0);
        startup.Migrated.Should().BeFalse();
        startup.IsSchemaCurrent.Should().BeFalse();
        startup.PendingMigrations.Should().Equal(Pending1, Pending2);

        var warning = log.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning).Subject;
        warning.Message.Should().Contain(Pending1).And.Contain(Pending2)
            .And.Contain("NOT applied").And.Contain(DatabaseStartup.MigrateOnStartupKey);
    }

    [Fact]
    public async Task Off_with_nothing_pending_logs_no_warning_and_runs_seeders_unchecked()
    {
        var schema = new FakeSchema();
        var log = new ListLogger();

        var startup = await DatabaseStartup.InitializeAsync(schema, migrateOnStartup: false, log);

        schema.MigrateCalls.Should().Be(0);
        startup.IsSchemaCurrent.Should().BeTrue();
        log.Entries.Should().NotContain(e => e.Level == LogLevel.Warning);
        log.Entries.Should().Contain(e => e.Message.Contains("up to date"));

        var ran = false;
        await startup.RunSeederAsync("S", [typeof(WorkCalendar)], () => { ran = true; return Task.CompletedTask; });
        ran.Should().BeTrue();
        schema.MissingChecks.Should().Be(0);
    }

    [Fact]
    public async Task Off_with_pending_skips_a_seeder_whose_table_is_missing()
    {
        var schema = new FakeSchema
        {
            Pending = [Pending1],
            Missing = { [typeof(WorkCalendar)] = ["dbo.WorkCalendars"] },
        };
        var log = new ListLogger();
        var startup = await DatabaseStartup.InitializeAsync(schema, migrateOnStartup: false, log);

        var ran = false;
        var result = await startup.RunSeederAsync("WorkCalendarSeeder", [typeof(WorkCalendar), typeof(WorkCalendarDay)],
            () => { ran = true; return Task.CompletedTask; });

        result.Should().BeFalse();
        ran.Should().BeFalse();
        log.Entries.Should().Contain(e => e.Level == LogLevel.Warning
            && e.Message.Contains("WorkCalendarSeeder skipped") && e.Message.Contains("dbo.WorkCalendars"));
    }

    [Fact]
    public async Task Off_with_pending_runs_a_seeder_whose_tables_and_columns_exist()
    {
        var schema = new FakeSchema
        {
            Pending = [Pending1],
            Missing = { [typeof(WorkCalendar)] = ["dbo.WorkCalendars"] },
        };
        var startup = await DatabaseStartup.InitializeAsync(schema, migrateOnStartup: false, new ListLogger());

        var ran = false;
        var result = await startup.RunSeederAsync("EmailTemplateSeeder", [typeof(EmailTemplate)],
            () => { ran = true; return Task.CompletedTask; });

        result.Should().BeTrue();
        ran.Should().BeTrue();
        schema.MissingChecks.Should().Be(1);
    }

    [Fact]
    public async Task Off_with_pending_skips_a_seeder_that_fails_on_a_missing_table_or_column()
    {
        var schema = new FakeSchema { Pending = [Pending1] };
        var log = new ListLogger();
        var startup = await DatabaseStartup.InitializeAsync(schema, migrateOnStartup: false, log);

        var result = await startup.RunSeederAsync("S", [typeof(EmailTemplate)],
            () => throw new InvalidOperationException("wrapper", new FakeMissingSchemaException()));

        result.Should().BeFalse();
        log.Entries.Should().Contain(e => e.Level == LogLevel.Warning && e.Message.Contains("S skipped"));
    }

    [Fact]
    public async Task Off_with_pending_any_other_seeder_error_still_fails_the_start()
    {
        var schema = new FakeSchema { Pending = [Pending1] };
        var startup = await DatabaseStartup.InitializeAsync(schema, migrateOnStartup: false, new ListLogger());

        var act = () => startup.RunSeederAsync("S", [typeof(EmailTemplate)],
            () => throw new InvalidOperationException("a real bug"));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("a real bug");
    }

    // ── Non-relational (integration-test host) ───────────────────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InMemory_provider_creates_the_schema_whatever_the_setting(bool migrateOnStartup)
    {
        var schema = new FakeSchema { IsRelational = false, Pending = [Pending1] };

        var startup = await DatabaseStartup.InitializeAsync(schema, migrateOnStartup, new ListLogger());

        schema.EnsureCreatedCalls.Should().Be(1);
        schema.MigrateCalls.Should().Be(0);
        startup.IsSchemaCurrent.Should().BeTrue();
    }

    [Fact]
    public async Task Ef_operations_on_the_InMemory_provider_report_nothing_missing()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var schema = new EfDatabaseSchemaOperations(db);

        schema.IsRelational.Should().BeFalse();
        (await schema.FindMissingSchemaObjectsAsync([typeof(WorkCalendar)], CancellationToken.None))
            .Should().BeEmpty();
        schema.IsMissingSchemaError(new InvalidOperationException()).Should().BeFalse();
    }

    // ── Comparing the model with the database's columns ──────────────────────
    // Uses the SQL Server model; building it never opens a connection.

    private static Microsoft.EntityFrameworkCore.Metadata.IModel SqlServerModel()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=(unused);Database=unused").Options);
        return db.Model;
    }

    private static IEnumerable<(string, string, string)> ColumnsOf(
        Microsoft.EntityFrameworkCore.Metadata.IModel model, Type type, params string[] except)
    {
        var entity = model.FindEntityType(type)!;
        var table = entity.GetTableName()!;
        var store = Microsoft.EntityFrameworkCore.Metadata.StoreObjectIdentifier.Table(table, entity.GetSchema());
        return entity.GetProperties()
            .Select(p => p.GetColumnName(store)!)
            .Where(c => !except.Contains(c))
            .Select(c => ("dbo", table, c));
    }

    [Fact]
    public void FindMissing_reports_missing_columns_and_tables_and_nothing_for_a_complete_table()
    {
        var model = SqlServerModel();
        var existing = ColumnsOf(model, typeof(ActionItem), except: "ActualEndDate")
            .Concat(ColumnsOf(model, typeof(EmailTemplate)))
            .ToList();

        EfDatabaseSchemaOperations.FindMissing(model, [typeof(ActionItem)], existing)
            .Should().Equal("dbo.ActionItems.ActualEndDate");
        EfDatabaseSchemaOperations.FindMissing(model, [typeof(WorkCalendar), typeof(EmailTemplate)], existing)
            .Should().Equal("dbo.WorkCalendars");
        EfDatabaseSchemaOperations.FindMissing(model, [typeof(EmailTemplate)], existing)
            .Should().BeEmpty();
    }

    [Fact]
    public void FindMissing_compares_schema_table_and_column_names_case_insensitively()
    {
        var model = SqlServerModel();
        var existing = ColumnsOf(model, typeof(EmailTemplate))
            .Select(c => (c.Item1.ToUpperInvariant(), c.Item2.ToLowerInvariant(), c.Item3.ToUpperInvariant()))
            .ToList();

        EfDatabaseSchemaOperations.FindMissing(model, [typeof(EmailTemplate)], existing).Should().BeEmpty();
    }

    // ── Fakes ────────────────────────────────────────────────────────────────

    private sealed class FakeMissingSchemaException : Exception;

    private sealed class FakeSchema : IDatabaseSchemaOperations
    {
        public bool IsRelational { get; init; } = true;
        public IReadOnlyList<string> Pending { get; init; } = [];
        public Dictionary<Type, string[]> Missing { get; } = new();
        public int MigrateCalls { get; private set; }
        public int EnsureCreatedCalls { get; private set; }
        public int MissingChecks { get; private set; }

        public Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken ct) => Task.FromResult(Pending);

        public Task MigrateAsync(CancellationToken ct)
        {
            MigrateCalls++;
            return Task.CompletedTask;
        }

        public Task EnsureCreatedAsync(CancellationToken ct)
        {
            EnsureCreatedCalls++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> FindMissingSchemaObjectsAsync(IReadOnlyCollection<Type> entityTypes, CancellationToken ct)
        {
            MissingChecks++;
            IReadOnlyList<string> missing = entityTypes
                .SelectMany(t => Missing.TryGetValue(t, out var m) ? m : [])
                .ToList();
            return Task.FromResult(missing);
        }

        public bool IsMissingSchemaError(Exception exception)
        {
            for (var ex = exception; ex is not null; ex = ex.InnerException)
                if (ex is FakeMissingSchemaException) return true;
            return false;
        }
    }

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed class ListLogger : ILogger
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
    }
}
