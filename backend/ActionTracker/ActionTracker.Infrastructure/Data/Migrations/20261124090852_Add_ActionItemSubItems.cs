using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActionTracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_ActionItemSubItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActionItemSubItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    ActionItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsDone = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DoneAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DoneByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedWithParent = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionItemSubItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActionItemSubItems_ActionItems_ActionItemId",
                        column: x => x.ActionItemId,
                        principalTable: "ActionItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActionItemSubItems_AspNetUsers_DoneByUserId",
                        column: x => x.DoneByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ActionItemSubItemAssignees",
                columns: table => new
                {
                    SubItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionItemSubItemAssignees", x => new { x.SubItemId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ActionItemSubItemAssignees_ActionItemSubItems_SubItemId",
                        column: x => x.SubItemId,
                        principalTable: "ActionItemSubItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActionItemSubItemAssignees_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActionItemSubItemAssignees_UserId",
                table: "ActionItemSubItemAssignees",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ActionItemSubItems_ActionItemId_SortOrder",
                table: "ActionItemSubItems",
                columns: new[] { "ActionItemId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ActionItemSubItems_DoneByUserId",
                table: "ActionItemSubItems",
                column: "DoneByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActionItemSubItemAssignees");

            migrationBuilder.DropTable(
                name: "ActionItemSubItems");
        }
    }
}
