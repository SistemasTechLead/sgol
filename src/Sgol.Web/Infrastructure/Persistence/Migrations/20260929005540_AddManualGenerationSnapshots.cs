using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManualGenerationSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "manual_origin_key",
                table: "work_obligation",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "manual_task_code",
                table: "work_obligation",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "parent_obligation_id",
                table: "work_obligation",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_obligation_parent_obligation_id",
                table: "work_obligation",
                column: "parent_obligation_id");

            migrationBuilder.CreateIndex(
                name: "UX_work_obligation_manual_active",
                table: "work_obligation",
                columns: new[] { "branch_id", "manual_task_code", "manual_origin_key" },
                unique: true,
                filter: "manual_task_code IN ('TAR-0008','TAR-0092') AND execution_status = 'PENDIENTE'");

            migrationBuilder.CreateIndex(
                name: "UX_work_obligation_manual_permanent",
                table: "work_obligation",
                columns: new[] { "branch_id", "manual_task_code", "manual_origin_key" },
                unique: true,
                filter: "manual_task_code IN ('TAR-0007','TAR-0011','TAR-0018','TAR-0093')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_work_obligation_manual_snapshot",
                table: "work_obligation",
                sql: "(manual_task_code IS NULL AND manual_origin_key IS NULL AND parent_obligation_id IS NULL AND COALESCE(input_payload->>'schemaVersion', '') <> '2') OR (manual_task_code IS NOT NULL AND manual_task_code IN ('TAR-0007','TAR-0008','TAR-0011','TAR-0018','TAR-0092','TAR-0093') AND manual_origin_key IS NOT NULL AND manual_origin_key ~ '^[0-9a-f]{64}$' AND input_payload IS NOT NULL AND COALESCE((input_payload->>'schemaVersion') = '2', false) AND COALESCE(input_payload->'input'->>'taskCode' = manual_task_code, false) AND COALESCE(jsonb_typeof(input_payload->'originIdentity') = 'array', false) AND COALESCE(jsonb_typeof(input_payload->'calendarDayVersionIds') = 'array', false) AND ((manual_task_code = 'TAR-0093' AND parent_obligation_id IS NOT NULL AND parent_obligation_id <> id) OR (manual_task_code <> 'TAR-0093' AND parent_obligation_id IS NULL)))");

            migrationBuilder.AddForeignKey(
                name: "FK_work_obligation_work_obligation_parent_obligation_id",
                table: "work_obligation",
                column: "parent_obligation_id",
                principalTable: "work_obligation",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Reversión bloqueada: los snapshots manuales conservan historia de obligaciones.");
        }
    }
}
