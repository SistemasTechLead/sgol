using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectionPreparedPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "direction_prepared_plan",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    name = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    area = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    period_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    period_number = table.Column<int>(type: "integer", nullable: true),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_direction_prepared_plan", x => x.id);
                    table.CheckConstraint("CK_direction_plan_period", "(period_number IS NOT NULL AND ((period_type = 'SEMANAL' AND period_number BETWEEN 1 AND 53) OR (period_type = 'MENSUAL' AND period_number BETWEEN 1 AND 12) OR (period_type = 'TRIMESTRAL' AND period_number BETWEEN 1 AND 4) OR (period_type = 'SEMESTRAL' AND period_number BETWEEN 1 AND 2))) OR (period_type = 'ANUAL' AND period_number IS NULL)");
                    table.CheckConstraint("CK_direction_plan_range", "ends_on >= starts_on AND year BETWEEN 1 AND 9999");
                    table.CheckConstraint("CK_direction_plan_status", "status = 'BORRADOR'");
                    table.ForeignKey(
                        name: "FK_direction_prepared_plan_branch_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "direction_prepared_plan_item",
                columns: table => new
                {
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_direction_prepared_plan_item", x => new { x.plan_id, x.task_code });
                    table.CheckConstraint("CK_direction_plan_item_position", "position BETWEEN 0 AND 7");
                    table.ForeignKey(
                        name: "FK_direction_prepared_plan_item_direction_prepared_plan_plan_id",
                        column: x => x.plan_id,
                        principalTable: "direction_prepared_plan",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_direction_prepared_plan_branch_id",
                table: "direction_prepared_plan",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_direction_prepared_plan_created_at_id",
                table: "direction_prepared_plan",
                columns: new[] { "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_direction_prepared_plan_item_plan_id_position",
                table: "direction_prepared_plan_item",
                columns: new[] { "plan_id", "position" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new InvalidOperationException("La reversión de migraciones está bloqueada en el MVP.");
        }
    }
}
