using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActionTracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceAdmins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1 ── Create the WorkspaceAdmins table
            migrationBuilder.CreateTable(
                name: "WorkspaceAdmins",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkspaceId = table.Column<int>(type: "int", nullable: false),
                    AdminUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    AdminUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceAdmins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkspaceAdmins_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // 2 ── Migrate existing single-admin data from Workspaces columns
            migrationBuilder.Sql(@"
                INSERT INTO WorkspaceAdmins (WorkspaceId, AdminUserId, AdminUserName)
                SELECT Id, AdminUserId, AdminUserName
                FROM   Workspaces
                WHERE  AdminUserId IS NOT NULL AND AdminUserId <> ''
            ");

            // 3 ── Drop the old index on Workspaces.AdminUserId
            migrationBuilder.DropIndex(
                name: "IX_Workspaces_AdminUserId",
                table: "Workspaces");

            // 4 ── Drop old admin columns from Workspaces
            migrationBuilder.DropColumn(name: "AdminUserId",   table: "Workspaces");
            migrationBuilder.DropColumn(name: "AdminUserName", table: "Workspaces");

            // 5 ── Create indexes on WorkspaceAdmins
            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceAdmins_WorkspaceId",
                table: "WorkspaceAdmins",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceAdmins_AdminUserId",
                table: "WorkspaceAdmins",
                column: "AdminUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop indexes then table
            migrationBuilder.DropTable(name: "WorkspaceAdmins");

            // Re-add old columns
            migrationBuilder.AddColumn<string>(
                name: "AdminUserId",
                table: "Workspaces",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AdminUserName",
                table: "Workspaces",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            // Re-create index
            migrationBuilder.CreateIndex(
                name: "IX_Workspaces_AdminUserId",
                table: "Workspaces",
                column: "AdminUserId");
        }
    }
}
