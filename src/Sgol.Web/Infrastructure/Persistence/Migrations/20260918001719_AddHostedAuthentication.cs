using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHostedAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "access_failed_count",
                table: "app_user",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "authentication_row_version",
                table: "app_user",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "lockout_end_utc",
                table: "app_user",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "lockout_level",
                table: "app_user",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "authentication_challenge",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    security_stamp_snapshot = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failed_attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    protected_totp_secret = table.Column<string>(type: "text", nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_authentication_challenge", x => x.id);
                    table.CheckConstraint("CK_authentication_challenge_failed_attempt_count", "failed_attempt_count >= 0");
                    table.CheckConstraint("CK_authentication_challenge_interval", "expires_at > created_at");
                    table.CheckConstraint("CK_authentication_challenge_purpose", "purpose IN ('CHANGE_PASSWORD', 'ENROLL_MFA', 'VERIFY_MFA', 'REGENERATE_RECOVERY_CODES')");
                    table.CheckConstraint("CK_authentication_challenge_row_version", "row_version >= 1");
                    table.CheckConstraint("CK_authentication_challenge_status", "status IN ('PENDING', 'CONSUMED', 'EXPIRED')");
                    table.ForeignKey(
                        name: "FK_authentication_challenge_app_user_user_id",
                        column: x => x.user_id,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mfa_recovery_code",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code_hash = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mfa_recovery_code", x => x.id);
                    table.CheckConstraint("CK_mfa_recovery_code_row_version", "row_version >= 1");
                    table.ForeignKey(
                        name: "FK_mfa_recovery_code_app_user_user_id",
                        column: x => x.user_id,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mfa_totp_credential",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    protected_secret = table.Column<string>(type: "text", nullable: false),
                    protection_version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    enrolled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_accepted_time_step = table.Column<long>(type: "bigint", nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mfa_totp_credential", x => x.id);
                    table.CheckConstraint("CK_mfa_totp_credential_protection_version", "protection_version = 1");
                    table.CheckConstraint("CK_mfa_totp_credential_row_version", "row_version >= 1");
                    table.ForeignKey(
                        name: "FK_mfa_totp_credential_app_user_user_id",
                        column: x => x.user_id,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_app_user_access_failed_count",
                table: "app_user",
                sql: "access_failed_count >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_app_user_authentication_row_version",
                table: "app_user",
                sql: "authentication_row_version >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_app_user_lockout_level",
                table: "app_user",
                sql: "lockout_level >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_authentication_challenge_user_id_status_expires_at",
                table: "authentication_challenge",
                columns: new[] { "user_id", "status", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "IX_mfa_recovery_code_user_id_consumed_at_revoked_at",
                table: "mfa_recovery_code",
                columns: new[] { "user_id", "consumed_at", "revoked_at" });

            migrationBuilder.CreateIndex(
                name: "IX_mfa_totp_credential_user_id",
                table: "mfa_totp_credential",
                column: "user_id",
                unique: true,
                filter: "revoked_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "Rollback is blocked because hosted authentication state is security-sensitive and append-only.");
        }
    }
}
