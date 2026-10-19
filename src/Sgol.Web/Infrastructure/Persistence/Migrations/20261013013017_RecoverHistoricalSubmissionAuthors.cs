using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecoverHistoricalSubmissionAuthors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                WITH delivered AS (
                    SELECT DISTINCT ON (e.resource_id) e.resource_id, e.actor_user_id,
                        (e.after_data->>'CompletedAt')::timestamptz AS at
                    FROM audit_event e JOIN app_user u ON u.id = e.actor_user_id
                    WHERE e.action = 'OPERATIONAL_TASK_COMPLETED' AND e.after_data->>'CompletedAt' IS NOT NULL
                    ORDER BY e.resource_id, e.occurred_at DESC, e.id DESC
                )
                UPDATE operational_submission s SET "CompletedBy" = d.actor_user_id, "CompletionKind" = 'ENTREGA'
                FROM delivered d WHERE d.resource_id = s."AssignmentId" AND d.at = s."CompletedAt"
                    AND s."CompletedBy" IS NULL AND (s."CompletionKind" IS NULL OR s."CompletionKind" = 'HISTORICO');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Proven historical authorship is not discarded. The lifecycle rollback archives this metadata.
        }
    }
}
