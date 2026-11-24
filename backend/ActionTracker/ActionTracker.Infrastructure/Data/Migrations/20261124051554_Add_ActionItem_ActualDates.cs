using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActionTracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_ActionItem_ActualDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ActualEndDate",
                table: "ActionItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActualStartDate",
                table: "ActionItems",
                type: "datetime2",
                nullable: true);

            // Backfill (WO-2026-11 A-1, decision D2): existing Done items get an
            // actual end; everything else stays NULL, including every actual start.
            // UpdatedAt is the best record of completion, but it is never stamped
            // on an item created as Done and left alone (it stays 0001-01-01), so
            // those rows use CreatedAt. The value is the calendar day at midnight,
            // like the planned dates. Soft-deleted Done rows are included so they
            // stay consistent if restored. Status 4 = ActionStatus.Done.
            migrationBuilder.Sql(
                """
                UPDATE dbo.ActionItems
                SET ActualEndDate = CAST(CAST(CASE WHEN UpdatedAt >= CreatedAt THEN UpdatedAt ELSE CreatedAt END AS date) AS datetime2)
                WHERE Status = 4;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActualEndDate",
                table: "ActionItems");

            migrationBuilder.DropColumn(
                name: "ActualStartDate",
                table: "ActionItems");
        }
    }
}
