using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScreenDeviceLinking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_screen_account_status",
                table: "screen_account");

            migrationBuilder.AddColumn<string>(
                name: "credential_hash",
                table: "screen_account",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "device_name",
                table: "screen_account",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "linked_at",
                table: "screen_account",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "session_stamp",
                table: "screen_account",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "screen_browser_ticket",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScreenId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_screen_browser_ticket", x => x.Id);
                    table.ForeignKey(
                        name: "FK_screen_browser_ticket_screen_account_ScreenId",
                        column: x => x.ScreenId,
                        principalTable: "screen_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "screen_link_request",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScreenId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CredentialHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DeviceName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_screen_link_request", x => x.Id);
                    table.ForeignKey(
                        name: "FK_screen_link_request_screen_account_ScreenId",
                        column: x => x.ScreenId,
                        principalTable: "screen_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_screen_account_link",
                table: "screen_account",
                sql: "(status = 'ACTIVA' AND credential_hash IS NOT NULL AND credential_hash ~ '^[A-F0-9]{64}$' AND session_stamp IS NOT NULL AND linked_at IS NOT NULL AND device_name IS NOT NULL) OR (status <> 'ACTIVA' AND credential_hash IS NULL AND session_stamp IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_screen_account_status",
                table: "screen_account",
                sql: "status IN ('PENDIENTE_VINCULACION', 'ACTIVA', 'REVOCADA')");

            migrationBuilder.CreateIndex(
                name: "IX_screen_browser_ticket_ExpiresAt",
                table: "screen_browser_ticket",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_screen_browser_ticket_ScreenId",
                table: "screen_browser_ticket",
                column: "ScreenId");

            migrationBuilder.CreateIndex(
                name: "IX_screen_browser_ticket_TicketHash",
                table: "screen_browser_ticket",
                column: "TicketHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_screen_link_request_ApprovalCode",
                table: "screen_link_request",
                column: "ApprovalCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_screen_link_request_ScreenId_ExpiresAt",
                table: "screen_link_request",
                columns: new[] { "ScreenId", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM screen_link_request) OR EXISTS (SELECT 1 FROM screen_browser_ticket)
                       OR EXISTS (SELECT 1 FROM screen_account WHERE status <> 'PENDIENTE_VINCULACION') THEN
                        RAISE EXCEPTION 'Preserve device linking history: revoke devices instead of downgrading';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "screen_browser_ticket");

            migrationBuilder.DropTable(
                name: "screen_link_request");

            migrationBuilder.DropCheckConstraint(
                name: "CK_screen_account_link",
                table: "screen_account");

            migrationBuilder.DropCheckConstraint(
                name: "CK_screen_account_status",
                table: "screen_account");

            migrationBuilder.DropColumn(
                name: "credential_hash",
                table: "screen_account");

            migrationBuilder.DropColumn(
                name: "device_name",
                table: "screen_account");

            migrationBuilder.DropColumn(
                name: "linked_at",
                table: "screen_account");

            migrationBuilder.DropColumn(
                name: "session_stamp",
                table: "screen_account");

            migrationBuilder.AddCheckConstraint(
                name: "CK_screen_account_status",
                table: "screen_account",
                sql: "status = 'PENDIENTE_VINCULACION'");
        }
    }
}
