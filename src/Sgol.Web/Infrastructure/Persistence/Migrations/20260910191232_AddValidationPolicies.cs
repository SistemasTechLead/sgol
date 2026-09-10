using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Sgol.Web.Infrastructure.Persistence.Versioning;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddValidationPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "validation_policy_version_id",
                table: "work_obligation",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "validation_policy_version",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_definition_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: false),
                    based_on_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    executor_role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    validator_relation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    validator_role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    allowed_results = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    effective_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    supersedes_id = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_validation_policy_version", x => x.id);
                    table.UniqueConstraint("AK_validation_policy_version_id_task_definition_id", x => new { x.id, x.task_definition_id });
                    table.CheckConstraint("CK_validation_policy_version_authority", "(task_definition_id IN ('019d3a10-0007-7000-8000-000000000007', '019d3a10-0018-7000-8000-000000000018') AND executor_role = 'PISO_VENTAS' AND validator_role = 'SUBCOORDINACION') OR (task_definition_id IN ('019d3a10-0005-7000-8000-000000000005', '019d3a10-0008-7000-8000-000000000008', '019d3a10-0011-7000-8000-000000000011', '019d3a10-0092-7000-8000-000000000092', '019d3a10-0093-7000-8000-000000000093') AND executor_role = 'SUBCOORDINACION' AND validator_role = 'ADMINISTRACION') OR (task_definition_id = '019d3a10-0026-7000-8000-000000000026' AND executor_role = 'ADMINISTRACION' AND validator_role = 'DIRECCION')");
                    table.CheckConstraint("CK_validation_policy_version_lifecycle", "(status = 'BORRADOR' AND effective_from IS NULL AND effective_to IS NULL AND reason IS NULL AND supersedes_id IS NULL) OR (status = 'VIGENTE' AND effective_from IS NOT NULL AND effective_to IS NULL AND btrim(reason) <> '') OR (status = 'SUSTITUIDA' AND effective_from IS NOT NULL AND effective_to > effective_from AND btrim(reason) <> '')");
                    table.CheckConstraint("CK_validation_policy_version_not_self_superseding", "supersedes_id IS NULL OR supersedes_id <> id");
                    table.CheckConstraint("CK_validation_policy_version_number", "version_no > 0");
                    table.CheckConstraint("CK_validation_policy_version_relation", "validator_relation = 'SUPERIOR_INMEDIATO'");
                    table.CheckConstraint("CK_validation_policy_version_required", "is_required");
                    table.CheckConstraint("CK_validation_policy_version_results", "allowed_results = '[\"CUMPLIDA\",\"INCOMPLETA\",\"NO_CUMPLIDA\"]'::jsonb");
                    table.CheckConstraint("CK_validation_policy_version_row_version", "row_version > 0");
                    table.CheckConstraint("CK_validation_policy_version_status", "status IN ('BORRADOR', 'VIGENTE', 'SUSTITUIDA')");
                    table.ForeignKey(
                        name: "FK_validation_policy_version_configuration_release_release_id",
                        column: x => x.release_id,
                        principalTable: "configuration_release",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_validation_policy_version_task_definition_task_definition_id",
                        column: x => x.task_definition_id,
                        principalTable: "task_definition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_validation_policy_version_task_definition_version_task_defi~",
                        columns: x => new { x.task_definition_version_id, x.task_definition_id },
                        principalTable: "task_definition_version",
                        principalColumns: new[] { "id", "task_definition_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_validation_policy_version_validation_policy_version_based_o~",
                        columns: x => new { x.based_on_id, x.task_definition_id },
                        principalTable: "validation_policy_version",
                        principalColumns: new[] { "id", "task_definition_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_validation_policy_version_validation_policy_version_superse~",
                        column: x => x.supersedes_id,
                        principalTable: "validation_policy_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_work_obligation_validation_policy_version_id",
                table: "work_obligation",
                column: "validation_policy_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_validation_policy_version_based_on_id_task_definition_id",
                table: "validation_policy_version",
                columns: new[] { "based_on_id", "task_definition_id" });

            migrationBuilder.CreateIndex(
                name: "IX_validation_policy_version_exact_task_version",
                table: "validation_policy_version",
                columns: new[] { "task_definition_version_id", "task_definition_id" });

            migrationBuilder.CreateIndex(
                name: "IX_validation_policy_version_number",
                table: "validation_policy_version",
                columns: new[] { "task_definition_id", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_validation_policy_version_one_current",
                table: "validation_policy_version",
                column: "task_definition_id",
                unique: true,
                filter: "status = 'VIGENTE'");

            migrationBuilder.CreateIndex(
                name: "IX_validation_policy_version_release_task",
                table: "validation_policy_version",
                columns: new[] { "release_id", "task_definition_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_validation_policy_version_supersedes_id",
                table: "validation_policy_version",
                column: "supersedes_id",
                unique: true,
                filter: "supersedes_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_work_obligation_validation_policy_version_validation_policy~",
                table: "work_obligation",
                column: "validation_policy_version_id",
                principalTable: "validation_policy_version",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql(VersioningPostgreSql.RequiredExtensionSql);
            migrationBuilder.Sql(
                """
                ALTER TABLE "validation_policy_version"
                ADD CONSTRAINT "EX_validation_policy_version_validity"
                EXCLUDE USING gist (
                    "task_definition_id" WITH =,
                    tstzrange("effective_from", "effective_to", '[)') WITH &&
                )
                WHERE ("status" IN ('VIGENTE', 'SUSTITUIDA'))
                """);
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION sgol_protect_validation_policy_version()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                DECLARE
                    predecessor_task_id uuid;
                BEGIN
                    IF TG_OP = 'UPDATE' AND (
                        OLD.status = 'SUSTITUIDA' OR
                        NEW.id IS DISTINCT FROM OLD.id OR
                        NEW.task_definition_id IS DISTINCT FROM OLD.task_definition_id OR
                        NEW.task_definition_version_id IS DISTINCT FROM OLD.task_definition_version_id OR
                        NEW.release_id IS DISTINCT FROM OLD.release_id OR
                        NEW.based_on_id IS DISTINCT FROM OLD.based_on_id OR
                        NEW.version_no IS DISTINCT FROM OLD.version_no OR
                        NEW.is_required IS DISTINCT FROM OLD.is_required OR
                        NEW.executor_role IS DISTINCT FROM OLD.executor_role OR
                        NEW.validator_relation IS DISTINCT FROM OLD.validator_relation OR
                        NEW.validator_role IS DISTINCT FROM OLD.validator_role OR
                        NEW.allowed_results IS DISTINCT FROM OLD.allowed_results) THEN
                        RAISE EXCEPTION 'Validation policy semantic fields and historical versions are immutable.'
                            USING ERRCODE = '55000';
                    END IF;

                    IF NEW.supersedes_id IS NOT NULL THEN
                        SELECT task_definition_id INTO predecessor_task_id
                        FROM validation_policy_version
                        WHERE id = NEW.supersedes_id;
                        IF predecessor_task_id IS DISTINCT FROM NEW.task_definition_id THEN
                            RAISE EXCEPTION 'A validation policy can only supersede a version of the same task.'
                                USING ERRCODE = '23514';
                        END IF;
                    END IF;
                    RETURN NEW;
                END;
                $function$;

                CREATE TRIGGER validation_policy_version_protected
                BEFORE INSERT OR UPDATE ON validation_policy_version
                FOR EACH ROW
                EXECUTE FUNCTION sgol_protect_validation_policy_version();
                """);
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION sgol_protect_obligation_validation_policy()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                DECLARE
                    policy_task_version_id uuid;
                BEGIN
                    IF TG_OP = 'UPDATE' AND
                       NEW.validation_policy_version_id IS DISTINCT FROM OLD.validation_policy_version_id THEN
                        RAISE EXCEPTION 'The validation policy captured by an obligation is immutable.'
                            USING ERRCODE = '55000';
                    END IF;

                    IF NEW.validation_policy_version_id IS NOT NULL THEN
                        SELECT task_definition_version_id INTO policy_task_version_id
                        FROM validation_policy_version
                        WHERE id = NEW.validation_policy_version_id;
                        IF policy_task_version_id IS DISTINCT FROM NEW.task_definition_version_id THEN
                            RAISE EXCEPTION 'The validation policy must match the obligation task definition version.'
                                USING ERRCODE = '23514';
                        END IF;
                    END IF;
                    RETURN NEW;
                END;
                $function$;

                CREATE TRIGGER work_obligation_validation_policy_protected
                BEFORE INSERT OR UPDATE OF validation_policy_version_id, task_definition_version_id ON work_obligation
                FOR EACH ROW
                EXECUTE FUNCTION sgol_protect_obligation_validation_policy();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Rollback is intentionally blocked for this forward-only migration.");
        }
    }
}
