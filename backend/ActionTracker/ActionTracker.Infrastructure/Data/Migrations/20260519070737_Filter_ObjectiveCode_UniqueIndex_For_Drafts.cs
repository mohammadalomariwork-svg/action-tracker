using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActionTracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Filter_ObjectiveCode_UniqueIndex_For_Drafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StrategicObjectives_ObjectiveCode",
                table: "StrategicObjectives");

            migrationBuilder.CreateIndex(
                name: "IX_StrategicObjectives_ObjectiveCode",
                table: "StrategicObjectives",
                column: "ObjectiveCode",
                unique: true,
                filter: "[IsDraft] = 0 AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StrategicObjectives_ObjectiveCode",
                table: "StrategicObjectives");

            migrationBuilder.CreateIndex(
                name: "IX_StrategicObjectives_ObjectiveCode",
                table: "StrategicObjectives",
                column: "ObjectiveCode",
                unique: true);
        }
    }
}
