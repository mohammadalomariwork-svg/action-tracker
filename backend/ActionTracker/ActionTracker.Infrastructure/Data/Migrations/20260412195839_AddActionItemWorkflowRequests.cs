using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActionTracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddActionItemWorkflowRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActionItemWorkflowRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ActionItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RequestedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RequestedByDisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RequestedNewStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequestedNewDueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequestedNewStatus = table.Column<int>(type: "int", nullable: true),
                    CurrentStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CurrentDueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CurrentStatus = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReviewedByDisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ReviewComment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionItemWorkflowRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActionItemWorkflowRequests_ActionItems_ActionItemId",
                        column: x => x.ActionItemId,
                        principalTable: "ActionItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActionItemWorkflowRequests_ActionItemId",
                table: "ActionItemWorkflowRequests",
                column: "ActionItemId",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ActionItemWorkflowRequests_RequestedByUserId",
                table: "ActionItemWorkflowRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ActionItemWorkflowRequests_Status",
                table: "ActionItemWorkflowRequests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActionItemWorkflowRequests");
        }
    }
}
