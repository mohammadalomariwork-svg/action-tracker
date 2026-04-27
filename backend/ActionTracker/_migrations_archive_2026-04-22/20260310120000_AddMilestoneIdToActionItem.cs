using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActionTracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMilestoneIdToActionItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MilestoneId",
                table: "ActionItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActionItems_MilestoneId",
                table: "ActionItems",
                column: "MilestoneId");

            migrationBuilder.AddForeignKey(
                name: "FK_ActionItems_Milestones_MilestoneId",
                table: "ActionItems",
                column: "MilestoneId",
                principalTable: "Milestones",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActionItems_Milestones_MilestoneId",
                table: "ActionItems");

            migrationBuilder.DropIndex(
                name: "IX_ActionItems_MilestoneId",
                table: "ActionItems");

            migrationBuilder.DropColumn(
                name: "MilestoneId",
                table: "ActionItems");
        }
    }
}
