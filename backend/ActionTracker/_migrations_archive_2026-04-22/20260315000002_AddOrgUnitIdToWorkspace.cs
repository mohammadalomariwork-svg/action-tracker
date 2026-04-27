using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActionTracker.Infrastructure.Data.Migrations
{
    public partial class AddOrgUnitIdToWorkspace : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrgUnitId",
                table: "Workspaces",
                type: "uniqueidentifier",
                nullable: true);

            // Backfill OrgUnitId for existing workspaces by matching OrganizationUnit name
            // to the OrgUnits table. If the name appears at multiple levels, pick the
            // shallowest (lowest Level value) to avoid ambiguity.
            migrationBuilder.Sql(@"
                UPDATE w
                SET w.OrgUnitId = (
                    SELECT TOP 1 o.Id
                    FROM OrgUnits o
                    WHERE o.Name = w.OrganizationUnit
                      AND o.IsDeleted = 0
                    ORDER BY o.Level ASC
                )
                FROM Workspaces w
                WHERE w.OrgUnitId IS NULL
                  AND w.OrganizationUnit IS NOT NULL
                  AND w.OrganizationUnit <> ''
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrgUnitId",
                table: "Workspaces");
        }
    }
}
