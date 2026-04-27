using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActionTracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectIdAndIsStandaloneToActionItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "ActionItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsStandalone",
                table: "ActionItems",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActionItems_ProjectId",
                table: "ActionItems",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_ActionItems_Projects_ProjectId",
                table: "ActionItems",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActionItems_Projects_ProjectId",
                table: "ActionItems");

            migrationBuilder.DropIndex(
                name: "IX_ActionItems_ProjectId",
                table: "ActionItems");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "ActionItems");

            migrationBuilder.DropColumn(
                name: "IsStandalone",
                table: "ActionItems");
        }
    }
}
