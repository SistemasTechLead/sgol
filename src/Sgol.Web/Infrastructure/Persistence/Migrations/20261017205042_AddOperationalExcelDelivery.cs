using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalExcelDelivery : Migration
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
                name: "CK_operational_attachment_type",
                table: "operational_attachment");

            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_assignment_schedule",
                table: "operational_assignment");

            migrationBuilder.AlterColumn<string>(
                name: "ContentType",
                table: "operational_attachment",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_task_requirement",
                table: "operational_task",
                sql: "\"Requirement\" IN ('FOTOGRAFIA','CAPTURA_PANTALLA','COMENTARIO','DOCUMENTO','EXCEL','NINGUNO')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_sequence_stage_snapshot",
                table: "operational_sequence_stage",
                sql: "\"TaskRevision\" > 0 AND \"LoadMinutes\" BETWEEN 1 AND 4800 AND \"Role\" IN ('PISO_VENTAS','SUBCOORDINACION','ADMINISTRACION','DIRECCION') AND \"Requirement\" IN ('FOTOGRAFIA','CAPTURA_PANTALLA','COMENTARIO','DOCUMENTO','EXCEL','NINGUNO') AND \"MinimumPhotos\" BETWEEN 1 AND 20 AND \"MaximumPhotos\" BETWEEN \"MinimumPhotos\" AND 20 AND \"MaximumPhotoBytes\" BETWEEN 1048576 AND 31457280 AND \"Revision\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_attachment_type",
                table: "operational_attachment",
                sql: "\"ContentType\" IN ('application/pdf','image/jpeg','image/png','application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_assignment_schedule",
                table: "operational_assignment",
                sql: "\"ScheduleMode\" IN ('DISTRIBUIDA','DIAS_FIJOS','PROGRESO') AND \"Requirement\" IN ('FOTOGRAFIA','CAPTURA_PANTALLA','COMENTARIO','DOCUMENTO','EXCEL','NINGUNO') AND (\"AssignedRole\" IS NULL OR \"AssignedRole\" IN ('DIRECCION','ADMINISTRACION','SUBCOORDINACION','PISO_VENTAS'))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_task_requirement",
                table: "operational_task");

            migrationBuilder.DropCheckConstraint(
                name: "CK_sequence_stage_snapshot",
                table: "operational_sequence_stage");

            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_attachment_type",
                table: "operational_attachment");

            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_assignment_schedule",
                table: "operational_assignment");

            migrationBuilder.AlterColumn<string>(
                name: "ContentType",
                table: "operational_attachment",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_task_requirement",
                table: "operational_task",
                sql: "\"Requirement\" IN ('FOTOGRAFIA','CAPTURA_PANTALLA','COMENTARIO','DOCUMENTO','NINGUNO')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_sequence_stage_snapshot",
                table: "operational_sequence_stage",
                sql: "\"TaskRevision\" > 0 AND \"LoadMinutes\" BETWEEN 1 AND 4800 AND \"Role\" IN ('PISO_VENTAS','SUBCOORDINACION','ADMINISTRACION','DIRECCION') AND \"Requirement\" IN ('FOTOGRAFIA','CAPTURA_PANTALLA','COMENTARIO','DOCUMENTO','NINGUNO') AND \"MinimumPhotos\" BETWEEN 1 AND 20 AND \"MaximumPhotos\" BETWEEN \"MinimumPhotos\" AND 20 AND \"MaximumPhotoBytes\" BETWEEN 1048576 AND 31457280 AND \"Revision\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_attachment_type",
                table: "operational_attachment",
                sql: "\"ContentType\" IN ('application/pdf','image/jpeg','image/png')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_assignment_schedule",
                table: "operational_assignment",
                sql: "\"ScheduleMode\" IN ('DISTRIBUIDA','DIAS_FIJOS','PROGRESO') AND \"Requirement\" IN ('FOTOGRAFIA','CAPTURA_PANTALLA','COMENTARIO','DOCUMENTO','NINGUNO') AND (\"AssignedRole\" IS NULL OR \"AssignedRole\" IN ('DIRECCION','ADMINISTRACION','SUBCOORDINACION','PISO_VENTAS'))");
        }
    }
}
