using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "work_plan",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_plan", x => x.id);
                    table.CheckConstraint("CK_work_plan_row_version", "row_version >= 1");
                    table.CheckConstraint("CK_work_plan_status", "status IN ('BORRADOR','PUBLICADO')");
                    table.ForeignKey(
                        name: "FK_work_plan_branch_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_plan_week_period_period_id",
                        column: x => x.period_id,
                        principalTable: "week_period",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_work_plan_period_id",
                table: "work_plan",
                column: "period_id");

            migrationBuilder.CreateIndex(
                name: "UX_work_plan_branch_period",
                table: "work_plan",
                columns: new[] { "branch_id", "period_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("HU-020 migrations are forward-only; rollback is blocked.");
        }
    }
}
