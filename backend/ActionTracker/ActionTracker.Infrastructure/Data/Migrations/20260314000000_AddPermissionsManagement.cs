using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActionTracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionsManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    RoleName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Area = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    OrgUnitScope = table.Column<int>(type: "int", nullable: false),
                    OrgUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrgUnitName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserPermissionOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UserDisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Area = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    OrgUnitScope = table.Column<int>(type: "int", nullable: false),
                    OrgUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrgUnitName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsGranted = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPermissionOverrides", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleName_Area_Action",
                table: "RolePermissions",
                columns: new[] { "RoleName", "Area", "Action" });

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissionOverrides_UserId_Area_Action",
                table: "UserPermissionOverrides",
                columns: new[] { "UserId", "Area", "Action" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "UserPermissionOverrides");
            migrationBuilder.DropTable(name: "RolePermissions");
        }
    }
}
