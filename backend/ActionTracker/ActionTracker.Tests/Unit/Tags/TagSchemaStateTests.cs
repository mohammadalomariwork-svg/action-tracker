using ActionTracker.Infrastructure.Data;
using ActionTracker.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ActionTracker.Tests.Unit.Tags;

/// <summary>
/// WO-2026-11 A-5 (D7): the tag-table check is remembered once the tables are
/// found and, while they are not, asked again at most once a minute — so a
/// migration applied while the API runs takes effect without a restart. (The
/// INFORMATION_SCHEMA query itself was checked on LocalDB, before and after
/// <c>Add_ActionItemTags</c>.)
/// </summary>
public class TagSchemaStateTests
{
    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 25, 8, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task Missing_tables_are_rechecked_once_a_minute_and_found_tables_are_remembered()
    {
        var reachable = false;
        var calls = 0;
        var inMemory = new ServiceCollection()
            .AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()))
            .BuildServiceProvider();

        // A database that cannot answer yet stands in for "tables missing".
        var scopes = new Mock<IServiceScopeFactory>();
        scopes.Setup(s => s.CreateScope()).Returns(() =>
        {
            calls++;
            if (!reachable) throw new InvalidOperationException("no tag tables yet");
            return inMemory.CreateScope();
        });

        var clock = new ManualClock();
        var state = new TagSchemaState(scopes.Object, NullLogger<TagSchemaState>.Instance, clock);

        (await state.IsAvailableAsync()).Should().BeFalse();
        calls.Should().Be(1);

        reachable = true;
        clock.Now += TimeSpan.FromSeconds(30);
        (await state.IsAvailableAsync()).Should().BeFalse("asked again only after a minute");
        calls.Should().Be(1);

        clock.Now += TagSchemaState.RecheckInterval;
        (await state.IsAvailableAsync()).Should().BeTrue();
        calls.Should().Be(2);

        reachable = false;
        clock.Now += TimeSpan.FromHours(1);
        (await state.IsAvailableAsync()).Should().BeTrue("once found, never asked again");
        calls.Should().Be(2);
    }
}
