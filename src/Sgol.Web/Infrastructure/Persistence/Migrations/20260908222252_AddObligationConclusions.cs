using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddObligationConclusions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "execution_result",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    obligation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    result_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    result_payload = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    evidence_review_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_execution_result", x => x.id);
                    table.CheckConstraint("CK_execution_result_code", "result_code = 'CONCLUIDA'");
                    table.CheckConstraint("CK_execution_result_payload", "result_payload = '{\"schemaVersion\": 1}'::jsonb");
                    table.ForeignKey(
                        name: "FK_execution_result_app_user_recorded_by",
                        column: x => x.recorded_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_execution_result_evidence_review_snapshot_evidence_review_id",
                        column: x => x.evidence_review_id,
                        principalTable: "evidence_review_snapshot",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_execution_result_work_obligation_obligation_id",
                        column: x => x.obligation_id,
                        principalTable: "work_obligation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_work_obligation_concluded_by",
                table: "work_obligation",
                column: "concluded_by");

            migrationBuilder.CreateIndex(
                name: "IX_execution_result_recorded_by_recorded_at_id",
                table: "execution_result",
                columns: new[] { "recorded_by", "recorded_at", "id" });

            migrationBuilder.CreateIndex(
                name: "UX_execution_result_evidence_review",
                table: "execution_result",
                column: "evidence_review_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_execution_result_obligation",
                table: "execution_result",
                column: "obligation_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_work_obligation_app_user_concluded_by",
                table: "work_obligation",
                column: "concluded_by",
                principalTable: "app_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                CREATE FUNCTION sgol_execution_result_guard()
                RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN
                  IF TG_OP <> 'INSERT' THEN
                    RAISE EXCEPTION 'EXECUTION_RESULT_IMMUTABLE' USING ERRCODE = '23514';
                  END IF;

                  IF NOT EXISTS (
                    SELECT 1
                    FROM evidence_review_snapshot snapshot
                    JOIN work_obligation obligation ON obligation.id = NEW.obligation_id
                    JOIN assignment_version assignment
                      ON assignment.obligation_id = obligation.id AND assignment.status = 'VIGENTE'
                    JOIN app_user actor
                      ON actor.id = NEW.recorded_by AND actor.person_id = assignment.person_id
                    WHERE snapshot.id = NEW.evidence_review_id
                      AND snapshot.obligation_id = obligation.id
                      AND snapshot.evidence_policy_version_id = obligation.evidence_policy_version_id
                      AND snapshot.result = 'COMPLETA'
                      AND snapshot.missing_requirements = '[]'::jsonb
                      AND actor.status = 'ACTIVA'
                      AND actor.mfa_enrolled_at IS NOT NULL)
                  OR EXISTS (
                    SELECT 1
                    FROM evidence_review_snapshot snapshot
                    CROSS JOIN unnest(snapshot.evidence_version_ids) AS used(version_id)
                    LEFT JOIN evidence_version version ON version.id = used.version_id
                    LEFT JOIN evidence_item item ON item.id = version.evidence_item_id
                    WHERE snapshot.id = NEW.evidence_review_id
                      AND (version.id IS NULL OR version.status <> 'VIGENTE'
                        OR item.obligation_id <> NEW.obligation_id
                        OR item.evidence_policy_version_id <> snapshot.evidence_policy_version_id))
                  THEN
                    RAISE EXCEPTION 'EXECUTION_RESULT_SNAPSHOT_INVALID' USING ERRCODE = '23514';
                  END IF;

                  RETURN NEW;
                END $body$;

                CREATE TRIGGER execution_result_guard
                  BEFORE INSERT OR UPDATE OR DELETE ON execution_result
                  FOR EACH ROW EXECUTE FUNCTION sgol_execution_result_guard();

                CREATE FUNCTION sgol_work_obligation_conclusion_guard()
                RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN
                  IF OLD.execution_status = 'CONCLUIDA' THEN
                    RAISE EXCEPTION 'WORK_OBLIGATION_CONCLUSION_IMMUTABLE' USING ERRCODE = '23514';
                  END IF;

                  IF NEW.execution_status IS DISTINCT FROM OLD.execution_status THEN
                    IF OLD.execution_status <> 'PENDIENTE'
                      OR NEW.execution_status <> 'CONCLUIDA'
                      OR NEW.concluded_at IS NULL
                      OR NEW.concluded_by IS NULL
                      OR NEW.row_version <> OLD.row_version + 1
                    THEN
                      RAISE EXCEPTION 'WORK_OBLIGATION_CONCLUSION_TRANSITION_INVALID' USING ERRCODE = '23514';
                    END IF;
                  ELSIF NEW.concluded_at IS DISTINCT FROM OLD.concluded_at
                    OR NEW.concluded_by IS DISTINCT FROM OLD.concluded_by
                  THEN
                    RAISE EXCEPTION 'WORK_OBLIGATION_CONCLUSION_FIELDS_INVALID' USING ERRCODE = '23514';
                  END IF;

                  RETURN NEW;
                END $body$;

                CREATE TRIGGER work_obligation_conclusion_guard
                  BEFORE UPDATE ON work_obligation
                  FOR EACH ROW EXECUTE FUNCTION sgol_work_obligation_conclusion_guard();

                CREATE FUNCTION sgol_obligation_conclusion_coherence_guard()
                RETURNS trigger LANGUAGE plpgsql AS $body$
                DECLARE
                  checked_obligation_id uuid;
                  obligation_state work_obligation%ROWTYPE;
                  result_count integer;
                  coherent_count integer;
                BEGIN
                  IF TG_TABLE_NAME = 'execution_result' THEN
                    checked_obligation_id := NEW.obligation_id;
                  ELSE
                    checked_obligation_id := NEW.id;
                  END IF;

                  SELECT * INTO obligation_state
                  FROM work_obligation
                  WHERE id = checked_obligation_id;

                  SELECT count(*), count(*) FILTER (
                    WHERE result.result_code = 'CONCLUIDA'
                      AND result.recorded_by = obligation_state.concluded_by
                      AND result.recorded_at = obligation_state.concluded_at)
                    INTO result_count, coherent_count
                  FROM execution_result result
                  WHERE result.obligation_id = checked_obligation_id;

                  IF (obligation_state.execution_status = 'CONCLUIDA'
                        AND (result_count <> 1 OR coherent_count <> 1))
                    OR (obligation_state.execution_status = 'PENDIENTE' AND result_count <> 0)
                  THEN
                    RAISE EXCEPTION 'OBLIGATION_CONCLUSION_INCOHERENT' USING ERRCODE = '23514';
                  END IF;

                  RETURN NULL;
                END $body$;

                CREATE CONSTRAINT TRIGGER execution_result_conclusion_coherence
                  AFTER INSERT OR UPDATE ON execution_result
                  DEFERRABLE INITIALLY DEFERRED
                  FOR EACH ROW EXECUTE FUNCTION sgol_obligation_conclusion_coherence_guard();

                CREATE CONSTRAINT TRIGGER work_obligation_conclusion_coherence
                  AFTER INSERT OR UPDATE ON work_obligation
                  DEFERRABLE INITIALLY DEFERRED
                  FOR EACH ROW EXECUTE FUNCTION sgol_obligation_conclusion_coherence_guard();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Rollback is blocked for the immutable HU-022 obligation conclusion migration.");
        }
    }
}
