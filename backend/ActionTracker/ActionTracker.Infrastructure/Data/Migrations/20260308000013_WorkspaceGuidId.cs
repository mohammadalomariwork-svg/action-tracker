using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActionTracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class WorkspaceGuidId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Drop FK constraints that reference Workspaces.Id ──────────
            migrationBuilder.DropForeignKey(
                name: "FK_WorkspaceAdmins_Workspaces_WorkspaceId",
                table: "WorkspaceAdmins");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Workspaces_WorkspaceId",
                table: "Projects");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectActionItems_Workspaces_WorkspaceId",
                table: "ProjectActionItems");

            // ── 2. Drop composite indexes that include WorkspaceId FK columns ─
            migrationBuilder.DropIndex(
                name: "IX_WorkspaceAdmins_WorkspaceId",
                table: "WorkspaceAdmins");

            migrationBuilder.DropIndex(
                name: "IX_Projects_WorkspaceId_Status_IsActive",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_ProjectActionItems_WorkspaceId_ProjectId_MilestoneId_Status",
                table: "ProjectActionItems");

            // ── 3. Convert Workspaces.Id from int to uniqueidentifier ─────────
            //
            // SQL Server cannot ALTER a PK column in-place, so we:
            //   a) Add a new GUID column
            //   b) Populate it with NEWID() for existing rows
            //   c) Drop the PK + identity column
            //   d) Rename the new column to Id and add PK

            migrationBuilder.Sql(@"
                ALTER TABLE Workspaces ADD Id_New uniqueidentifier NOT NULL DEFAULT NEWID();
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE Workspaces DROP CONSTRAINT PK_Workspaces;
                ALTER TABLE Workspaces DROP COLUMN Id;
                EXEC sp_rename 'Workspaces.Id_New', 'Id', 'COLUMN';
                ALTER TABLE Workspaces ADD CONSTRAINT PK_Workspaces PRIMARY KEY (Id);
            ");

            // ── 4. Convert WorkspaceAdmins.WorkspaceId from int to uniqueidentifier
            //
            // Since the old FK rows no longer match (different types after step 3),
            // and WorkspaceAdmins.WorkspaceId values referred to old int IDs
            // (which are now gone), we clear the column and reset it:
            migrationBuilder.Sql(@"
                ALTER TABLE WorkspaceAdmins ADD WorkspaceId_New uniqueidentifier NULL;
                UPDATE WorkspaceAdmins SET WorkspaceId_New = NULL;
                ALTER TABLE WorkspaceAdmins DROP COLUMN WorkspaceId;
                EXEC sp_rename 'WorkspaceAdmins.WorkspaceId_New', 'WorkspaceId', 'COLUMN';
                ALTER TABLE WorkspaceAdmins ALTER COLUMN WorkspaceId uniqueidentifier NOT NULL;
            ");

            // ── 5. Convert Projects.WorkspaceId from int to uniqueidentifier ──
            migrationBuilder.Sql(@"
                ALTER TABLE Projects ADD WorkspaceId_New uniqueidentifier NULL;
                UPDATE Projects SET WorkspaceId_New = NULL;
                ALTER TABLE Projects DROP COLUMN WorkspaceId;
                EXEC sp_rename 'Projects.WorkspaceId_New', 'WorkspaceId', 'COLUMN';
                ALTER TABLE Projects ALTER COLUMN WorkspaceId uniqueidentifier NOT NULL;
            ");

            // ── 6. Convert ProjectActionItems.WorkspaceId from int to uniqueidentifier
            migrationBuilder.Sql(@"
                ALTER TABLE ProjectActionItems ADD WorkspaceId_New uniqueidentifier NULL;
                UPDATE ProjectActionItems SET WorkspaceId_New = NULL;
                ALTER TABLE ProjectActionItems DROP COLUMN WorkspaceId;
                EXEC sp_rename 'ProjectActionItems.WorkspaceId_New', 'WorkspaceId', 'COLUMN';
                ALTER TABLE ProjectActionItems ALTER COLUMN WorkspaceId uniqueidentifier NOT NULL;
            ");

            // ── 7. Re-add FK constraints ──────────────────────────────────────
            migrationBuilder.AddForeignKey(
                name: "FK_WorkspaceAdmins_Workspaces_WorkspaceId",
                table: "WorkspaceAdmins",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Workspaces_WorkspaceId",
                table: "Projects",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectActionItems_Workspaces_WorkspaceId",
                table: "ProjectActionItems",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ── 8. Re-create indexes ──────────────────────────────────────────
            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceAdmins_WorkspaceId",
                table: "WorkspaceAdmins",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_WorkspaceId_Status_IsActive",
                table: "Projects",
                columns: new[] { "WorkspaceId", "Status", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectActionItems_WorkspaceId_ProjectId_MilestoneId_Status",
                table: "ProjectActionItems",
                columns: new[] { "WorkspaceId", "ProjectId", "MilestoneId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop FK constraints
            migrationBuilder.DropForeignKey(
                name: "FK_WorkspaceAdmins_Workspaces_WorkspaceId",
                table: "WorkspaceAdmins");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Workspaces_WorkspaceId",
                table: "Projects");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectActionItems_Workspaces_WorkspaceId",
                table: "ProjectActionItems");

            // Drop indexes
            migrationBuilder.DropIndex(
                name: "IX_WorkspaceAdmins_WorkspaceId",
                table: "WorkspaceAdmins");

            migrationBuilder.DropIndex(
                name: "IX_Projects_WorkspaceId_Status_IsActive",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_ProjectActionItems_WorkspaceId_ProjectId_MilestoneId_Status",
                table: "ProjectActionItems");

            // Revert Workspaces.Id to int identity
            migrationBuilder.Sql(@"
                ALTER TABLE Workspaces ADD Id_Old int IDENTITY(1,1) NOT NULL;
                ALTER TABLE Workspaces DROP CONSTRAINT PK_Workspaces;
                ALTER TABLE Workspaces DROP COLUMN Id;
                EXEC sp_rename 'Workspaces.Id_Old', 'Id', 'COLUMN';
                ALTER TABLE Workspaces ADD CONSTRAINT PK_Workspaces PRIMARY KEY (Id);
            ");

            // Revert WorkspaceAdmins.WorkspaceId
            migrationBuilder.Sql(@"
                ALTER TABLE WorkspaceAdmins ADD WorkspaceId_Old int NOT NULL DEFAULT 0;
                ALTER TABLE WorkspaceAdmins DROP COLUMN WorkspaceId;
                EXEC sp_rename 'WorkspaceAdmins.WorkspaceId_Old', 'WorkspaceId', 'COLUMN';
            ");

            // Revert Projects.WorkspaceId
            migrationBuilder.Sql(@"
                ALTER TABLE Projects ADD WorkspaceId_Old int NOT NULL DEFAULT 0;
                ALTER TABLE Projects DROP COLUMN WorkspaceId;
                EXEC sp_rename 'Projects.WorkspaceId_Old', 'WorkspaceId', 'COLUMN';
            ");

            // Revert ProjectActionItems.WorkspaceId
            migrationBuilder.Sql(@"
                ALTER TABLE ProjectActionItems ADD WorkspaceId_Old int NOT NULL DEFAULT 0;
                ALTER TABLE ProjectActionItems DROP COLUMN WorkspaceId;
                EXEC sp_rename 'ProjectActionItems.WorkspaceId_Old', 'WorkspaceId', 'COLUMN';
            ");

            // Re-add FK constraints
            migrationBuilder.AddForeignKey(
                name: "FK_WorkspaceAdmins_Workspaces_WorkspaceId",
                table: "WorkspaceAdmins",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Workspaces_WorkspaceId",
                table: "Projects",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectActionItems_Workspaces_WorkspaceId",
                table: "ProjectActionItems",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Re-create indexes
            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceAdmins_WorkspaceId",
                table: "WorkspaceAdmins",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_WorkspaceId_Status_IsActive",
                table: "Projects",
                columns: new[] { "WorkspaceId", "Status", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectActionItems_WorkspaceId_ProjectId_MilestoneId_Status",
                table: "ProjectActionItems",
                columns: new[] { "WorkspaceId", "ProjectId", "MilestoneId", "Status" });
        }
    }
}
