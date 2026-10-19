using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonalSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.AddColumn<int>(
                name: "StartMinute",
                table: "operational_task",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TemporalDecisionJson",
                table: "operational_assignment",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemporalPolicyJson",
                table: "operational_assignment",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "closing_coverage_version",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ChangedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IntentKey = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_closing_coverage_version", x => x.Id);
                    table.ForeignKey(
                        name: "FK_closing_coverage_version_person_PersonId",
                        column: x => x.PersonId,
                        principalTable: "person",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "closing_rotation_version",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    From = table.Column<DateOnly>(type: "date", nullable: false),
                    Anchor = table.Column<DateOnly>(type: "date", nullable: false),
                    WeekDay = table.Column<int>(type: "integer", nullable: false),
                    ExtendedExit = table.Column<int>(type: "integer", nullable: false),
                    RegularPersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExcludedPersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ChangedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IntentKey = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_closing_rotation_version", x => x.Id);
                    table.CheckConstraint("CK_closing_rotation", "\"WeekDay\" BETWEEN 0 AND 6 AND \"ExtendedExit\" BETWEEN 1 AND 1440");
                    table.ForeignKey(
                        name: "FK_closing_rotation_version_person_ExcludedPersonId",
                        column: x => x.ExcludedPersonId,
                        principalTable: "person",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_closing_rotation_version_person_RegularPersonId",
                        column: x => x.RegularPersonId,
                        principalTable: "person",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "personal_schedule_exception",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    IntervalsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ChangedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IntentKey = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestHash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_personal_schedule_exception", x => x.Id);
                    table.ForeignKey(
                        name: "FK_personal_schedule_exception_person_PersonId",
                        column: x => x.PersonId,
                        principalTable: "person",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "personal_schedule_version",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    From = table.Column<DateOnly>(type: "date", nullable: false),
                    Through = table.Column<DateOnly>(type: "date", nullable: true),
                    Superseded = table.Column<bool>(type: "boolean", nullable: false),
                    Zone = table.Column<string>(type: "text", nullable: false),
                    DaysJson = table.Column<string>(type: "jsonb", nullable: false),
                    ChangedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IntentKey = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestHash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_personal_schedule_version", x => x.Id);
                    table.CheckConstraint("CK_personal_schedule_range", "\"Through\" IS NULL OR \"Through\" >= \"From\"");
                    table.ForeignKey(
                        name: "FK_personal_schedule_version_person_PersonId",
                        column: x => x.PersonId,
                        principalTable: "person",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "temporal_assignment_state",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_temporal_assignment_state", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0005-7000-8000-000000000005"),
                columns: new[] { "ClosingGraceMinutes", "DayStartMinutesJson", "ExecutionPolicy", "Splittable", "StartMinute" },
                values: new object[] { 0, "{}", "UNCLASSIFIED", false, 0 });

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0007-7000-8000-000000000007"),
                columns: new[] { "ClosingGraceMinutes", "DayStartMinutesJson", "ExecutionPolicy", "Splittable", "StartMinute" },
                values: new object[] { 0, "{}", "UNCLASSIFIED", false, 0 });

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0008-7000-8000-000000000008"),
                columns: new[] { "ClosingGraceMinutes", "DayStartMinutesJson", "ExecutionPolicy", "Splittable", "StartMinute" },
                values: new object[] { 0, "{}", "UNCLASSIFIED", false, 0 });

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0011-7000-8000-000000000011"),
                columns: new[] { "ClosingGraceMinutes", "DayStartMinutesJson", "ExecutionPolicy", "Splittable", "StartMinute" },
                values: new object[] { 0, "{}", "UNCLASSIFIED", false, 0 });

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0018-7000-8000-000000000018"),
                columns: new[] { "ClosingGraceMinutes", "DayStartMinutesJson", "ExecutionPolicy", "Splittable", "StartMinute" },
                values: new object[] { 0, "{}", "UNCLASSIFIED", false, 0 });

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0026-7000-8000-000000000026"),
                columns: new[] { "ClosingGraceMinutes", "DayStartMinutesJson", "ExecutionPolicy", "Splittable", "StartMinute" },
                values: new object[] { 0, "{}", "UNCLASSIFIED", false, 0 });

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0092-7000-8000-000000000092"),
                columns: new[] { "ClosingGraceMinutes", "DayStartMinutesJson", "ExecutionPolicy", "Splittable", "StartMinute" },
                values: new object[] { 0, "{}", "UNCLASSIFIED", false, 0 });

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0093-7000-8000-000000000093"),
                columns: new[] { "ClosingGraceMinutes", "DayStartMinutesJson", "ExecutionPolicy", "Splittable", "StartMinute" },
                values: new object[] { 0, "{}", "UNCLASSIFIED", false, 0 });

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_task_temporal",
                table: "operational_task",
                sql: "\"StartMinute\" BETWEEN 0 AND 59 AND \"ExecutionPolicy\" IN ('UNCLASSIFIED','FIXED','FLEXIBLE') AND \"ClosingGraceMinutes\" IN (0,60) AND (NOT \"Splittable\" OR \"ExecutionPolicy\" = 'FLEXIBLE') AND (\"ClosingGraceMinutes\" = 0 OR (\"ExecutionPolicy\" = 'FIXED' AND NOT \"Splittable\"))");

            migrationBuilder.CreateIndex(
                name: "IX_closing_coverage_version_Day_Revision",
                table: "closing_coverage_version",
                columns: new[] { "Day", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_closing_coverage_version_IntentKey",
                table: "closing_coverage_version",
                column: "IntentKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_closing_coverage_version_PersonId",
                table: "closing_coverage_version",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_closing_rotation_version_ExcludedPersonId",
                table: "closing_rotation_version",
                column: "ExcludedPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_closing_rotation_version_IntentKey",
                table: "closing_rotation_version",
                column: "IntentKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_closing_rotation_version_RegularPersonId",
                table: "closing_rotation_version",
                column: "RegularPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_closing_rotation_version_Revision",
                table: "closing_rotation_version",
                column: "Revision",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_personal_schedule_exception_IntentKey",
                table: "personal_schedule_exception",
                column: "IntentKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_personal_schedule_exception_PersonId_Day_Revision",
                table: "personal_schedule_exception",
                columns: new[] { "PersonId", "Day", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_personal_schedule_version_IntentKey",
                table: "personal_schedule_version",
                column: "IntentKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_personal_schedule_version_PersonId_Revision",
                table: "personal_schedule_version",
                columns: new[] { "PersonId", "Revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "closing_coverage_version");

            migrationBuilder.DropTable(
                name: "closing_rotation_version");

            migrationBuilder.DropTable(
                name: "personal_schedule_exception");

            migrationBuilder.DropTable(
                name: "personal_schedule_version");

            migrationBuilder.DropTable(
                name: "temporal_assignment_state");

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

            migrationBuilder.DropColumn(
                name: "StartMinute",
                table: "operational_task");

            migrationBuilder.DropColumn(
                name: "TemporalDecisionJson",
                table: "operational_assignment");

            migrationBuilder.DropColumn(
                name: "TemporalPolicyJson",
                table: "operational_assignment");
        }
    }
}
