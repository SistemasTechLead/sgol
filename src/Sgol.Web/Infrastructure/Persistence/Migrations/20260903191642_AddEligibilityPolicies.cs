using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEligibilityPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_task_definition_version_id_task_definition_id",
                table: "task_definition_version",
                columns: new[] { "id", "task_definition_id" });

            migrationBuilder.CreateTable(
                name: "eligibility_policy_version",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_definition_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: false),
                    based_on_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    required_role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    requires_availability = table.Column<bool>(type: "boolean", nullable: false),
                    required_shift = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    effective_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    supersedes_id = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eligibility_policy_version", x => x.id);
                    table.CheckConstraint("CK_eligibility_policy_version_availability", "requires_availability");
                    table.CheckConstraint("CK_eligibility_policy_version_lifecycle", "(status = 'BORRADOR' AND effective_from IS NULL AND effective_to IS NULL AND reason IS NULL AND supersedes_id IS NULL) OR (status = 'VIGENTE' AND effective_from IS NOT NULL AND effective_to IS NULL AND btrim(reason) <> '') OR (status = 'SUSTITUIDA' AND effective_from IS NOT NULL AND effective_to > effective_from AND btrim(reason) <> '')");
                    table.CheckConstraint("CK_eligibility_policy_version_not_self_superseding", "supersedes_id IS NULL OR supersedes_id <> id");
                    table.CheckConstraint("CK_eligibility_policy_version_number", "version_no > 0");
                    table.CheckConstraint("CK_eligibility_policy_version_role", "required_role IN ('ADMINISTRACION','SUBCOORDINACION','PISO_VENTAS')");
                    table.CheckConstraint("CK_eligibility_policy_version_row_version", "row_version > 0");
                    table.CheckConstraint("CK_eligibility_policy_version_shift", "required_shift IS NULL");
                    table.CheckConstraint("CK_eligibility_policy_version_status", "status IN ('BORRADOR', 'VIGENTE', 'SUSTITUIDA')");
                    table.ForeignKey(
                        name: "FK_eligibility_policy_version_configuration_release_release_id",
                        column: x => x.release_id,
                        principalTable: "configuration_release",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_eligibility_policy_version_eligibility_policy_version_based~",
                        column: x => x.based_on_id,
                        principalTable: "eligibility_policy_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_eligibility_policy_version_eligibility_policy_version_super~",
                        column: x => x.supersedes_id,
                        principalTable: "eligibility_policy_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_eligibility_policy_version_task_definition_task_definition_~",
                        column: x => x.task_definition_id,
                        principalTable: "task_definition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_eligibility_policy_version_task_definition_version_task_def~",
                        columns: x => new { x.task_definition_version_id, x.task_definition_id },
                        principalTable: "task_definition_version",
                        principalColumns: new[] { "id", "task_definition_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_eligibility_policy_version_based_on_id",
                table: "eligibility_policy_version",
                column: "based_on_id");

            migrationBuilder.CreateIndex(
                name: "IX_eligibility_policy_version_exact_task_version",
                table: "eligibility_policy_version",
                columns: new[] { "task_definition_version_id", "task_definition_id" });

            migrationBuilder.CreateIndex(
                name: "IX_eligibility_policy_version_number",
                table: "eligibility_policy_version",
                columns: new[] { "task_definition_id", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_eligibility_policy_version_one_current",
                table: "eligibility_policy_version",
                column: "task_definition_id",
                unique: true,
                filter: "status = 'VIGENTE'");

            migrationBuilder.CreateIndex(
                name: "IX_eligibility_policy_version_release_task",
                table: "eligibility_policy_version",
                columns: new[] { "release_id", "task_definition_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_eligibility_policy_version_supersedes_id",
                table: "eligibility_policy_version",
                column: "supersedes_id",
                unique: true,
                filter: "supersedes_id IS NOT NULL");

            migrationBuilder.Sql(global::Sgol.Web.Infrastructure.Persistence.Versioning.VersioningPostgreSql.RequiredExtensionSql);
            migrationBuilder.Sql(global::Sgol.Web.Infrastructure.Persistence.Versioning.VersioningPostgreSql.AddNoOverlapConstraintSql(
                "eligibility_policy_version",
                "task_definition_id"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Rollback is intentionally blocked for this forward-only migration.");
        }
    }
}
