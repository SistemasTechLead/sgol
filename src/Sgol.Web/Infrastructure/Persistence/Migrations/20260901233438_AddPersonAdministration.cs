using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_employment_version_person_id_branch_id",
                table: "employment_version");

            migrationBuilder.DropIndex(
                name: "IX_employment_version_supersedes_id",
                table: "employment_version");

            migrationBuilder.CreateTable(
                name: "idempotency_record",
                columns: table => new
                {
                    scope = table.Column<string>(type: "text", nullable: false),
                    key = table.Column<Guid>(type: "uuid", nullable: false),
                    request_hash = table.Column<string>(type: "character(64)", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    resource_type = table.Column<string>(type: "text", nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    response_code = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_record", x => new { x.scope, x.key });
                });

            migrationBuilder.CreateIndex(
                name: "IX_employment_version_person_id_branch_id",
                table: "employment_version",
                columns: new[] { "person_id", "branch_id" },
                unique: true,
                filter: "valid_to IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_employment_version_supersedes_id",
                table: "employment_version",
                column: "supersedes_id",
                unique: true,
                filter: "supersedes_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_employment_version_interval",
                table: "employment_version",
                sql: "valid_to IS NULL OR valid_to >= valid_from");

            migrationBuilder.AddCheckConstraint(
                name: "CK_employment_version_status",
                table: "employment_version",
                sql: "status IN ('ACTIVA', 'INACTIVA')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_person_stable_code_not_blank",
                table: "person",
                sql: "btrim(stable_code) <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "Rollback is blocked because person employment history and idempotency records must be preserved.");
        }
    }
}
