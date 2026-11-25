namespace ActionTracker.Application.Common.Interfaces;

/// <summary>
/// Whether the tag tables (WO-2026-11 A-5, migration <c>Add_ActionItemTags</c>)
/// exist in the database. While they do not — the API started with
/// <c>Database:MigrateOnStartup</c> off and the migration pending — action-item
/// reads skip tags, tag suggestions are empty, and a save carrying tags or a
/// list filtered by tag is refused with 503. Once the tables are found the
/// answer stays true; until then the database is asked again at most once a
/// minute, so applying the migration takes effect without a restart.
/// </summary>
public interface ITagSchemaState
{
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}
