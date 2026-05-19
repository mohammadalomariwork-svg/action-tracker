using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActionTracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_StrategicPlan_Drafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDraft",
                table: "StrategicPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "OriginalPlanId",
                table: "StrategicPlans",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDraft",
                table: "StrategicObjectives",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "OriginalObjectiveId",
                table: "StrategicObjectives",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDraft",
                table: "KpiTargets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "OriginalKpiTargetId",
                table: "KpiTargets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDraft",
                table: "Kpis",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "OriginalKpiId",
                table: "Kpis",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StrategicPlans_OriginalPlanId",
                table: "StrategicPlans",
                column: "OriginalPlanId",
                unique: true,
                filter: "[OriginalPlanId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StrategicObjectives_OriginalObjectiveId",
                table: "StrategicObjectives",
                column: "OriginalObjectiveId");

            migrationBuilder.CreateIndex(
                name: "IX_KpiTargets_OriginalKpiTargetId",
                table: "KpiTargets",
                column: "OriginalKpiTargetId");

            migrationBuilder.CreateIndex(
                name: "IX_Kpis_OriginalKpiId",
                table: "Kpis",
                column: "OriginalKpiId");

            migrationBuilder.AddForeignKey(
                name: "FK_Kpis_Kpis_OriginalKpiId",
                table: "Kpis",
                column: "OriginalKpiId",
                principalTable: "Kpis",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_KpiTargets_KpiTargets_OriginalKpiTargetId",
                table: "KpiTargets",
                column: "OriginalKpiTargetId",
                principalTable: "KpiTargets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StrategicObjectives_StrategicObjectives_OriginalObjectiveId",
                table: "StrategicObjectives",
                column: "OriginalObjectiveId",
                principalTable: "StrategicObjectives",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StrategicPlans_StrategicPlans_OriginalPlanId",
                table: "StrategicPlans",
                column: "OriginalPlanId",
                principalTable: "StrategicPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Kpis_Kpis_OriginalKpiId",
                table: "Kpis");

            migrationBuilder.DropForeignKey(
                name: "FK_KpiTargets_KpiTargets_OriginalKpiTargetId",
                table: "KpiTargets");

            migrationBuilder.DropForeignKey(
                name: "FK_StrategicObjectives_StrategicObjectives_OriginalObjectiveId",
                table: "StrategicObjectives");

            migrationBuilder.DropForeignKey(
                name: "FK_StrategicPlans_StrategicPlans_OriginalPlanId",
                table: "StrategicPlans");

            migrationBuilder.DropIndex(
                name: "IX_StrategicPlans_OriginalPlanId",
                table: "StrategicPlans");

            migrationBuilder.DropIndex(
                name: "IX_StrategicObjectives_OriginalObjectiveId",
                table: "StrategicObjectives");

            migrationBuilder.DropIndex(
                name: "IX_KpiTargets_OriginalKpiTargetId",
                table: "KpiTargets");

            migrationBuilder.DropIndex(
                name: "IX_Kpis_OriginalKpiId",
                table: "Kpis");

            migrationBuilder.DropColumn(
                name: "IsDraft",
                table: "StrategicPlans");

            migrationBuilder.DropColumn(
                name: "OriginalPlanId",
                table: "StrategicPlans");

            migrationBuilder.DropColumn(
                name: "IsDraft",
                table: "StrategicObjectives");

            migrationBuilder.DropColumn(
                name: "OriginalObjectiveId",
                table: "StrategicObjectives");

            migrationBuilder.DropColumn(
                name: "IsDraft",
                table: "KpiTargets");

            migrationBuilder.DropColumn(
                name: "OriginalKpiTargetId",
                table: "KpiTargets");

            migrationBuilder.DropColumn(
                name: "IsDraft",
                table: "Kpis");

            migrationBuilder.DropColumn(
                name: "OriginalKpiId",
                table: "Kpis");
        }
    }
}
