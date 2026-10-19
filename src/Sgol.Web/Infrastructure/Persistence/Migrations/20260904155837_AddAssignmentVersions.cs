using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "assignment_version",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    obligation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    assignment_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    explanation = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    assigned_by = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    supersedes_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assignment_version", x => x.id);
                    table.CheckConstraint("CK_assignment_version_correction_metadata", "(assignment_type = 'AUTOMATICA' AND reason IS NULL AND assigned_by IS NULL AND supersedes_id IS NULL) OR (assignment_type = 'CORRECCION' AND btrim(reason) <> '' AND assigned_by IS NOT NULL AND supersedes_id IS NOT NULL)");
                    table.CheckConstraint("CK_assignment_version_explanation_object", "jsonb_typeof(explanation) = 'object'");
                    table.CheckConstraint("CK_assignment_version_not_self_superseding", "supersedes_id IS NULL OR supersedes_id <> id");
                    table.CheckConstraint("CK_assignment_version_status", "status IN ('VIGENTE','SUSTITUIDA')");
                    table.CheckConstraint("CK_assignment_version_type", "assignment_type IN ('AUTOMATICA','CORRECCION')");
                    table.ForeignKey(
                        name: "FK_assignment_version_app_user_assigned_by",
                        column: x => x.assigned_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_assignment_version_assignment_version_supersedes_id",
                        column: x => x.supersedes_id,
                        principalTable: "assignment_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_assignment_version_person_person_id",
                        column: x => x.person_id,
                        principalTable: "person",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_assignment_version_work_obligation_obligation_id",
                        column: x => x.obligation_id,
                        principalTable: "work_obligation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_assignment_version_assigned_by",
                table: "assignment_version",
                column: "assigned_by");

            migrationBuilder.CreateIndex(
                name: "IX_assignment_version_obligation_id_status",
                table: "assignment_version",
                columns: new[] { "obligation_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_assignment_version_person_id_status_assigned_at",
                table: "assignment_version",
                columns: new[] { "person_id", "status", "assigned_at" });

            migrationBuilder.CreateIndex(
                name: "UX_assignment_version_current_obligation",
                table: "assignment_version",
                column: "obligation_id",
                unique: true,
                filter: "status = 'VIGENTE'");

            migrationBuilder.CreateIndex(
                name: "UX_assignment_version_supersedes",
                table: "assignment_version",
                column: "supersedes_id",
                unique: true,
                filter: "supersedes_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Rollback is blocked for assignment history.");
        }
    }
}
