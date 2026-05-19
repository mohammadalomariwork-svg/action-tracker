using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActionTracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Filter_StrategicPlan_OrgUnitId_ActiveUnique_For_Drafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StrategicPlans_OrgUnitId_ActiveUnique",
                table: "StrategicPlans");

            migrationBuilder.CreateIndex(
                name: "IX_StrategicPlans_OrgUnitId_ActiveUnique",
                table: "StrategicPlans",
                column: "OrgUnitId",
                unique: true,
                filter: "[IsDraft] = 0 AND [IsDeleted] = 0 AND [Status] IN (1, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StrategicPlans_OrgUnitId_ActiveUnique",
                table: "StrategicPlans");

            migrationBuilder.CreateIndex(
                name: "IX_StrategicPlans_OrgUnitId_ActiveUnique",
                table: "StrategicPlans",
                column: "OrgUnitId",
                unique: true,
                filter: "[IsDeleted] = 0 AND [Status] IN (1, 2)");
        }
    }
}
