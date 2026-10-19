using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskCalendarsCapacityAndProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DifferentPeople",
                table: "operational_task",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "GoalUnits",
                table: "operational_task",
                type: "integer",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.AddColumn<string>(
                name: "ProgressUnit",
                table: "operational_task",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "unidades");

            migrationBuilder.AddColumn<string>(
                name: "ScheduleMode",
                table: "operational_task",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "DISTRIBUIDA");

            migrationBuilder.AddColumn<int>(
                name: "StartHour",
                table: "operational_task",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WeekDays",
                table: "operational_task",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "DifferentPeople",
                table: "operational_assignment",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "GoalUnits",
                table: "operational_assignment",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ProgressUnit",
                table: "operational_assignment",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "unidades");

            migrationBuilder.AddColumn<int>(
                name: "ProgressUnits",
                table: "operational_assignment",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ScheduleKey",
                table: "operational_assignment",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScheduleMode",
                table: "operational_assignment",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "DISTRIBUIDA");

            migrationBuilder.CreateTable(
                name: "operational_capacity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    From = table.Column<DateOnly>(type: "date", nullable: false),
                    Through = table.Column<DateOnly>(type: "date", nullable: false),
                    Percent = table.Column<int>(type: "integer", nullable: false),
                    DailyMinutes = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_capacity", x => x.Id);
                    table.CheckConstraint("CK_capacity_values", "\"Percent\" BETWEEN 0 AND 100 AND \"DailyMinutes\" BETWEEN 1 AND 480 AND \"Through\" >= \"From\"");
                    table.ForeignKey(
                        name: "FK_operational_capacity_person_PersonId",
                        column: x => x.PersonId,
                        principalTable: "person",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "operational_progress_entry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Units = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_progress_entry", x => x.Id);
                    table.CheckConstraint("CK_progress_units", "\"Units\" > 0");
                    table.ForeignKey(
                        name: "FK_operational_progress_entry_operational_assignment_Assignmen~",
                        column: x => x.AssignmentId,
                        principalTable: "operational_assignment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0005-7000-8000-000000000005"),
                columns: new[] { "DifferentPeople", "GoalUnits", "ProgressUnit", "ScheduleMode", "StartHour", "WeekDays" },
                values: new object[] { false, 100, "unidades", "DISTRIBUIDA", 0, 0 });

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0007-7000-8000-000000000007"),
                columns: new[] { "DifferentPeople", "GoalUnits", "ProgressUnit", "ScheduleMode", "StartHour", "WeekDays" },
                values: new object[] { false, 100, "unidades", "DISTRIBUIDA", 0, 0 });

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0008-7000-8000-000000000008"),
                columns: new[] { "DifferentPeople", "GoalUnits", "ProgressUnit", "ScheduleMode", "StartHour", "WeekDays" },
                values: new object[] { false, 100, "unidades", "DISTRIBUIDA", 0, 0 });

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0011-7000-8000-000000000011"),
                columns: new[] { "DifferentPeople", "GoalUnits", "ProgressUnit", "ScheduleMode", "StartHour", "WeekDays" },
                values: new object[] { false, 100, "unidades", "DISTRIBUIDA", 0, 0 });

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0018-7000-8000-000000000018"),
                columns: new[] { "DifferentPeople", "GoalUnits", "ProgressUnit", "ScheduleMode", "StartHour", "WeekDays" },
                values: new object[] { false, 100, "unidades", "DISTRIBUIDA", 0, 0 });

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0026-7000-8000-000000000026"),
                columns: new[] { "DifferentPeople", "GoalUnits", "ProgressUnit", "ScheduleMode", "StartHour", "WeekDays" },
                values: new object[] { false, 100, "unidades", "DISTRIBUIDA", 0, 0 });

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0092-7000-8000-000000000092"),
                columns: new[] { "DifferentPeople", "GoalUnits", "ProgressUnit", "ScheduleMode", "StartHour", "WeekDays" },
                values: new object[] { false, 100, "unidades", "DISTRIBUIDA", 0, 0 });

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0093-7000-8000-000000000093"),
                columns: new[] { "DifferentPeople", "GoalUnits", "ProgressUnit", "ScheduleMode", "StartHour", "WeekDays" },
                values: new object[] { false, 100, "unidades", "DISTRIBUIDA", 0, 0 });

            migrationBuilder.CreateIndex(
                name: "IX_operational_assignment_ScheduleKey",
                table: "operational_assignment",
                column: "ScheduleKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_operational_capacity_PersonId_From",
                table: "operational_capacity",
                columns: new[] { "PersonId", "From" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_progress_entry_AssignmentId_OccurredAt",
                table: "operational_progress_entry",
                columns: new[] { "AssignmentId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "operational_capacity");

            migrationBuilder.DropTable(
                name: "operational_progress_entry");

            migrationBuilder.DropIndex(
                name: "IX_operational_assignment_ScheduleKey",
                table: "operational_assignment");

            migrationBuilder.DropColumn(
                name: "DifferentPeople",
                table: "operational_task");

            migrationBuilder.DropColumn(
                name: "GoalUnits",
                table: "operational_task");

            migrationBuilder.DropColumn(
                name: "ProgressUnit",
                table: "operational_task");

            migrationBuilder.DropColumn(
                name: "ScheduleMode",
                table: "operational_task");

            migrationBuilder.DropColumn(
                name: "StartHour",
                table: "operational_task");

            migrationBuilder.DropColumn(
                name: "WeekDays",
                table: "operational_task");

            migrationBuilder.DropColumn(
                name: "DifferentPeople",
                table: "operational_assignment");

            migrationBuilder.DropColumn(
                name: "GoalUnits",
                table: "operational_assignment");

            migrationBuilder.DropColumn(
                name: "ProgressUnit",
                table: "operational_assignment");

            migrationBuilder.DropColumn(
                name: "ProgressUnits",
                table: "operational_assignment");

            migrationBuilder.DropColumn(
                name: "ScheduleKey",
                table: "operational_assignment");

            migrationBuilder.DropColumn(
                name: "ScheduleMode",
                table: "operational_assignment");
        }
    }
}
