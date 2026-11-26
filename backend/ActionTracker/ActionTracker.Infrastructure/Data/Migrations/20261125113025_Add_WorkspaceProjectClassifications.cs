using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActionTracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_WorkspaceProjectClassifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InternalClassificationId",
                table: "Projects",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkspaceProjectClassifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceProjectClassifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkspaceProjectClassifications_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_InternalClassificationId",
                table: "Projects",
                column: "InternalClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceProjectClassifications_WorkspaceId_NormalizedName",
                table: "WorkspaceProjectClassifications",
                columns: new[] { "WorkspaceId", "NormalizedName" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceProjectClassifications_WorkspaceId_SortOrder",
                table: "WorkspaceProjectClassifications",
                columns: new[] { "WorkspaceId", "SortOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_WorkspaceProjectClassifications_InternalClassificationId",
                table: "Projects",
                column: "InternalClassificationId",
                principalTable: "WorkspaceProjectClassifications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_WorkspaceProjectClassifications_InternalClassificationId",
                table: "Projects");

            migrationBuilder.DropTable(
                name: "WorkspaceProjectClassifications");

            migrationBuilder.DropIndex(
                name: "IX_Projects_InternalClassificationId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "InternalClassificationId",
                table: "Projects");
        }
    }
}
