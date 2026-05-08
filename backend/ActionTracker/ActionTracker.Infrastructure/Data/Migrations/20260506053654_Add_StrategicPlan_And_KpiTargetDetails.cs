using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActionTracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_StrategicPlan_And_KpiTargetDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StrategicPlanId",
                table: "StrategicObjectives",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Baseline",
                table: "KpiTargets",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CalculationNotes",
                table: "KpiTargets",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataSource",
                table: "KpiTargets",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Direction",
                table: "KpiTargets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "StrategicPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    OrgUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartYear = table.Column<int>(type: "int", nullable: false),
                    DurationYears = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategicPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StrategicPlans_OrgUnits_OrgUnitId",
                        column: x => x.OrgUnitId,
                        principalTable: "OrgUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StrategicObjectives_StrategicPlanId",
                table: "StrategicObjectives",
                column: "StrategicPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_StrategicPlans_OrgUnitId_ActiveUnique",
                table: "StrategicPlans",
                column: "OrgUnitId",
                unique: true,
                filter: "[IsDeleted] = 0 AND [Status] IN (1, 2)");

            migrationBuilder.AddForeignKey(
                name: "FK_StrategicObjectives_StrategicPlans_StrategicPlanId",
                table: "StrategicObjectives",
                column: "StrategicPlanId",
                principalTable: "StrategicPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ── Data backfill (CR-001-A) ──────────────────────────────────────
            // Insert one "Legacy Strategy" StrategicPlan per OrgUnit that already
            // has at least one StrategicObjective, then point those objectives at
            // the matching legacy plan. Backfill rows are flagged by their name
            // pattern so the Down() method can reverse them cleanly.
            //
            // StrategicPlanId on StrategicObjective remains nullable in this
            // migration window. A follow-up migration can tighten it to NOT NULL
            // once any data-clean-up is complete.
            migrationBuilder.Sql(@"
INSERT INTO [StrategicPlans]
    ([Id], [OrgUnitId], [Name], [StartYear], [DurationYears], [Status],
     [ApprovedAtUtc], [IsDeleted], [CreatedAt], [CreatedBy])
SELECT
    NEWID(),
    o.[OrgUnitId],
    CONCAT(ou.[Name], N' — Legacy Strategy'),
    YEAR(GETUTCDATE()),
    1,
    2, -- StrategicPlanStatus.Approved
    GETUTCDATE(),
    0,
    GETUTCDATE(),
    NULL
FROM (SELECT DISTINCT [OrgUnitId] FROM [StrategicObjectives]) o
INNER JOIN [OrgUnits] ou ON ou.[Id] = o.[OrgUnitId]
WHERE NOT EXISTS (
    SELECT 1 FROM [StrategicPlans] sp
    WHERE sp.[OrgUnitId] = o.[OrgUnitId]
      AND sp.[IsDeleted] = 0
      AND sp.[Status] IN (1, 2)
);
");

            migrationBuilder.Sql(@"
UPDATE so
SET so.[StrategicPlanId] = sp.[Id]
FROM [StrategicObjectives] so
INNER JOIN [StrategicPlans] sp
    ON sp.[OrgUnitId] = so.[OrgUnitId]
   AND sp.[IsDeleted] = 0
   AND sp.[Status] = 2
   AND sp.[Name] LIKE N'%— Legacy Strategy'
WHERE so.[StrategicPlanId] IS NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse the legacy backfill before dropping the schema.
            migrationBuilder.Sql(@"
UPDATE so
SET so.[StrategicPlanId] = NULL
FROM [StrategicObjectives] so
INNER JOIN [StrategicPlans] sp
    ON sp.[Id] = so.[StrategicPlanId]
WHERE sp.[Name] LIKE N'%— Legacy Strategy'
  AND sp.[DurationYears] = 1
  AND sp.[CreatedBy] IS NULL;
");

            migrationBuilder.Sql(@"
DELETE FROM [StrategicPlans]
WHERE [Name] LIKE N'%— Legacy Strategy'
  AND [DurationYears] = 1
  AND [CreatedBy] IS NULL;
");

            migrationBuilder.DropForeignKey(
                name: "FK_StrategicObjectives_StrategicPlans_StrategicPlanId",
                table: "StrategicObjectives");

            migrationBuilder.DropTable(
                name: "StrategicPlans");

            migrationBuilder.DropIndex(
                name: "IX_StrategicObjectives_StrategicPlanId",
                table: "StrategicObjectives");

            migrationBuilder.DropColumn(
                name: "StrategicPlanId",
                table: "StrategicObjectives");

            migrationBuilder.DropColumn(
                name: "Baseline",
                table: "KpiTargets");

            migrationBuilder.DropColumn(
                name: "CalculationNotes",
                table: "KpiTargets");

            migrationBuilder.DropColumn(
                name: "DataSource",
                table: "KpiTargets");

            migrationBuilder.DropColumn(
                name: "Direction",
                table: "KpiTargets");
        }
    }
}
