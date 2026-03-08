using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActionTracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ActionItemsEscalations2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActionItemEscalations_AspNetUsers_EscalatedByUserId",
                table: "ActionItemEscalations");

            // Fix EscalatedByUserId: remove erroneous GETUTCDATE() default that was
            // mistakenly placed on this string column in the previous snapshot.
            migrationBuilder.AlterColumn<string>(
                name: "EscalatedByUserId",
                table: "ActionItemEscalations",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            // Add GETUTCDATE() default to CreatedAt
            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "ActionItemEscalations",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            // Add NEWID() default to Id (was missing from the initial migration)
            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "ActionItemEscalations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            // Change Explanation from nvarchar(max) to nvarchar(2000) to match configuration
            migrationBuilder.AlterColumn<string>(
                name: "Explanation",
                table: "ActionItemEscalations",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddForeignKey(
                name: "FK_ActionItemEscalations_AspNetUsers_EscalatedByUserId",
                table: "ActionItemEscalations",
                column: "EscalatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActionItemEscalations_AspNetUsers_EscalatedByUserId",
                table: "ActionItemEscalations");

            // Revert Explanation back to nvarchar(max)
            migrationBuilder.AlterColumn<string>(
                name: "Explanation",
                table: "ActionItemEscalations",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);

            // Revert Id: remove NEWID() default
            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "ActionItemEscalations",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldDefaultValueSql: "NEWID()");

            // Revert CreatedAt: remove GETUTCDATE() default
            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "ActionItemEscalations",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.AlterColumn<string>(
                name: "EscalatedByUserId",
                table: "ActionItemEscalations",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.AddForeignKey(
                name: "FK_ActionItemEscalations_AspNetUsers_EscalatedByUserId",
                table: "ActionItemEscalations",
                column: "EscalatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
