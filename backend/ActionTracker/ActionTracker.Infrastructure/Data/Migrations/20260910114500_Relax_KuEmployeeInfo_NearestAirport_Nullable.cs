using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActionTracker.Infrastructure.Data.Migrations
{
    /// <summary>
    /// <c>dbo.ku_employee_info</c> is a mirror of the HR table and is excluded from
    /// EF migrations, so schema drift is fixed with raw SQL. The HR source allows
    /// NULL in <c>NearestAirport</c> (632 of ~1,700 rows at the time of writing) while
    /// the mirror declared it NOT NULL, which made the data-sync bulk copy fail.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260910114500_Relax_KuEmployeeInfo_NearestAirport_Nullable")]
    public partial class Relax_KuEmployeeInfo_NearestAirport_Nullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1
    FROM sys.columns c
    WHERE c.object_id = OBJECT_ID(N'dbo.ku_employee_info')
      AND c.name = N'NearestAirport'
      AND c.is_nullable = 0)
BEGIN
    ALTER TABLE dbo.ku_employee_info ALTER COLUMN NearestAirport nvarchar(255) NULL;
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally left as-is: re-adding NOT NULL would fail once NULLs
            // have been synced from HR, and the mirror must match the source.
        }
    }
}
