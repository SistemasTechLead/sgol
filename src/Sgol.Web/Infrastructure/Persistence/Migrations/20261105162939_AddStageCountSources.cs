using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStageCountSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing definitions/runs retain version 0; no operational row is reinterpreted.
            migrationBuilder.DropCheckConstraint(
                name: "CK_process_item_link",
                table: "operational_sequence_step");

            migrationBuilder.AddColumn<int>(
                name: "CountSemanticsVersion",
                table: "operational_sequence_version",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "InputCountSourceTaskId",
                table: "operational_sequence_step",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InputCountSourceTaskId",
                table: "operational_sequence_stage",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CountSemanticsVersion",
                table: "operational_sequence_run",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CountSourcesJson",
                table: "operational_process_plan_start",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_operational_sequence_stage_RunId_TaskId",
                table: "operational_sequence_stage",
                columns: new[] { "RunId", "TaskId" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_step_VersionId_InputCountSourceTaskId",
                table: "operational_sequence_step",
                columns: new[] { "VersionId", "InputCountSourceTaskId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_process_item_link",
                table: "operational_sequence_step",
                sql: "(\"OutputItemId\" IS NULL AND \"OutputTargetTaskId\" IS NULL) OR (\"OutputItemId\" IS NOT NULL AND (\"OutputTargetTaskId\" IS NULL OR \"OutputTargetTaskId\" <> \"TaskId\") AND NOT \"IsFinalAudit\")");

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_stage_RunId_InputCountSourceTaskId",
                table: "operational_sequence_stage",
                columns: new[] { "RunId", "InputCountSourceTaskId" });

            migrationBuilder.AddForeignKey(
                name: "FK_process_stage_count_source",
                table: "operational_sequence_stage",
                columns: new[] { "RunId", "InputCountSourceTaskId" },
                principalTable: "operational_sequence_stage",
                principalColumns: new[] { "RunId", "TaskId" });

            migrationBuilder.AddForeignKey(
                name: "FK_process_step_count_source",
                table: "operational_sequence_step",
                columns: new[] { "VersionId", "InputCountSourceTaskId" },
                principalTable: "operational_sequence_step",
                principalColumns: new[] { "VersionId", "TaskId" });
            migrationBuilder.Sql("""
                CREATE FUNCTION sgol_count_source_guard() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN
                  IF TG_TABLE_NAME = 'operational_sequence_stage' THEN
                    IF TG_OP = 'UPDATE' AND OLD."InputCountSourceTaskId" IS DISTINCT FROM NEW."InputCountSourceTaskId" THEN
                      RAISE EXCEPTION 'Execution count source is immutable' USING ERRCODE = '23514';
                    END IF;
                    IF NEW."InputCountSourceTaskId" IS NOT NULL AND (NEW."IsFinalAudit" OR NOT EXISTS (
                      SELECT 1 FROM operational_sequence_stage s WHERE s."RunId" = NEW."RunId" AND s."TaskId" = NEW."InputCountSourceTaskId"
                        AND s."Position" < NEW."Position" AND s."OutputItemRateId" IS NOT NULL)) THEN
                      RAISE EXCEPTION 'Count source must be an earlier producer in the same run' USING ERRCODE = '23514';
                    END IF;
                  ELSIF TG_TABLE_NAME = 'operational_process_plan_start' THEN
                    IF OLD."CountSourcesJson" IS DISTINCT FROM NEW."CountSourcesJson" THEN
                      RAISE EXCEPTION 'Prepared count sources are immutable' USING ERRCODE = '23514';
                    END IF;
                  ELSIF OLD."CountSemanticsVersion" IS DISTINCT FROM NEW."CountSemanticsVersion" THEN
                    RAISE EXCEPTION 'Execution count semantics are immutable' USING ERRCODE = '23514';
                  END IF;
                  RETURN NEW;
                END $body$;
                CREATE TRIGGER process_count_source_guard BEFORE INSERT OR UPDATE ON operational_sequence_stage FOR EACH ROW EXECUTE FUNCTION sgol_count_source_guard();
                CREATE TRIGGER process_count_plan_guard BEFORE UPDATE ON operational_process_plan_start FOR EACH ROW EXECUTE FUNCTION sgol_count_source_guard();
                CREATE TRIGGER process_count_run_guard BEFORE UPDATE ON operational_sequence_run FOR EACH ROW EXECUTE FUNCTION sgol_count_source_guard();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM operational_sequence_version WHERE \"CountSemanticsVersion\" >= 2) THEN RAISE EXCEPTION 'Stage count history prevents downgrade'; END IF; END $$;");
            migrationBuilder.Sql("DROP TRIGGER process_count_source_guard ON operational_sequence_stage; DROP TRIGGER process_count_plan_guard ON operational_process_plan_start; DROP TRIGGER process_count_run_guard ON operational_sequence_run; DROP FUNCTION sgol_count_source_guard();");
            migrationBuilder.DropForeignKey(
                name: "FK_process_stage_count_source",
                table: "operational_sequence_stage");

            migrationBuilder.DropForeignKey(
                name: "FK_process_step_count_source",
                table: "operational_sequence_step");

            migrationBuilder.DropIndex(
                name: "IX_operational_sequence_step_VersionId_InputCountSourceTaskId",
                table: "operational_sequence_step");

            migrationBuilder.DropCheckConstraint(
                name: "CK_process_item_link",
                table: "operational_sequence_step");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_operational_sequence_stage_RunId_TaskId",
                table: "operational_sequence_stage");

            migrationBuilder.DropIndex(
                name: "IX_operational_sequence_stage_RunId_InputCountSourceTaskId",
                table: "operational_sequence_stage");

            migrationBuilder.DropColumn(
                name: "CountSemanticsVersion",
                table: "operational_sequence_version");

            migrationBuilder.DropColumn(
                name: "InputCountSourceTaskId",
                table: "operational_sequence_step");

            migrationBuilder.DropColumn(
                name: "InputCountSourceTaskId",
                table: "operational_sequence_stage");

            migrationBuilder.DropColumn(
                name: "CountSemanticsVersion",
                table: "operational_sequence_run");

            migrationBuilder.DropColumn(
                name: "CountSourcesJson",
                table: "operational_process_plan_start");

            migrationBuilder.AddCheckConstraint(
                name: "CK_process_item_link",
                table: "operational_sequence_step",
                sql: "(\"OutputItemId\" IS NULL AND \"OutputTargetTaskId\" IS NULL) OR (\"OutputItemId\" IS NOT NULL AND \"OutputTargetTaskId\" IS NOT NULL AND \"OutputTargetTaskId\" <> \"TaskId\" AND NOT \"IsFinalAudit\")");
        }
    }
}
