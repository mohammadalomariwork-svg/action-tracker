using ActionTracker.Infrastructure.Data;
using ActionTracker.Infrastructure.Data.Seeders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ActionTracker.Tests.Unit;

/// <summary>
/// <see cref="EmailTemplateSeeder"/> is insert-only (WO-2026-11 A-3, fix 1):
/// <list type="bullet">
/// <item>an empty table gets every seeded template;</item>
/// <item>a missing key is inserted;</item>
/// <item>an existing template is never changed — subject and body stay as they
/// are, whether or not they were edited in the Email Templates screen and
/// whether or not they still equal the seed;</item>
/// <item>a soft-deleted template counts as existing: it is neither re-inserted
/// nor restored.</item>
/// </list>
/// Before A-3 a missing key also made the seeder overwrite every existing body
/// that differed from the seed; the tests below pin that this no longer happens.
/// </summary>
public class EmailTemplateSeederTests : IDisposable
{
    private readonly AppDbContext _db = new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task An_empty_table_gets_every_seeded_template()
    {
        await EmailTemplateSeeder.SeedAsync(_db, NullLogger.Instance);

        var keys = await _db.EmailTemplates.Select(t => t.TemplateKey).ToListAsync();
        keys.Should().BeEquivalentTo(EmailTemplateSeeder.SeedTemplates().Select(t => t.TemplateKey));
        keys.Should().Contain(new[]
        {
            "ActionItem.SubItemAssigned", "ActionItem.SubItemCompleted", "ActionItem.UpdateSubmitted", "ActionItem.DirectionGiven",
        });

        var assigned = await _db.EmailTemplates.SingleAsync(t => t.TemplateKey == "ActionItem.SubItemAssigned");
        assigned.HtmlBody.Should().Contain("{{SubItemTitle}}").And.Contain("{{ItemUrl}}").And.Contain("{{ActorName}}");
        assigned.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Seeded_keys_and_ids_are_unique()
    {
        var seed = EmailTemplateSeeder.SeedTemplates();
        seed.Select(t => t.TemplateKey).Should().OnlyHaveUniqueItems();
        seed.Select(t => t.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Nothing_is_written_when_every_key_exists()
    {
        await EmailTemplateSeeder.SeedAsync(_db, NullLogger.Instance);
        var edited = await _db.EmailTemplates.FirstAsync(t => t.TemplateKey == "ActionItem.Created");
        edited.HtmlBody = "<p>Edited in the Email Templates screen</p>";
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        await EmailTemplateSeeder.SeedAsync(_db, NullLogger.Instance);

        (await _db.EmailTemplates.FirstAsync(t => t.TemplateKey == "ActionItem.Created")).HtmlBody
            .Should().Be("<p>Edited in the Email Templates screen</p>");
    }

    [Fact]
    public async Task A_missing_key_is_inserted_and_existing_subjects_and_bodies_are_kept()
    {
        await EmailTemplateSeeder.SeedAsync(_db, NullLogger.Instance);

        // State of a database seeded before A-3 (no UpdateSubmitted template)
        // with admin edits, and one template whose stored body is an older seed.
        _db.EmailTemplates.RemoveRange(_db.EmailTemplates.Where(t => t.TemplateKey == "ActionItem.UpdateSubmitted"));
        var created = await _db.EmailTemplates.FirstAsync(t => t.TemplateKey == "ActionItem.Created");
        created.HtmlBody = "<p>Body edited</p>";
        created.Subject  = "Subject edited";
        (await _db.EmailTemplates.FirstAsync(t => t.TemplateKey == "ActionItem.Assigned")).Subject = "Only the subject edited";
        (await _db.EmailTemplates.FirstAsync(t => t.TemplateKey == "ActionItem.Overdue")).HtmlBody = "<p>Older seed body</p>";
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var before = await _db.EmailTemplates.AsNoTracking()
            .ToDictionaryAsync(t => t.TemplateKey, t => (t.Subject, t.HtmlBody, t.UpdatedAt));

        await EmailTemplateSeeder.SeedAsync(_db, NullLogger.Instance);

        var after = await _db.EmailTemplates.AsNoTracking().ToListAsync();
        after.Should().ContainSingle(t => t.TemplateKey == "ActionItem.UpdateSubmitted");
        foreach (var (key, old) in before)
        {
            var now = after.Single(t => t.TemplateKey == key);
            (now.Subject, now.HtmlBody, now.UpdatedAt).Should().Be(old, $"{key} existed before the seed ran");
        }

        after.Single(t => t.TemplateKey == "ActionItem.Created").HtmlBody.Should().Be("<p>Body edited</p>");
        after.Single(t => t.TemplateKey == "ActionItem.Created").Subject.Should().Be("Subject edited");
        after.Single(t => t.TemplateKey == "ActionItem.Overdue").HtmlBody.Should().Be("<p>Older seed body</p>");
    }

    [Fact]
    public async Task A_soft_deleted_template_is_neither_reinserted_nor_restored()
    {
        await EmailTemplateSeeder.SeedAsync(_db, NullLogger.Instance);
        var deleted = await _db.EmailTemplates.FirstAsync(t => t.TemplateKey == "ActionItem.Completed");
        deleted.IsDeleted = true;
        _db.EmailTemplates.RemoveRange(_db.EmailTemplates.Where(t => t.TemplateKey == "ActionItem.UpdateSubmitted"));
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        await EmailTemplateSeeder.SeedAsync(_db, NullLogger.Instance);

        var completed = await _db.EmailTemplates.IgnoreQueryFilters()
            .Where(t => t.TemplateKey == "ActionItem.Completed").ToListAsync();
        completed.Should().ContainSingle().Which.IsDeleted.Should().BeTrue();
    }
}
