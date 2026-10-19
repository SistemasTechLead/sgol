using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGenerationRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "generation_request",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    idempotency_key = table.Column<Guid>(type: "uuid", nullable: false),
                    request_hash = table.Column<string>(type: "character(64)", nullable: false),
                    rule_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origin_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    origin_reference = table.Column<string>(type: "text", nullable: false),
                    result = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    obligation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    error_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_generation_request", x => x.id);
                    table.CheckConstraint("CK_generation_request_hu014_no_obligation", "obligation_id IS NULL");
                    table.CheckConstraint("CK_generation_request_origin", "btrim(origin_type) <> '' AND btrim(origin_reference) <> ''");
                    table.CheckConstraint("CK_generation_request_result", "result IN ('ACEPTADA','RECUPERADA','RECHAZADA')");
                    table.ForeignKey(
                        name: "FK_generation_request_activation_rule_version_rule_version_id",
                        column: x => x.rule_version_id,
                        principalTable: "activation_rule_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_generation_request_app_user_requested_by",
                        column: x => x.requested_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_generation_request_branch_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_generation_request_week_period_period_id",
                        column: x => x.period_id,
                        principalTable: "week_period",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_generation_request_branch_id",
                table: "generation_request",
                column: "branch_id");
            migrationBuilder.CreateIndex(
                name: "IX_generation_request_period_id",
                table: "generation_request",
                column: "period_id");
            migrationBuilder.CreateIndex(
                name: "IX_generation_request_requested_by",
                table: "generation_request",
                column: "requested_by");
            migrationBuilder.CreateIndex(
                name: "UX_generation_request_functional_key",
                table: "generation_request",
                columns: new[] { "rule_version_id", "branch_id", "period_id", "origin_type", "origin_reference" },
                unique: true);
            migrationBuilder.CreateIndex(
                name: "UX_generation_request_idempotency_key",
                table: "generation_request",
                column: "idempotency_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("HU-014 migrations are forward-only; rollback is blocked.");
        }
    }
}
