using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInternalNotices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "internal_notice",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notice_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    resource_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_internal_notice", x => x.id);
                    table.CheckConstraint("CK_internal_notice_non_empty_ids", "id <> '00000000-0000-0000-0000-000000000000'::uuid AND recipient_user_id <> '00000000-0000-0000-0000-000000000000'::uuid AND resource_id <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("CK_internal_notice_read_at", "read_at IS NULL OR read_at >= created_at");
                    table.CheckConstraint("CK_internal_notice_resource_type", "resource_type = 'ASSIGNMENT_VERSION'");
                    table.CheckConstraint("CK_internal_notice_type", "notice_type = 'OBLIGATION_ASSIGNED'");
                    table.ForeignKey(
                        name: "FK_internal_notice_app_user_recipient_user_id",
                        column: x => x.recipient_user_id,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_internal_notice_assignment_version_resource_id",
                        column: x => x.resource_id,
                        principalTable: "assignment_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_internal_notice_recipient_user_id_created_at_id",
                table: "internal_notice",
                columns: new[] { "recipient_user_id", "created_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_internal_notice_recipient_user_id_read_at_created_at_id",
                table: "internal_notice",
                columns: new[] { "recipient_user_id", "read_at", "created_at", "id" },
                descending: new[] { false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_internal_notice_resource_id",
                table: "internal_notice",
                column: "resource_id");

            migrationBuilder.CreateIndex(
                name: "UX_internal_notice_producer",
                table: "internal_notice",
                columns: new[] { "notice_type", "resource_type", "resource_id" },
                unique: true);

            migrationBuilder.Sql(
                """
                CREATE FUNCTION sgol_validate_internal_notice()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                DECLARE
                    assignment_person_id uuid;
                    assignment_assigned_at timestamp with time zone;
                    recipient_person_id uuid;
                    recipient_status text;
                    assignment_branch_id uuid;
                BEGIN
                    IF TG_OP = 'UPDATE' THEN
                        IF NEW.id IS DISTINCT FROM OLD.id
                           OR NEW.recipient_user_id IS DISTINCT FROM OLD.recipient_user_id
                           OR NEW.notice_type IS DISTINCT FROM OLD.notice_type
                           OR NEW.resource_type IS DISTINCT FROM OLD.resource_type
                           OR NEW.resource_id IS DISTINCT FROM OLD.resource_id
                           OR NEW.created_at IS DISTINCT FROM OLD.created_at
                           OR OLD.read_at IS NOT NULL
                           OR NEW.read_at IS NULL THEN
                            RAISE EXCEPTION 'internal_notice only permits the unread-to-read transition'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_internal_notice_immutable';
                        END IF;

                        RETURN NEW;
                    END IF;

                    SELECT assignment.person_id, assignment.assigned_at, obligation.branch_id
                    INTO assignment_person_id, assignment_assigned_at, assignment_branch_id
                    FROM assignment_version AS assignment
                    JOIN work_obligation AS obligation ON obligation.id = assignment.obligation_id
                    WHERE assignment.id = NEW.resource_id;

                    SELECT person_id, status
                    INTO recipient_person_id, recipient_status
                    FROM app_user
                    WHERE id = NEW.recipient_user_id;

                    IF assignment_person_id IS NULL
                       OR recipient_person_id IS DISTINCT FROM assignment_person_id
                       OR recipient_status IS DISTINCT FROM 'ACTIVA'
                       OR assignment_branch_id IS DISTINCT FROM '019d2d67-2c00-7000-8000-000000000001'::uuid
                       OR NEW.created_at IS DISTINCT FROM assignment_assigned_at
                       OR NEW.read_at IS NOT NULL THEN
                        RAISE EXCEPTION 'internal_notice producer invariants are not satisfied'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_internal_notice_producer';
                    END IF;

                    RETURN NEW;
                END;
                $function$;

                CREATE TRIGGER TR_internal_notice_validate
                BEFORE INSERT OR UPDATE ON internal_notice
                FOR EACH ROW
                EXECUTE FUNCTION sgol_validate_internal_notice();

                CREATE FUNCTION sgol_reject_internal_notice_delete()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    RAISE EXCEPTION 'internal_notice rows are append-only'
                        USING ERRCODE = '23514', CONSTRAINT = 'CK_internal_notice_append_only';
                END;
                $function$;

                CREATE TRIGGER TR_internal_notice_no_delete
                BEFORE DELETE ON internal_notice
                FOR EACH ROW
                EXECUTE FUNCTION sgol_reject_internal_notice_delete();

                CREATE FUNCTION sgol_require_assignment_notice()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM internal_notice
                        WHERE notice_type = 'OBLIGATION_ASSIGNED'
                          AND resource_type = 'ASSIGNMENT_VERSION'
                          AND resource_id = NEW.id) THEN
                        RAISE EXCEPTION 'assignment_version requires its internal notice'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_assignment_version_internal_notice';
                    END IF;

                    RETURN NULL;
                END;
                $function$;

                CREATE CONSTRAINT TRIGGER TR_assignment_version_notice
                AFTER INSERT ON assignment_version
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW
                EXECUTE FUNCTION sgol_require_assignment_notice();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Rollback is blocked for AddInternalNotices.");
        }
    }
}
