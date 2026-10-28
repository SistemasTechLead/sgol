using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgendaReflowPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_scheduling_policy",
                table: "operational_scheduling_policy");

            migrationBuilder.DropCheckConstraint(
                name: "CK_assignment_scheduling",
                table: "operational_assignment");

            migrationBuilder.AddCheckConstraint(
                name: "CK_scheduling_policy",
                table: "operational_scheduling_policy",
                sql: "\"MarginPercent\" BETWEEN 0 AND 500 AND \"Revision\" > 0 AND \"FormulaVersion\" = 1 AND \"AgendaVersion\" IN (1,2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_assignment_scheduling",
                table: "operational_assignment",
                sql: "(\"SchedulingPolicyId\" IS NULL AND \"ClosureMarginPercent\" IS NULL AND \"NominalStartsAt\" IS NULL) OR (\"SchedulingPolicyId\" IS NOT NULL AND \"ClosureMarginPercent\" IS NOT NULL AND \"ClosureMarginPercent\" BETWEEN 0 AND 500 AND \"NominalStartsAt\" IS NOT NULL AND (\"StartsAt\" >= \"NominalStartsAt\" OR \"OperationalCriticality\" = 'FLEXIBLE') AND \"OperationalCriticality\" IS NOT NULL AND \"OperationalCriticality\" IN ('CRITICA','IMPORTANTE','FLEXIBLE'))");
            migrationBuilder.Sql("""
                INSERT INTO operational_scheduling_policy ("Id", "Revision", "MarginPercent", "FormulaVersion", "AgendaVersion", "ChangedAt")
                SELECT 'b06ff0ad-6160-438a-a847-03201b541e72'::uuid, s."Revision" + 1, p."MarginPercent", 1, 2, CURRENT_TIMESTAMP
                FROM operational_scheduling_settings s JOIN operational_scheduling_policy p ON p."Id" = s."CurrentPolicyId" WHERE s."Id" = 1;
                UPDATE operational_scheduling_settings SET "CurrentPolicyId" = 'b06ff0ad-6160-438a-a847-03201b541e72'::uuid, "Revision" = "Revision" + 1 WHERE "Id" = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM operational_scheduling_policy WHERE \"AgendaVersion\" = 2) THEN RAISE EXCEPTION 'Published agenda policies are immutable; restore the verified backup'; END IF; END $$;");
            migrationBuilder.DropCheckConstraint(
                name: "CK_scheduling_policy",
                table: "operational_scheduling_policy");

            migrationBuilder.DropCheckConstraint(
                name: "CK_assignment_scheduling",
                table: "operational_assignment");

            migrationBuilder.AddCheckConstraint(
                name: "CK_scheduling_policy",
                table: "operational_scheduling_policy",
                sql: "\"MarginPercent\" BETWEEN 0 AND 500 AND \"Revision\" > 0 AND \"FormulaVersion\" = 1 AND \"AgendaVersion\" = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_assignment_scheduling",
                table: "operational_assignment",
                sql: "(\"SchedulingPolicyId\" IS NULL AND \"ClosureMarginPercent\" IS NULL AND \"NominalStartsAt\" IS NULL) OR (\"SchedulingPolicyId\" IS NOT NULL AND \"ClosureMarginPercent\" IS NOT NULL AND \"ClosureMarginPercent\" BETWEEN 0 AND 500 AND \"NominalStartsAt\" IS NOT NULL AND \"StartsAt\" >= \"NominalStartsAt\" AND \"OperationalCriticality\" IS NOT NULL AND \"OperationalCriticality\" IN ('CRITICA','IMPORTANTE','FLEXIBLE'))");
        }
    }
}
