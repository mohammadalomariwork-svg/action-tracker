using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActionTracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_WorkspacePMs_And_StrategicProjectApprovers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignedReviewerDisplayName",
                table: "ProjectApprovalRequests",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedReviewerUserId",
                table: "ProjectApprovalRequests",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Level",
                table: "ProjectApprovalRequests",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TotalLevels",
                table: "ProjectApprovalRequests",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "WorkspaceProjectManagers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    AddedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceProjectManagers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkspaceProjectManagers_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkspaceStrategicProjectApprovers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    AddedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceStrategicProjectApprovers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkspaceStrategicProjectApprovers_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectApprovalRequests_AssignedReviewerUserId",
                table: "ProjectApprovalRequests",
                column: "AssignedReviewerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceProjectManagers_UserId",
                table: "WorkspaceProjectManagers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceProjectManagers_WorkspaceId",
                table: "WorkspaceProjectManagers",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceProjectManagers_WorkspaceId_UserId",
                table: "WorkspaceProjectManagers",
                columns: new[] { "WorkspaceId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceStrategicProjectApprovers_WorkspaceId",
                table: "WorkspaceStrategicProjectApprovers",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceStrategicProjectApprovers_WorkspaceId_Order",
                table: "WorkspaceStrategicProjectApprovers",
                columns: new[] { "WorkspaceId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceStrategicProjectApprovers_WorkspaceId_UserId",
                table: "WorkspaceStrategicProjectApprovers",
                columns: new[] { "WorkspaceId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkspaceProjectManagers");

            migrationBuilder.DropTable(
                name: "WorkspaceStrategicProjectApprovers");

            migrationBuilder.DropIndex(
                name: "IX_ProjectApprovalRequests_AssignedReviewerUserId",
                table: "ProjectApprovalRequests");

            migrationBuilder.DropColumn(
                name: "AssignedReviewerDisplayName",
                table: "ProjectApprovalRequests");

            migrationBuilder.DropColumn(
                name: "AssignedReviewerUserId",
                table: "ProjectApprovalRequests");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "ProjectApprovalRequests");

            migrationBuilder.DropColumn(
                name: "TotalLevels",
                table: "ProjectApprovalRequests");
        }
    }
}
