using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActionTracker.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Creates dbo.ObjectiveCodeSeq, the atomic source of strategic-objective
    /// code numbers (replaces the race- and delete-fragile COUNT(*)+1 logic in
    /// StrategicObjectiveService). The start value is computed AT APPLY TIME
    /// from the highest existing "SO-n" code, so already-issued codes are
    /// never re-issued. Existing rows are not read-modified in any way.
    /// Idempotent: skips creation if the sequence already exists (e.g. it was
    /// pre-created manually before this migration ran).
    /// </summary>
    public partial class Add_ObjectiveCodeSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.sequences
               WHERE name = 'ObjectiveCodeSeq' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    DECLARE @start BIGINT;
    SELECT @start = ISNULL(MAX(TRY_CAST(SUBSTRING(ObjectiveCode, 4, 20) AS INT)), 0) + 1
    FROM StrategicObjectives
    WHERE ObjectiveCode LIKE 'SO-%';

    DECLARE @sql NVARCHAR(400) =
        N'CREATE SEQUENCE dbo.ObjectiveCodeSeq AS INT START WITH '
        + CAST(@start AS NVARCHAR(20))
        + N' INCREMENT BY 1 NO CYCLE NO CACHE;';
    EXEC sp_executesql @sql;
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP SEQUENCE IF EXISTS dbo.ObjectiveCodeSeq;");
        }
    }
}
