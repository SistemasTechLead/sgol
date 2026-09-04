using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanPublications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_assignment_version_id_obligation_id",
                table: "assignment_version",
                columns: new[] { "id", "obligation_id" });

            migrationBuilder.CreateTable(
                name: "plan_version",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    scope_role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    published_by = table.Column<Guid>(type: "uuid", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    supersedes_id = table.Column<Guid>(type: "uuid", nullable: true),
                    plan_row_version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_version", x => x.id);
                    table.UniqueConstraint("AK_plan_version_id_plan_scope", x => new { x.id, x.plan_id, x.scope_role });
                    table.CheckConstraint("CK_plan_version_not_self_superseding", "supersedes_id IS NULL OR supersedes_id <> id");
                    table.CheckConstraint("CK_plan_version_number", "version_no > 0");
                    table.CheckConstraint("CK_plan_version_plan_row_version", "plan_row_version >= 2");
                    table.CheckConstraint("CK_plan_version_scope_role", "scope_role IN ('DIRECCION','ADMINISTRACION','SUBCOORDINACION')");
                    table.CheckConstraint("CK_plan_version_status", "status IN ('VIGENTE','SUSTITUIDA')");
                    table.ForeignKey(
                        name: "FK_plan_version_app_user_published_by",
                        column: x => x.published_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_plan_version_plan_version_supersedes_id_plan_id_scope_role",
                        columns: x => new { x.supersedes_id, x.plan_id, x.scope_role },
                        principalTable: "plan_version",
                        principalColumns: new[] { "id", "plan_id", "scope_role" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_plan_version_work_plan_plan_id",
                        column: x => x.plan_id,
                        principalTable: "work_plan",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plan_version_obligation",
                columns: table => new
                {
                    plan_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    obligation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignment_version_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_version_obligation", x => new { x.plan_version_id, x.obligation_id });
                    table.ForeignKey(
                        name: "FK_plan_version_obligation_assignment_version_assignment_versi~",
                        columns: x => new { x.assignment_version_id, x.obligation_id },
                        principalTable: "assignment_version",
                        principalColumns: new[] { "id", "obligation_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_plan_version_obligation_plan_version_plan_version_id",
                        column: x => x.plan_version_id,
                        principalTable: "plan_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_plan_version_obligation_work_obligation_obligation_id",
                        column: x => x.obligation_id,
                        principalTable: "work_obligation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_plan_version_published_by",
                table: "plan_version",
                column: "published_by");

            migrationBuilder.CreateIndex(
                name: "IX_plan_version_supersedes_id_plan_id_scope_role",
                table: "plan_version",
                columns: new[] { "supersedes_id", "plan_id", "scope_role" });

            migrationBuilder.CreateIndex(
                name: "UX_plan_version_current_scope",
                table: "plan_version",
                columns: new[] { "plan_id", "scope_role" },
                unique: true,
                filter: "status = 'VIGENTE'");

            migrationBuilder.CreateIndex(
                name: "UX_plan_version_plan_number",
                table: "plan_version",
                columns: new[] { "plan_id", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_plan_version_plan_row_version",
                table: "plan_version",
                columns: new[] { "plan_id", "plan_row_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_plan_version_supersedes",
                table: "plan_version",
                column: "supersedes_id",
                unique: true,
                filter: "supersedes_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_plan_version_obligation_assignment_version_id_obligation_id",
                table: "plan_version_obligation",
                columns: new[] { "assignment_version_id", "obligation_id" });

            migrationBuilder.CreateIndex(
                name: "IX_plan_version_obligation_obligation_id",
                table: "plan_version_obligation",
                column: "obligation_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Rollback is blocked for the HU-021 plan-publication migration.");
        }
    }
}
