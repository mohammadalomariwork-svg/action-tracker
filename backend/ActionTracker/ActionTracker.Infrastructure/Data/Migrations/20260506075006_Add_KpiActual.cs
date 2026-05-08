using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActionTracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_KpiActual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KpiActuals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    KpiTargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodType = table.Column<int>(type: "int", nullable: false),
                    PeriodIndex = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    ActualValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    SubmittedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SubmittedByDisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssignedReviewerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AssignedReviewerDisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReviewedByDisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewerComment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KpiActuals", x => x.Id);
                    table.CheckConstraint("CK_KpiActuals_PeriodIndex", "([PeriodType] = 0 AND [PeriodIndex] BETWEEN 1 AND 4) OR ([PeriodType] = 1 AND [PeriodIndex] BETWEEN 1 AND 2) OR ([PeriodType] = 2 AND [PeriodIndex] = 1)");
                    table.ForeignKey(
                        name: "FK_KpiActuals_KpiTargets_KpiTargetId",
                        column: x => x.KpiTargetId,
                        principalTable: "KpiTargets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KpiActuals_AssignedReviewerUserId_Status",
                table: "KpiActuals",
                columns: new[] { "AssignedReviewerUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_KpiActuals_KpiTargetId_Year",
                table: "KpiActuals",
                columns: new[] { "KpiTargetId", "Year" });

            migrationBuilder.CreateIndex(
                name: "IX_KpiActuals_TargetPeriod_NonRejected",
                table: "KpiActuals",
                columns: new[] { "KpiTargetId", "PeriodType", "PeriodIndex", "Year" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Status] <> 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KpiActuals");
        }
    }
}
