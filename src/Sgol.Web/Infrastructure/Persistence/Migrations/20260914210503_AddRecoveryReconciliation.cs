using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecoveryReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "recovery_reconciliation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recovery_reconciliation", x => x.id);
                    table.CheckConstraint("CK_recovery_reconciliation_reason", "length(reason) BETWEEN 1 AND 500");
                });

            migrationBuilder.CreateTable(
                name: "recovery_reconciliation_difference",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reconciliation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false),
                    group_name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    resource_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    stable_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    field_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expected_sha256 = table.Column<string>(type: "character(64)", nullable: true),
                    actual_sha256 = table.Column<string>(type: "character(64)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recovery_reconciliation_difference", x => x.id);
                    table.CheckConstraint("CK_recovery_reconciliation_difference_ordinal", "ordinal > 0 AND ordinal <= 100000");
                    table.ForeignKey(
                        name: "FK_recovery_reconciliation_difference_recovery_reconciliation_~",
                        column: x => x.reconciliation_id,
                        principalTable: "recovery_reconciliation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recovery_reconciliation_event",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reconciliation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<long>(type: "bigint", nullable: false),
                    event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    technical_actor = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    error_code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    reference_manifest_sha256 = table.Column<string>(type: "character(64)", nullable: true),
                    report_manifest_sha256 = table.Column<string>(type: "character(64)", nullable: true),
                    restore_evidence_sha256 = table.Column<string>(type: "character(64)", nullable: true),
                    reference_root_sha256 = table.Column<string>(type: "character(64)", nullable: true),
                    actual_root_sha256 = table.Column<string>(type: "character(64)", nullable: true),
                    difference_count = table.Column<int>(type: "integer", nullable: true),
                    differences_truncated = table.Column<bool>(type: "boolean", nullable: true),
                    observed_rpo_seconds = table.Column<long>(type: "bigint", nullable: true),
                    observed_rto_seconds = table.Column<long>(type: "bigint", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recovery_reconciliation_event", x => x.id);
                    table.CheckConstraint("CK_recovery_reconciliation_event_difference_count", "difference_count IS NULL OR difference_count >= 0");
                    table.CheckConstraint("CK_recovery_reconciliation_event_rpo", "observed_rpo_seconds IS NULL OR observed_rpo_seconds >= 0");
                    table.CheckConstraint("CK_recovery_reconciliation_event_rto", "observed_rto_seconds IS NULL OR observed_rto_seconds >= 0");
                    table.CheckConstraint("CK_recovery_reconciliation_event_sequence", "sequence > 0");
                    table.ForeignKey(
                        name: "FK_recovery_reconciliation_event_recovery_reconciliation_recon~",
                        column: x => x.reconciliation_id,
                        principalTable: "recovery_reconciliation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_recovery_reconciliation_branch_id_requested_at_id",
                table: "recovery_reconciliation",
                columns: new[] { "branch_id", "requested_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_recovery_reconciliation_difference_reconciliation_id_ordinal",
                table: "recovery_reconciliation_difference",
                columns: new[] { "reconciliation_id", "ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recovery_reconciliation_event_reconciliation_id_sequence",
                table: "recovery_reconciliation_event",
                columns: new[] { "reconciliation_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_recovery_reconciliation_terminal",
                table: "recovery_reconciliation_event",
                column: "reconciliation_id",
                unique: true,
                filter: "status IN ('DIFFERENT','FAILED','APPROVED')");

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX UX_recovery_reconciliation_event_stage
                    ON recovery_reconciliation_event (reconciliation_id, event_type);

                ALTER TABLE recovery_reconciliation_event
                    ADD CONSTRAINT CK_recovery_reconciliation_event_actor
                        CHECK ((actor_user_id IS NOT NULL)::integer + (technical_actor IS NOT NULL)::integer = 1),
                    ADD CONSTRAINT CK_recovery_reconciliation_event_technical_actor
                        CHECK (technical_actor IS NULL OR technical_actor IN ('SGOL_WORKER','SGOL_OPERATIONS')),
                    ADD CONSTRAINT CK_recovery_reconciliation_event_status
                        CHECK (status IN ('REQUESTED','REFERENCE_CAPTURING','REFERENCE_READY','RESTORE_STARTED',
                            'RECONCILING','MATCHED','DIFFERENT','FAILED','APPROVED')),
                    ADD CONSTRAINT CK_recovery_reconciliation_event_type
                        CHECK (event_type IN ('RECOVERY_RECONCILIATION_REQUESTED','RECOVERY_REFERENCE_CAPTURE_STARTED',
                            'RECOVERY_REFERENCE_READY','RECOVERY_RESTORE_STARTED','RECOVERY_RECONCILIATION_STARTED',
                            'RECOVERY_RECONCILIATION_COMPLETED','RECOVERY_RECONCILIATION_FAILED',
                            'RECOVERY_RECONCILIATION_APPROVED')),
                    ADD CONSTRAINT CK_recovery_reconciliation_event_hashes CHECK (
                        (reference_manifest_sha256 IS NULL OR reference_manifest_sha256 ~ '^[0-9a-f]{64}$') AND
                        (report_manifest_sha256 IS NULL OR report_manifest_sha256 ~ '^[0-9a-f]{64}$') AND
                        (restore_evidence_sha256 IS NULL OR restore_evidence_sha256 ~ '^[0-9a-f]{64}$') AND
                        (reference_root_sha256 IS NULL OR reference_root_sha256 ~ '^[0-9a-f]{64}$') AND
                        (actual_root_sha256 IS NULL OR actual_root_sha256 ~ '^[0-9a-f]{64}$'));

                ALTER TABLE recovery_reconciliation_difference
                    ADD CONSTRAINT CK_recovery_reconciliation_difference_kind CHECK (kind IN (
                        'IDENTITY_MISSING','IDENTITY_ADDITIONAL','LINK_MISSING','LINK_CHANGED','VERSION_CHANGED',
                        'COUNT_CHANGED','VALUE_CHANGED','EVIDENCE_MISSING','EVIDENCE_CORRUPT',
                        'EVIDENCE_INACCESSIBLE','AUDIT_MISSING','AUDIT_ALTERED','UNEXPECTED_POST_RECOVERY_RECORD')),
                    ADD CONSTRAINT CK_recovery_reconciliation_difference_hashes CHECK (
                        (expected_sha256 IS NULL OR expected_sha256 ~ '^[0-9a-f]{64}$') AND
                        (actual_sha256 IS NULL OR actual_sha256 ~ '^[0-9a-f]{64}$'));

                CREATE FUNCTION reject_recovery_reconciliation_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    RAISE EXCEPTION USING
                        ERRCODE = '55000',
                        MESSAGE = 'RECOVERY_RECONCILIATION_APPEND_ONLY';
                END;
                $function$;

                CREATE TRIGGER recovery_reconciliation_append_only
                BEFORE UPDATE OR DELETE ON recovery_reconciliation
                FOR EACH ROW EXECUTE FUNCTION reject_recovery_reconciliation_mutation();

                CREATE TRIGGER recovery_reconciliation_event_append_only
                BEFORE UPDATE OR DELETE ON recovery_reconciliation_event
                FOR EACH ROW EXECUTE FUNCTION reject_recovery_reconciliation_mutation();

                CREATE TRIGGER recovery_reconciliation_difference_append_only
                BEFORE UPDATE OR DELETE ON recovery_reconciliation_difference
                FOR EACH ROW EXECUTE FUNCTION reject_recovery_reconciliation_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Destructive rollback is blocked for append-only recovery reconciliation data.");
        }
    }
}
