using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAvailabilityAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "availability_day_version",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    local_date = table.Column<DateOnly>(type: "date", nullable: false),
                    is_available = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    supersedes_id = table.Column<Guid>(type: "uuid", nullable: true),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_availability_day_version", x => x.id);
                    table.CheckConstraint("CK_availability_day_version_row_version", "row_version >= 1");
                    table.CheckConstraint("CK_availability_day_version_status", "status IN ('VIGENTE', 'HISTORICA')");
                    table.ForeignKey(
                        name: "FK_availability_day_version_app_user_changed_by",
                        column: x => x.changed_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_availability_day_version_availability_day_version_supersede~",
                        column: x => x.supersedes_id,
                        principalTable: "availability_day_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_availability_day_version_branch_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_availability_day_version_person_person_id",
                        column: x => x.person_id,
                        principalTable: "person",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_availability_day_version_branch_id",
                table: "availability_day_version",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_availability_day_version_changed_by",
                table: "availability_day_version",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "IX_availability_day_version_person_id_branch_id_local_date",
                table: "availability_day_version",
                columns: new[] { "person_id", "branch_id", "local_date" },
                unique: true,
                filter: "status = 'VIGENTE'");

            migrationBuilder.CreateIndex(
                name: "IX_availability_day_version_supersedes_id",
                table: "availability_day_version",
                column: "supersedes_id",
                unique: true,
                filter: "supersedes_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "Rollback is blocked because availability history and concurrency metadata must be preserved.");
        }
    }
}
