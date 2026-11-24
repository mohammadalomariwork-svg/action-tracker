using ActionTracker.Infrastructure.Data;
using ActionTracker.Infrastructure.Data.Seeders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ActionTracker.Tests.Unit;

/// <summary>
/// Pins down what <see cref="EmailTemplateSeeder"/> does today (WO-2026-11 A-2
/// added two templates and must not change this behaviour):
/// <list type="bullet">
/// <item>nothing is written when every seeded key already exists;</item>
/// <item>when at least one key is missing it inserts the missing ones and, in
/// the same save, overwrites Subject + HtmlBody of every existing template
/// whose HtmlBody differs from the seed — an edit made in the Email Templates
/// screen is lost then; a Subject-only edit is kept.</item>
/// </list>
/// </summary>
public class EmailTemplateSeederTests : IDisposable
{
    private readonly AppDbContext _db = new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Seeds_the_sub_item_templates()
    {
        await EmailTemplateSeeder.SeedAsync(_db, NullLogger.Instance);

        var keys = await _db.EmailTemplates.Select(t => t.TemplateKey).ToListAsync();
        keys.Should().Contain(new[] { "ActionItem.SubItemAssigned", "ActionItem.SubItemCompleted" });
        var assigned = await _db.EmailTemplates.SingleAsync(t => t.TemplateKey == "ActionItem.SubItemAssigned");
        assigned.HtmlBody.Should().Contain("{{SubItemTitle}}").And.Contain("{{ItemUrl}}").And.Contain("{{ActorName}}");
        assigned.IsActive.Should().BeTrue();
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
    public async Task A_missing_key_inserts_it_and_overwrites_edited_bodies_but_not_subject_only_edits()
    {
        await EmailTemplateSeeder.SeedAsync(_db, NullLogger.Instance);
        var originalSubject = (await _db.EmailTemplates.FirstAsync(t => t.TemplateKey == "ActionItem.Created")).Subject;

        // State of a database seeded before A-2, with two admin edits.
        _db.EmailTemplates.RemoveRange(_db.EmailTemplates.Where(t => t.TemplateKey.StartsWith("ActionItem.SubItem")));
        (await _db.EmailTemplates.FirstAsync(t => t.TemplateKey == "ActionItem.Created")).HtmlBody = "<p>Body edited</p>";
        (await _db.EmailTemplates.FirstAsync(t => t.TemplateKey == "ActionItem.Created")).Subject = "Subject edited";
        (await _db.EmailTemplates.FirstAsync(t => t.TemplateKey == "ActionItem.Assigned")).Subject = "Only the subject edited";
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        await EmailTemplateSeeder.SeedAsync(_db, NullLogger.Instance);

        (await _db.EmailTemplates.CountAsync(t => t.TemplateKey.StartsWith("ActionItem.SubItem"))).Should().Be(2);
        var created = await _db.EmailTemplates.FirstAsync(t => t.TemplateKey == "ActionItem.Created");
        created.HtmlBody.Should().NotBe("<p>Body edited</p>");          // overwritten from the seed
        created.Subject.Should().Be(originalSubject);                     // subject reset with it
        (await _db.EmailTemplates.FirstAsync(t => t.TemplateKey == "ActionItem.Assigned")).Subject
            .Should().Be("Only the subject edited");                      // body unchanged → kept
    }
}
