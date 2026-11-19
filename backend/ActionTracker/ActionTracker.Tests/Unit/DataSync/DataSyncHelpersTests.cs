using ActionTracker.Infrastructure.DataSync;
using FluentAssertions;

namespace ActionTracker.Tests.Unit.DataSync;

public class SqlIdentifierTests
{
    [Theory]
    [InlineData("dbo.ku_employee_info", "dbo", "ku_employee_info")]
    [InlineData("ku_employee_info",     "dbo", "ku_employee_info")]
    [InlineData("[hr].[Employees]",     "hr",  "Employees")]
    [InlineData("  staging . People ",  "staging", "People")]
    public void SplitTableName_ParsesSchemaAndTable(string input, string schema, string table)
    {
        var result = SqlIdentifier.SplitTableName(input);
        result.Schema.Should().Be(schema);
        result.Table.Should().Be(table);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a.b.c")]
    [InlineData("dbo.emp;DROP TABLE x")]
    [InlineData("dbo.emp'--")]
    public void SplitTableName_RejectsUnsafeInput(string input)
    {
        var act = () => SqlIdentifier.SplitTableName(input);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Quote_BracketsAndEscapes()
    {
        SqlIdentifier.Quote(("dbo", "ku_employee_info")).Should().Be("[dbo].[ku_employee_info]");
        SqlIdentifier.Quote("we]ird").Should().Be("[we]]ird]");
    }
}

public class CronValidationTests
{
    [Theory]
    [InlineData("0 * * * *")]
    [InlineData("*/15 * * * *")]
    [InlineData("0 2 * * *")]
    [InlineData("0 6 * * 1")]
    [InlineData("30 0 * * * *")] // 6-field with seconds
    public void Validate_AcceptsWellFormedExpressions(string cron)
    {
        var act = () => CronValidation.Validate(cron);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("every hour")]
    [InlineData("0 *")]
    [InlineData("99 * * * *")]
    [InlineData("0 25 * * *")]
    public void Validate_RejectsMalformedExpressions(string cron)
    {
        var act = () => CronValidation.Validate(cron);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ResolveTimeZone_BlankMeansUtc()
    {
        CronValidation.ResolveTimeZone(null).Should().Be(TimeZoneInfo.Utc);
        CronValidation.ResolveTimeZone("").Should().Be(TimeZoneInfo.Utc);
    }

    [Fact]
    public void ResolveTimeZone_UnknownIdThrows()
    {
        var act = () => CronValidation.ResolveTimeZone("Mars/Olympus_Mons");
        act.Should().Throw<ArgumentException>();
    }
}

public class CronDescriberTests
{
    [Theory]
    [InlineData("0 * * * *",    "Every hour")]
    [InlineData("15 * * * *",   "Every hour at :15")]
    [InlineData("*/15 * * * *", "Every 15 minutes")]
    [InlineData("0 */6 * * *",  "Every 6 hours")]
    [InlineData("0 2 * * *",    "Daily at 02:00")]
    [InlineData("0 6 * * 1",    "Every Monday at 06:00")]
    [InlineData("0 3 1 * *",    "Monthly on day 1 at 03:00")]
    public void Describe_KnownShapes(string cron, string expected)
    {
        CronDescriber.Describe(cron).Should().Be(expected);
    }

    [Fact]
    public void Describe_UnknownShape_ReturnsRawExpression()
    {
        CronDescriber.Describe("5 4 * * 1-5").Should().Be("5 4 * * 1-5");
    }
}

public class NoOpDataSyncSchedulerTests
{
    [Fact]
    public void AddOrUpdate_RecordsSchedule_AndRemoveClearsIt()
    {
        var scheduler = new NoOpDataSyncScheduler();

        scheduler.AddOrUpdate("employee-info-sync", "0 * * * *", null);
        scheduler.Registered.Should().ContainKey("employee-info-sync");
        scheduler.GetNextExecutionUtc("employee-info-sync").Should().BeNull();

        scheduler.Remove("employee-info-sync");
        scheduler.Registered.Should().NotContainKey("employee-info-sync");
    }

    [Fact]
    public void AddOrUpdate_InvalidCron_Throws()
    {
        var scheduler = new NoOpDataSyncScheduler();
        var act = () => scheduler.AddOrUpdate("x", "not a cron", null);
        act.Should().Throw<ArgumentException>();
        scheduler.Registered.Should().BeEmpty();
    }
}
