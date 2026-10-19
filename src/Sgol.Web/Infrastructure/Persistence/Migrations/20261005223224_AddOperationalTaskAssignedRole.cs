using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalTaskAssignedRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignedRole",
                table: "operational_task",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "PISO_VENTAS");

            migrationBuilder.AddColumn<string>(
                name: "AssignedRole",
                table: "operational_assignment",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0005-7000-8000-000000000005"),
                column: "AssignedRole",
                value: "PISO_VENTAS");

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0007-7000-8000-000000000007"),
                column: "AssignedRole",
                value: "PISO_VENTAS");

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0008-7000-8000-000000000008"),
                column: "AssignedRole",
                value: "PISO_VENTAS");

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0011-7000-8000-000000000011"),
                column: "AssignedRole",
                value: "PISO_VENTAS");

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0018-7000-8000-000000000018"),
                column: "AssignedRole",
                value: "PISO_VENTAS");

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0026-7000-8000-000000000026"),
                column: "AssignedRole",
                value: "PISO_VENTAS");

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0092-7000-8000-000000000092"),
                column: "AssignedRole",
                value: "PISO_VENTAS");

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0093-7000-8000-000000000093"),
                column: "AssignedRole",
                value: "PISO_VENTAS");

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_task_role",
                table: "operational_task",
                sql: "\"AssignedRole\" IN ('DIRECCION','ADMINISTRACION','SUBCOORDINACION','PISO_VENTAS')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_task_role",
                table: "operational_task");

            migrationBuilder.DropColumn(
                name: "AssignedRole",
                table: "operational_task");

            migrationBuilder.DropColumn(
                name: "AssignedRole",
                table: "operational_assignment");
        }
    }
}
