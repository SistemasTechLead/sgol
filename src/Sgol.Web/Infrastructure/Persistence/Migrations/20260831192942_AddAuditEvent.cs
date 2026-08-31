using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_event",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_type = table.Column<string>(type: "text", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    resource_type = table.Column<string>(type: "text", nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: true),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<string>(type: "text", nullable: true),
                    before_data = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    after_data = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    outcome = table.Column<string>(type: "text", nullable: false),
                    source_ip_hash = table.Column<string>(type: "character(64)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_event", x => x.id);
                });

            migrationBuilder.Sql(
                """
                CREATE FUNCTION sgol_reject_audit_event_change()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    RAISE EXCEPTION 'audit_event is append-only'
                        USING ERRCODE = '55000';
                END;
                $function$;

                CREATE TRIGGER audit_event_append_only
                BEFORE UPDATE OR DELETE ON audit_event
                FOR EACH ROW
                EXECUTE FUNCTION sgol_reject_audit_event_change();
                """);

            migrationBuilder.CreateIndex(
                name: "IX_audit_event_actor_user_id_occurred_at",
                table: "audit_event",
                columns: new[] { "actor_user_id", "occurred_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_audit_event_branch_id_occurred_at",
                table: "audit_event",
                columns: new[] { "branch_id", "occurred_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_audit_event_resource_type_resource_id_occurred_at",
                table: "audit_event",
                columns: new[] { "resource_type", "resource_id", "occurred_at" },
                descending: new[] { false, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $block$
                BEGIN
                    RAISE EXCEPTION 'audit_event migration is forward-only';
                END;
                $block$;
                """);
        }
    }
}
