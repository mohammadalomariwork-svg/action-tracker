using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActionTracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ActionItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActionItems_AspNetUsers_AssigneeId",
                table: "ActionItems");

            migrationBuilder.DropIndex(
                name: "IX_ActionItems_AssigneeId",
                table: "ActionItems");

            migrationBuilder.DropColumn(
                name: "AssigneeId",
                table: "ActionItems");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "ActionItems");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "ActionItems");

            // Drop FK and PK that depend on Id first
            migrationBuilder.DropPrimaryKey(
                name: "PK_ActionItems",
                table: "ActionItems");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "ActionItems");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "ActionItems",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ActionItems",
                table: "ActionItems",
                column: "Id");

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "ActionItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkspaceId",
                table: "ActionItems",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "ActionItemAssignees",
                columns: table => new
                {
                    ActionItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionItemAssignees", x => new { x.ActionItemId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ActionItemAssignees_ActionItems_ActionItemId",
                        column: x => x.ActionItemId,
                        principalTable: "ActionItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActionItemAssignees_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActionItems_WorkspaceId",
                table: "ActionItems",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_ActionItemAssignees_UserId",
                table: "ActionItemAssignees",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ActionItems_Workspaces_WorkspaceId",
                table: "ActionItems",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActionItems_Workspaces_WorkspaceId",
                table: "ActionItems");

            migrationBuilder.DropTable(
                name: "ActionItemAssignees");

            migrationBuilder.DropIndex(
                name: "IX_ActionItems_WorkspaceId",
                table: "ActionItems");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "ActionItems");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "ActionItems");

            migrationBuilder.DropPrimaryKey(
    name: "PK_ActionItems",
    table: "ActionItems");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "ActionItems");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "ActionItems",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ActionItems",
                table: "ActionItems",
                column: "Id");

            migrationBuilder.AddColumn<string>(
                name: "AssigneeId",
                table: "ActionItems",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "ActionItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "ActionItems",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ActionItems_AssigneeId",
                table: "ActionItems",
                column: "AssigneeId");

            migrationBuilder.AddForeignKey(
                name: "FK_ActionItems_AspNetUsers_AssigneeId",
                table: "ActionItems",
                column: "AssigneeId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
