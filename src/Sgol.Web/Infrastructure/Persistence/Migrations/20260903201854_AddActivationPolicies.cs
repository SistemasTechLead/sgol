using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddActivationPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "activation_rule_version",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_definition_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: false),
                    based_on_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    schedule = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    origin_key_schema = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    effective_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    supersedes_id = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activation_rule_version", x => x.id);
                    table.CheckConstraint("CK_activation_rule_version_lifecycle", "(status = 'BORRADOR' AND effective_from IS NULL AND effective_to IS NULL AND reason IS NULL AND supersedes_id IS NULL) OR (status = 'VIGENTE' AND effective_from IS NOT NULL AND effective_to IS NULL AND btrim(reason) <> '') OR (status = 'SUSTITUIDA' AND effective_from IS NOT NULL AND effective_to > effective_from AND btrim(reason) <> '')");
                    table.CheckConstraint("CK_activation_rule_version_manual_schedule", "(mode = 'MANUAL' AND schedule = 'null'::jsonb) OR (mode = 'RECURRENTE' AND jsonb_typeof(schedule) = 'object')");
                    table.CheckConstraint("CK_activation_rule_version_mode", "mode IN ('MANUAL','RECURRENTE')");
                    table.CheckConstraint("CK_activation_rule_version_not_self_superseding", "supersedes_id IS NULL OR supersedes_id <> id");
                    table.CheckConstraint("CK_activation_rule_version_number", "version_no > 0");
                    table.CheckConstraint("CK_activation_rule_version_origin_schema", "origin_key_schema IN ('MANUAL_REFERENCE_V1','WORKING_DAY_WINDOW_V1','SERVICE_DUE_DATE_REFERENCE_V1')");
                    table.CheckConstraint("CK_activation_rule_version_row_version", "row_version > 0");
                    table.CheckConstraint("CK_activation_rule_version_schedule_shape", "(task_definition_id = '019d3a10-0005-7000-8000-000000000005'::uuid AND schedule = '{\"kind\":\"WORKING_DAY_WINDOWS\",\"localTimes\":[\"12:00\",\"17:00\"],\"timeZone\":\"America/Mexico_City\",\"workingDaysOnly\":true}'::jsonb) OR (task_definition_id IN ('019d3a10-0007-7000-8000-000000000007'::uuid,'019d3a10-0008-7000-8000-000000000008'::uuid,'019d3a10-0011-7000-8000-000000000011'::uuid,'019d3a10-0018-7000-8000-000000000018'::uuid,'019d3a10-0092-7000-8000-000000000092'::uuid,'019d3a10-0093-7000-8000-000000000093'::uuid) AND schedule = 'null'::jsonb) OR (task_definition_id = '019d3a10-0026-7000-8000-000000000026'::uuid AND schedule ? 'localTime' AND schedule->>'localTime' ~ '^(?:[01][0-9]|2[0-3]):[0-5][0-9]$' AND schedule - 'localTime' = '{\"kind\":\"BUSINESS_DAYS_BEFORE_DUE_DATE\",\"businessDaysBefore\":3,\"timeZone\":\"America/Mexico_City\",\"adjustDueDateToPreviousBusinessDay\":true}'::jsonb)");
                    table.CheckConstraint("CK_activation_rule_version_status", "status IN ('BORRADOR', 'VIGENTE', 'SUSTITUIDA')");
                    table.CheckConstraint("CK_activation_rule_version_task_policy", "(task_definition_id = '019d3a10-0005-7000-8000-000000000005'::uuid AND mode = 'RECURRENTE' AND origin_key_schema = 'WORKING_DAY_WINDOW_V1') OR (task_definition_id IN ('019d3a10-0007-7000-8000-000000000007'::uuid,'019d3a10-0008-7000-8000-000000000008'::uuid,'019d3a10-0011-7000-8000-000000000011'::uuid,'019d3a10-0018-7000-8000-000000000018'::uuid,'019d3a10-0092-7000-8000-000000000092'::uuid,'019d3a10-0093-7000-8000-000000000093'::uuid) AND mode = 'MANUAL' AND origin_key_schema = 'MANUAL_REFERENCE_V1') OR (task_definition_id = '019d3a10-0026-7000-8000-000000000026'::uuid AND mode = 'RECURRENTE' AND origin_key_schema = 'SERVICE_DUE_DATE_REFERENCE_V1')");
                    table.ForeignKey(
                        name: "FK_activation_rule_version_activation_rule_version_based_on_id",
                        column: x => x.based_on_id,
                        principalTable: "activation_rule_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_activation_rule_version_activation_rule_version_supersedes_~",
                        column: x => x.supersedes_id,
                        principalTable: "activation_rule_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_activation_rule_version_configuration_release_release_id",
                        column: x => x.release_id,
                        principalTable: "configuration_release",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_activation_rule_version_task_definition_task_definition_id",
                        column: x => x.task_definition_id,
                        principalTable: "task_definition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_activation_rule_version_task_definition_version_task_defini~",
                        columns: x => new { x.task_definition_version_id, x.task_definition_id },
                        principalTable: "task_definition_version",
                        principalColumns: new[] { "id", "task_definition_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_activation_rule_version_based_on_id",
                table: "activation_rule_version",
                column: "based_on_id");

            migrationBuilder.CreateIndex(
                name: "IX_activation_rule_version_exact_task_version",
                table: "activation_rule_version",
                columns: new[] { "task_definition_version_id", "task_definition_id" });

            migrationBuilder.CreateIndex(
                name: "IX_activation_rule_version_number",
                table: "activation_rule_version",
                columns: new[] { "task_definition_id", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_activation_rule_version_one_current",
                table: "activation_rule_version",
                column: "task_definition_id",
                unique: true,
                filter: "status = 'VIGENTE'");

            migrationBuilder.CreateIndex(
                name: "IX_activation_rule_version_release_task",
                table: "activation_rule_version",
                columns: new[] { "release_id", "task_definition_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_activation_rule_version_supersedes_id",
                table: "activation_rule_version",
                column: "supersedes_id",
                unique: true,
                filter: "supersedes_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "La reversión de AddActivationPolicies está bloqueada para preservar historia de configuración.");
        }
    }
}
