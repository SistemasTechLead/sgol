using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalChecklist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_task_requirement",
                table: "operational_task");

            migrationBuilder.DropCheckConstraint(
                name: "CK_sequence_stage_snapshot",
                table: "operational_sequence_stage");

            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_assignment_schedule",
                table: "operational_assignment");

            migrationBuilder.AddColumn<string>(
                name: "ChecklistJson",
                table: "operational_task",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "ChecklistJson",
                table: "operational_sequence_stage",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "ChecklistDraftJson",
                table: "operational_assignment",
                type: "text",
                nullable: false,
                defaultValue: "null");

            migrationBuilder.AddColumn<string>(
                name: "ChecklistJson",
                table: "operational_assignment",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0005-7000-8000-000000000005"),
                column: "ChecklistJson",
                value: "[]");

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0007-7000-8000-000000000007"),
                column: "ChecklistJson",
                value: "[]");

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0008-7000-8000-000000000008"),
                column: "ChecklistJson",
                value: "[]");

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0011-7000-8000-000000000011"),
                column: "ChecklistJson",
                value: "[]");

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0018-7000-8000-000000000018"),
                column: "ChecklistJson",
                value: "[]");

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0026-7000-8000-000000000026"),
                column: "ChecklistJson",
                value: "[]");

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0092-7000-8000-000000000092"),
                column: "ChecklistJson",
                value: "[]");

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0093-7000-8000-000000000093"),
                column: "ChecklistJson",
                value: "[]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_task_requirement",
                table: "operational_task",
                sql: "\"Requirement\" IN ('FOTOGRAFIA','CAPTURA_PANTALLA','COMENTARIO','DOCUMENTO','EXCEL','CHECKLIST','NINGUNO')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_sequence_stage_snapshot",
                table: "operational_sequence_stage",
                sql: "\"TaskRevision\" > 0 AND \"LoadMinutes\" BETWEEN 1 AND 4800 AND \"Role\" IN ('PISO_VENTAS','SUBCOORDINACION','ADMINISTRACION','DIRECCION') AND \"Requirement\" IN ('FOTOGRAFIA','CAPTURA_PANTALLA','COMENTARIO','DOCUMENTO','EXCEL','CHECKLIST','NINGUNO') AND \"MinimumPhotos\" BETWEEN 1 AND 20 AND \"MaximumPhotos\" BETWEEN \"MinimumPhotos\" AND 20 AND \"MaximumPhotoBytes\" BETWEEN 1048576 AND 31457280 AND \"Revision\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_assignment_schedule",
                table: "operational_assignment",
                sql: "\"ScheduleMode\" IN ('DISTRIBUIDA','DIAS_FIJOS','PROGRESO') AND \"Requirement\" IN ('FOTOGRAFIA','CAPTURA_PANTALLA','COMENTARIO','DOCUMENTO','EXCEL','CHECKLIST','NINGUNO') AND (\"AssignedRole\" IS NULL OR \"AssignedRole\" IN ('DIRECCION','ADMINISTRACION','SUBCOORDINACION','PISO_VENTAS'))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM operational_task WHERE "Requirement" = 'CHECKLIST')
                    OR EXISTS (SELECT 1 FROM operational_assignment WHERE "Requirement" = 'CHECKLIST' OR "ChecklistDraftJson" <> 'null')
                    OR EXISTS (SELECT 1 FROM operational_sequence_stage WHERE "Requirement" = 'CHECKLIST') THEN
                    RAISE EXCEPTION 'Checklist definitions and evidence exist. Preserve their history before removing this migration.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_task_requirement",
                table: "operational_task");

            migrationBuilder.DropCheckConstraint(
                name: "CK_sequence_stage_snapshot",
                table: "operational_sequence_stage");

            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_assignment_schedule",
                table: "operational_assignment");

            migrationBuilder.DropColumn(
                name: "ChecklistJson",
                table: "operational_task");

            migrationBuilder.DropColumn(
                name: "ChecklistJson",
                table: "operational_sequence_stage");

            migrationBuilder.DropColumn(
                name: "ChecklistDraftJson",
                table: "operational_assignment");

            migrationBuilder.DropColumn(
                name: "ChecklistJson",
                table: "operational_assignment");

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_task_requirement",
                table: "operational_task",
                sql: "\"Requirement\" IN ('FOTOGRAFIA','CAPTURA_PANTALLA','COMENTARIO','DOCUMENTO','EXCEL','NINGUNO')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_sequence_stage_snapshot",
                table: "operational_sequence_stage",
                sql: "\"TaskRevision\" > 0 AND \"LoadMinutes\" BETWEEN 1 AND 4800 AND \"Role\" IN ('PISO_VENTAS','SUBCOORDINACION','ADMINISTRACION','DIRECCION') AND \"Requirement\" IN ('FOTOGRAFIA','CAPTURA_PANTALLA','COMENTARIO','DOCUMENTO','EXCEL','NINGUNO') AND \"MinimumPhotos\" BETWEEN 1 AND 20 AND \"MaximumPhotos\" BETWEEN \"MinimumPhotos\" AND 20 AND \"MaximumPhotoBytes\" BETWEEN 1048576 AND 31457280 AND \"Revision\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_assignment_schedule",
                table: "operational_assignment",
                sql: "\"ScheduleMode\" IN ('DISTRIBUIDA','DIAS_FIJOS','PROGRESO') AND \"Requirement\" IN ('FOTOGRAFIA','CAPTURA_PANTALLA','COMENTARIO','DOCUMENTO','EXCEL','NINGUNO') AND (\"AssignedRole\" IS NULL OR \"AssignedRole\" IN ('DIRECCION','ADMINISTRACION','SUBCOORDINACION','PISO_VENTAS'))");
        }
    }
}
