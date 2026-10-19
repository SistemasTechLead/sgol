using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyTaskSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER FUNCTION public.sgol_work_week_valid(jsonb) SET search_path = pg_catalog, public;
                CREATE TABLE retired_task_time_configuration (
                    task_id uuid PRIMARY KEY, configuration jsonb NOT NULL,
                    retired_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP);
                INSERT INTO retired_task_time_configuration(task_id, configuration)
                SELECT "Id", jsonb_build_object('ExecutionPolicy', "ExecutionPolicy", 'Splittable', "Splittable", 'ClosingGraceMinutes', "ClosingGraceMinutes", 'DayStartMinutesJson', "DayStartMinutesJson") FROM operational_task;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_task_temporal",
                table: "operational_task");

            migrationBuilder.DropColumn(
                name: "ClosingGraceMinutes",
                table: "operational_task");

            migrationBuilder.DropColumn(
                name: "DayStartMinutesJson",
                table: "operational_task");

            migrationBuilder.DropColumn(
                name: "ExecutionPolicy",
                table: "operational_task");

            migrationBuilder.DropColumn(
                name: "Splittable",
                table: "operational_task");

            migrationBuilder.AddColumn<Guid>(
                name: "ClosingTaskId",
                table: "temporal_assignment_state",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SundayStartMinute",
                table: "operational_task",
                type: "integer",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0005-7000-8000-000000000005"),
                column: "SundayStartMinute",
                value: null);

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0007-7000-8000-000000000007"),
                column: "SundayStartMinute",
                value: null);

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0008-7000-8000-000000000008"),
                column: "SundayStartMinute",
                value: null);

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0011-7000-8000-000000000011"),
                column: "SundayStartMinute",
                value: null);

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0018-7000-8000-000000000018"),
                column: "SundayStartMinute",
                value: null);

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0026-7000-8000-000000000026"),
                column: "SundayStartMinute",
                value: null);

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0092-7000-8000-000000000092"),
                column: "SundayStartMinute",
                value: null);

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0093-7000-8000-000000000093"),
                column: "SundayStartMinute",
                value: null);

            migrationBuilder.Sql("""
                UPDATE operational_task t SET "SundayStartMinute" = (r.configuration->'DayStartMinutesJson'->>'0')::integer
                FROM retired_task_time_configuration r WHERE r.task_id=t."Id";
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_task_time",
                table: "operational_task",
                sql: "\"StartMinute\" BETWEEN 0 AND 59 AND (\"SundayStartMinute\" IS NULL OR \"SundayStartMinute\" BETWEEN 0 AND 1439)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS(SELECT 1 FROM operational_task WHERE "SundayStartMinute" IS NOT NULL)
                     OR EXISTS(SELECT 1 FROM temporal_assignment_state WHERE "ClosingTaskId" IS NOT NULL)
                  THEN RAISE EXCEPTION 'Preserve Sunday times and shared closing configuration before downgrade'; END IF;
                END $$;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_task_time",
                table: "operational_task");

            migrationBuilder.DropColumn(
                name: "ClosingTaskId",
                table: "temporal_assignment_state");

            migrationBuilder.DropColumn(
                name: "SundayStartMinute",
                table: "operational_task");

            migrationBuilder.AddColumn<int>(
                name: "ClosingGraceMinutes",
                table: "operational_task",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DayStartMinutesJson",
                table: "operational_task",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "ExecutionPolicy",
                table: "operational_task",
                type: "text",
                nullable: false,
                defaultValue: "UNCLASSIFIED");

            migrationBuilder.AddColumn<bool>(
                name: "Splittable",
                table: "operational_task",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0005-7000-8000-000000000005"),
                columns: new[] { "ClosingGraceMinutes", "DayStartMinutesJson", "ExecutionPolicy", "Splittable" },
                values: new object[] { 0, "{}", "UNCLASSIFIED", false });

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0007-7000-8000-000000000007"),
                columns: new[] { "ClosingGraceMinutes", "DayStartMinutesJson", "ExecutionPolicy", "Splittable" },
                values: new object[] { 0, "{}", "UNCLASSIFIED", false });

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0008-7000-8000-000000000008"),
                columns: new[] { "ClosingGraceMinutes", "DayStartMinutesJson", "ExecutionPolicy", "Splittable" },
                values: new object[] { 0, "{}", "UNCLASSIFIED", false });

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0011-7000-8000-000000000011"),
                columns: new[] { "ClosingGraceMinutes", "DayStartMinutesJson", "ExecutionPolicy", "Splittable" },
                values: new object[] { 0, "{}", "UNCLASSIFIED", false });

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0018-7000-8000-000000000018"),
                columns: new[] { "ClosingGraceMinutes", "DayStartMinutesJson", "ExecutionPolicy", "Splittable" },
                values: new object[] { 0, "{}", "UNCLASSIFIED", false });

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0026-7000-8000-000000000026"),
                columns: new[] { "ClosingGraceMinutes", "DayStartMinutesJson", "ExecutionPolicy", "Splittable" },
                values: new object[] { 0, "{}", "UNCLASSIFIED", false });

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0092-7000-8000-000000000092"),
                columns: new[] { "ClosingGraceMinutes", "DayStartMinutesJson", "ExecutionPolicy", "Splittable" },
                values: new object[] { 0, "{}", "UNCLASSIFIED", false });

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0093-7000-8000-000000000093"),
                columns: new[] { "ClosingGraceMinutes", "DayStartMinutesJson", "ExecutionPolicy", "Splittable" },
                values: new object[] { 0, "{}", "UNCLASSIFIED", false });

            migrationBuilder.Sql("""
                UPDATE operational_task t SET "ExecutionPolicy"=r.configuration->>'ExecutionPolicy',
                  "Splittable"=(r.configuration->>'Splittable')::boolean,
                  "ClosingGraceMinutes"=(r.configuration->>'ClosingGraceMinutes')::integer,
                  "DayStartMinutesJson"=r.configuration->'DayStartMinutesJson'
                FROM retired_task_time_configuration r WHERE r.task_id=t."Id";
                DROP TABLE retired_task_time_configuration;
                """);
            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_task_temporal",
                table: "operational_task",
                sql: "\"StartMinute\" BETWEEN 0 AND 59 AND \"ExecutionPolicy\" IN ('UNCLASSIFIED','FIXED','FLEXIBLE') AND \"ClosingGraceMinutes\" IN (0,60) AND (NOT \"Splittable\" OR \"ExecutionPolicy\" = 'FLEXIBLE') AND (\"ClosingGraceMinutes\" = 0 OR (\"ExecutionPolicy\" = 'FIXED' AND NOT \"Splittable\"))");
        }
    }
}
