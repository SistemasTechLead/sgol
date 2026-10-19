using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Sgol.Web.Infrastructure.Persistence.Versioning;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "calendar_day_version",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    local_date = table.Column<DateOnly>(type: "date", nullable: false),
                    day_type = table.Column<string>(type: "text", nullable: false),
                    is_working_day = table.Column<bool>(type: "boolean", nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pending_reason = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    effective_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    supersedes_id = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calendar_day_version", x => x.id);
                    table.CheckConstraint("CK_calendar_day_version_day_type", "day_type IN ('LABORABLE', 'FESTIVO', 'CIERRE_EXTRAORDINARIO')");
                    table.CheckConstraint("CK_calendar_day_version_lifecycle", "(status = 'BORRADOR' AND effective_from IS NULL AND effective_to IS NULL AND reason IS NULL AND supersedes_id IS NULL) OR (status = 'VIGENTE' AND effective_from IS NOT NULL AND effective_to IS NULL AND btrim(reason) <> '') OR (status = 'SUSTITUIDA' AND effective_from IS NOT NULL AND effective_to > effective_from AND btrim(reason) <> '')");
                    table.CheckConstraint("CK_calendar_day_version_not_self_superseding", "supersedes_id IS NULL OR supersedes_id <> id");
                    table.CheckConstraint("CK_calendar_day_version_pending_reason", "(status = 'BORRADOR' AND btrim(pending_reason) <> '') OR (status IN ('VIGENTE', 'SUSTITUIDA') AND pending_reason IS NULL)");
                    table.CheckConstraint("CK_calendar_day_version_row_version", "row_version > 0");
                    table.CheckConstraint("CK_calendar_day_version_status", "status IN ('BORRADOR', 'VIGENTE', 'SUSTITUIDA')");
                    table.CheckConstraint("CK_calendar_day_version_working_consistency", "(day_type = 'LABORABLE' AND is_working_day) OR (day_type IN ('FESTIVO', 'CIERRE_EXTRAORDINARIO') AND NOT is_working_day)");
                    table.ForeignKey(
                        name: "FK_calendar_day_version_branch_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_calendar_day_version_calendar_day_version_supersedes_id",
                        column: x => x.supersedes_id,
                        principalTable: "calendar_day_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_calendar_day_version_configuration_release_release_id",
                        column: x => x.release_id,
                        principalTable: "configuration_release",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_calendar_day_version_one_current",
                table: "calendar_day_version",
                columns: new[] { "branch_id", "local_date" },
                unique: true,
                filter: "status = 'VIGENTE'");

            migrationBuilder.CreateIndex(
                name: "IX_calendar_day_version_release_id_local_date",
                table: "calendar_day_version",
                columns: new[] { "release_id", "local_date" },
                unique: true,
                filter: "status = 'BORRADOR'");

            migrationBuilder.CreateIndex(
                name: "IX_calendar_day_version_supersedes_id",
                table: "calendar_day_version",
                column: "supersedes_id",
                unique: true,
                filter: "supersedes_id IS NOT NULL");

            migrationBuilder.Sql(VersioningPostgreSql.AddNoOverlapConstraintSql(
                "calendar_day_version",
                "branch_id",
                "local_date"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Rollback is intentionally blocked for this forward-only migration.");
        }
    }
}
