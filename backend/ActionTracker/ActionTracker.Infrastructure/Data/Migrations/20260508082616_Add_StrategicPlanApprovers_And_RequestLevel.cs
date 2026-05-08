using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActionTracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_StrategicPlanApprovers_And_RequestLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Level",
                table: "StrategicPlanApprovalRequests",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TotalLevels",
                table: "StrategicPlanApprovalRequests",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "StrategicPlanApprovers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    StrategicPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategicPlanApprovers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StrategicPlanApprovers_StrategicPlans_StrategicPlanId",
                        column: x => x.StrategicPlanId,
                        principalTable: "StrategicPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StrategicPlanApprovers_StrategicPlanId_Order",
                table: "StrategicPlanApprovers",
                columns: new[] { "StrategicPlanId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StrategicPlanApprovers_StrategicPlanId_UserId",
                table: "StrategicPlanApprovers",
                columns: new[] { "StrategicPlanId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StrategicPlanApprovers");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "StrategicPlanApprovalRequests");

            migrationBuilder.DropColumn(
                name: "TotalLevels",
                table: "StrategicPlanApprovalRequests");
        }
    }
}
