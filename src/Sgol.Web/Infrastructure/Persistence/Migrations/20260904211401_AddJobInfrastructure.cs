using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJobInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "outbox_event",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    aggregate_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    available_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_error = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_event", x => x.id);
                    table.CheckConstraint("CK_outbox_event_attempt_count", "attempt_count >= 0 AND attempt_count <= 5");
                    table.CheckConstraint("CK_outbox_event_payload", "jsonb_typeof(payload) = 'object' AND (payload - 'schemaVersion' - 'correlationId' - 'data') = '{}'::jsonb AND payload ? 'schemaVersion' AND payload ? 'correlationId' AND payload ? 'data' AND jsonb_typeof(payload -> 'schemaVersion') = 'number' AND payload ->> 'schemaVersion' = '1' AND jsonb_typeof(payload -> 'correlationId') = 'string' AND payload ->> 'correlationId' ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND jsonb_typeof(payload -> 'data') = 'object' AND octet_length(payload::text) <= 65536");
                    table.CheckConstraint("CK_outbox_event_state", "(processed_at IS NULL AND attempt_count = 0 AND last_error IS NULL) OR (processed_at IS NULL AND attempt_count BETWEEN 1 AND 5 AND last_error IS NOT NULL) OR (processed_at IS NOT NULL AND attempt_count BETWEEN 1 AND 5 AND last_error IS NULL)");
                    table.CheckConstraint("CK_outbox_event_timeline", "available_at >= created_at AND (processed_at IS NULL OR processed_at >= created_at)");
                    table.CheckConstraint("CK_outbox_event_type", "event_type ~ '^[A-Z][A-Z0-9_.]{0,127}$'");
                });

            migrationBuilder.CreateTable(
                name: "scheduled_job_run",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    scheduled_for = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    checkpoint = table.Column<string>(type: "jsonb", nullable: true),
                    error = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scheduled_job_run", x => x.id);
                    table.CheckConstraint("CK_scheduled_job_run_checkpoint", "checkpoint IS NULL OR (jsonb_typeof(checkpoint) = 'object' AND octet_length(checkpoint::text) <= 65536)");
                    table.CheckConstraint("CK_scheduled_job_run_name", "job_name ~ '^[A-Z][A-Z0-9_]{0,63}$'");
                    table.CheckConstraint("CK_scheduled_job_run_state", "(status = 'RUNNING' AND ended_at IS NULL AND error IS NULL) OR (status = 'SUCCEEDED' AND ended_at IS NOT NULL AND error IS NULL) OR (status = 'FAILED' AND ended_at IS NOT NULL AND error IS NOT NULL)");
                    table.CheckConstraint("CK_scheduled_job_run_timeline", "ended_at IS NULL OR ended_at >= started_at");
                });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_event_claim",
                table: "outbox_event",
                columns: new[] { "processed_at", "available_at", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_event_type_created_at",
                table: "outbox_event",
                columns: new[] { "event_type", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_job_run_status_scheduled_for",
                table: "scheduled_job_run",
                columns: new[] { "status", "scheduled_for" });

            migrationBuilder.CreateIndex(
                name: "UX_scheduled_job_run_name_scheduled_for",
                table: "scheduled_job_run",
                columns: new[] { "job_name", "scheduled_for" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Rollback is blocked for AddJobInfrastructure.");
        }
    }
}
