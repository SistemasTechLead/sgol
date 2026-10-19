using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeOperationalStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_operational_assignment_operational_occasion_OccasionId",
                table: "operational_assignment");

            migrationBuilder.DropForeignKey(
                name: "FK_operational_photograph_operational_capture_session_CaptureS~",
                table: "operational_photograph");

            migrationBuilder.DropIndex(
                name: "IX_operational_task_TaskCode",
                table: "operational_task");

            migrationBuilder.DropIndex(
                name: "IX_operational_assignment_OccasionId",
                table: "operational_assignment");

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "operational_photograph",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<long>(
                name: "TaskRevision",
                table: "operational_assignment",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_operational_task_TaskCode",
                table: "operational_task",
                column: "TaskCode");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_operational_occasion_Id_PlanId",
                table: "operational_occasion",
                columns: new[] { "Id", "PlanId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_operational_capture_session_Id_AssignmentId",
                table: "operational_capture_session",
                columns: new[] { "Id", "AssignmentId" });

            migrationBuilder.CreateTable(
                name: "operational_attachment",
                columns: table => new
                {
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_attachment", x => x.AssignmentId);
                    table.CheckConstraint("CK_operational_attachment_size", "octet_length(\"Content\") BETWEEN 1 AND 15728640");
                    table.CheckConstraint("CK_operational_attachment_type", "\"ContentType\" IN ('application/pdf','image/jpeg','image/png')");
                    table.ForeignKey(
                        name: "FK_operational_attachment_operational_assignment_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "operational_assignment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "plan_capacity_adjustment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceCapacityId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    From = table.Column<DateOnly>(type: "date", nullable: false),
                    Through = table.Column<DateOnly>(type: "date", nullable: false),
                    Percent = table.Column<int>(type: "integer", nullable: false),
                    DailyMinutes = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SourceRevision = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_capacity_adjustment", x => x.Id);
                    table.CheckConstraint("CK_plan_capacity_values", "\"Through\" >= \"From\" AND \"Percent\" BETWEEN 0 AND 100 AND \"DailyMinutes\" BETWEEN 1 AND 480");
                    table.ForeignKey(
                        name: "FK_plan_capacity_adjustment_direction_prepared_plan_PlanId",
                        column: x => x.PlanId,
                        principalTable: "direction_prepared_plan",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_plan_capacity_adjustment_person_PersonId",
                        column: x => x.PersonId,
                        principalTable: "person",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plan_capacity_previous_segment",
                columns: table => new
                {
                    AdjustmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    From = table.Column<DateOnly>(type: "date", nullable: false),
                    Through = table.Column<DateOnly>(type: "date", nullable: false),
                    Percent = table.Column<int>(type: "integer", nullable: false),
                    DailyMinutes = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_capacity_previous_segment", x => new { x.AdjustmentId, x.Position });
                    table.CheckConstraint("CK_plan_capacity_previous_values", "\"Position\" >= 0 AND \"Through\" >= \"From\" AND \"Percent\" BETWEEN 0 AND 100 AND \"DailyMinutes\" BETWEEN 1 AND 480");
                    table.ForeignKey(
                        name: "FK_plan_capacity_previous_segment_plan_capacity_adjustment_Adj~",
                        column: x => x.AdjustmentId,
                        principalTable: "plan_capacity_adjustment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_task_schedule",
                table: "operational_task",
                sql: "\"ScheduleMode\" IN ('DISTRIBUIDA','DIAS_FIJOS','PROGRESO') AND \"WeekDays\" BETWEEN 0 AND 127 AND \"StartHour\" BETWEEN 0 AND 23 AND \"GoalUnits\" BETWEEN 1 AND 1000000 AND length(\"ProgressUnit\") BETWEEN 1 AND 60 AND (\"ScheduleMode\" <> 'DIAS_FIJOS' OR (\"PeriodType\" = 'SEMANAL' AND \"WeekDays\" > 0)) AND (\"ScheduleMode\" <> 'PROGRESO' OR (\"PeriodType\" = 'SEMANAL' AND NOT \"DifferentPeople\")) AND (\"PeriodType\" <> 'OCASIONAL' OR (\"ScheduleMode\" = 'DISTRIBUIDA' AND \"WeekDays\" = 0 AND \"StartHour\" = 0 AND \"Repetitions\" = 1 AND NOT \"DifferentPeople\"))");

            migrationBuilder.CreateIndex(
                name: "IX_operational_progress_entry_ActorId",
                table: "operational_progress_entry",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_photograph_CaptureSessionId_AssignmentId",
                table: "operational_photograph",
                columns: new[] { "CaptureSessionId", "AssignmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_occasion_CreatedBy",
                table: "operational_occasion",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_operational_assignment_OccasionId_PlanId",
                table: "operational_assignment",
                columns: new[] { "OccasionId", "PlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_assignment_ReviewedBy",
                table: "operational_assignment",
                column: "ReviewedBy");

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_assignment_progress",
                table: "operational_assignment",
                sql: "\"GoalUnits\" BETWEEN 0 AND 1000000 AND \"ProgressUnits\" BETWEEN 0 AND \"GoalUnits\" AND \"LoadMinutes\" BETWEEN 1 AND 4800 AND (\"TaskRevision\" IS NULL OR \"TaskRevision\" > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_assignment_range",
                table: "operational_assignment",
                sql: "\"DueAt\" > \"StartsAt\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_assignment_review",
                table: "operational_assignment",
                sql: "(\"ReviewStatus\" IS NULL OR \"ReviewStatus\" IN ('PENDIENTE_REVISION','CONCLUIDA','NO_SATISFECHA','REASIGNADA')) AND ((\"ReviewedBy\" IS NULL AND \"ReviewedAt\" IS NULL) OR (\"ReviewedBy\" IS NOT NULL AND \"ReviewedAt\" IS NOT NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_assignment_schedule",
                table: "operational_assignment",
                sql: "\"ScheduleMode\" IN ('DISTRIBUIDA','DIAS_FIJOS','PROGRESO') AND \"Requirement\" IN ('FOTOGRAFIA','CAPTURA_PANTALLA','COMENTARIO','DOCUMENTO','NINGUNO') AND (\"AssignedRole\" IS NULL OR \"AssignedRole\" IN ('DIRECCION','ADMINISTRACION','SUBCOORDINACION','PISO_VENTAS'))");

            migrationBuilder.CreateIndex(
                name: "IX_direction_prepared_plan_item_task_code",
                table: "direction_prepared_plan_item",
                column: "task_code");

            migrationBuilder.CreateIndex(
                name: "IX_direction_prepared_plan_created_by",
                table: "direction_prepared_plan",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_operational_attachment_CreatedAt",
                table: "operational_attachment",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_plan_capacity_adjustment_PersonId",
                table: "plan_capacity_adjustment",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_plan_capacity_adjustment_PlanId_CreatedAt_Id",
                table: "plan_capacity_adjustment",
                columns: new[] { "PlanId", "CreatedAt", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_direction_prepared_plan_app_user_created_by",
                table: "direction_prepared_plan",
                column: "created_by",
                principalTable: "app_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_direction_prepared_plan_item_operational_task_task_code",
                table: "direction_prepared_plan_item",
                column: "task_code",
                principalTable: "operational_task",
                principalColumn: "TaskCode",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_operational_assignment_app_user_ReviewedBy",
                table: "operational_assignment",
                column: "ReviewedBy",
                principalTable: "app_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_operational_assignment_operational_occasion_OccasionId_Plan~",
                table: "operational_assignment",
                columns: new[] { "OccasionId", "PlanId" },
                principalTable: "operational_occasion",
                principalColumns: new[] { "Id", "PlanId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_operational_occasion_app_user_CreatedBy",
                table: "operational_occasion",
                column: "CreatedBy",
                principalTable: "app_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_operational_photograph_operational_capture_session_CaptureS~",
                table: "operational_photograph",
                columns: new[] { "CaptureSessionId", "AssignmentId" },
                principalTable: "operational_capture_session",
                principalColumns: new[] { "Id", "AssignmentId" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_operational_progress_entry_app_user_ActorId",
                table: "operational_progress_entry",
                column: "ActorId",
                principalTable: "app_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
            // Copy evidence before removing the old columns. Unknown task revisions remain NULL.
            migrationBuilder.Sql("""
                UPDATE operational_photograph p SET "FileName" = a."FileName"
                FROM operational_assignment a
                WHERE p."AssignmentId" = a."Id" AND p."Position" = 0 AND p."AttachedAt" IS NOT NULL
                    AND p."FileName" LIKE 'foto-historica-%' AND a."FileName" IS NOT NULL;

                INSERT INTO operational_attachment ("AssignmentId", "FileName", "ContentType", "Content", "CreatedAt")
                SELECT "Id", "FileName", "ContentType", "FileContent", COALESCE("CompletedAt", "StartsAt")
                FROM operational_assignment WHERE "FileContent" IS NOT NULL;

                INSERT INTO plan_capacity_adjustment
                    ("Id", "PlanId", "SourceCapacityId", "PersonId", "CreatedAt", "From", "Through", "Percent", "DailyMinutes", "Reason", "SourceRevision")
                SELECT e.id, p.id, e.resource_id,
                    COALESCE(e.after_data->'setting'->>'PersonId', e.after_data->'change'->>'PersonId')::uuid,
                    e.occurred_at,
                    COALESCE(e.after_data->'setting'->>'From', e.after_data->'change'->>'From')::date,
                    COALESCE(e.after_data->'setting'->>'Through', e.after_data->'change'->>'Through')::date,
                    COALESCE(e.after_data->'setting'->>'Percent', e.after_data->'change'->>'Percent')::integer,
                    COALESCE(e.after_data->'setting'->>'DailyMinutes', e.after_data->'change'->>'DailyMinutes')::integer,
                    btrim(COALESCE(e.after_data->'setting'->>'Reason', e.after_data->'change'->>'Reason')),
                    (e.after_data->'setting'->>'Revision')::bigint
                FROM audit_event e JOIN direction_prepared_plan p ON p.id::text = e.after_data->>'Id'
                WHERE e.action = 'PLAN_CAPACITY_ADJUSTED';

                INSERT INTO plan_capacity_previous_segment
                    ("AdjustmentId", "Position", "From", "Through", "Percent", "DailyMinutes", "Reason", "Revision")
                SELECT a."Id", (v.ordinality - 1)::integer, (v.value->>'From')::date,
                    (v.value->>'Through')::date, (v.value->>'Percent')::integer,
                    (v.value->>'DailyMinutes')::integer, v.value->>'Reason', (v.value->>'Revision')::bigint
                FROM plan_capacity_adjustment a JOIN audit_event e ON e.id = a."Id",
                    LATERAL jsonb_array_elements(COALESCE(e.before_data, '[]'::jsonb)) WITH ORDINALITY AS v(value, ordinality);

                CREATE EXTENSION IF NOT EXISTS btree_gist;
                ALTER TABLE operational_capacity ADD CONSTRAINT "EX_operational_capacity_dates"
                    EXCLUDE USING gist ("PersonId" WITH =, daterange("From", "Through", '[]') WITH &&)
                    DEFERRABLE INITIALLY DEFERRED;
                """);

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "operational_assignment");

            migrationBuilder.DropColumn(
                name: "FileContent",
                table: "operational_assignment");

            migrationBuilder.DropColumn(
                name: "FileName",
                table: "operational_assignment");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE operational_capacity DROP CONSTRAINT \"EX_operational_capacity_dates\"");
            migrationBuilder.DropForeignKey(
                name: "FK_direction_prepared_plan_app_user_created_by",
                table: "direction_prepared_plan");

            migrationBuilder.DropForeignKey(
                name: "FK_direction_prepared_plan_item_operational_task_task_code",
                table: "direction_prepared_plan_item");

            migrationBuilder.DropForeignKey(
                name: "FK_operational_assignment_app_user_ReviewedBy",
                table: "operational_assignment");

            migrationBuilder.DropForeignKey(
                name: "FK_operational_assignment_operational_occasion_OccasionId_Plan~",
                table: "operational_assignment");

            migrationBuilder.DropForeignKey(
                name: "FK_operational_occasion_app_user_CreatedBy",
                table: "operational_occasion");

            migrationBuilder.DropForeignKey(
                name: "FK_operational_photograph_operational_capture_session_CaptureS~",
                table: "operational_photograph");

            migrationBuilder.DropForeignKey(
                name: "FK_operational_progress_entry_app_user_ActorId",
                table: "operational_progress_entry");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_operational_task_TaskCode",
                table: "operational_task");

            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_task_schedule",
                table: "operational_task");

            migrationBuilder.DropIndex(
                name: "IX_operational_progress_entry_ActorId",
                table: "operational_progress_entry");

            migrationBuilder.DropIndex(
                name: "IX_operational_photograph_CaptureSessionId_AssignmentId",
                table: "operational_photograph");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_operational_occasion_Id_PlanId",
                table: "operational_occasion");

            migrationBuilder.DropIndex(
                name: "IX_operational_occasion_CreatedBy",
                table: "operational_occasion");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_operational_capture_session_Id_AssignmentId",
                table: "operational_capture_session");

            migrationBuilder.DropIndex(
                name: "IX_operational_assignment_OccasionId_PlanId",
                table: "operational_assignment");

            migrationBuilder.DropIndex(
                name: "IX_operational_assignment_ReviewedBy",
                table: "operational_assignment");

            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_assignment_progress",
                table: "operational_assignment");

            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_assignment_range",
                table: "operational_assignment");

            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_assignment_review",
                table: "operational_assignment");

            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_assignment_schedule",
                table: "operational_assignment");

            migrationBuilder.DropIndex(
                name: "IX_direction_prepared_plan_item_task_code",
                table: "direction_prepared_plan_item");

            migrationBuilder.DropIndex(
                name: "IX_direction_prepared_plan_created_by",
                table: "direction_prepared_plan");

            migrationBuilder.DropColumn(
                name: "TaskRevision",
                table: "operational_assignment");



            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "operational_assignment",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "FileContent",
                table: "operational_assignment",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                table: "operational_assignment",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_operational_task_TaskCode",
                table: "operational_task",
                column: "TaskCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_operational_assignment_OccasionId",
                table: "operational_assignment",
                column: "OccasionId");

            migrationBuilder.AddForeignKey(
                name: "FK_operational_assignment_operational_occasion_OccasionId",
                table: "operational_assignment",
                column: "OccasionId",
                principalTable: "operational_occasion",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_operational_photograph_operational_capture_session_CaptureS~",
                table: "operational_photograph",
                column: "CaptureSessionId",
                principalTable: "operational_capture_session",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
            migrationBuilder.Sql("""
                UPDATE operational_assignment a
                SET "FileContent" = f."Content", "ContentType" = f."ContentType", "FileName" = f."FileName"
                FROM operational_attachment f WHERE f."AssignmentId" = a."Id";
                UPDATE operational_assignment a SET "FileName" = p."FileName"
                FROM operational_photograph p
                WHERE p."AssignmentId" = a."Id" AND p."AttachedAt" IS NOT NULL AND p."Position" = 0;
                -- The previous schema kept the original name on the assignment and a generated camera filename.
                UPDATE operational_photograph
                SET "FileName" = 'foto-historica-' || "AssignmentId"::text || CASE WHEN "ContentType" = 'image/png' THEN '.png' ELSE '.jpg' END
                WHERE length("FileName") > 100;
                """);
            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "operational_photograph",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.DropTable(
                name: "plan_capacity_previous_segment");

            migrationBuilder.DropTable(
                name: "operational_attachment");

            migrationBuilder.DropTable(
                name: "plan_capacity_adjustment");
        }
    }
}
