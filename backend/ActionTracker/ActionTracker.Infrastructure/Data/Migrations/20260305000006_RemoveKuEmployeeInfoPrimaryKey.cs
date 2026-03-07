using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActionTracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveKuEmployeeInfoPrimaryKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The real [dbo].[ku_employee_info] table (populated by the Oracle
            // EBS ETL) has no primary key.  Drop the surrogate PK that was
            // added when the migration first created the table locally.
            // Use conditional SQL in case the PK was never created (e.g. when
            // the ETL-synced table already existed without a primary key).
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.key_constraints
                    WHERE name = 'PK_ku_employee_info' AND type = 'PK'
                )
                    ALTER TABLE [ku_employee_info] DROP CONSTRAINT [PK_ku_employee_info];
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddPrimaryKey(
                name: "PK_ku_employee_info",
                table: "ku_employee_info",
                column: "AssignmentId");
        }
    }
}
