using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessItemIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION sgol_process_item_immutable() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN
                  IF TG_TABLE_NAME = 'process_item_rate' THEN
                    RAISE EXCEPTION 'Item rate revisions are immutable' USING ERRCODE = '23514';
                  ELSIF TG_TABLE_NAME = 'operational_sequence_stage' THEN
                    IF ROW(OLD."OutputItemRateId",OLD."OutputItemName",OLD."OutputItemMinutes",OLD."IsFinalAudit") IS DISTINCT FROM ROW(NEW."OutputItemRateId",NEW."OutputItemName",NEW."OutputItemMinutes",NEW."IsFinalAudit") THEN
                      RAISE EXCEPTION 'Execution item snapshots are immutable' USING ERRCODE = '23514';
                    END IF;
                  ELSIF TG_TABLE_NAME = 'operational_process_plan_start' THEN
                    IF OLD."ItemRatesJson" IS DISTINCT FROM NEW."ItemRatesJson" THEN RAISE EXCEPTION 'Prepared item snapshots are immutable' USING ERRCODE = '23514'; END IF;
                  ELSIF OLD."ItemQuantity" IS NOT NULL AND ROW(OLD."ItemQuantity",OLD."OmittedStagesJson") IS DISTINCT FROM ROW(NEW."ItemQuantity",NEW."OmittedStagesJson") THEN
                    RAISE EXCEPTION 'Delivered item counts are immutable; return for correction' USING ERRCODE = '23514';
                  END IF;
                  RETURN NEW;
                END $body$;
                CREATE TRIGGER process_item_rate_immutable BEFORE UPDATE OR DELETE ON process_item_rate FOR EACH ROW EXECUTE FUNCTION sgol_process_item_immutable();
                CREATE TRIGGER process_item_stage_snapshot BEFORE UPDATE ON operational_sequence_stage FOR EACH ROW EXECUTE FUNCTION sgol_process_item_immutable();
                CREATE TRIGGER process_item_prepared_snapshot BEFORE UPDATE ON operational_process_plan_start FOR EACH ROW EXECUTE FUNCTION sgol_process_item_immutable();
                CREATE TRIGGER process_item_count_immutable BEFORE UPDATE ON operational_sequence_attempt FOR EACH ROW EXECUTE FUNCTION sgol_process_item_immutable();
                """);
            migrationBuilder.AddUniqueConstraint(
                name: "AK_operational_sequence_step_VersionId_TaskId",
                table: "operational_sequence_step",
                columns: new[] { "VersionId", "TaskId" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_step_VersionId",
                table: "operational_sequence_step",
                column: "VersionId",
                unique: true,
                filter: "\"IsFinalAudit\"");

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_step_VersionId_OutputTargetTaskId",
                table: "operational_sequence_step",
                columns: new[] { "VersionId", "OutputTargetTaskId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_process_item_link",
                table: "operational_sequence_step",
                sql: "(\"OutputItemId\" IS NULL AND \"OutputTargetTaskId\" IS NULL) OR (\"OutputItemId\" IS NOT NULL AND \"OutputTargetTaskId\" IS NOT NULL AND \"OutputTargetTaskId\" <> \"TaskId\" AND NOT \"IsFinalAudit\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_process_item_result",
                table: "operational_sequence_stage",
                sql: "(\"CalculatedLoadMinutes\" IS NULL AND \"LoadSourceAttemptId\" IS NULL) OR (\"CalculatedLoadMinutes\" IS NOT NULL AND \"CalculatedLoadMinutes\" > 0 AND \"LoadSourceAttemptId\" IS NOT NULL AND (\"CalculatedLoadMinutes\" > 4800 OR \"LoadMinutes\" = \"CalculatedLoadMinutes\"))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_process_item_snapshot",
                table: "operational_sequence_stage",
                sql: "(\"OutputItemRateId\" IS NULL AND \"OutputItemName\" IS NULL AND \"OutputItemMinutes\" IS NULL) OR (\"OutputItemRateId\" IS NOT NULL AND \"OutputItemName\" IS NOT NULL AND \"OutputItemMinutes\" IS NOT NULL AND \"OutputItemMinutes\" > 0 AND NOT \"IsFinalAudit\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_process_item_continuation",
                table: "operational_sequence_attempt",
                sql: "\"ContinuationAttemptId\" IS NULL OR \"ContinuationAttemptId\" <> \"Id\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_process_item_quantity",
                table: "operational_sequence_attempt",
                sql: "\"ItemQuantity\" IS NULL OR \"ItemQuantity\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_process_item_resolution",
                table: "operational_sequence_attempt",
                sql: "\"CapacityResolution\" IS NULL OR \"CapacityResolution\" IN ('','PENDING','LOWEST','FORCE')");

            migrationBuilder.AddForeignKey(
                name: "FK_operational_sequence_step_operational_sequence_step_Version~",
                table: "operational_sequence_step",
                columns: new[] { "VersionId", "OutputTargetTaskId" },
                principalTable: "operational_sequence_step",
                principalColumns: new[] { "VersionId", "TaskId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER process_item_rate_immutable ON process_item_rate; DROP TRIGGER process_item_stage_snapshot ON operational_sequence_stage; DROP TRIGGER process_item_prepared_snapshot ON operational_process_plan_start; DROP TRIGGER process_item_count_immutable ON operational_sequence_attempt; DROP FUNCTION sgol_process_item_immutable();");
            migrationBuilder.DropForeignKey(
                name: "FK_operational_sequence_step_operational_sequence_step_Version~",
                table: "operational_sequence_step");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_operational_sequence_step_VersionId_TaskId",
                table: "operational_sequence_step");

            migrationBuilder.DropIndex(
                name: "IX_operational_sequence_step_VersionId",
                table: "operational_sequence_step");

            migrationBuilder.DropIndex(
                name: "IX_operational_sequence_step_VersionId_OutputTargetTaskId",
                table: "operational_sequence_step");

            migrationBuilder.DropCheckConstraint(
                name: "CK_process_item_link",
                table: "operational_sequence_step");

            migrationBuilder.DropCheckConstraint(
                name: "CK_process_item_result",
                table: "operational_sequence_stage");

            migrationBuilder.DropCheckConstraint(
                name: "CK_process_item_snapshot",
                table: "operational_sequence_stage");

            migrationBuilder.DropCheckConstraint(
                name: "CK_process_item_continuation",
                table: "operational_sequence_attempt");

            migrationBuilder.DropCheckConstraint(
                name: "CK_process_item_quantity",
                table: "operational_sequence_attempt");

            migrationBuilder.DropCheckConstraint(
                name: "CK_process_item_resolution",
                table: "operational_sequence_attempt");
        }
    }
}
