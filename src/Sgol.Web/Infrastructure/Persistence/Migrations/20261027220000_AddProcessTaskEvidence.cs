using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessTaskEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_sequence_run_closure",
                table: "operational_sequence_run");

            migrationBuilder.AddColumn<bool>(
                name: "UsesTaskEvidence",
                table: "operational_sequence_run",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_sequence_run_closure",
                table: "operational_sequence_run",
                sql: "(\"Status\" = 'EN_CURSO' AND \"ClosedAt\" IS NULL AND \"ClosedBy\" IS NULL AND \"ClosureOutcome\" IS NULL AND \"AuthorizedDocumentId\" IS NULL) OR (\"Status\" = 'CONCLUIDA' AND \"ClosedAt\" IS NOT NULL AND \"ClosedBy\" IS NOT NULL AND (\"UsesTaskEvidence\" OR \"AuthorizedDocumentId\" IS NOT NULL) AND \"ClosureOutcome\" IS NOT NULL AND \"ClosureComment\" IS NOT NULL AND \"ClosureOutcome\" IN ('SIN_DIFERENCIAS','ACEPTADA_CON_DIFERENCIAS') AND length(trim(\"ClosureComment\")) > 0) OR (\"Status\" = 'CANCELADA' AND \"ClosedAt\" IS NOT NULL AND \"ClosedBy\" IS NOT NULL AND \"ClosureOutcome\" = 'CANCELACION' AND \"ClosureOutcome\" IS NOT NULL AND \"ClosureComment\" IS NOT NULL AND length(trim(\"ClosureComment\")) > 0 AND \"AuthorizedDocumentId\" IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM operational_sequence_run WHERE \"UsesTaskEvidence\") THEN RAISE EXCEPTION 'Cannot remove task evidence mode while process executions use it'; END IF; END $$;");
            migrationBuilder.DropCheckConstraint(
                name: "CK_sequence_run_closure",
                table: "operational_sequence_run");

            migrationBuilder.DropColumn(
                name: "UsesTaskEvidence",
                table: "operational_sequence_run");

            migrationBuilder.AddCheckConstraint(
                name: "CK_sequence_run_closure",
                table: "operational_sequence_run",
                sql: "(\"Status\" = 'EN_CURSO' AND \"ClosedAt\" IS NULL AND \"ClosedBy\" IS NULL AND \"ClosureOutcome\" IS NULL AND \"AuthorizedDocumentId\" IS NULL) OR (\"Status\" = 'CONCLUIDA' AND \"ClosedAt\" IS NOT NULL AND \"ClosedBy\" IS NOT NULL AND \"AuthorizedDocumentId\" IS NOT NULL AND \"ClosureOutcome\" IS NOT NULL AND \"ClosureComment\" IS NOT NULL AND \"ClosureOutcome\" IN ('SIN_DIFERENCIAS','ACEPTADA_CON_DIFERENCIAS') AND length(trim(\"ClosureComment\")) > 0) OR (\"Status\" = 'CANCELADA' AND \"ClosedAt\" IS NOT NULL AND \"ClosedBy\" IS NOT NULL AND \"ClosureOutcome\" = 'CANCELACION' AND \"ClosureOutcome\" IS NOT NULL AND \"ClosureComment\" IS NOT NULL AND length(trim(\"ClosureComment\")) > 0 AND \"AuthorizedDocumentId\" IS NULL)");
        }
    }
}
