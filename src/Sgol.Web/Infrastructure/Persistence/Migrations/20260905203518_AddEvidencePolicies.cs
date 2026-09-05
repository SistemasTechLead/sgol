using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Sgol.Web.Infrastructure.Persistence.Versioning;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEvidencePolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "evidence_policy_version_id",
                table: "work_obligation",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "evidence_policy_version",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_definition_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: false),
                    based_on_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    effective_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    supersedes_id = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evidence_policy_version", x => x.id);
                    table.UniqueConstraint("AK_evidence_policy_version_id_task_definition_id", x => new { x.id, x.task_definition_id });
                    table.CheckConstraint("CK_evidence_policy_version_lifecycle", "(status = 'BORRADOR' AND effective_from IS NULL AND effective_to IS NULL AND reason IS NULL AND supersedes_id IS NULL) OR (status = 'VIGENTE' AND effective_from IS NOT NULL AND effective_to IS NULL AND btrim(reason) <> '') OR (status = 'SUSTITUIDA' AND effective_from IS NOT NULL AND effective_to > effective_from AND btrim(reason) <> '')");
                    table.CheckConstraint("CK_evidence_policy_version_not_self_superseding", "supersedes_id IS NULL OR supersedes_id <> id");
                    table.CheckConstraint("CK_evidence_policy_version_number", "version_no > 0");
                    table.CheckConstraint("CK_evidence_policy_version_row_version", "row_version > 0");
                    table.CheckConstraint("CK_evidence_policy_version_status", "status IN ('BORRADOR', 'VIGENTE', 'SUSTITUIDA')");
                    table.ForeignKey(
                        name: "FK_evidence_policy_version_configuration_release_release_id",
                        column: x => x.release_id,
                        principalTable: "configuration_release",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_evidence_policy_version_evidence_policy_version_based_on_id",
                        column: x => x.based_on_id,
                        principalTable: "evidence_policy_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_evidence_policy_version_evidence_policy_version_supersedes_~",
                        column: x => x.supersedes_id,
                        principalTable: "evidence_policy_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_evidence_policy_version_task_definition_task_definition_id",
                        column: x => x.task_definition_id,
                        principalTable: "task_definition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_evidence_policy_version_task_definition_version_task_defini~",
                        columns: x => new { x.task_definition_version_id, x.task_definition_id },
                        principalTable: "task_definition_version",
                        principalColumns: new[] { "id", "task_definition_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evidence_requirement_catalog",
                columns: table => new
                {
                    task_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requirement_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    condition_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ordinal = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evidence_requirement_catalog", x => new { x.task_definition_id, x.requirement_code });
                    table.UniqueConstraint("AK_evidence_requirement_catalog_contract_key", x => new { x.task_definition_id, x.requirement_code, x.kind, x.condition_code });
                    table.CheckConstraint("CK_evidence_requirement_catalog_condition", "condition_code IN ('SIEMPRE','DIFERENCIA_O_DANO')");
                    table.CheckConstraint("CK_evidence_requirement_catalog_conditional_photo", "condition_code <> 'DIFERENCIA_O_DANO' OR (requirement_code = 'FOTO_DIFERENCIA_DANO' AND kind = 'FOTOGRAFIA')");
                    table.CheckConstraint("CK_evidence_requirement_catalog_kind", "kind IN ('REGISTRO_DIGITAL','DOCUMENTO_REFERENCIADO','FOTOGRAFIA','FORMULARIO_REFERENCIADO','CHECKLIST_ESTRUCTURADO','DATO_ESTRUCTURADO')");
                    table.CheckConstraint("CK_evidence_requirement_catalog_ordinal", "ordinal > 0");
                    table.ForeignKey(
                        name: "FK_evidence_requirement_catalog_task_definition_task_definitio~",
                        column: x => x.task_definition_id,
                        principalTable: "task_definition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evidence_requirement_version",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requirement_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    condition_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ordinal = table.Column<short>(type: "smallint", nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evidence_requirement_version", x => x.id);
                    table.CheckConstraint("CK_evidence_requirement_version_ordinal", "ordinal > 0");
                    table.CheckConstraint("CK_evidence_requirement_version_required", "is_required");
                    table.ForeignKey(
                        name: "FK_evidence_requirement_version_evidence_policy_version_policy~",
                        columns: x => new { x.policy_version_id, x.task_definition_id },
                        principalTable: "evidence_policy_version",
                        principalColumns: new[] { "id", "task_definition_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_evidence_requirement_version_evidence_requirement_catalog_t~",
                        columns: x => new { x.task_definition_id, x.requirement_code, x.kind, x.condition_code },
                        principalTable: "evidence_requirement_catalog",
                        principalColumns: new[] { "task_definition_id", "requirement_code", "kind", "condition_code" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "evidence_requirement_catalog",
                columns: new[] { "requirement_code", "task_definition_id", "condition_code", "kind", "ordinal" },
                values: new object[,]
                {
                    { "ACCION_O_CONFORMIDAD", new Guid("019d3a10-0005-7000-8000-000000000005"), "SIEMPRE", "REGISTRO_DIGITAL", (short)2 },
                    { "CALCULO_AVANCE", new Guid("019d3a10-0005-7000-8000-000000000005"), "SIEMPRE", "REGISTRO_DIGITAL", (short)1 },
                    { "FECHA_HORA", new Guid("019d3a10-0007-7000-8000-000000000007"), "SIEMPRE", "DATO_ESTRUCTURADO", (short)3 },
                    { "LIBERACION", new Guid("019d3a10-0007-7000-8000-000000000007"), "SIEMPRE", "REGISTRO_DIGITAL", (short)1 },
                    { "MERCANCIA", new Guid("019d3a10-0007-7000-8000-000000000007"), "SIEMPRE", "DATO_ESTRUCTURADO", (short)2 },
                    { "RETORNO_EXHIBICION", new Guid("019d3a10-0007-7000-8000-000000000007"), "SIEMPRE", "REGISTRO_DIGITAL", (short)4 },
                    { "AVISO_INTERNO", new Guid("019d3a10-0008-7000-8000-000000000008"), "SIEMPRE", "REGISTRO_DIGITAL", (short)5 },
                    { "DECISION", new Guid("019d3a10-0008-7000-8000-000000000008"), "SIEMPRE", "REGISTRO_DIGITAL", (short)3 },
                    { "EXPEDIENTE", new Guid("019d3a10-0008-7000-8000-000000000008"), "SIEMPRE", "DOCUMENTO_REFERENCIADO", (short)1 },
                    { "FUNDAMENTO", new Guid("019d3a10-0008-7000-8000-000000000008"), "SIEMPRE", "DATO_ESTRUCTURADO", (short)4 },
                    { "SECUENCIA", new Guid("019d3a10-0008-7000-8000-000000000008"), "SIEMPRE", "REGISTRO_DIGITAL", (short)2 },
                    { "AUTORIZACION", new Guid("019d3a10-0011-7000-8000-000000000011"), "SIEMPRE", "DOCUMENTO_REFERENCIADO", (short)2 },
                    { "COMPROBANTES", new Guid("019d3a10-0011-7000-8000-000000000011"), "SIEMPRE", "DOCUMENTO_REFERENCIADO", (short)4 },
                    { "ENTREGA", new Guid("019d3a10-0011-7000-8000-000000000011"), "SIEMPRE", "REGISTRO_DIGITAL", (short)5 },
                    { "EVALUACION", new Guid("019d3a10-0011-7000-8000-000000000011"), "SIEMPRE", "REGISTRO_DIGITAL", (short)1 },
                    { "REPARACION_O_CAMBIO", new Guid("019d3a10-0011-7000-8000-000000000011"), "SIEMPRE", "REGISTRO_DIGITAL", (short)3 },
                    { "CHECKLIST_COMPLETO", new Guid("019d3a10-0018-7000-8000-000000000018"), "SIEMPRE", "CHECKLIST_ESTRUCTURADO", (short)1 },
                    { "FOTOGRAFIA_FINAL", new Guid("019d3a10-0018-7000-8000-000000000018"), "SIEMPRE", "FOTOGRAFIA", (short)2 },
                    { "PLANOGRAMA_O_LISTA", new Guid("019d3a10-0018-7000-8000-000000000018"), "SIEMPRE", "DOCUMENTO_REFERENCIADO", (short)3 },
                    { "COMPROBANTE_LOCALIZABLE", new Guid("019d3a10-0026-7000-8000-000000000026"), "SIEMPRE", "DOCUMENTO_REFERENCIADO", (short)2 },
                    { "FORM_ADM_02", new Guid("019d3a10-0026-7000-8000-000000000026"), "SIEMPRE", "FORMULARIO_REFERENCIADO", (short)1 },
                    { "DOCUMENTO_RECEPCION", new Guid("019d3a10-0092-7000-8000-000000000092"), "SIEMPRE", "DOCUMENTO_REFERENCIADO", (short)1 },
                    { "F_ENT_001", new Guid("019d3a10-0092-7000-8000-000000000092"), "SIEMPRE", "FORMULARIO_REFERENCIADO", (short)2 },
                    { "FOTO_DIFERENCIA_DANO", new Guid("019d3a10-0092-7000-8000-000000000092"), "DIFERENCIA_O_DANO", "FOTOGRAFIA", (short)3 },
                    { "ANOTACION_F_ENT_001", new Guid("019d3a10-0093-7000-8000-000000000093"), "SIEMPRE", "FORMULARIO_REFERENCIADO", (short)2 },
                    { "CONSTANCIA_AVISO_INTERNO", new Guid("019d3a10-0093-7000-8000-000000000093"), "SIEMPRE", "REGISTRO_DIGITAL", (short)3 },
                    { "FOTOGRAFIA_INCIDENCIA", new Guid("019d3a10-0093-7000-8000-000000000093"), "SIEMPRE", "FOTOGRAFIA", (short)1 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_work_obligation_evidence_policy_version_id",
                table: "work_obligation",
                column: "evidence_policy_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_evidence_policy_version_based_on_id",
                table: "evidence_policy_version",
                column: "based_on_id");

            migrationBuilder.CreateIndex(
                name: "IX_evidence_policy_version_exact_task_version",
                table: "evidence_policy_version",
                columns: new[] { "task_definition_version_id", "task_definition_id" });

            migrationBuilder.CreateIndex(
                name: "IX_evidence_policy_version_number",
                table: "evidence_policy_version",
                columns: new[] { "task_definition_id", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_evidence_policy_version_one_current",
                table: "evidence_policy_version",
                column: "task_definition_id",
                unique: true,
                filter: "status = 'VIGENTE'");

            migrationBuilder.CreateIndex(
                name: "IX_evidence_policy_version_release_task",
                table: "evidence_policy_version",
                columns: new[] { "release_id", "task_definition_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_evidence_policy_version_supersedes_id",
                table: "evidence_policy_version",
                column: "supersedes_id",
                unique: true,
                filter: "supersedes_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_evidence_requirement_catalog_task_ordinal",
                table: "evidence_requirement_catalog",
                columns: new[] { "task_definition_id", "ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_evidence_requirement_version_policy_code",
                table: "evidence_requirement_version",
                columns: new[] { "policy_version_id", "requirement_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_evidence_requirement_version_policy_ordinal",
                table: "evidence_requirement_version",
                columns: new[] { "policy_version_id", "ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_evidence_requirement_version_policy_version_id_task_definit~",
                table: "evidence_requirement_version",
                columns: new[] { "policy_version_id", "task_definition_id" });

            migrationBuilder.CreateIndex(
                name: "IX_evidence_requirement_version_task_definition_id_requirement~",
                table: "evidence_requirement_version",
                columns: new[] { "task_definition_id", "requirement_code", "kind", "condition_code" });

            migrationBuilder.AddForeignKey(
                name: "FK_work_obligation_evidence_policy_version_evidence_policy_ver~",
                table: "work_obligation",
                column: "evidence_policy_version_id",
                principalTable: "evidence_policy_version",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql(VersioningPostgreSql.RequiredExtensionSql);
            migrationBuilder.Sql(
                """
                ALTER TABLE "evidence_policy_version"
                ADD CONSTRAINT "EX_evidence_policy_version_validity"
                EXCLUDE USING gist (
                    "task_definition_id" WITH =,
                    tstzrange("effective_from", "effective_to", '[)') WITH &&
                )
                WHERE ("status" IN ('VIGENTE', 'SUSTITUIDA'))
                """);
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION sgol_reject_evidence_contract_change()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    RAISE EXCEPTION 'Evidence requirement contracts are append-only.'
                        USING ERRCODE = '55000';
                END;
                $function$;

                CREATE TRIGGER evidence_requirement_catalog_append_only
                BEFORE UPDATE OR DELETE ON evidence_requirement_catalog
                FOR EACH ROW
                EXECUTE FUNCTION sgol_reject_evidence_contract_change();

                CREATE TRIGGER evidence_requirement_version_append_only
                BEFORE UPDATE OR DELETE ON evidence_requirement_version
                FOR EACH ROW
                EXECUTE FUNCTION sgol_reject_evidence_contract_change();
                """);
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION sgol_protect_obligation_evidence_policy()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    IF NEW.evidence_policy_version_id IS DISTINCT FROM OLD.evidence_policy_version_id THEN
                        RAISE EXCEPTION 'The evidence policy captured by an obligation is immutable.'
                            USING ERRCODE = '55000';
                    END IF;
                    RETURN NEW;
                END;
                $function$;

                CREATE TRIGGER work_obligation_evidence_policy_immutable
                BEFORE UPDATE OF evidence_policy_version_id ON work_obligation
                FOR EACH ROW
                EXECUTE FUNCTION sgol_protect_obligation_evidence_policy();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Rollback is intentionally blocked for this forward-only migration.");
        }
    }
}
