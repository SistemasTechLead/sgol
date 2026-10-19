using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeparateOperationalLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_operational_assignment_app_user_ReviewedBy",
                table: "operational_assignment");

            migrationBuilder.DropForeignKey(
                name: "FK_operational_assignment_operational_assignment_ReassignedFro~",
                table: "operational_assignment");

            migrationBuilder.DropForeignKey(
                name: "FK_operational_attachment_operational_assignment_AssignmentId",
                table: "operational_attachment");

            migrationBuilder.DropForeignKey(
                name: "FK_operational_capture_session_operational_assignment_Assignme~",
                table: "operational_capture_session");

            migrationBuilder.DropForeignKey(
                name: "FK_operational_photograph_operational_assignment_AssignmentId",
                table: "operational_photograph");

            migrationBuilder.DropForeignKey(
                name: "FK_operational_progress_entry_operational_assignment_Assignmen~",
                table: "operational_progress_entry");

            migrationBuilder.DropIndex(
                name: "IX_operational_assignment_PersonId_CompletedAt",
                table: "operational_assignment");

            migrationBuilder.DropIndex(
                name: "IX_operational_assignment_ReviewedBy",
                table: "operational_assignment");

            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_assignment_review",
                table: "operational_assignment");

            migrationBuilder.RenameColumn(
                name: "ReviewComment",
                table: "operational_assignment",
                newName: "ReassignmentComment");

            migrationBuilder.CreateTable(
                name: "operational_review",
                columns: table => new
                {
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewStatus = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    ReviewComment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ReviewDecision = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_review", x => x.AssignmentId);
                    table.ForeignKey(
                        name: "FK_operational_review_operational_assignment_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "operational_assignment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "operational_submission",
                columns: table => new
                {
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletionKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Comment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Conclusion = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_submission", x => x.AssignmentId);
                    table.ForeignKey(
                        name: "FK_operational_submission_operational_assignment_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "operational_assignment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO operational_submission ("AssignmentId", "CompletedAt", "CompletionKind", "Comment", "Conclusion")
                SELECT "Id", "CompletedAt", CASE WHEN "CompletedAt" IS NOT NULL THEN 'HISTORICO' END, "Comment", "Conclusion"
                FROM operational_assignment;
                INSERT INTO operational_review ("AssignmentId", "ReviewStatus", "ReviewComment", "ReviewedAt", "ReviewedBy", "ReviewDecision")
                SELECT "Id", "ReviewStatus", "ReassignmentComment", "ReviewedAt", "ReviewedBy",
                    CASE WHEN "ReviewStatus" IN ('CONCLUIDA','NO_SATISFECHA') THEN "ReviewStatus"
                        WHEN "ReviewStatus" = 'REASIGNADA' THEN COALESCE((SELECT e.after_data->>'decision' FROM audit_event e
                            WHERE e.resource_id = a."Id" AND e.action = 'OPERATIONAL_TASK_REVIEWED'
                            ORDER BY e.occurred_at DESC,e.id DESC LIMIT 1),'HISTORICO') END
                FROM operational_assignment a;
                UPDATE operational_review SET "ReviewComment" = NULL WHERE "ReviewedAt" IS NULL AND COALESCE("ReviewStatus",'') NOT IN ('CONCLUIDA','NO_SATISFECHA','REASIGNADA');
                UPDATE operational_assignment SET "ReassignmentComment" = NULL WHERE "ReviewedAt" IS NOT NULL OR "ReviewStatus" IN ('CONCLUIDA','NO_SATISFECHA','REASIGNADA');
                -- An explicit delivery event with the same timestamp proves its author; current ownership does not.
                WITH delivered AS (
                    SELECT DISTINCT ON (e.resource_id) e.resource_id, e.actor_user_id,
                        (e.after_data->>'CompletedAt')::timestamptz AS at
                    FROM audit_event e JOIN app_user u ON u.id = e.actor_user_id
                    WHERE e.action = 'OPERATIONAL_TASK_COMPLETED' AND e.after_data->>'CompletedAt' IS NOT NULL
                    ORDER BY e.resource_id, e.occurred_at DESC, e.id DESC
                )
                UPDATE operational_submission s SET "CompletedBy" = d.actor_user_id, "CompletionKind" = 'ENTREGA'
                FROM delivered d WHERE d.resource_id = s."AssignmentId" AND d.at = s."CompletedAt";
                -- Recover metadata archived by a prior downgrade only when the original event still matches.
                WITH archive AS (SELECT DISTINCT ON (resource_id) resource_id, after_data FROM audit_event
                    WHERE action = 'OPERATIONAL_LIFECYCLE_ROLLBACK_SNAPSHOT' ORDER BY resource_id, occurred_at DESC, id DESC)
                UPDATE operational_submission s SET "CompletedBy" = (a.after_data->'submission'->>'CompletedBy')::uuid,
                    "CompletionKind" = a.after_data->'submission'->>'CompletionKind'
                FROM archive a WHERE a.resource_id = s."AssignmentId"
                    AND s."CompletedAt" IS NOT DISTINCT FROM (a.after_data->'submission'->>'CompletedAt')::timestamptz
                    AND s."Comment" IS NOT DISTINCT FROM a.after_data->'submission'->>'Comment'
                    AND s."Conclusion" IS NOT DISTINCT FROM a.after_data->'submission'->>'Conclusion';
                WITH archive AS (SELECT DISTINCT ON (resource_id) resource_id, after_data FROM audit_event
                    WHERE action = 'OPERATIONAL_LIFECYCLE_ROLLBACK_SNAPSHOT' ORDER BY resource_id, occurred_at DESC, id DESC)
                UPDATE operational_review r SET "ReviewDecision" = a.after_data->'review'->>'ReviewDecision',
                    "ReviewComment" = a.after_data->'review'->>'ReviewComment'
                FROM archive a, operational_assignment t WHERE a.resource_id = r."AssignmentId" AND t."Id" = r."AssignmentId"
                    AND r."ReviewedAt" IS NOT DISTINCT FROM (a.after_data->'review'->>'ReviewedAt')::timestamptz
                    AND r."ReviewStatus" IS NOT DISTINCT FROM a.after_data->'review'->>'ReviewStatus'
                    AND COALESCE(r."ReviewComment",t."ReassignmentComment") IS NOT DISTINCT FROM COALESCE(a.after_data->'review'->>'ReviewComment',a.after_data->>'reassignment_comment');
                WITH archive AS (SELECT DISTINCT ON (resource_id) resource_id, after_data FROM audit_event
                    WHERE action = 'OPERATIONAL_LIFECYCLE_ROLLBACK_SNAPSHOT' ORDER BY resource_id, occurred_at DESC, id DESC)
                UPDATE operational_assignment t SET "ReassignmentComment" = a.after_data->>'reassignment_comment'
                FROM archive a WHERE a.resource_id = t."Id"
                    AND t."ReassignedFromId" IS NOT DISTINCT FROM (a.after_data->>'reassigned_from_id')::uuid;
                ALTER TABLE operational_submission ADD CONSTRAINT "FK_submission_author" FOREIGN KEY ("CompletedBy") REFERENCES app_user(id) ON DELETE RESTRICT;
                ALTER TABLE operational_review ADD CONSTRAINT "FK_review_author" FOREIGN KEY ("ReviewedBy") REFERENCES app_user(id) ON DELETE RESTRICT;
                ALTER TABLE operational_submission ADD CONSTRAINT "CK_submission_kind" CHECK ("CompletionKind" IS NULL OR "CompletionKind" IN ('ENTREGA','SUPERVISION','HISTORICO'));
                ALTER TABLE operational_submission ADD CONSTRAINT "CK_submission_actor" CHECK ("CompletionKind" IS NULL OR "CompletionKind" = 'HISTORICO' OR ("CompletedAt" IS NOT NULL AND "CompletedBy" IS NOT NULL));
                ALTER TABLE operational_review ADD CONSTRAINT "CK_review_state" CHECK (("ReviewStatus" IS NULL OR "ReviewStatus" IN ('PENDIENTE_REVISION','CONCLUIDA','NO_SATISFECHA','REASIGNADA')) AND (("ReviewedAt" IS NULL AND "ReviewedBy" IS NULL) OR ("ReviewedAt" IS NOT NULL AND "ReviewedBy" IS NOT NULL)) AND ("ReviewDecision" IS NULL OR "ReviewDecision" IN ('CONCLUIDA','NO_SATISFECHA','MISMA_PERSONA','ROL','HISTORICO')));
                ALTER TABLE operational_assignment ADD CONSTRAINT "FK_assignment_required_submission" FOREIGN KEY ("Id") REFERENCES operational_submission("AssignmentId") DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE operational_assignment ADD CONSTRAINT "FK_assignment_required_review" FOREIGN KEY ("Id") REFERENCES operational_review("AssignmentId") DEFERRABLE INITIALLY DEFERRED;
                """);
            migrationBuilder.DropColumn(
                name: "Comment",
                table: "operational_assignment");
            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "operational_assignment");
            migrationBuilder.DropColumn(
                name: "Conclusion",
                table: "operational_assignment");
            migrationBuilder.DropColumn(
                name: "ReviewStatus",
                table: "operational_assignment");
            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "operational_assignment");
            migrationBuilder.DropColumn(
                name: "ReviewedBy",
                table: "operational_assignment");

            migrationBuilder.CreateIndex(
                name: "IX_operational_assignment_PersonId",
                table: "operational_assignment",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_review_ReviewedBy",
                table: "operational_review",
                column: "ReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_operational_submission_CompletedAt",
                table: "operational_submission",
                column: "CompletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_operational_submission_CompletedBy",
                table: "operational_submission",
                column: "CompletedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_operational_assignment_operational_review_ReassignedFromId",
                table: "operational_assignment",
                column: "ReassignedFromId",
                principalTable: "operational_review",
                principalColumn: "AssignmentId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_operational_attachment_operational_review_AssignmentId",
                table: "operational_attachment",
                column: "AssignmentId",
                principalTable: "operational_review",
                principalColumn: "AssignmentId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_operational_capture_session_operational_review_AssignmentId",
                table: "operational_capture_session",
                column: "AssignmentId",
                principalTable: "operational_review",
                principalColumn: "AssignmentId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_operational_photograph_operational_review_AssignmentId",
                table: "operational_photograph",
                column: "AssignmentId",
                principalTable: "operational_review",
                principalColumn: "AssignmentId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_operational_progress_entry_operational_review_AssignmentId",
                table: "operational_progress_entry",
                column: "AssignmentId",
                principalTable: "operational_review",
                principalColumn: "AssignmentId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- The old schema has one comment field and cannot represent both event provenances.
                INSERT INTO audit_event (id, occurred_at, actor_type, action, resource_type, resource_id, branch_id, correlation_id, outcome, after_data)
                SELECT gen_random_uuid(), clock_timestamp(), 'SYSTEM', 'OPERATIONAL_LIFECYCLE_ROLLBACK_SNAPSHOT',
                    'OPERATIONAL_ASSIGNMENT', a."Id", p.branch_id, gen_random_uuid(), 'PRESERVED',
                    jsonb_build_object('submission',to_jsonb(s),'review',to_jsonb(r),
                        'reassignment_comment',a."ReassignmentComment",'reassigned_from_id',a."ReassignedFromId")
                FROM operational_assignment a JOIN direction_prepared_plan p ON p.id = a."PlanId"
                    JOIN operational_submission s ON s."AssignmentId" = a."Id"
                    JOIN operational_review r ON r."AssignmentId" = a."Id";
                ALTER TABLE operational_assignment DROP CONSTRAINT "FK_assignment_required_submission";
                ALTER TABLE operational_assignment DROP CONSTRAINT "FK_assignment_required_review";
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_operational_assignment_operational_review_ReassignedFromId",
                table: "operational_assignment");

            migrationBuilder.DropForeignKey(
                name: "FK_operational_attachment_operational_review_AssignmentId",
                table: "operational_attachment");

            migrationBuilder.DropForeignKey(
                name: "FK_operational_capture_session_operational_review_AssignmentId",
                table: "operational_capture_session");

            migrationBuilder.DropForeignKey(
                name: "FK_operational_photograph_operational_review_AssignmentId",
                table: "operational_photograph");

            migrationBuilder.DropForeignKey(
                name: "FK_operational_progress_entry_operational_review_AssignmentId",
                table: "operational_progress_entry");

            migrationBuilder.DropIndex(
                name: "IX_operational_assignment_PersonId",
                table: "operational_assignment");

            migrationBuilder.RenameColumn(
                name: "ReassignmentComment",
                table: "operational_assignment",
                newName: "ReviewComment");

            migrationBuilder.AddColumn<string>(
                name: "Comment",
                table: "operational_assignment",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "operational_assignment",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Conclusion",
                table: "operational_assignment",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewStatus",
                table: "operational_assignment",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewedAt",
                table: "operational_assignment",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedBy",
                table: "operational_assignment",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_operational_assignment_PersonId_CompletedAt",
                table: "operational_assignment",
                columns: new[] { "PersonId", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_assignment_ReviewedBy",
                table: "operational_assignment",
                column: "ReviewedBy");

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_assignment_review",
                table: "operational_assignment",
                sql: "(\"ReviewStatus\" IS NULL OR \"ReviewStatus\" IN ('PENDIENTE_REVISION','CONCLUIDA','NO_SATISFECHA','REASIGNADA')) AND ((\"ReviewedBy\" IS NULL AND \"ReviewedAt\" IS NULL) OR (\"ReviewedBy\" IS NOT NULL AND \"ReviewedAt\" IS NOT NULL))");

            migrationBuilder.AddForeignKey(
                name: "FK_operational_assignment_app_user_ReviewedBy",
                table: "operational_assignment",
                column: "ReviewedBy",
                principalTable: "app_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_operational_assignment_operational_assignment_ReassignedFro~",
                table: "operational_assignment",
                column: "ReassignedFromId",
                principalTable: "operational_assignment",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_operational_attachment_operational_assignment_AssignmentId",
                table: "operational_attachment",
                column: "AssignmentId",
                principalTable: "operational_assignment",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_operational_capture_session_operational_assignment_Assignme~",
                table: "operational_capture_session",
                column: "AssignmentId",
                principalTable: "operational_assignment",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_operational_photograph_operational_assignment_AssignmentId",
                table: "operational_photograph",
                column: "AssignmentId",
                principalTable: "operational_assignment",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_operational_progress_entry_operational_assignment_Assignmen~",
                table: "operational_progress_entry",
                column: "AssignmentId",
                principalTable: "operational_assignment",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
                UPDATE operational_assignment a SET "CompletedAt" = s."CompletedAt", "Comment" = s."Comment", "Conclusion" = s."Conclusion"
                FROM operational_submission s WHERE s."AssignmentId" = a."Id";
                UPDATE operational_assignment a SET "ReviewStatus" = r."ReviewStatus", "ReviewComment" = COALESCE(r."ReviewComment",a."ReviewComment"), "ReviewedAt" = r."ReviewedAt", "ReviewedBy" = r."ReviewedBy"
                FROM operational_review r WHERE r."AssignmentId" = a."Id";
                """);
            migrationBuilder.DropTable(
                name: "operational_review");
            migrationBuilder.DropTable(
                name: "operational_submission");
        }
    }
}
