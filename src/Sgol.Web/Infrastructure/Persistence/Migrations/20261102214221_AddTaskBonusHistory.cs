using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskBonusHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "task_bonus_closure",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Week = table.Column<DateOnly>(type: "date", nullable: false),
                    RulesVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_bonus_closure", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "task_bonus_fact",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_bonus_fact", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_task_bonus_closure_PersonId_Week_RulesVersion",
                table: "task_bonus_closure",
                columns: new[] { "PersonId", "Week", "RulesVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_bonus_fact_AssignmentId_Revision_Kind",
                table: "task_bonus_fact",
                columns: new[] { "AssignmentId", "Revision", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_bonus_fact_RecordedAt",
                table: "task_bonus_fact",
                column: "RecordedAt");
            // Initial inventory preserves completed and retired assignments as well as pending ones.
            // Economic history is deliberately independent of operational deletion cascades.
            migrationBuilder.Sql("""
                INSERT INTO task_bonus_fact ("Id", "AssignmentId", "Revision", "RecordedAt", "Kind", "SnapshotJson")
                SELECT gen_random_uuid(), a."Id", a."Revision", CURRENT_TIMESTAMP, 'BASELINE',
                    jsonb_build_object('Id',a."Id",'ReassignedFromId',a."ReassignedFromId",'PersonId',a."PersonId",
                    'PersonName',a."PersonName",'TaskCode',a."TaskCode",'Name',a."Name",'Role',a."AssignedRole",
                    'StartsAt',a."StartsAt",'DueAt',a."DueAt",'Criticality',a."OperationalCriticality",
                    'ScheduleMode',a."ScheduleMode",'GoalUnits',a."GoalUnits",'LoadMinutes',a."LoadMinutes",
                    'CompletedAt',s."CompletedAt",'CompletedBy',s."CompletedBy",'CompletionKind',s."CompletionKind",
                    'ReviewedAt',r."ReviewedAt",'ReviewedBy',r."ReviewedBy",'ReviewStatus',r."ReviewStatus",
                    'RetiredAt',a."RetiredAt",'ScheduleKey',a."ScheduleKey",
                    'ProcessStageId',(SELECT p."StageId" FROM operational_sequence_attempt p WHERE p."AssignmentId"=a."Id" LIMIT 1))
                FROM operational_assignment a
                JOIN operational_submission s ON s."AssignmentId"=a."Id"
                JOIN operational_review r ON r."AssignmentId"=a."Id"
                ON CONFLICT ("AssignmentId","Revision","Kind") DO NOTHING;
                INSERT INTO task_bonus_fact ("Id", "AssignmentId", "Revision", "RecordedAt", "Kind", "SnapshotJson")
                SELECT t."Id", t."AssignmentId", row_number() OVER (PARTITION BY t."AssignmentId" ORDER BY t."At",t."Id"),
                    t."At", 'HANDOFF', jsonb_set(to_jsonb(t),'{SnapshotJson}',to_jsonb(t."SnapshotJson"::text))
                FROM operational_transfer t ON CONFLICT ("AssignmentId","Revision","Kind") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "task_bonus_closure");

            migrationBuilder.DropTable(
                name: "task_bonus_fact");
        }
    }
}
