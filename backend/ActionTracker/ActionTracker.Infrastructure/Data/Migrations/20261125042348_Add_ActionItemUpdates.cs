using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActionTracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_ActionItemUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ActionItemComments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "ActionItemComments",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "SubItemId",
                table: "ActionItemComments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "ActionItemComments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActionItemComments_SubItemId",
                table: "ActionItemComments",
                column: "SubItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_ActionItemComments_ActionItemSubItems_SubItemId",
                table: "ActionItemComments",
                column: "SubItemId",
                principalTable: "ActionItemSubItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ── Backfill (WO-2026-11 A-3, Q11, approved as D1) ──────────────
            // 1. Every existing row becomes a submitted (read-only) update.
            //    Status is already 1 from the column default; SubmittedAt
            //    takes the row's CreatedAt.
            migrationBuilder.Sql(
                """
                UPDATE dbo.ActionItemComments
                SET Status = 1, SubmittedAt = CreatedAt
                WHERE SubmittedAt IS NULL;
                """);

            // 2. Copy the action-item comments the detail page wrote to the
            //    shared Comments table, as submitted updates with the same Id.
            //    Comments rows are neither changed nor deleted. Rows whose
            //    action item no longer exists cannot meet the foreign key and
            //    stay only in Comments. Re-runnable: the same INSERT is in
            //    ActionTracker.Infrastructure/Data/Scripts/A3-copy-action-item-comments.sql
            //    (a test keeps the two identical).
            migrationBuilder.Sql(
                """
                INSERT INTO dbo.ActionItemComments
                    (Id, ActionItemId, SubItemId, Content, AuthorUserId, IsHighImportance,
                     CreatedAt, UpdatedAt, Status, SubmittedAt, IsDeleted)
                SELECT c.Id, c.RelatedEntityId, NULL, c.Content, c.AuthorUserId, c.IsHighImportance,
                       c.CreatedAt, c.UpdatedAt, 1, c.CreatedAt, 0
                FROM dbo.Comments AS c
                INNER JOIN dbo.ActionItems AS a ON a.Id = c.RelatedEntityId
                WHERE c.RelatedEntityType = N'ActionItem'
                  AND NOT EXISTS (SELECT 1 FROM dbo.ActionItemComments AS x WHERE x.Id = c.Id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The rows copied from Comments stay in ActionItemComments (they
            // are still in Comments too); only the A-3 columns are removed.
            migrationBuilder.DropForeignKey(
                name: "FK_ActionItemComments_ActionItemSubItems_SubItemId",
                table: "ActionItemComments");

            migrationBuilder.DropIndex(
                name: "IX_ActionItemComments_SubItemId",
                table: "ActionItemComments");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ActionItemComments");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ActionItemComments");

            migrationBuilder.DropColumn(
                name: "SubItemId",
                table: "ActionItemComments");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "ActionItemComments");
        }
    }
}
