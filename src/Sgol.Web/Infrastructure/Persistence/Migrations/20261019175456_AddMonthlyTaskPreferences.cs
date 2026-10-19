using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMonthlyTaskPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_task_schedule",
                table: "operational_task");

            migrationBuilder.AlterColumn<int>(
                name: "GoalUnits",
                table: "operational_task",
                type: "integer",
                nullable: true,
                defaultValue: 100,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 100);

            migrationBuilder.AddColumn<string>(
                name: "PreferredMonthDays",
                table: "operational_task",
                type: "character varying(83)",
                maxLength: 83,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PreferredMonthDays",
                table: "operational_assignment",
                type: "character varying(83)",
                maxLength: 83,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MonthlyPendingJson",
                table: "direction_prepared_plan",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_task_month_days",
                table: "operational_task",
                sql: "\"PreferredMonthDays\" = '' OR (\"PeriodType\" = 'MENSUAL' AND \"ScheduleMode\" = 'DISTRIBUIDA' AND \"PreferredMonthDays\" ~ '^([1-9]|[12][0-9]|3[01])(,([1-9]|[12][0-9]|3[01]))*$' AND \"Repetitions\" = cardinality(string_to_array(\"PreferredMonthDays\", ',')) AND cardinality(string_to_array(\"PreferredMonthDays\", ',')) <= 31)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_task_schedule",
                table: "operational_task",
                sql: "\"ScheduleMode\" IN ('DISTRIBUIDA','DIAS_FIJOS','PROGRESO') AND \"WeekDays\" BETWEEN 0 AND 127 AND \"StartHour\" BETWEEN 0 AND 23 AND (\"GoalUnits\" IS NULL OR \"GoalUnits\" BETWEEN 1 AND 1000000) AND length(\"ProgressUnit\") <= 60 AND ((NOT \"Active\" AND \"ScheduleMode\" = 'PROGRESO') OR (\"GoalUnits\" IS NOT NULL AND length(\"ProgressUnit\") >= 1)) AND (\"ScheduleMode\" <> 'DIAS_FIJOS' OR (\"PeriodType\" = 'SEMANAL' AND \"WeekDays\" > 0)) AND (\"ScheduleMode\" <> 'PROGRESO' OR (\"PeriodType\" = 'SEMANAL' AND (NOT \"Active\" OR NOT \"DifferentPeople\"))) AND (\"PeriodType\" <> 'OCASIONAL' OR (\"ScheduleMode\" = 'DISTRIBUIDA' AND \"WeekDays\" = 0 AND \"StartHour\" = 0 AND \"Repetitions\" = 1 AND NOT \"DifferentPeople\"))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_task_month_days",
                table: "operational_task");

            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_task_schedule",
                table: "operational_task");

            migrationBuilder.DropColumn(
                name: "PreferredMonthDays",
                table: "operational_task");

            migrationBuilder.DropColumn(
                name: "PreferredMonthDays",
                table: "operational_assignment");

            migrationBuilder.DropColumn(
                name: "MonthlyPendingJson",
                table: "direction_prepared_plan");

            migrationBuilder.AlterColumn<int>(
                name: "GoalUnits",
                table: "operational_task",
                type: "integer",
                nullable: false,
                defaultValue: 100,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true,
                oldDefaultValue: 100);

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_task_schedule",
                table: "operational_task",
                sql: "\"ScheduleMode\" IN ('DISTRIBUIDA','DIAS_FIJOS','PROGRESO') AND \"WeekDays\" BETWEEN 0 AND 127 AND \"StartHour\" BETWEEN 0 AND 23 AND \"GoalUnits\" BETWEEN 1 AND 1000000 AND length(\"ProgressUnit\") BETWEEN 1 AND 60 AND (\"ScheduleMode\" <> 'DIAS_FIJOS' OR (\"PeriodType\" = 'SEMANAL' AND \"WeekDays\" > 0)) AND (\"ScheduleMode\" <> 'PROGRESO' OR (\"PeriodType\" = 'SEMANAL' AND NOT \"DifferentPeople\")) AND (\"PeriodType\" <> 'OCASIONAL' OR (\"ScheduleMode\" = 'DISTRIBUIDA' AND \"WeekDays\" = 0 AND \"StartHour\" = 0 AND \"Repetitions\" = 1 AND NOT \"DifferentPeople\"))");
        }
    }
}

