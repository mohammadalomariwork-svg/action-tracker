using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActionTracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TeamId",
                table: "ActionTrackerUserDetails",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeamName",
                table: "ActionTrackerUserDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "ActionTrackerUserDetails");

            migrationBuilder.DropColumn(
                name: "TeamName",
                table: "ActionTrackerUserDetails");
        }
    }
}
