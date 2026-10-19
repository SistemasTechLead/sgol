using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PreserveEvidenceFileCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "file_count",
                table: "evidence_version",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE evidence_version v SET file_count =
                    (SELECT count(*) FROM evidence_version_file f WHERE f.evidence_version_id = v.id)
                WHERE v.file_object_id IS NOT NULL;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_evidence_version_file_count",
                table: "evidence_version",
                sql: "(file_object_id IS NULL AND file_count = 0) OR (file_object_id IS NOT NULL AND file_count BETWEEN 1 AND 5)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_evidence_version_file_count",
                table: "evidence_version");

            migrationBuilder.DropColumn(
                name: "file_count",
                table: "evidence_version");
        }
    }
}
