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

            migrationBuilder.AlterColumn<string>(
                name: "EscalatedByUserId",
                table: "ActionItemEscalations",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "ActionItemEscalations",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

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

            migrationBuilder.AlterColumn<string>(
                name: "EscalatedByUserId",
                table: "ActionItemEscalations",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "ActionItemEscalations",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETUTCDATE()");

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
