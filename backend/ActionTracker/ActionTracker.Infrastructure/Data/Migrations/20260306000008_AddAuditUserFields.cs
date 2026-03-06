using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActionTracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditUserFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "StrategicObjectives",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "StrategicObjectives",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "StrategicObjectives",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "OrgUnits",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "OrgUnits",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "OrgUnits",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Kpis",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Kpis",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Kpis",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "StrategicObjectives");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "StrategicObjectives");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "StrategicObjectives");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "OrgUnits");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "OrgUnits");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "OrgUnits");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Kpis");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Kpis");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Kpis");
        }
    }
}
