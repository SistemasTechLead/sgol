using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkObligations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_generation_request_hu014_no_obligation",
                table: "generation_request");

            migrationBuilder.CreateTable(
                name: "work_obligation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_definition_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    generation_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origin_reference = table.Column<string>(type: "text", nullable: false),
                    input_payload = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    execution_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    concluded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    concluded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_obligation", x => x.id);
                    table.UniqueConstraint("AK_work_obligation_id_generation_request_id", x => new { x.id, x.generation_request_id });
                    table.CheckConstraint("CK_work_obligation_conclusion", "(execution_status = 'PENDIENTE' AND concluded_at IS NULL AND concluded_by IS NULL) OR (execution_status = 'CONCLUIDA' AND concluded_at IS NOT NULL AND concluded_by IS NOT NULL)");
                    table.CheckConstraint("CK_work_obligation_execution_status", "execution_status IN ('PENDIENTE','CONCLUIDA')");
                    table.CheckConstraint("CK_work_obligation_origin", "btrim(origin_reference) <> ''");
                    table.CheckConstraint("CK_work_obligation_row_version", "row_version > 0");
                    table.ForeignKey(
                        name: "FK_work_obligation_branch_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_obligation_generation_request_generation_request_id",
                        column: x => x.generation_request_id,
                        principalTable: "generation_request",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_obligation_task_definition_version_task_definition_ver~",
                        column: x => x.task_definition_version_id,
                        principalTable: "task_definition_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_obligation_week_period_period_id",
                        column: x => x.period_id,
                        principalTable: "week_period",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_generation_request_obligation_id_id",
                table: "generation_request",
                columns: new[] { "obligation_id", "id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_generation_request_obligation_id",
                table: "generation_request",
                column: "obligation_id",
                unique: true,
                filter: "obligation_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_work_obligation_branch_id_execution_status_due_at",
                table: "work_obligation",
                columns: new[] { "branch_id", "execution_status", "due_at" });

            migrationBuilder.CreateIndex(
                name: "IX_work_obligation_period_id",
                table: "work_obligation",
                column: "period_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_obligation_task_definition_version_id",
                table: "work_obligation",
                column: "task_definition_version_id");

            migrationBuilder.CreateIndex(
                name: "UX_work_obligation_generation_request",
                table: "work_obligation",
                column: "generation_request_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_generation_request_work_obligation_obligation_id_id",
                table: "generation_request",
                columns: new[] { "obligation_id", "id" },
                principalTable: "work_obligation",
                principalColumns: new[] { "id", "generation_request_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("HU-015 migrations are forward-only; rollback is blocked.");
        }
    }
}
