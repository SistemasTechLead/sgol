using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomaticProcessCalendar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_process_plan_start",
                table: "operational_process_plan_start");

            migrationBuilder.AddColumn<bool>(
                name: "Automatic",
                table: "operational_process_plan_start",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastAssignmentAttemptAt",
                table: "operational_process_plan_start",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingReason",
                table: "operational_process_plan_start",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_process_plan_start",
                table: "operational_process_plan_start",
                sql: "\"Status\" IN ('PREPARADO','ACTIVADO','PENDIENTE') AND \"Revision\" > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_process_plan_start",
                table: "operational_process_plan_start");

            migrationBuilder.DropColumn(
                name: "Automatic",
                table: "operational_process_plan_start");

            migrationBuilder.DropColumn(
                name: "LastAssignmentAttemptAt",
                table: "operational_process_plan_start");

            migrationBuilder.DropColumn(
                name: "PendingReason",
                table: "operational_process_plan_start");

            migrationBuilder.AddCheckConstraint(
                name: "CK_process_plan_start",
                table: "operational_process_plan_start",
                sql: "\"Status\" IN ('PREPARADO','ACTIVADO') AND \"Revision\" > 0");
        }
    }
}
