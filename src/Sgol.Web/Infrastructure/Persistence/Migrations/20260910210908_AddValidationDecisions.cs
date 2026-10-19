using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddValidationDecisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "validation_requirement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    obligation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_validation_requirement", x => x.id);
                    table.CheckConstraint("CK_validation_requirement_row_version", "row_version > 0");
                    table.CheckConstraint("CK_validation_requirement_state", "(status = 'PENDIENTE' AND resolved_at IS NULL) OR (status = 'RESUELTA' AND resolved_at IS NOT NULL)");
                    table.CheckConstraint("CK_validation_requirement_status", "status IN ('PENDIENTE','RESUELTA')");
                    table.ForeignKey(
                        name: "FK_validation_requirement_validation_policy_version_policy_ver~",
                        column: x => x.policy_version_id,
                        principalTable: "validation_policy_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_validation_requirement_work_obligation_obligation_id",
                        column: x => x.obligation_id,
                        principalTable: "work_obligation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "validation_decision_version",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    requirement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    result = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    foundation = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    authority_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    validator_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    validator_person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    validator_role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    assignment_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    responsible_person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    supersedes_id = table.Column<Guid>(type: "uuid", nullable: true),
                    evidence_review_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_validation_decision_version", x => x.id);
                    table.CheckConstraint("CK_validation_decision_authority", "authority_type IN ('ORDINARIA','ESCALAMIENTO','AUTOVALIDACION_DIRECCION','SUSTITUCION_ORIGINAL','SUSTITUCION_SUPERIOR')");
                    table.CheckConstraint("CK_validation_decision_foundation", "char_length(foundation) BETWEEN 1 AND 1000 AND foundation = btrim(foundation) AND replace(foundation, chr(9), '') !~ '[<>[:cntrl:]]'");
                    table.CheckConstraint("CK_validation_decision_reason", "((authority_type IN ('ORDINARIA','AUTOVALIDACION_DIRECCION') AND reason IS NULL) OR (authority_type NOT IN ('ORDINARIA','AUTOVALIDACION_DIRECCION') AND char_length(reason) BETWEEN 1 AND 500 AND reason = btrim(reason) AND replace(reason, chr(9), '') !~ '[<>[:cntrl:]]'))");
                    table.CheckConstraint("CK_validation_decision_result", "result IN ('CUMPLIDA','INCOMPLETA','NO_CUMPLIDA')");
                    table.CheckConstraint("CK_validation_decision_status", "status IN ('VIGENTE','SUSTITUIDA')");
                    table.CheckConstraint("CK_validation_decision_version", "version_no > 0 AND ((version_no = 1 AND supersedes_id IS NULL) OR (version_no > 1 AND supersedes_id IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_validation_decision_version_app_user_validator_user_id",
                        column: x => x.validator_user_id,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_validation_decision_version_assignment_version_assignment_v~",
                        column: x => x.assignment_version_id,
                        principalTable: "assignment_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_validation_decision_version_evidence_review_snapshot_eviden~",
                        column: x => x.evidence_review_snapshot_id,
                        principalTable: "evidence_review_snapshot",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_validation_decision_version_person_responsible_person_id",
                        column: x => x.responsible_person_id,
                        principalTable: "person",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_validation_decision_version_person_validator_person_id",
                        column: x => x.validator_person_id,
                        principalTable: "person",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_validation_decision_version_validation_decision_version_sup~",
                        column: x => x.supersedes_id,
                        principalTable: "validation_decision_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_validation_decision_version_validation_requirement_requirem~",
                        column: x => x.requirement_id,
                        principalTable: "validation_requirement",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_validation_decision_version_assignment_version_id",
                table: "validation_decision_version",
                column: "assignment_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_validation_decision_version_evidence_review_snapshot_id",
                table: "validation_decision_version",
                column: "evidence_review_snapshot_id");

            migrationBuilder.CreateIndex(
                name: "IX_validation_decision_version_requirement_id_version_no",
                table: "validation_decision_version",
                columns: new[] { "requirement_id", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_validation_decision_version_responsible_person_id",
                table: "validation_decision_version",
                column: "responsible_person_id");

            migrationBuilder.CreateIndex(
                name: "IX_validation_decision_version_supersedes_id",
                table: "validation_decision_version",
                column: "supersedes_id",
                unique: true,
                filter: "supersedes_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_validation_decision_version_validator_person_id",
                table: "validation_decision_version",
                column: "validator_person_id");

            migrationBuilder.CreateIndex(
                name: "IX_validation_decision_version_validator_user_id_decided_at_id",
                table: "validation_decision_version",
                columns: new[] { "validator_user_id", "decided_at", "id" });

            migrationBuilder.CreateIndex(
                name: "UX_validation_decision_version_current_requirement",
                table: "validation_decision_version",
                column: "requirement_id",
                unique: true,
                filter: "status = 'VIGENTE'");

            migrationBuilder.CreateIndex(
                name: "IX_validation_requirement_policy_version_id_created_at_id",
                table: "validation_requirement",
                columns: new[] { "policy_version_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_validation_requirement_status_created_at_id",
                table: "validation_requirement",
                columns: new[] { "status", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "UX_validation_requirement_obligation",
                table: "validation_requirement",
                column: "obligation_id",
                unique: true);

            migrationBuilder.Sql("""
                CREATE FUNCTION sgol_validation_requirement_guard() RETURNS trigger LANGUAGE plpgsql AS $fn$
                BEGIN
                  IF TG_OP = 'INSERT' THEN
                    IF NOT EXISTS (SELECT 1 FROM work_obligation o WHERE o.id = NEW.obligation_id
                      AND o.execution_status = 'CONCLUIDA' AND o.concluded_at = NEW.created_at
                      AND o.validation_policy_version_id = NEW.policy_version_id) THEN
                      RAISE EXCEPTION 'validation requirement obligation or policy mismatch' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                  END IF;
                  IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'validation_requirement is append-only' USING ERRCODE = '23514'; END IF;
                  IF OLD.id <> NEW.id OR OLD.obligation_id <> NEW.obligation_id OR OLD.policy_version_id <> NEW.policy_version_id OR OLD.created_at <> NEW.created_at THEN
                    RAISE EXCEPTION 'validation_requirement identity is immutable' USING ERRCODE = '23514';
                  END IF;
                  IF NOT ((OLD.status = 'PENDIENTE' AND NEW.status = 'RESUELTA' AND NEW.row_version = OLD.row_version) OR
                          (OLD.status = 'RESUELTA' AND NEW.status = 'RESUELTA' AND NEW.row_version = OLD.row_version + 1)) THEN
                    RAISE EXCEPTION 'invalid validation_requirement transition' USING ERRCODE = '23514';
                  END IF;
                  RETURN NEW;
                END $fn$;
                CREATE TRIGGER trg_validation_requirement_guard BEFORE INSERT OR UPDATE OR DELETE ON validation_requirement
                FOR EACH ROW EXECUTE FUNCTION sgol_validation_requirement_guard();

                CREATE FUNCTION sgol_validation_decision_guard() RETURNS trigger LANGUAGE plpgsql AS $fn$
                BEGIN
                  IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'validation_decision_version is append-only' USING ERRCODE = '23514'; END IF;
                  IF OLD.status <> 'VIGENTE' OR NEW.status <> 'SUSTITUIDA' OR
                     ROW(OLD.id, OLD.requirement_id, OLD.version_no, OLD.result, OLD.foundation, OLD.authority_type,
                         OLD.validator_user_id, OLD.validator_person_id, OLD.validator_role, OLD.assignment_version_id,
                         OLD.responsible_person_id, OLD.decided_at, OLD.reason, OLD.supersedes_id, OLD.evidence_review_snapshot_id)
                     IS DISTINCT FROM
                     ROW(NEW.id, NEW.requirement_id, NEW.version_no, NEW.result, NEW.foundation, NEW.authority_type,
                         NEW.validator_user_id, NEW.validator_person_id, NEW.validator_role, NEW.assignment_version_id,
                         NEW.responsible_person_id, NEW.decided_at, NEW.reason, NEW.supersedes_id, NEW.evidence_review_snapshot_id) THEN
                    RAISE EXCEPTION 'validation_decision_version is immutable' USING ERRCODE = '23514';
                  END IF;
                  RETURN NEW;
                END $fn$;
                CREATE TRIGGER trg_validation_decision_guard BEFORE UPDATE OR DELETE ON validation_decision_version
                FOR EACH ROW EXECUTE FUNCTION sgol_validation_decision_guard();

                CREATE FUNCTION sgol_validation_decision_consistent() RETURNS trigger LANGUAGE plpgsql AS $fn$
                DECLARE v_obligation uuid; v_policy uuid; v_assignment_person uuid; v_snapshot_obligation uuid; v_user_person uuid;
                BEGIN
                  SELECT r.obligation_id, r.policy_version_id INTO v_obligation, v_policy FROM validation_requirement r WHERE r.id = NEW.requirement_id;
                  IF v_obligation IS NULL THEN RAISE EXCEPTION 'validation requirement missing' USING ERRCODE = '23514'; END IF;
                  IF NOT EXISTS (SELECT 1 FROM work_obligation o WHERE o.id = v_obligation AND o.execution_status = 'CONCLUIDA'
                    AND o.validation_policy_version_id = v_policy AND o.concluded_at IS NOT NULL) THEN
                    RAISE EXCEPTION 'validation obligation or policy mismatch' USING ERRCODE = '23514';
                  END IF;
                  SELECT a.person_id INTO v_assignment_person FROM assignment_version a WHERE a.id = NEW.assignment_version_id AND a.obligation_id = v_obligation;
                  SELECT s.obligation_id INTO v_snapshot_obligation FROM evidence_review_snapshot s WHERE s.id = NEW.evidence_review_snapshot_id;
                  SELECT u.person_id INTO v_user_person FROM app_user u WHERE u.id = NEW.validator_user_id;
                  IF v_assignment_person IS DISTINCT FROM NEW.responsible_person_id OR v_snapshot_obligation IS DISTINCT FROM v_obligation OR
                     v_user_person IS DISTINCT FROM NEW.validator_person_id THEN
                    RAISE EXCEPTION 'validation decision snapshot mismatch' USING ERRCODE = '23514';
                  END IF;
                  IF NEW.version_no > 1 AND NOT EXISTS (SELECT 1 FROM validation_decision_version p WHERE p.id = NEW.supersedes_id
                    AND p.requirement_id = NEW.requirement_id AND p.version_no = NEW.version_no - 1) THEN
                    RAISE EXCEPTION 'validation decision chain mismatch' USING ERRCODE = '23514';
                  END IF;
                  RETURN NEW;
                END $fn$;
                CREATE TRIGGER trg_validation_decision_consistent BEFORE INSERT ON validation_decision_version
                FOR EACH ROW EXECUTE FUNCTION sgol_validation_decision_consistent();

                CREATE FUNCTION sgol_validation_requirement_consistent() RETURNS trigger LANGUAGE plpgsql AS $fn$
                DECLARE v_status text; v_current integer; v_requirement uuid;
                BEGIN
                  IF TG_TABLE_NAME = 'validation_requirement' THEN v_requirement := NEW.id; ELSE v_requirement := NEW.requirement_id; END IF;
                  SELECT status INTO v_status FROM validation_requirement WHERE id = v_requirement;
                  SELECT count(*) INTO v_current FROM validation_decision_version WHERE requirement_id = v_requirement AND status = 'VIGENTE';
                  IF (v_status = 'PENDIENTE' AND v_current <> 0) OR (v_status = 'RESUELTA' AND v_current <> 1) THEN
                    RAISE EXCEPTION 'validation requirement/current decision mismatch' USING ERRCODE = '23514';
                  END IF;
                  RETURN NEW;
                END $fn$;
                CREATE CONSTRAINT TRIGGER trg_validation_requirement_consistent
                AFTER INSERT OR UPDATE ON validation_requirement DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION sgol_validation_requirement_consistent();
                CREATE CONSTRAINT TRIGGER trg_validation_decision_requirement_consistent
                AFTER INSERT OR UPDATE ON validation_decision_version DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION sgol_validation_requirement_consistent();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Rollback is blocked for AddValidationDecisions.");
        }
    }
}
