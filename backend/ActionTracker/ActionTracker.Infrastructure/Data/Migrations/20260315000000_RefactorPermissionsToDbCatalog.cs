using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActionTracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefactorPermissionsToDbCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Create catalog tables ──────────────────────────────────────────

            migrationBuilder.CreateTable(
                name: "PermissionAreas",
                columns: table => new
                {
                    Id           = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    Name         = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName  = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description  = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive     = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsDeleted    = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt    = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy    = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionAreas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PermissionActions",
                columns: table => new
                {
                    Id           = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    Name         = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName  = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description  = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive     = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsDeleted    = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt    = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy    = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionActions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AreaPermissionMappings",
                columns: table => new
                {
                    Id         = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    AreaId     = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AreaName   = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActionId   = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive   = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsDeleted  = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt  = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy  = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AreaPermissionMappings", x => x.Id);
                });

            // ── 2. Catalog table indexes ──────────────────────────────────────────

            migrationBuilder.CreateIndex(
                name: "IX_PermissionAreas_Name",
                table: "PermissionAreas",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PermissionActions_Name",
                table: "PermissionActions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AreaPermissionMappings_AreaId_ActionId",
                table: "AreaPermissionMappings",
                columns: new[] { "AreaId", "ActionId" });

            // ── 3. Refactor RolePermissions ───────────────────────────────────────

            // Drop old index
            migrationBuilder.DropIndex(
                name: "IX_RolePermissions_RoleName_Area_Action",
                table: "RolePermissions");

            // Drop old enum columns
            migrationBuilder.DropColumn(name: "Area",   table: "RolePermissions");
            migrationBuilder.DropColumn(name: "Action", table: "RolePermissions");

            // Add new catalog-reference columns
            migrationBuilder.AddColumn<Guid>(
                name: "AreaId",
                table: "RolePermissions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.AddColumn<string>(
                name: "AreaName",
                table: "RolePermissions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ActionId",
                table: "RolePermissions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.AddColumn<string>(
                name: "ActionName",
                table: "RolePermissions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // Re-create index on new columns
            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleName_AreaId_ActionId",
                table: "RolePermissions",
                columns: new[] { "RoleName", "AreaId", "ActionId" });

            // ── 4. Refactor UserPermissionOverrides ───────────────────────────────

            // Drop old index
            migrationBuilder.DropIndex(
                name: "IX_UserPermissionOverrides_UserId_Area_Action",
                table: "UserPermissionOverrides");

            // Drop old enum columns
            migrationBuilder.DropColumn(name: "Area",   table: "UserPermissionOverrides");
            migrationBuilder.DropColumn(name: "Action", table: "UserPermissionOverrides");

            // Add new catalog-reference columns
            migrationBuilder.AddColumn<Guid>(
                name: "AreaId",
                table: "UserPermissionOverrides",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.AddColumn<string>(
                name: "AreaName",
                table: "UserPermissionOverrides",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ActionId",
                table: "UserPermissionOverrides",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.AddColumn<string>(
                name: "ActionName",
                table: "UserPermissionOverrides",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // Re-create index on new columns
            migrationBuilder.CreateIndex(
                name: "IX_UserPermissionOverrides_UserId_AreaId_ActionId",
                table: "UserPermissionOverrides",
                columns: new[] { "UserId", "AreaId", "ActionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ── Reverse UserPermissionOverrides ───────────────────────────────────

            migrationBuilder.DropIndex(
                name: "IX_UserPermissionOverrides_UserId_AreaId_ActionId",
                table: "UserPermissionOverrides");

            migrationBuilder.DropColumn(name: "AreaId",     table: "UserPermissionOverrides");
            migrationBuilder.DropColumn(name: "AreaName",   table: "UserPermissionOverrides");
            migrationBuilder.DropColumn(name: "ActionId",   table: "UserPermissionOverrides");
            migrationBuilder.DropColumn(name: "ActionName", table: "UserPermissionOverrides");

            migrationBuilder.AddColumn<int>(
                name: "Area",
                table: "UserPermissionOverrides",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Action",
                table: "UserPermissionOverrides",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissionOverrides_UserId_Area_Action",
                table: "UserPermissionOverrides",
                columns: new[] { "UserId", "Area", "Action" });

            // ── Reverse RolePermissions ───────────────────────────────────────────

            migrationBuilder.DropIndex(
                name: "IX_RolePermissions_RoleName_AreaId_ActionId",
                table: "RolePermissions");

            migrationBuilder.DropColumn(name: "AreaId",     table: "RolePermissions");
            migrationBuilder.DropColumn(name: "AreaName",   table: "RolePermissions");
            migrationBuilder.DropColumn(name: "ActionId",   table: "RolePermissions");
            migrationBuilder.DropColumn(name: "ActionName", table: "RolePermissions");

            migrationBuilder.AddColumn<int>(
                name: "Area",
                table: "RolePermissions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Action",
                table: "RolePermissions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleName_Area_Action",
                table: "RolePermissions",
                columns: new[] { "RoleName", "Area", "Action" });

            // ── Drop catalog tables ───────────────────────────────────────────────

            migrationBuilder.DropTable(name: "AreaPermissionMappings");
            migrationBuilder.DropTable(name: "PermissionActions");
            migrationBuilder.DropTable(name: "PermissionAreas");
        }
    }
}
