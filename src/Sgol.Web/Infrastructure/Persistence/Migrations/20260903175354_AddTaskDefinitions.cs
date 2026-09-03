using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Sgol.Web.Infrastructure.Persistence.Versioning;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "task_definition",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_definition", x => x.id);
                    table.CheckConstraint("CK_task_definition_mvp_code", "task_code IN ('TAR-0005','TAR-0007','TAR-0008','TAR-0011','TAR-0018','TAR-0026','TAR-0092','TAR-0093')");
                });

            migrationBuilder.CreateTable(
                name: "task_definition_version",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    effective_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    task_payload = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    supersedes_id = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_definition_version", x => x.id);
                    table.CheckConstraint("CK_task_definition_version_lifecycle", "(status = 'BORRADOR' AND effective_from IS NULL AND effective_to IS NULL AND reason IS NULL AND supersedes_id IS NULL) OR (status IN ('VIGENTE','INACTIVA_NUEVAS') AND effective_from IS NOT NULL AND effective_to IS NULL AND btrim(reason) <> '') OR (status = 'SUSTITUIDA' AND effective_from IS NOT NULL AND effective_to > effective_from AND btrim(reason) <> '')");
                    table.CheckConstraint("CK_task_definition_version_not_self_superseding", "supersedes_id IS NULL OR supersedes_id <> id");
                    table.CheckConstraint("CK_task_definition_version_number", "version_no > 0");
                    table.CheckConstraint("CK_task_definition_version_row_version", "row_version > 0");
                    table.CheckConstraint("CK_task_definition_version_schema", "schema_version = 1 AND task_payload = '{}'::jsonb");
                    table.CheckConstraint("CK_task_definition_version_status", "status IN ('BORRADOR','VIGENTE','SUSTITUIDA','INACTIVA_NUEVAS')");
                    table.ForeignKey(
                        name: "FK_task_definition_version_configuration_release_release_id",
                        column: x => x.release_id,
                        principalTable: "configuration_release",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_definition_version_task_definition_task_definition_id",
                        column: x => x.task_definition_id,
                        principalTable: "task_definition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_definition_version_task_definition_version_supersedes_~",
                        column: x => x.supersedes_id,
                        principalTable: "task_definition_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "task_definition",
                columns: new[] { "id", "name", "task_code" },
                values: new object[,]
                {
                    { new Guid("019d3a10-0005-7000-8000-000000000005"), "Monitorear el avance de ventas y activar acciones correctivas", "TAR-0005" },
                    { new Guid("019d3a10-0007-7000-8000-000000000007"), "Liberar la mercancía al vencer un Separado de Cortesía", "TAR-0007" },
                    { new Guid("019d3a10-0008-7000-8000-000000000008"), "Resolver una controversia de asignación de venta con base en evidencia", "TAR-0008" },
                    { new Guid("019d3a10-0011-7000-8000-000000000011"), "Gestionar la reparación o el cambio de una garantía autorizada después de 90 días", "TAR-0011" },
                    { new Guid("019d3a10-0018-7000-8000-000000000018"), "Montar o actualizar exhibiciones conforme al planograma y la zonificación", "TAR-0018" },
                    { new Guid("019d3a10-0026-7000-8000-000000000026"), "Programar y realizar el pago de servicios básicos", "TAR-0026" },
                    { new Guid("019d3a10-0092-7000-8000-000000000092"), "Coordinar la recepción de mercancía contra la nota de envío", "TAR-0092" },
                    { new Guid("019d3a10-0093-7000-8000-000000000093"), "Documentar y notificar incidencia de recepción de mercancía", "TAR-0093" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_task_definition_task_code",
                table: "task_definition",
                column: "task_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_definition_version_number",
                table: "task_definition_version",
                columns: new[] { "task_definition_id", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_definition_version_one_live",
                table: "task_definition_version",
                column: "task_definition_id",
                unique: true,
                filter: "status IN ('VIGENTE', 'INACTIVA_NUEVAS')");

            migrationBuilder.CreateIndex(
                name: "IX_task_definition_version_release_definition",
                table: "task_definition_version",
                columns: new[] { "release_id", "task_definition_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_definition_version_supersedes_id",
                table: "task_definition_version",
                column: "supersedes_id",
                unique: true,
                filter: "supersedes_id IS NOT NULL");

            migrationBuilder.Sql(VersioningPostgreSql.RequiredExtensionSql);
            migrationBuilder.Sql(
                """
                ALTER TABLE "task_definition_version"
                ADD CONSTRAINT "EX_task_definition_version_validity"
                EXCLUDE USING gist (
                    "task_definition_id" WITH =,
                    tstzrange("effective_from", "effective_to", '[)') WITH &&
                )
                WHERE ("status" IN ('VIGENTE', 'SUSTITUIDA', 'INACTIVA_NUEVAS'))
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Rollback is intentionally blocked for this forward-only migration.");
        }
    }
}
