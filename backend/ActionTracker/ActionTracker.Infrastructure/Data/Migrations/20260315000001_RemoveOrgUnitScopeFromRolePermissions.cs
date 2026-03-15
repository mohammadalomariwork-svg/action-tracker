using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActionTracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOrgUnitScopeFromRolePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrgUnitId",
                table: "RolePermissions");

            migrationBuilder.DropColumn(
                name: "OrgUnitName",
                table: "RolePermissions");

            migrationBuilder.DropColumn(
                name: "OrgUnitScope",
                table: "RolePermissions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrgUnitId",
                table: "RolePermissions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrgUnitName",
                table: "RolePermissions",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrgUnitScope",
                table: "RolePermissions",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
