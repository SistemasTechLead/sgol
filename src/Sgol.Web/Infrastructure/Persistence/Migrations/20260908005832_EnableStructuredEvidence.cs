using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnableStructuredEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_evidence_version_file_object_id",
                table: "evidence_version");

            migrationBuilder.DropCheckConstraint(
                name: "CK_evidence_version_binary_only",
                table: "evidence_version");

            migrationBuilder.AlterColumn<Guid>(
                name: "file_object_id",
                table: "evidence_version",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "IX_evidence_version_file_object_id",
                table: "evidence_version",
                column: "file_object_id",
                unique: true,
                filter: "file_object_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_evidence_version_source",
                table: "evidence_version",
                sql: "(file_object_id IS NULL) <> (structured_payload IS NULL)");

            migrationBuilder.Sql("""
                CREATE FUNCTION sgol_evidence_text_valid(value jsonb, maximum integer, is_reference boolean)
                RETURNS boolean LANGUAGE plpgsql IMMUTABLE AS $body$
                DECLARE text_value text;
                BEGIN
                  IF jsonb_typeof(value) <> 'string' THEN RETURN false; END IF;
                  text_value := value #>> '{}';
                  RETURN char_length(text_value) BETWEEN 1 AND maximum
                    AND text_value = btrim(text_value)
                    AND text_value !~ '[<>[:cntrl:]]'
                    AND (NOT is_reference OR (strpos(text_value, '/') = 0 AND strpos(text_value, chr(92)) = 0
                      AND text_value !~* '^[a-z][a-z0-9+.-]*:'));
                END $body$;

                CREATE FUNCTION sgol_evidence_timestamp_valid(value jsonb)
                RETURNS boolean LANGUAGE plpgsql IMMUTABLE AS $body$
                DECLARE parsed timestamptz; text_value text;
                BEGIN
                  IF jsonb_typeof(value) <> 'string' THEN RETURN false; END IF;
                  text_value := value #>> '{}';
                  IF text_value !~ '^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]{1,7})?Z$' THEN RETURN false; END IF;
                  parsed := text_value::timestamptz;
                  RETURN true;
                EXCEPTION WHEN OTHERS THEN RETURN false;
                END $body$;

                CREATE FUNCTION sgol_evidence_exact_keys(value jsonb, expected text[])
                RETURNS boolean LANGUAGE sql IMMUTABLE AS $body$
                  SELECT jsonb_typeof(value) = 'object'
                    AND (SELECT count(*) FROM jsonb_object_keys(value)) = cardinality(expected)
                    AND value ?& expected
                $body$;

                CREATE FUNCTION sgol_evidence_structured_payload_valid(code text, kind text, payload jsonb)
                RETURNS boolean LANGUAGE plpgsql IMMUTABLE AS $body$
                DECLARE keys text[];
                BEGIN
                  IF payload IS NULL OR jsonb_typeof(payload) <> 'object' OR
                     jsonb_typeof(payload->'schemaVersion') <> 'number' OR payload->>'schemaVersion' <> '1'
                  THEN RETURN false; END IF;

                  CASE code
                    WHEN 'CALCULO_AVANCE' THEN
                      keys := ARRAY['schemaVersion','expectedTarget','actualSales','sourceReference'];
                      RETURN kind = 'REGISTRO_DIGITAL' AND sgol_evidence_exact_keys(payload, keys)
                        AND jsonb_typeof(payload->'expectedTarget') = 'number' AND (payload->>'expectedTarget')::numeric > 0
                        AND scale((payload->>'expectedTarget')::numeric) <= 2 AND abs((payload->>'expectedTarget')::numeric) < 1000000000000000
                        AND jsonb_typeof(payload->'actualSales') = 'number' AND (payload->>'actualSales')::numeric >= 0
                        AND scale((payload->>'actualSales')::numeric) <= 2 AND abs((payload->>'actualSales')::numeric) < 1000000000000000
                        AND sgol_evidence_text_valid(payload->'sourceReference', 120, true);
                    WHEN 'ACCION_O_CONFORMIDAD' THEN
                      keys := ARRAY['schemaVersion','outcome','actionDescription','responsiblePersonId','startsAt'];
                      RETURN kind = 'REGISTRO_DIGITAL' AND sgol_evidence_exact_keys(payload, keys)
                        AND payload->>'outcome' IN ('ACCION','CONFORMIDAD')
                        AND ((payload->>'outcome' = 'CONFORMIDAD' AND payload->'actionDescription' = 'null'::jsonb
                              AND payload->'responsiblePersonId' = 'null'::jsonb AND payload->'startsAt' = 'null'::jsonb)
                          OR (payload->>'outcome' = 'ACCION' AND sgol_evidence_text_valid(payload->'actionDescription', 500, false)
                              AND jsonb_typeof(payload->'responsiblePersonId') = 'string'
                              AND lower((payload->>'responsiblePersonId')::uuid::text) = payload->>'responsiblePersonId'
                              AND sgol_evidence_timestamp_valid(payload->'startsAt')));
                    WHEN 'LIBERACION' THEN
                      keys := ARRAY['schemaVersion','releasedAt','releaseReference'];
                      RETURN kind = 'REGISTRO_DIGITAL' AND sgol_evidence_exact_keys(payload, keys)
                        AND sgol_evidence_timestamp_valid(payload->'releasedAt') AND sgol_evidence_text_valid(payload->'releaseReference',120,true);
                    WHEN 'MERCANCIA' THEN
                      keys := ARRAY['schemaVersion','merchandiseReference'];
                      RETURN kind = 'DATO_ESTRUCTURADO' AND sgol_evidence_exact_keys(payload, keys)
                        AND sgol_evidence_text_valid(payload->'merchandiseReference',120,true);
                    WHEN 'FECHA_HORA' THEN
                      keys := ARRAY['schemaVersion','occurredAt'];
                      RETURN kind = 'DATO_ESTRUCTURADO' AND sgol_evidence_exact_keys(payload, keys)
                        AND sgol_evidence_timestamp_valid(payload->'occurredAt');
                    WHEN 'RETORNO_EXHIBICION' THEN
                      keys := ARRAY['schemaVersion','returnedAt','returnReference'];
                      RETURN kind = 'REGISTRO_DIGITAL' AND sgol_evidence_exact_keys(payload, keys)
                        AND sgol_evidence_timestamp_valid(payload->'returnedAt') AND sgol_evidence_text_valid(payload->'returnReference',120,true);
                    WHEN 'SECUENCIA' THEN
                      keys := ARRAY['schemaVersion','sequenceSummary'];
                      RETURN kind = 'REGISTRO_DIGITAL' AND sgol_evidence_exact_keys(payload, keys)
                        AND sgol_evidence_text_valid(payload->'sequenceSummary',500,false);
                    WHEN 'DECISION' THEN
                      keys := ARRAY['schemaVersion','decisionSummary','decidedAt'];
                      RETURN kind = 'REGISTRO_DIGITAL' AND sgol_evidence_exact_keys(payload, keys)
                        AND sgol_evidence_text_valid(payload->'decisionSummary',500,false) AND sgol_evidence_timestamp_valid(payload->'decidedAt');
                    WHEN 'FUNDAMENTO' THEN
                      keys := ARRAY['schemaVersion','foundationSummary'];
                      RETURN kind = 'DATO_ESTRUCTURADO' AND sgol_evidence_exact_keys(payload, keys)
                        AND sgol_evidence_text_valid(payload->'foundationSummary',500,false);
                    WHEN 'AVISO_INTERNO' THEN
                      keys := ARRAY['schemaVersion','noticeReference','notifiedAt'];
                      RETURN kind = 'REGISTRO_DIGITAL' AND sgol_evidence_exact_keys(payload, keys)
                        AND sgol_evidence_text_valid(payload->'noticeReference',120,true) AND sgol_evidence_timestamp_valid(payload->'notifiedAt');
                    WHEN 'EVALUACION' THEN
                      keys := ARRAY['schemaVersion','assessmentSummary','assessedAt'];
                      RETURN kind = 'REGISTRO_DIGITAL' AND sgol_evidence_exact_keys(payload, keys)
                        AND sgol_evidence_text_valid(payload->'assessmentSummary',500,false) AND sgol_evidence_timestamp_valid(payload->'assessedAt');
                    WHEN 'REPARACION_O_CAMBIO' THEN
                      keys := ARRAY['schemaVersion','solutionType','solutionReference','completedAt'];
                      RETURN kind = 'REGISTRO_DIGITAL' AND sgol_evidence_exact_keys(payload, keys)
                        AND payload->>'solutionType' IN ('REPARACION','CAMBIO')
                        AND sgol_evidence_text_valid(payload->'solutionReference',120,true) AND sgol_evidence_timestamp_valid(payload->'completedAt');
                    WHEN 'ENTREGA' THEN
                      keys := ARRAY['schemaVersion','deliveryReference','deliveredAt'];
                      RETURN kind = 'REGISTRO_DIGITAL' AND sgol_evidence_exact_keys(payload, keys)
                        AND sgol_evidence_text_valid(payload->'deliveryReference',120,true) AND sgol_evidence_timestamp_valid(payload->'deliveredAt');
                    WHEN 'CHECKLIST_COMPLETO' THEN
                      keys := ARRAY['schemaVersion','productCorrect','zoneAndFamilyCorrect','stableFormation','labelsVisible','alignmentConsistent','occupancyJustified','clean','intact','signageCorrect','matchesPlanogramOrList'];
                      RETURN kind = 'CHECKLIST_ESTRUCTURADO' AND sgol_evidence_exact_keys(payload, keys)
                        AND NOT EXISTS (SELECT 1 FROM unnest(keys[2:11]) AS checklist_key(name)
                          WHERE jsonb_typeof(payload->checklist_key.name) <> 'boolean');
                    WHEN 'FORM_ADM_02' THEN
                      keys := ARRAY['schemaVersion','formCode','formReference','completedAt'];
                      RETURN kind = 'FORMULARIO_REFERENCIADO' AND sgol_evidence_exact_keys(payload, keys)
                        AND payload->>'formCode' = 'FORM-ADM-02' AND sgol_evidence_text_valid(payload->'formReference',120,true)
                        AND sgol_evidence_timestamp_valid(payload->'completedAt');
                    WHEN 'F_ENT_001' THEN
                      keys := ARRAY['schemaVersion','formCode','formReference','completedAt','hasDifference','hasDamage'];
                      RETURN kind = 'FORMULARIO_REFERENCIADO' AND sgol_evidence_exact_keys(payload, keys)
                        AND payload->>'formCode' = 'F-ENT-001' AND sgol_evidence_text_valid(payload->'formReference',120,true)
                        AND sgol_evidence_timestamp_valid(payload->'completedAt')
                        AND jsonb_typeof(payload->'hasDifference') = 'boolean' AND jsonb_typeof(payload->'hasDamage') = 'boolean';
                    WHEN 'ANOTACION_F_ENT_001' THEN
                      keys := ARRAY['schemaVersion','formCode','formReference','annotationReference','recordedAt'];
                      RETURN kind = 'FORMULARIO_REFERENCIADO' AND sgol_evidence_exact_keys(payload, keys)
                        AND payload->>'formCode' = 'F-ENT-001' AND sgol_evidence_text_valid(payload->'formReference',120,true)
                        AND sgol_evidence_text_valid(payload->'annotationReference',120,true) AND sgol_evidence_timestamp_valid(payload->'recordedAt');
                    WHEN 'CONSTANCIA_AVISO_INTERNO' THEN
                      keys := ARRAY['schemaVersion','noticeReference','notifiedAt'];
                      RETURN kind = 'REGISTRO_DIGITAL' AND sgol_evidence_exact_keys(payload, keys)
                        AND sgol_evidence_text_valid(payload->'noticeReference',120,true) AND sgol_evidence_timestamp_valid(payload->'notifiedAt');
                    ELSE RETURN false;
                  END CASE;
                END $body$;

                CREATE OR REPLACE FUNCTION sgol_evidence_version_guard() RETURNS trigger LANGUAGE plpgsql AS $body$
                DECLARE current_count integer; item_row evidence_item%ROWTYPE;
                BEGIN
                  IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'EVIDENCE_DELETE_FORBIDDEN' USING ERRCODE = '23514'; END IF;
                  IF TG_OP = 'UPDATE' AND
                     (ROW(NEW.id,NEW.evidence_item_id,NEW.version_no,NEW.file_object_id,NEW.structured_payload,
                          NEW.submitted_by,NEW.submitted_at,NEW.reason,NEW.supersedes_id)
                      IS DISTINCT FROM
                      ROW(OLD.id,OLD.evidence_item_id,OLD.version_no,OLD.file_object_id,OLD.structured_payload,
                          OLD.submitted_by,OLD.submitted_at,OLD.reason,OLD.supersedes_id) OR
                      OLD.status <> 'VIGENTE' OR NEW.status <> 'SUSTITUIDA' OR NEW.row_version <> OLD.row_version + 1)
                  THEN RAISE EXCEPTION 'EVIDENCE_VERSION_IMMUTABLE' USING ERRCODE = '23514'; END IF;

                  IF TG_OP <> 'DELETE' THEN
                    SELECT * INTO item_row FROM evidence_item WHERE id = NEW.evidence_item_id;
                    IF NOT FOUND THEN RAISE EXCEPTION 'EVIDENCE_ITEM_NOT_FOUND' USING ERRCODE = '23514'; END IF;
                    IF NEW.file_object_id IS NOT NULL THEN
                      IF item_row.requirement_kind NOT IN ('FOTOGRAFIA','DOCUMENTO_REFERENCIADO') OR NOT EXISTS (
                        SELECT 1 FROM file_object f WHERE f.id = NEW.file_object_id AND f.scan_status = 'LIMPIO'
                          AND f.bucket_class = 'CLEAN' AND f.linked_evidence_item_id = NEW.evidence_item_id
                          AND f.requirement_version_id = item_row.requirement_version_id
                          AND f.link_expires_at >= NEW.submitted_at)
                      THEN RAISE EXCEPTION 'EVIDENCE_FILE_NOT_LINKABLE' USING ERRCODE = '23514'; END IF;
                      IF item_row.requirement_code = 'FOTO_DIFERENCIA_DANO' AND NOT EXISTS (
                        SELECT 1 FROM evidence_item fi JOIN evidence_version fv ON fv.evidence_item_id = fi.id
                        WHERE fi.obligation_id = item_row.obligation_id AND fi.evidence_policy_version_id = item_row.evidence_policy_version_id
                          AND fi.requirement_code = 'F_ENT_001' AND fv.status = 'VIGENTE'
                          AND sgol_evidence_structured_payload_valid(fi.requirement_code, fi.requirement_kind, fv.structured_payload)
                          AND ((fv.structured_payload->>'hasDifference')::boolean OR (fv.structured_payload->>'hasDamage')::boolean))
                      THEN RAISE EXCEPTION 'EVIDENCE_CONDITION_NOT_APPLICABLE' USING ERRCODE = '23514'; END IF;
                    ELSIF NOT sgol_evidence_structured_payload_valid(item_row.requirement_code, item_row.requirement_kind, NEW.structured_payload) THEN
                      RAISE EXCEPTION 'EVIDENCE_STRUCTURED_PAYLOAD_INVALID' USING ERRCODE = '23514';
                    END IF;
                  END IF;

                  SELECT count(*) INTO current_count FROM evidence_version
                    WHERE evidence_item_id = COALESCE(NEW.evidence_item_id, OLD.evidence_item_id) AND status = 'VIGENTE';
                  IF current_count <> 1 THEN RAISE EXCEPTION 'EVIDENCE_CURRENT_VERSION_INVALID' USING ERRCODE = '23514'; END IF;
                  IF TG_OP <> 'DELETE' AND NEW.version_no > 1 AND NOT EXISTS (
                    SELECT 1 FROM evidence_version p WHERE p.id = NEW.supersedes_id
                      AND p.evidence_item_id = NEW.evidence_item_id AND p.version_no = NEW.version_no - 1 AND p.status = 'SUSTITUIDA')
                  THEN RAISE EXCEPTION 'EVIDENCE_CHAIN_INVALID' USING ERRCODE = '23514'; END IF;
                  IF TG_OP = 'UPDATE' AND NOT EXISTS (
                    SELECT 1 FROM evidence_version s WHERE s.supersedes_id = NEW.id
                      AND s.evidence_item_id = NEW.evidence_item_id AND s.version_no = NEW.version_no + 1)
                  THEN RAISE EXCEPTION 'EVIDENCE_SUCCESSOR_REQUIRED' USING ERRCODE = '23514'; END IF;
                  RETURN NEW;
                END $body$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Rollback is blocked for the immutable TECH-EVID-002 structured evidence migration.");
        }
    }
}
