using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActionTracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmpIdAndUpdateManagerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add the HR employee ID column
            migrationBuilder.AddColumn<string>(
                name: "EmpId",
                table: "ActionTrackerUserDetails",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            // Widen ManagerId to match the HR EmpId format (500 chars)
            migrationBuilder.AlterColumn<string>(
                name: "ManagerId",
                table: "ActionTrackerUserDetails",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmpId",
                table: "ActionTrackerUserDetails");

            migrationBuilder.AlterColumn<string>(
                name: "ManagerId",
                table: "ActionTrackerUserDetails",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }
    }
}
