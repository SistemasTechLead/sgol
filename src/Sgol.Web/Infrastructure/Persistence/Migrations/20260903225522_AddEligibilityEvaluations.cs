using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEligibilityEvaluations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "eligibility_evaluation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluation_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    obligation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    eligibility_date = table.Column<DateOnly>(type: "date", nullable: false),
                    eligibility_date_source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    input_snapshot = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    result = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    winner_person_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eligibility_evaluation", x => x.id);
                    table.CheckConstraint("CK_eligibility_evaluation_date_source", "eligibility_date_source IN ('MANUAL_REQUEST','SCHEDULED_OCCURRENCE')");
                    table.CheckConstraint("CK_eligibility_evaluation_no_winner", "winner_person_id IS NULL");
                    table.CheckConstraint("CK_eligibility_evaluation_result", "result IN ('CANDIDATOS_ELEGIBLES','SIN_CANDIDATO_ELEGIBLE')");
                    table.CheckConstraint("CK_eligibility_evaluation_snapshot_object", "jsonb_typeof(input_snapshot) = 'object'");
                    table.ForeignKey(
                        name: "FK_eligibility_evaluation_eligibility_policy_version_policy_ve~",
                        column: x => x.policy_version_id,
                        principalTable: "eligibility_policy_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_eligibility_evaluation_person_winner_person_id",
                        column: x => x.winner_person_id,
                        principalTable: "person",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_eligibility_evaluation_work_obligation_obligation_id",
                        column: x => x.obligation_id,
                        principalTable: "work_obligation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "eligibility_candidate",
                columns: table => new
                {
                    evaluation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_eligible = table.Column<bool>(type: "boolean", nullable: false),
                    reasons = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    active_load = table.Column<int>(type: "integer", nullable: true),
                    last_auto_assignment_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    stable_code = table.Column<string>(type: "text", nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eligibility_candidate", x => new { x.evaluation_id, x.person_id });
                    table.CheckConstraint("CK_eligibility_candidate_hu016_fields", "active_load IS NULL AND last_auto_assignment_at IS NULL AND rank IS NULL");
                    table.CheckConstraint("CK_eligibility_candidate_reasons", "jsonb_typeof(reasons) = 'array' AND reasons <@ '[\"PERSONA_INACTIVA\",\"EMPLEO_NO_VIGENTE\",\"SUCURSAL_NO_COINCIDE\",\"ROL_ACTIVO_AUSENTE\",\"ROL_REQUERIDO_NO_COINCIDE\",\"DISPONIBILIDAD_AUSENTE\",\"DISPONIBILIDAD_NO_POSITIVA\",\"TURNO_NO_COINCIDE\"]'::jsonb AND ((is_eligible AND jsonb_array_length(reasons) = 0) OR (NOT is_eligible AND jsonb_array_length(reasons) > 0))");
                    table.CheckConstraint("CK_eligibility_candidate_stable_code", "btrim(stable_code) <> ''");
                    table.ForeignKey(
                        name: "FK_eligibility_candidate_eligibility_evaluation_evaluation_id",
                        column: x => x.evaluation_id,
                        principalTable: "eligibility_evaluation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_eligibility_candidate_person_person_id",
                        column: x => x.person_id,
                        principalTable: "person",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_eligibility_candidate_person_id",
                table: "eligibility_candidate",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "IX_eligibility_evaluation_obligation_id_evaluated_at_id",
                table: "eligibility_evaluation",
                columns: new[] { "obligation_id", "evaluated_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_eligibility_evaluation_policy_version_id",
                table: "eligibility_evaluation",
                column: "policy_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_eligibility_evaluation_winner_person_id",
                table: "eligibility_evaluation",
                column: "winner_person_id");

            migrationBuilder.CreateIndex(
                name: "UX_eligibility_evaluation_request",
                table: "eligibility_evaluation",
                column: "evaluation_request_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Rollback is blocked for append-only eligibility history.");
        }
    }
}
