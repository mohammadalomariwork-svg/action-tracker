using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActionTracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_OrgUnit_HeadUserId_And_StrategicPlanApprovalRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HeadUserId",
                table: "OrgUnits",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StrategicPlanApprovalRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    StrategicPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestType = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RequestedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RequestedByDisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReviewedByDisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AssignedReviewerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AssignedReviewerDisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Justification = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ReviewerComment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategicPlanApprovalRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StrategicPlanApprovalRequests_StrategicPlans_StrategicPlanId",
                        column: x => x.StrategicPlanId,
                        principalTable: "StrategicPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrgUnits_HeadUserId",
                table: "OrgUnits",
                column: "HeadUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StrategicPlanApprovalRequests_AssignedReviewerUserId_Status",
                table: "StrategicPlanApprovalRequests",
                columns: new[] { "AssignedReviewerUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StrategicPlanApprovalRequests_RequestedByUserId",
                table: "StrategicPlanApprovalRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StrategicPlanApprovalRequests_StrategicPlanId_Status",
                table: "StrategicPlanApprovalRequests",
                columns: new[] { "StrategicPlanId", "Status" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_OrgUnits_AspNetUsers_HeadUserId",
                table: "OrgUnits",
                column: "HeadUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrgUnits_AspNetUsers_HeadUserId",
                table: "OrgUnits");

            migrationBuilder.DropTable(
                name: "StrategicPlanApprovalRequests");

            migrationBuilder.DropIndex(
                name: "IX_OrgUnits_HeadUserId",
                table: "OrgUnits");

            migrationBuilder.DropColumn(
                name: "HeadUserId",
                table: "OrgUnits");
        }
    }
}
