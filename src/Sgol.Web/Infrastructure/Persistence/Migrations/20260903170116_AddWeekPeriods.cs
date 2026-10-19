using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWeekPeriods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "week_period",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    iso_year = table.Column<int>(type: "integer", nullable: false),
                    iso_week = table.Column<int>(type: "integer", nullable: false),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: false),
                    derived_status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_week_period", x => x.id);
                    table.CheckConstraint("CK_week_period_dates", "ends_on = starts_on + 6 AND EXTRACT(ISODOW FROM starts_on) = 1");
                    table.CheckConstraint("CK_week_period_derived_status", "derived_status IN ('VIGENTE', 'TRANSCURRIDA')");
                    table.CheckConstraint("CK_week_period_iso_week", "iso_week BETWEEN 1 AND 53");
                    table.CheckConstraint("CK_week_period_iso_year", "iso_year BETWEEN 1 AND 9999");
                    table.ForeignKey(
                        name: "FK_week_period_branch_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_week_period_branch_iso_week",
                table: "week_period",
                columns: new[] { "branch_id", "iso_year", "iso_week" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Rollback is blocked; apply a forward corrective migration.");
        }
    }
}
