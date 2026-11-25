using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Domain.Entities;
using ActionTracker.Infrastructure.Data;
using ActionTracker.Infrastructure.Data.Startup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Infrastructure.Services;

/// <summary>
/// <see cref="ITagSchemaState"/> (WO-2026-11 A-5): asks INFORMATION_SCHEMA
/// whether the tables and columns <see cref="Tag"/> and <see cref="ActionItemTag"/>
/// map to exist. Singleton. Once they are found the answer is cached for the
/// life of the process; while they are missing the database is asked again at
/// most once per <see cref="RecheckInterval"/>, so applying the migration takes
/// effect without restarting the API. The EF InMemory provider (integration
/// test host) always has them.
/// </summary>
public sealed class TagSchemaState : ITagSchemaState
{
    public static readonly TimeSpan RecheckInterval = TimeSpan.FromMinutes(1);

    private static readonly Type[] TagEntities = [typeof(Tag), typeof(ActionItemTag)];

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<TagSchemaState> _logger;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private volatile bool _available;
    private DateTimeOffset _lastCheck = DateTimeOffset.MinValue;
    private bool _warned;

    public TagSchemaState(IServiceScopeFactory scopes, ILogger<TagSchemaState> logger, TimeProvider? clock = null)
    {
        _scopes = scopes;
        _logger = logger;
        _clock  = clock ?? TimeProvider.System;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (_available) return true;
        if (_clock.GetUtcNow() - _lastCheck < RecheckInterval) return false;

        await _gate.WaitAsync(ct);
        try
        {
            if (_available) return true;
            if (_clock.GetUtcNow() - _lastCheck < RecheckInterval) return false;

            var missing = await FindMissingAsync(ct);
            _lastCheck = _clock.GetUtcNow();
            _available = missing.Count == 0;

            if (_available)
            {
                if (_warned)
                    _logger.LogInformation("The tag tables are now in the database: tags are available.");
            }
            else if (!_warned)
            {
                _warned = true;
                _logger.LogWarning(
                    "Tags are switched off until the migration Add_ActionItemTags is applied (missing: {Missing}). " +
                    "Action items are read and saved without tags; saves with tags and tag filters get 503.",
                    string.Join(", ", missing));
            }
            return _available;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _lastCheck = _clock.GetUtcNow();
            _logger.LogWarning(ex, "Could not check whether the tag tables exist; tags stay off for now.");
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<string>> FindMissingAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!db.Database.IsRelational())
            return [];

        var rows = await db.Database
            .SqlQueryRaw<EfDatabaseSchemaOperations.SchemaColumnRow>(
                "SELECT TABLE_SCHEMA AS [Schema], TABLE_NAME AS [Table], COLUMN_NAME AS [Column] " +
                "FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME IN ('Tags', 'ActionItemTags')")
            .ToListAsync(ct);

        return EfDatabaseSchemaOperations.FindMissing(
            db.Model, TagEntities, rows.Select(r => (r.Schema, r.Table, r.Column)));
    }
}
