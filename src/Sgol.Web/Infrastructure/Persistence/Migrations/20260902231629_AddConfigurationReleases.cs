using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Sgol.Web.Infrastructure.Persistence.Versioning;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConfigurationReleases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(VersioningPostgreSql.RequiredExtensionSql);

            migrationBuilder.CreateTable(
                name: "configuration_release",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    effective_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    published_by = table.Column<Guid>(type: "uuid", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    supersedes_id = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuration_release", x => x.id);
                    table.CheckConstraint("CK_configuration_release_lifecycle", "(status = 'BORRADOR' AND effective_from IS NULL AND effective_to IS NULL AND reason IS NULL AND supersedes_id IS NULL) OR (status = 'VIGENTE' AND effective_from IS NOT NULL AND effective_to IS NULL AND btrim(reason) <> '') OR (status = 'SUSTITUIDA' AND effective_from IS NOT NULL AND effective_to > effective_from AND btrim(reason) <> '')");
                    table.CheckConstraint("CK_configuration_release_not_self_superseding", "supersedes_id IS NULL OR supersedes_id <> id");
                    table.CheckConstraint("CK_configuration_release_publication", "(status = 'BORRADOR' AND version_no IS NULL AND published_by IS NULL AND published_at IS NULL) OR (status IN ('VIGENTE', 'SUSTITUIDA') AND version_no > 0 AND published_by IS NOT NULL AND published_at IS NOT NULL)");
                    table.CheckConstraint("CK_configuration_release_row_version", "row_version > 0");
                    table.CheckConstraint("CK_configuration_release_status", "status IN ('BORRADOR', 'VIGENTE', 'SUSTITUIDA')");
                    table.ForeignKey(
                        name: "FK_configuration_release_app_user_published_by",
                        column: x => x.published_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_configuration_release_branch_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_configuration_release_configuration_release_supersedes_id",
                        column: x => x.supersedes_id,
                        principalTable: "configuration_release",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_configuration_release_branch_id_version_no",
                table: "configuration_release",
                columns: new[] { "branch_id", "version_no" },
                unique: true,
                filter: "version_no IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_configuration_release_one_current",
                table: "configuration_release",
                column: "branch_id",
                unique: true,
                filter: "status = 'VIGENTE'");

            migrationBuilder.CreateIndex(
                name: "IX_configuration_release_published_by",
                table: "configuration_release",
                column: "published_by");

            migrationBuilder.CreateIndex(
                name: "IX_configuration_release_supersedes_id",
                table: "configuration_release",
                column: "supersedes_id",
                unique: true,
                filter: "supersedes_id IS NOT NULL");

            migrationBuilder.Sql(VersioningPostgreSql.AddNoOverlapConstraintSql(
                "configuration_release",
                "branch_id"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Rollback is intentionally blocked for this forward-only migration.");
        }
    }
}
