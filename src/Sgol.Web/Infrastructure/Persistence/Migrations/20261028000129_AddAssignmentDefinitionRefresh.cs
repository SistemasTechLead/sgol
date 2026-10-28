using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentDefinitionRefresh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_assignment_progress",
                table: "operational_assignment");

            migrationBuilder.AddColumn<string>(
                name: "DefinitionSnapshotJson",
                table: "operational_sequence_stage",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefinitionSnapshotJson",
                table: "operational_assignment",
                type: "jsonb",
                nullable: true);

            // Recover a required-role/calendar snapshot only from its actual revision.
            // Effective assignment fields win over catalogue history (corrections may differ).
            migrationBuilder.Sql("""
                UPDATE operational_assignment a
                SET "DefinitionSnapshotJson" =
                    COALESCE((SELECT e.after_data FROM audit_event e
                        WHERE e.action = 'OPERATIONAL_TASK_SAVED' AND e.resource_id = a."TaskId"
                          AND e.after_data ->> 'Revision' = a."TaskRevision"::text
                        ORDER BY e.occurred_at DESC LIMIT 1), to_jsonb(t))
                    || jsonb_build_object(
                        'Name', CASE WHEN a."OccasionId" IS NULL THEN a."Name" ELSE t."Name" END,
                        'Description', a."Description", 'Requirement', a."Requirement",
                        'MinimumPhotos', a."MinimumPhotos", 'MaximumPhotos', a."MaximumPhotos", 'MaximumPhotoBytes', a."MaximumPhotoBytes",
                        'SpecializationId', a."SpecializationId", 'LoadMinutes', a."LoadMinutes", 'OperationalCriticality', a."OperationalCriticality",
                        'PeriodType', a."PeriodType", 'ScheduleMode', a."ScheduleMode", 'Repetitions', a."Repetitions",
                        'GoalUnits', a."GoalUnits", 'ProgressUnit', a."ProgressUnit", 'DifferentPeople', a."DifferentPeople", 'PreferredMonthDays', a."PreferredMonthDays",
                        '_CalendarKnown', COALESCE(a."TaskRevision" = t."Revision", false) OR EXISTS (
                            SELECT 1 FROM audit_event e WHERE e.action = 'OPERATIONAL_TASK_SAVED' AND e.resource_id = a."TaskId"
                              AND e.after_data ->> 'Revision' = a."TaskRevision"::text))
                FROM operational_task t
                WHERE a."TaskId" = t."Id" AND a."DefinitionSnapshotJson" IS NULL;

                UPDATE operational_sequence_stage s SET "DefinitionSnapshotJson" = to_jsonb(t) || jsonb_build_object(
                    'Name', s."Name", 'Description', s."Description", 'AssignedRole', s."Role", 'Requirement', s."Requirement",
                    'SpecializationId', s."SpecializationId", 'LoadMinutes', s."LoadMinutes", 'OperationalCriticality', s."OperationalCriticality",
                    'MinimumPhotos', s."MinimumPhotos", 'MaximumPhotos', s."MaximumPhotos", 'MaximumPhotoBytes', s."MaximumPhotoBytes")
                FROM operational_task t WHERE s."TaskId" = t."Id" AND s."DefinitionSnapshotJson" IS NULL;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_assignment_progress",
                table: "operational_assignment",
                sql: "\"GoalUnits\" BETWEEN 0 AND 1000000 AND \"ProgressUnits\" BETWEEN 0 AND 1000000 AND \"LoadMinutes\" BETWEEN 1 AND 4800 AND (\"TaskRevision\" IS NULL OR \"TaskRevision\" > 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_assignment_progress",
                table: "operational_assignment");

            migrationBuilder.DropColumn(
                name: "DefinitionSnapshotJson",
                table: "operational_sequence_stage");

            migrationBuilder.DropColumn(
                name: "DefinitionSnapshotJson",
                table: "operational_assignment");

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_assignment_progress",
                table: "operational_assignment",
                sql: "\"GoalUnits\" BETWEEN 0 AND 1000000 AND \"ProgressUnits\" BETWEEN 0 AND \"GoalUnits\" AND \"LoadMinutes\" BETWEEN 1 AND 4800 AND (\"TaskRevision\" IS NULL OR \"TaskRevision\" > 0)");
        }
    }
}
