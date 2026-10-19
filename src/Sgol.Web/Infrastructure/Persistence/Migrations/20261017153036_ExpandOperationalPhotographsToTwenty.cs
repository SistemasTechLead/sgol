using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpandOperationalPhotographsToTwenty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(name: "CK_operational_photo_position", table: "operational_photograph");
            migrationBuilder.AddCheckConstraint(name: "CK_operational_photo_position", table: "operational_photograph", sql: "\"Position\" BETWEEN 0 AND 19");
            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_task_photo_limits",
                table: "operational_task");

            migrationBuilder.DropCheckConstraint(
                name: "CK_sequence_stage_snapshot",
                table: "operational_sequence_stage");

            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_assignment_photo_limits",
                table: "operational_assignment");

            migrationBuilder.AlterColumn<int>(
                name: "MaximumPhotos",
                table: "operational_task",
                type: "integer",
                nullable: false,
                defaultValue: 20,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 5);

            migrationBuilder.AlterColumn<int>(
                name: "MaximumPhotos",
                table: "operational_assignment",
                type: "integer",
                nullable: false,
                defaultValue: 20,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 5);

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_task_photo_limits",
                table: "operational_task",
                sql: "\"MinimumPhotos\" BETWEEN 1 AND 20 AND \"MaximumPhotos\" BETWEEN \"MinimumPhotos\" AND 20 AND \"MaximumPhotoBytes\" BETWEEN 1048576 AND 31457280");

            migrationBuilder.AddCheckConstraint(
                name: "CK_sequence_stage_snapshot",
                table: "operational_sequence_stage",
                sql: "\"TaskRevision\" > 0 AND \"LoadMinutes\" BETWEEN 1 AND 4800 AND \"Role\" IN ('PISO_VENTAS','SUBCOORDINACION','ADMINISTRACION','DIRECCION') AND \"Requirement\" IN ('FOTOGRAFIA','CAPTURA_PANTALLA','COMENTARIO','DOCUMENTO','NINGUNO') AND \"MinimumPhotos\" BETWEEN 1 AND 20 AND \"MaximumPhotos\" BETWEEN \"MinimumPhotos\" AND 20 AND \"MaximumPhotoBytes\" BETWEEN 1048576 AND 31457280 AND \"Revision\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_assignment_photo_limits",
                table: "operational_assignment",
                sql: "\"MinimumPhotos\" BETWEEN 1 AND 20 AND \"MaximumPhotos\" BETWEEN \"MinimumPhotos\" AND 20 AND \"MaximumPhotoBytes\" BETWEEN 1048576 AND 31457280");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(name: "CK_operational_photo_position", table: "operational_photograph");
            migrationBuilder.AddCheckConstraint(name: "CK_operational_photo_position", table: "operational_photograph", sql: "\"Position\" BETWEEN 0 AND 4");
            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_task_photo_limits",
                table: "operational_task");

            migrationBuilder.DropCheckConstraint(
                name: "CK_sequence_stage_snapshot",
                table: "operational_sequence_stage");

            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_assignment_photo_limits",
                table: "operational_assignment");

            migrationBuilder.AlterColumn<int>(
                name: "MaximumPhotos",
                table: "operational_task",
                type: "integer",
                nullable: false,
                defaultValue: 5,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 20);

            migrationBuilder.AlterColumn<int>(
                name: "MaximumPhotos",
                table: "operational_assignment",
                type: "integer",
                nullable: false,
                defaultValue: 5,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 20);

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_task_photo_limits",
                table: "operational_task",
                sql: "\"MinimumPhotos\" BETWEEN 1 AND 5 AND \"MaximumPhotos\" BETWEEN \"MinimumPhotos\" AND 5 AND \"MaximumPhotoBytes\" BETWEEN 1048576 AND 31457280");

            migrationBuilder.AddCheckConstraint(
                name: "CK_sequence_stage_snapshot",
                table: "operational_sequence_stage",
                sql: "\"TaskRevision\" > 0 AND \"LoadMinutes\" BETWEEN 1 AND 4800 AND \"Role\" IN ('PISO_VENTAS','SUBCOORDINACION','ADMINISTRACION','DIRECCION') AND \"Requirement\" IN ('FOTOGRAFIA','CAPTURA_PANTALLA','COMENTARIO','DOCUMENTO','NINGUNO') AND \"MinimumPhotos\" BETWEEN 1 AND 5 AND \"MaximumPhotos\" BETWEEN \"MinimumPhotos\" AND 5 AND \"MaximumPhotoBytes\" BETWEEN 1048576 AND 31457280 AND \"Revision\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_assignment_photo_limits",
                table: "operational_assignment",
                sql: "\"MinimumPhotos\" BETWEEN 1 AND 5 AND \"MaximumPhotos\" BETWEEN \"MinimumPhotos\" AND 5 AND \"MaximumPhotoBytes\" BETWEEN 1048576 AND 31457280");
        }
    }
}
