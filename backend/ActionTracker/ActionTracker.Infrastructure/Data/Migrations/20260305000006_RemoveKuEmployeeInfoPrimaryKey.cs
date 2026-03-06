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
            migrationBuilder.DropPrimaryKey(
                name: "PK_ku_employee_info",
                table: "ku_employee_info");
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
