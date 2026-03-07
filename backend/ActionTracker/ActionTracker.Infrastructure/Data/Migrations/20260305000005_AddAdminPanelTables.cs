using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActionTracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminPanelTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // -----------------------------------------------------------------
            // OrgUnits — hierarchical org structure (up to 10 levels)
            // -----------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "OrgUnits",
                columns: table => new
                {
                    Id = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: false,
                        defaultValueSql: "NEWSEQUENTIALID()"),
                    Name = table.Column<string>(
                        type: "nvarchar(200)",
                        maxLength: 200,
                        nullable: false),
                    Code = table.Column<string>(
                        type: "nvarchar(50)",
                        maxLength: 50,
                        nullable: true),
                    Description = table.Column<string>(
                        type: "nvarchar(500)",
                        maxLength: 500,
                        nullable: true),
                    Level = table.Column<int>(
                        type: "int",
                        nullable: false),
                    ParentId = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: true),
                    IsDeleted = table.Column<bool>(
                        type: "bit",
                        nullable: false,
                        defaultValue: false),
                    CreatedAt = table.Column<DateTime>(
                        type: "datetime2",
                        nullable: false,
                        defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(
                        type: "datetime2",
                        nullable: true),
                    DeletedAt = table.Column<DateTime>(
                        type: "datetime2",
                        nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgUnits", x => x.Id);
                    table.CheckConstraint("CK_OrgUnits_Level", "[Level] BETWEEN 1 AND 10");
                    table.ForeignKey(
                        name: "FK_OrgUnits_OrgUnits_ParentId",
                        column: x => x.ParentId,
                        principalTable: "OrgUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrgUnits_ParentId",
                table: "OrgUnits",
                column: "ParentId");

            // -----------------------------------------------------------------
            // StrategicObjectives — linked to an OrgUnit
            // -----------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "StrategicObjectives",
                columns: table => new
                {
                    Id = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: false,
                        defaultValueSql: "NEWSEQUENTIALID()"),
                    ObjectiveCode = table.Column<string>(
                        type: "nvarchar(20)",
                        maxLength: 20,
                        nullable: false),
                    Statement = table.Column<string>(
                        type: "nvarchar(300)",
                        maxLength: 300,
                        nullable: false),
                    Description = table.Column<string>(
                        type: "nvarchar(1000)",
                        maxLength: 1000,
                        nullable: false),
                    OrgUnitId = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: false),
                    IsDeleted = table.Column<bool>(
                        type: "bit",
                        nullable: false,
                        defaultValue: false),
                    CreatedAt = table.Column<DateTime>(
                        type: "datetime2",
                        nullable: false,
                        defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(
                        type: "datetime2",
                        nullable: true),
                    DeletedAt = table.Column<DateTime>(
                        type: "datetime2",
                        nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategicObjectives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StrategicObjectives_OrgUnits_OrgUnitId",
                        column: x => x.OrgUnitId,
                        principalTable: "OrgUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StrategicObjectives_ObjectiveCode",
                table: "StrategicObjectives",
                column: "ObjectiveCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StrategicObjectives_OrgUnitId",
                table: "StrategicObjectives",
                column: "OrgUnitId");

            // -----------------------------------------------------------------
            // Kpis — KPI per strategic objective, number auto-assigned
            // -----------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "Kpis",
                columns: table => new
                {
                    Id = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: false,
                        defaultValueSql: "NEWSEQUENTIALID()"),
                    KpiNumber = table.Column<int>(
                        type: "int",
                        nullable: false),
                    Name = table.Column<string>(
                        type: "nvarchar(300)",
                        maxLength: 300,
                        nullable: false),
                    Description = table.Column<string>(
                        type: "nvarchar(1000)",
                        maxLength: 1000,
                        nullable: false),
                    CalculationMethod = table.Column<string>(
                        type: "nvarchar(500)",
                        maxLength: 500,
                        nullable: false),
                    Period = table.Column<int>(
                        type: "int",
                        nullable: false),
                    Unit = table.Column<string>(
                        type: "nvarchar(50)",
                        maxLength: 50,
                        nullable: true),
                    StrategicObjectiveId = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: false),
                    IsDeleted = table.Column<bool>(
                        type: "bit",
                        nullable: false,
                        defaultValue: false),
                    CreatedAt = table.Column<DateTime>(
                        type: "datetime2",
                        nullable: false,
                        defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(
                        type: "datetime2",
                        nullable: true),
                    DeletedAt = table.Column<DateTime>(
                        type: "datetime2",
                        nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kpis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Kpis_StrategicObjectives_StrategicObjectiveId",
                        column: x => x.StrategicObjectiveId,
                        principalTable: "StrategicObjectives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Kpis_StrategicObjectiveId_KpiNumber",
                table: "Kpis",
                columns: new[] { "StrategicObjectiveId", "KpiNumber" },
                unique: true);

            // -----------------------------------------------------------------
            // KpiTargets — monthly target + actual per KPI / year
            // -----------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "KpiTargets",
                columns: table => new
                {
                    Id = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: false,
                        defaultValueSql: "NEWSEQUENTIALID()"),
                    KpiId = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: false),
                    Year = table.Column<int>(
                        type: "int",
                        nullable: false),
                    Month = table.Column<int>(
                        type: "int",
                        nullable: false),
                    Target = table.Column<decimal>(
                        type: "decimal(18,4)",
                        nullable: true),
                    Actual = table.Column<decimal>(
                        type: "decimal(18,4)",
                        nullable: true),
                    Notes = table.Column<string>(
                        type: "nvarchar(500)",
                        maxLength: 500,
                        nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KpiTargets", x => x.Id);
                    table.CheckConstraint("CK_KpiTargets_Month", "[Month] BETWEEN 1 AND 12");
                    table.ForeignKey(
                        name: "FK_KpiTargets_Kpis_KpiId",
                        column: x => x.KpiId,
                        principalTable: "Kpis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KpiTargets_KpiId_Year_Month",
                table: "KpiTargets",
                columns: new[] { "KpiId", "Year", "Month" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "KpiTargets");
            migrationBuilder.DropTable(name: "Kpis");
            migrationBuilder.DropTable(name: "StrategicObjectives");
            migrationBuilder.DropTable(name: "OrgUnits");
        }
    }
}
