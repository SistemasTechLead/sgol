using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEvidenceReviewSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "evidence_review_snapshot",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    obligation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evidence_policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    result = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    schema_version = table.Column<short>(type: "smallint", nullable: false),
                    input_fingerprint = table.Column<string>(type: "character(64)", nullable: false),
                    canonical_input = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    requirements_snapshot = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    missing_requirements = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    evidence_version_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    evaluated_by = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evidence_review_snapshot", x => x.id);
                    table.CheckConstraint("CK_evidence_review_snapshot_actor", "evaluated_by = 'SYSTEM'");
                    table.CheckConstraint("CK_evidence_review_snapshot_fingerprint", "input_fingerprint ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("CK_evidence_review_snapshot_json", "jsonb_typeof(canonical_input) = 'object' AND jsonb_typeof(requirements_snapshot) = 'array' AND jsonb_typeof(missing_requirements) = 'array'");
                    table.CheckConstraint("CK_evidence_review_snapshot_result", "result IN ('COMPLETA','INCOMPLETA')");
                    table.CheckConstraint("CK_evidence_review_snapshot_result_projection", "(result = 'COMPLETA') = (jsonb_array_length(missing_requirements) = 0 AND NOT jsonb_path_exists(requirements_snapshot, '$[*] ? (@.applicability == \"NO_RESUELTA\")'))");
                    table.CheckConstraint("CK_evidence_review_snapshot_schema", "schema_version = 1");
                    table.CheckConstraint("CK_evidence_review_snapshot_versions", "array_position(evidence_version_ids, NULL) IS NULL");
                    table.ForeignKey(
                        name: "FK_evidence_review_snapshot_app_user_requested_by_user_id",
                        column: x => x.requested_by_user_id,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_evidence_review_snapshot_evidence_policy_version_evidence_p~",
                        column: x => x.evidence_policy_version_id,
                        principalTable: "evidence_policy_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_evidence_review_snapshot_work_obligation_obligation_id",
                        column: x => x.obligation_id,
                        principalTable: "work_obligation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_evidence_review_snapshot_evidence_policy_version_id_evaluat~",
                table: "evidence_review_snapshot",
                columns: new[] { "evidence_policy_version_id", "evaluated_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_evidence_review_snapshot_obligation_id_evaluated_at_id",
                table: "evidence_review_snapshot",
                columns: new[] { "obligation_id", "evaluated_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_evidence_review_snapshot_requested_by_user_id",
                table: "evidence_review_snapshot",
                column: "requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "UX_evidence_review_snapshot_obligation_fingerprint",
                table: "evidence_review_snapshot",
                columns: new[] { "obligation_id", "input_fingerprint" },
                unique: true);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "UX_evidence_review_snapshot_obligation_input"
                  ON evidence_review_snapshot (obligation_id, canonical_input);

                CREATE FUNCTION sgol_evidence_review_snapshot_guard()
                RETURNS trigger LANGUAGE plpgsql AS $body$
                DECLARE
                  requirement_count integer;
                  projected_requirements jsonb;
                  projected_missing jsonb;
                  projected_versions uuid[];
                BEGIN
                  IF TG_OP <> 'INSERT' THEN
                    RAISE EXCEPTION 'EVIDENCE_REVIEW_SNAPSHOT_IMMUTABLE' USING ERRCODE = '23514';
                  END IF;

                  IF NOT sgol_evidence_exact_keys(
                      NEW.canonical_input,
                      ARRAY['schemaVersion','obligationId','evidencePolicyVersionId','taskCode','requirements'])
                    OR NEW.canonical_input->>'schemaVersion' <> '1'
                    OR lower(NEW.obligation_id::text) <> NEW.canonical_input->>'obligationId'
                    OR lower(NEW.evidence_policy_version_id::text) <> NEW.canonical_input->>'evidencePolicyVersionId'
                    OR jsonb_typeof(NEW.canonical_input->'requirements') <> 'array'
                    OR NOT EXISTS (
                      SELECT 1
                      FROM work_obligation obligation
                      JOIN task_definition_version task_version ON task_version.id = obligation.task_definition_version_id
                      JOIN task_definition task ON task.id = task_version.task_definition_id
                      JOIN evidence_policy_version policy ON policy.id = NEW.evidence_policy_version_id
                      WHERE obligation.id = NEW.obligation_id
                        AND obligation.evidence_policy_version_id = NEW.evidence_policy_version_id
                        AND policy.task_definition_id = task.id
                        AND task.task_code = NEW.canonical_input->>'taskCode')
                  THEN
                    RAISE EXCEPTION 'EVIDENCE_REVIEW_CANONICAL_INPUT_INVALID' USING ERRCODE = '23514';
                  END IF;

                  SELECT count(*) INTO requirement_count
                  FROM evidence_requirement_version requirement
                  WHERE requirement.policy_version_id = NEW.evidence_policy_version_id;

                  IF requirement_count = 0 OR requirement_count <> jsonb_array_length(NEW.canonical_input->'requirements')
                    OR EXISTS (
                      SELECT 1
                      FROM jsonb_array_elements(NEW.canonical_input->'requirements') WITH ORDINALITY AS entry(value, position)
                      LEFT JOIN evidence_requirement_version requirement
                        ON lower(requirement.id::text) = entry.value->>'requirementVersionId'
                       AND requirement.policy_version_id = NEW.evidence_policy_version_id
                       AND requirement.requirement_code = entry.value->>'requirementCode'
                       AND requirement.kind = entry.value->>'kind'
                       AND requirement.condition_code = entry.value->>'conditionCode'
                       AND requirement.ordinal = (entry.value->>'ordinal')::smallint
                       AND requirement.is_required
                      WHERE NOT sgol_evidence_exact_keys(entry.value,
                        ARRAY['requirementVersionId','requirementCode','kind','conditionCode','ordinal','applicability','satisfied',
                              'evidenceItemId','evidenceVersionId','conditionEvidenceVersionId','missingReason'])
                         OR requirement.id IS NULL
                         OR requirement.ordinal <> entry.position
                         OR entry.value->>'applicability' NOT IN ('APLICABLE','NO_APLICABLE','NO_RESUELTA')
                         OR jsonb_typeof(entry.value->'satisfied') <> 'boolean'
                         OR (entry.value->>'conditionCode' = 'SIEMPRE' AND entry.value->>'applicability' <> 'APLICABLE')
                         OR (entry.value->>'conditionCode' = 'SIEMPRE' AND entry.value->'conditionEvidenceVersionId' <> 'null'::jsonb)
                         OR (entry.value->>'conditionCode' = 'DIFERENCIA_O_DANO'
                             AND (entry.value->>'requirementCode' <> 'FOTO_DIFERENCIA_DANO' OR entry.value->>'kind' <> 'FOTOGRAFIA'))
                         OR (entry.value->>'applicability' <> 'APLICABLE'
                             AND (entry.value->'evidenceItemId' <> 'null'::jsonb OR entry.value->'evidenceVersionId' <> 'null'::jsonb))
                         OR (entry.value->>'applicability' = 'NO_APLICABLE' AND (entry.value->>'satisfied')::boolean IS NOT TRUE)
                         OR (entry.value->>'applicability' = 'NO_RESUELTA' AND (entry.value->>'satisfied')::boolean IS NOT FALSE)
                         OR (entry.value->>'applicability' <> 'APLICABLE' AND entry.value->'missingReason' <> 'null'::jsonb)
                         OR (entry.value->>'applicability' = 'APLICABLE' AND entry.value->'evidenceVersionId' = 'null'::jsonb
                             AND ((entry.value->>'satisfied')::boolean IS NOT FALSE
                                  OR entry.value->'evidenceItemId' <> 'null'::jsonb
                                  OR entry.value->>'missingReason' <> 'EVIDENCIA_VIGENTE_AUSENTE'))
                         OR (entry.value->>'applicability' = 'APLICABLE' AND entry.value->'evidenceVersionId' <> 'null'::jsonb
                             AND (entry.value->'evidenceItemId' = 'null'::jsonb
                                  OR (NOT (entry.value->>'satisfied')::boolean
                                      AND entry.value->>'missingReason' <> 'EVIDENCIA_VIGENTE_NO_SATISFACE')))
                         OR ((entry.value->>'satisfied')::boolean AND entry.value->'missingReason' <> 'null'::jsonb)
                         OR (NOT (entry.value->>'satisfied')::boolean
                             AND entry.value->>'applicability' = 'APLICABLE'
                             AND entry.value->>'missingReason' NOT IN ('EVIDENCIA_VIGENTE_AUSENTE','EVIDENCIA_VIGENTE_NO_SATISFACE'))
                    )
                  THEN
                    RAISE EXCEPTION 'EVIDENCE_REVIEW_REQUIREMENTS_INVALID' USING ERRCODE = '23514';
                  END IF;

                  IF EXISTS (
                    SELECT 1
                    FROM jsonb_array_elements(NEW.canonical_input->'requirements') AS conditional(value)
                    WHERE conditional.value->>'conditionCode' = 'DIFERENCIA_O_DANO'
                      AND NOT (
                        (NOT EXISTS (
                           SELECT 1 FROM evidence_item form_item
                           JOIN evidence_version form_version ON form_version.evidence_item_id = form_item.id
                           WHERE form_item.obligation_id = NEW.obligation_id
                             AND form_item.evidence_policy_version_id = NEW.evidence_policy_version_id
                             AND form_item.requirement_code = 'F_ENT_001'
                             AND form_version.status = 'VIGENTE')
                         AND conditional.value->>'applicability' = 'NO_RESUELTA'
                         AND conditional.value->'conditionEvidenceVersionId' = 'null'::jsonb)
                        OR EXISTS (
                          SELECT 1 FROM evidence_item form_item
                          JOIN evidence_version form_version ON form_version.evidence_item_id = form_item.id
                          WHERE form_item.obligation_id = NEW.obligation_id
                            AND form_item.evidence_policy_version_id = NEW.evidence_policy_version_id
                            AND form_item.requirement_code = 'F_ENT_001'
                            AND form_version.status = 'VIGENTE'
                            AND sgol_evidence_structured_payload_valid(
                              form_item.requirement_code, form_item.requirement_kind, form_version.structured_payload)
                            AND lower(form_version.id::text) = conditional.value->>'conditionEvidenceVersionId'
                            AND conditional.value->>'applicability' = CASE
                              WHEN (form_version.structured_payload->>'hasDifference')::boolean
                                OR (form_version.structured_payload->>'hasDamage')::boolean
                              THEN 'APLICABLE' ELSE 'NO_APLICABLE' END)
                      )
                  ) THEN
                    RAISE EXCEPTION 'EVIDENCE_REVIEW_CONDITION_INVALID' USING ERRCODE = '23514';
                  END IF;

                  IF EXISTS (
                    SELECT 1
                    FROM jsonb_array_elements(NEW.canonical_input->'requirements') AS entry(value)
                    WHERE entry.value->>'applicability' = 'APLICABLE'
                      AND entry.value->'evidenceVersionId' <> 'null'::jsonb
                      AND NOT EXISTS (
                        SELECT 1
                        FROM evidence_item item
                        JOIN evidence_version version ON version.evidence_item_id = item.id
                        LEFT JOIN file_object file ON file.id = version.file_object_id
                        WHERE lower(item.id::text) = entry.value->>'evidenceItemId'
                          AND lower(version.id::text) = entry.value->>'evidenceVersionId'
                          AND item.obligation_id = NEW.obligation_id
                          AND item.evidence_policy_version_id = NEW.evidence_policy_version_id
                          AND lower(item.requirement_version_id::text) = entry.value->>'requirementVersionId'
                          AND version.status = 'VIGENTE'
                          AND ((version.file_object_id IS NOT NULL AND file.scan_status = 'LIMPIO' AND file.bucket_class = 'CLEAN'
                                AND file.linked_evidence_item_id = item.id AND file.requirement_version_id = item.requirement_version_id)
                            OR (version.structured_payload IS NOT NULL
                                AND sgol_evidence_structured_payload_valid(item.requirement_code, item.requirement_kind, version.structured_payload)))
                      )
                  ) THEN
                    RAISE EXCEPTION 'EVIDENCE_REVIEW_EVIDENCE_INVALID' USING ERRCODE = '23514';
                  END IF;

                  IF EXISTS (
                    SELECT 1
                    FROM jsonb_array_elements(NEW.canonical_input->'requirements') AS entry(value)
                    JOIN evidence_item item ON lower(item.id::text) = entry.value->>'evidenceItemId'
                    JOIN evidence_version version ON lower(version.id::text) = entry.value->>'evidenceVersionId'
                    WHERE entry.value->>'applicability' = 'APLICABLE'
                      AND entry.value->'evidenceVersionId' <> 'null'::jsonb
                      AND (entry.value->>'satisfied')::boolean IS DISTINCT FROM CASE
                        WHEN version.file_object_id IS NOT NULL THEN true
                        WHEN item.requirement_code = 'CHECKLIST_COMPLETO' THEN
                          NOT EXISTS (
                            SELECT 1 FROM jsonb_each(version.structured_payload - 'schemaVersion') AS checklist(key, value)
                            WHERE checklist.value <> 'true'::jsonb)
                        WHEN item.requirement_code = 'ACCION_O_CONFORMIDAD' THEN
                          EXISTS (
                            SELECT 1
                            FROM evidence_item calculation_item
                            JOIN evidence_version calculation_version ON calculation_version.evidence_item_id = calculation_item.id
                            WHERE calculation_item.obligation_id = NEW.obligation_id
                              AND calculation_item.evidence_policy_version_id = NEW.evidence_policy_version_id
                              AND calculation_item.requirement_code = 'CALCULO_AVANCE'
                              AND calculation_version.status = 'VIGENTE'
                              AND sgol_evidence_structured_payload_valid(
                                calculation_item.requirement_code, calculation_item.requirement_kind, calculation_version.structured_payload)
                              AND version.structured_payload->>'outcome' = CASE
                                WHEN (calculation_version.structured_payload->>'actualSales')::numeric
                                   / (calculation_version.structured_payload->>'expectedTarget')::numeric * 100 < 90
                                THEN 'ACCION' ELSE 'CONFORMIDAD' END)
                        ELSE true
                      END
                  ) THEN
                    RAISE EXCEPTION 'EVIDENCE_REVIEW_SATISFACTION_INVALID' USING ERRCODE = '23514';
                  END IF;

                  SELECT coalesce(jsonb_agg(value - 'evidenceItemId' - 'conditionEvidenceVersionId' ORDER BY position), '[]'::jsonb)
                    INTO projected_requirements
                  FROM jsonb_array_elements(NEW.canonical_input->'requirements') WITH ORDINALITY AS entry(value, position);

                  SELECT coalesce(jsonb_agg(jsonb_build_object(
                      'requirementVersionId', value->'requirementVersionId',
                      'requirementCode', value->'requirementCode',
                      'kind', value->'kind',
                      'conditionCode', value->'conditionCode',
                      'ordinal', value->'ordinal',
                      'missingReason', value->'missingReason') ORDER BY position), '[]'::jsonb)
                    INTO projected_missing
                  FROM jsonb_array_elements(NEW.canonical_input->'requirements') WITH ORDINALITY AS entry(value, position)
                  WHERE value->>'applicability' = 'APLICABLE' AND NOT (value->>'satisfied')::boolean;

                  SELECT coalesce(array_agg(version_id ORDER BY version_text), ARRAY[]::uuid[])
                    INTO projected_versions
                  FROM (
                    SELECT DISTINCT value->>'evidenceVersionId' AS version_text,
                      (value->>'evidenceVersionId')::uuid AS version_id
                    FROM jsonb_array_elements(NEW.canonical_input->'requirements') AS entry(value)
                    WHERE value->'evidenceVersionId' <> 'null'::jsonb
                  ) versions;

                  IF NEW.requirements_snapshot <> projected_requirements
                    OR NEW.missing_requirements <> projected_missing
                    OR NEW.evidence_version_ids <> projected_versions
                    OR (NEW.result = 'COMPLETA') <> (
                      jsonb_array_length(projected_missing) = 0
                      AND NOT jsonb_path_exists(projected_requirements, '$[*] ? (@.applicability == "NO_RESUELTA")'))
                  THEN
                    RAISE EXCEPTION 'EVIDENCE_REVIEW_PROJECTION_INVALID' USING ERRCODE = '23514';
                  END IF;

                  RETURN NEW;
                EXCEPTION WHEN invalid_text_representation OR numeric_value_out_of_range THEN
                  RAISE EXCEPTION 'EVIDENCE_REVIEW_CANONICAL_INPUT_INVALID' USING ERRCODE = '23514';
                END $body$;

                CREATE TRIGGER evidence_review_snapshot_guard
                  BEFORE INSERT OR UPDATE OR DELETE ON evidence_review_snapshot
                  FOR EACH ROW EXECUTE FUNCTION sgol_evidence_review_snapshot_guard();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Rollback is blocked for the immutable HU-026 evidence review snapshot migration.");
        }
    }
}
