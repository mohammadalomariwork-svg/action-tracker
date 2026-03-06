using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActionTracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserOrgUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrgUnitId",
                table: "AspNetUsers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_OrgUnitId",
                table: "AspNetUsers",
                column: "OrgUnitId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_OrgUnits_OrgUnitId",
                table: "AspNetUsers",
                column: "OrgUnitId",
                principalTable: "OrgUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_OrgUnits_OrgUnitId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_OrgUnitId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "OrgUnitId",
                table: "AspNetUsers");
        }
    }
}
