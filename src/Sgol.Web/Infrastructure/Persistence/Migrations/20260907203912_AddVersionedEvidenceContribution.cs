using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVersionedEvidenceContribution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "evidence_item",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    obligation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evidence_policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requirement_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requirement_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    requirement_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evidence_item", x => x.id);
                    table.CheckConstraint("CK_evidence_item_row_version", "row_version > 0");
                    table.ForeignKey(
                        name: "FK_evidence_item_evidence_policy_version_evidence_policy_versi~",
                        column: x => x.evidence_policy_version_id,
                        principalTable: "evidence_policy_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_evidence_item_evidence_requirement_version_requirement_vers~",
                        column: x => x.requirement_version_id,
                        principalTable: "evidence_requirement_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_evidence_item_work_obligation_obligation_id",
                        column: x => x.obligation_id,
                        principalTable: "work_obligation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "file_object",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    obligation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evidence_policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requirement_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requirement_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    requirement_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    document_subtype = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    bucket_class = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    object_key = table.Column<string>(type: "character varying(73)", maxLength: 73, nullable: false),
                    original_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    declared_media_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    detected_media_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "character(64)", nullable: false),
                    scan_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    scan_engine = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    scan_error_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    scanned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    upload_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    upload_completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    link_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    linked_evidence_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    replicated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file_object", x => x.id);
                    table.CheckConstraint("CK_file_object_branch", "branch_id = '019d2d67-2c00-7000-8000-000000000001'::uuid");
                    table.CheckConstraint("CK_file_object_bucket", "bucket_class IN ('QUARANTINE','CLEAN')");
                    table.CheckConstraint("CK_file_object_key", "object_key ~ '^v1/[0-9a-f]{2}/[0-9a-f]{2}/[0-9a-f]{64}$'");
                    table.CheckConstraint("CK_file_object_error_code", "scan_error_code IS NULL OR scan_error_code ~ '^[A-Z][A-Z0-9_]{0,63}$'");
                    table.CheckConstraint("CK_file_object_kind", "requirement_kind IN ('FOTOGRAFIA','DOCUMENTO_REFERENCIADO')");
                    table.CheckConstraint("CK_file_object_kind_media", "(requirement_kind = 'FOTOGRAFIA' AND declared_media_type IN ('image/jpeg','image/png')) OR (requirement_kind = 'DOCUMENTO_REFERENCIADO' AND declared_media_type = 'application/pdf')");
                    table.CheckConstraint("CK_file_object_link", "linked_evidence_item_id IS NULL OR (scan_status = 'LIMPIO' AND bucket_class = 'CLEAN')");
                    table.CheckConstraint("CK_file_object_media", "declared_media_type IN ('image/jpeg','image/png','application/pdf')");
                    table.CheckConstraint("CK_file_object_original_name", "char_length(original_name) BETWEEN 1 AND 255 AND original_name = btrim(original_name) AND original_name !~ '[\\\\/]' AND original_name !~ '[[:cntrl:]]'");
                    table.CheckConstraint("CK_file_object_replication_reserved", "replicated_at IS NULL");
                    table.CheckConstraint("CK_file_object_row_version", "row_version > 0");
                    table.CheckConstraint("CK_file_object_sha256", "sha256 ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("CK_file_object_size", "size_bytes BETWEEN 1 AND 15728640");
                    table.CheckConstraint("CK_file_object_state", "(scan_status = 'PENDIENTE' AND bucket_class = 'QUARANTINE' AND detected_media_type IS NULL AND scanned_at IS NULL AND link_expires_at IS NULL) OR (scan_status = 'LIMPIO' AND bucket_class = 'CLEAN' AND detected_media_type = declared_media_type AND scanned_at IS NOT NULL AND link_expires_at = scanned_at + interval '24 hours') OR (scan_status IN ('INFECTADO','INVALIDO','ERROR_ESCANEO') AND bucket_class = 'QUARANTINE' AND scanned_at IS NOT NULL AND scan_error_code IS NOT NULL AND link_expires_at IS NULL)");
                    table.CheckConstraint("CK_file_object_status", "scan_status IN ('PENDIENTE','LIMPIO','INFECTADO','INVALIDO','ERROR_ESCANEO')");
                    table.CheckConstraint("CK_file_object_subtype", "(requirement_code = 'DOCUMENTO_RECEPCION' AND document_subtype IN ('NOTA','REMISION','FACTURA')) OR (requirement_code <> 'DOCUMENTO_RECEPCION' AND document_subtype IS NULL)");
                    table.CheckConstraint("CK_file_object_timeline", "upload_expires_at = created_at + interval '10 minutes' AND (upload_completed_at IS NULL OR upload_completed_at BETWEEN created_at AND upload_expires_at) AND (scanned_at IS NULL OR scanned_at >= created_at)");
                    table.ForeignKey(
                        name: "FK_file_object_app_user_uploaded_by",
                        column: x => x.uploaded_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_file_object_branch_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_file_object_evidence_item_linked_evidence_item_id",
                        column: x => x.linked_evidence_item_id,
                        principalTable: "evidence_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_file_object_evidence_policy_version_evidence_policy_version~",
                        column: x => x.evidence_policy_version_id,
                        principalTable: "evidence_policy_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_file_object_evidence_requirement_version_requirement_versio~",
                        column: x => x.requirement_version_id,
                        principalTable: "evidence_requirement_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_file_object_work_obligation_obligation_id",
                        column: x => x.obligation_id,
                        principalTable: "work_obligation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evidence_version",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    evidence_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    file_object_id = table.Column<Guid>(type: "uuid", nullable: false),
                    structured_payload = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    submitted_by = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    supersedes_id = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evidence_version", x => x.id);
                    table.CheckConstraint("CK_evidence_version_binary_only", "structured_payload IS NULL");
                    table.CheckConstraint("CK_evidence_version_chain", "(version_no = 1 AND supersedes_id IS NULL) OR (version_no > 1 AND supersedes_id IS NOT NULL)");
                    table.CheckConstraint("CK_evidence_version_number", "version_no > 0");
                    table.CheckConstraint("CK_evidence_version_reason", "reason IS NULL OR (char_length(reason) BETWEEN 1 AND 500 AND reason = btrim(reason) AND reason !~ '[<>[:cntrl:]]')");
                    table.CheckConstraint("CK_evidence_version_row_version", "row_version > 0");
                    table.CheckConstraint("CK_evidence_version_status", "status IN ('VIGENTE','SUSTITUIDA')");
                    table.ForeignKey(
                        name: "FK_evidence_version_app_user_submitted_by",
                        column: x => x.submitted_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_evidence_version_evidence_item_evidence_item_id",
                        column: x => x.evidence_item_id,
                        principalTable: "evidence_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_evidence_version_evidence_version_supersedes_id",
                        column: x => x.supersedes_id,
                        principalTable: "evidence_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_evidence_version_file_object_file_object_id",
                        column: x => x.file_object_id,
                        principalTable: "file_object",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_evidence_item_evidence_policy_version_id",
                table: "evidence_item",
                column: "evidence_policy_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_evidence_item_obligation_id",
                table: "evidence_item",
                column: "obligation_id");

            migrationBuilder.CreateIndex(
                name: "IX_evidence_item_requirement_version_id",
                table: "evidence_item",
                column: "requirement_version_id");

            migrationBuilder.CreateIndex(
                name: "UX_evidence_item_obligation_requirement",
                table: "evidence_item",
                columns: new[] { "obligation_id", "requirement_version_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_evidence_version_evidence_item_id_version_no",
                table: "evidence_version",
                columns: new[] { "evidence_item_id", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_evidence_version_file_object_id",
                table: "evidence_version",
                column: "file_object_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_evidence_version_submitted_by",
                table: "evidence_version",
                column: "submitted_by");

            migrationBuilder.CreateIndex(
                name: "IX_evidence_version_supersedes_id",
                table: "evidence_version",
                column: "supersedes_id",
                unique: true,
                filter: "supersedes_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_evidence_version_current_item",
                table: "evidence_version",
                column: "evidence_item_id",
                unique: true,
                filter: "status = 'VIGENTE'");

            migrationBuilder.CreateIndex(
                name: "IX_file_object_branch_id",
                table: "file_object",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_file_object_evidence_policy_version_id",
                table: "file_object",
                column: "evidence_policy_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_file_object_link_expires_at",
                table: "file_object",
                column: "link_expires_at",
                filter: "linked_evidence_item_id IS NULL AND link_expires_at IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_file_object_linked_evidence_item_id",
                table: "file_object",
                column: "linked_evidence_item_id",
                filter: "linked_evidence_item_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_file_object_object_key",
                table: "file_object",
                column: "object_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_file_object_obligation_id_requirement_version_id_scan_status",
                table: "file_object",
                columns: new[] { "obligation_id", "requirement_version_id", "scan_status" });

            migrationBuilder.CreateIndex(
                name: "IX_file_object_requirement_version_id",
                table: "file_object",
                column: "requirement_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_file_object_scan_status_created_at",
                table: "file_object",
                columns: new[] { "scan_status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_file_object_uploaded_by_created_at",
                table: "file_object",
                columns: new[] { "uploaded_by", "created_at" });

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "UX_outbox_event_evidence_inspection_pending"
                ON outbox_event (event_type, aggregate_id)
                WHERE event_type = 'EVIDENCE.FILE_INSPECTION_REQUESTED.V1'
                  AND processed_at IS NULL
                  AND attempt_count < 5;

                CREATE FUNCTION sgol_evidence_validate_snapshot() RETURNS trigger LANGUAGE plpgsql AS $body$
                DECLARE obligation_policy uuid; requirement_policy uuid; stored_code text; stored_kind text;
                BEGIN
                  SELECT evidence_policy_version_id INTO obligation_policy FROM work_obligation WHERE id = NEW.obligation_id;
                  SELECT policy_version_id, requirement_code, kind
                    INTO requirement_policy, stored_code, stored_kind
                    FROM evidence_requirement_version WHERE id = NEW.requirement_version_id;
                  IF obligation_policy IS NULL OR NEW.evidence_policy_version_id <> obligation_policy OR
                     NEW.evidence_policy_version_id <> requirement_policy OR
                     NEW.requirement_code <> stored_code OR NEW.requirement_kind <> stored_kind THEN
                    RAISE EXCEPTION 'EVIDENCE_SNAPSHOT_MISMATCH' USING ERRCODE = '23514';
                  END IF;
                  RETURN NEW;
                END $body$;

                CREATE TRIGGER trg_file_object_snapshot
                BEFORE INSERT OR UPDATE ON file_object
                FOR EACH ROW EXECUTE FUNCTION sgol_evidence_validate_snapshot();
                CREATE TRIGGER trg_evidence_item_snapshot
                BEFORE INSERT OR UPDATE ON evidence_item
                FOR EACH ROW EXECUTE FUNCTION sgol_evidence_validate_snapshot();

                CREATE FUNCTION sgol_file_object_guard() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN
                  IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'EVIDENCE_DELETE_FORBIDDEN' USING ERRCODE = '23514'; END IF;
                  IF TG_OP = 'UPDATE' THEN
                    IF ROW(NEW.id,NEW.branch_id,NEW.obligation_id,NEW.evidence_policy_version_id,NEW.requirement_version_id,
                           NEW.requirement_code,NEW.requirement_kind,NEW.document_subtype,NEW.object_key,NEW.original_name,
                           NEW.declared_media_type,NEW.size_bytes,NEW.sha256,NEW.uploaded_by,NEW.created_at,NEW.upload_expires_at)
                       IS DISTINCT FROM
                       ROW(OLD.id,OLD.branch_id,OLD.obligation_id,OLD.evidence_policy_version_id,OLD.requirement_version_id,
                           OLD.requirement_code,OLD.requirement_kind,OLD.document_subtype,OLD.object_key,OLD.original_name,
                           OLD.declared_media_type,OLD.size_bytes,OLD.sha256,OLD.uploaded_by,OLD.created_at,OLD.upload_expires_at)
                    THEN RAISE EXCEPTION 'EVIDENCE_FILE_IMMUTABLE' USING ERRCODE = '23514'; END IF;
                    IF NEW.row_version <> OLD.row_version + 1 THEN RAISE EXCEPTION 'EVIDENCE_ROW_VERSION_INVALID' USING ERRCODE = '23514'; END IF;
                    IF OLD.scan_status <> 'PENDIENTE' AND NOT
                       (OLD.scan_status = 'LIMPIO' AND NEW.scan_status = 'LIMPIO' AND OLD.linked_evidence_item_id IS NULL AND NEW.linked_evidence_item_id IS NOT NULL) AND NOT
                       (OLD.scan_status = 'LIMPIO' AND NEW.scan_status = 'INVALIDO' AND OLD.linked_evidence_item_id IS NULL AND OLD.link_expires_at <= now())
                    THEN RAISE EXCEPTION 'EVIDENCE_FILE_TRANSITION_INVALID' USING ERRCODE = '23514'; END IF;
                  END IF;
                  RETURN NEW;
                END $body$;
                CREATE TRIGGER trg_file_object_guard
                BEFORE UPDATE OR DELETE ON file_object
                FOR EACH ROW EXECUTE FUNCTION sgol_file_object_guard();

                CREATE FUNCTION sgol_evidence_item_guard() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN
                  IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'EVIDENCE_DELETE_FORBIDDEN' USING ERRCODE = '23514'; END IF;
                  IF ROW(NEW.id,NEW.obligation_id,NEW.evidence_policy_version_id,NEW.requirement_version_id,
                         NEW.requirement_code,NEW.requirement_kind,NEW.created_at)
                     IS DISTINCT FROM ROW(OLD.id,OLD.obligation_id,OLD.evidence_policy_version_id,OLD.requirement_version_id,
                         OLD.requirement_code,OLD.requirement_kind,OLD.created_at) OR NEW.row_version <> OLD.row_version + 1
                  THEN RAISE EXCEPTION 'EVIDENCE_ITEM_IMMUTABLE' USING ERRCODE = '23514'; END IF;
                  RETURN NEW;
                END $body$;
                CREATE TRIGGER trg_evidence_item_guard
                BEFORE UPDATE OR DELETE ON evidence_item
                FOR EACH ROW EXECUTE FUNCTION sgol_evidence_item_guard();

                CREATE FUNCTION sgol_evidence_version_guard() RETURNS trigger LANGUAGE plpgsql AS $body$
                DECLARE current_count integer;
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
                  IF TG_OP <> 'DELETE' AND NOT EXISTS (
                    SELECT 1 FROM file_object f WHERE f.id = NEW.file_object_id AND f.scan_status = 'LIMPIO'
                      AND f.bucket_class = 'CLEAN' AND f.linked_evidence_item_id = NEW.evidence_item_id
                      AND f.link_expires_at >= NEW.submitted_at)
                  THEN RAISE EXCEPTION 'EVIDENCE_FILE_NOT_LINKABLE' USING ERRCODE = '23514'; END IF;
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
                CREATE CONSTRAINT TRIGGER trg_evidence_version_guard
                AFTER INSERT OR UPDATE OR DELETE ON evidence_version
                DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION sgol_evidence_version_guard();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Rollback is blocked for the immutable HU-025 evidence history migration.");
        }
    }
}
