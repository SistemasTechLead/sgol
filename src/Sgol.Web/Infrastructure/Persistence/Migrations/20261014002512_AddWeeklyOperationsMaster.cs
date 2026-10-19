using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWeeklyOperationsMaster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_direction_prepared_plan_branch_id",
                table: "direction_prepared_plan");

            migrationBuilder.AddColumn<bool>(
                name: "IsWeeklyOperationsTask",
                table: "operational_task",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_weekly_operations_plan",
                table: "direction_prepared_plan",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_direction_prepared_plan_branch_id_year_period_number",
                table: "direction_prepared_plan",
                columns: new[] { "branch_id", "year", "period_number" },
                unique: true,
                filter: "is_weekly_operations_plan");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_direction_prepared_plan_branch_id_year_period_number",
                table: "direction_prepared_plan");

            migrationBuilder.DropColumn(
                name: "IsWeeklyOperationsTask",
                table: "operational_task");

            migrationBuilder.DropColumn(
                name: "is_weekly_operations_plan",
                table: "direction_prepared_plan");

            migrationBuilder.CreateIndex(
                name: "IX_direction_prepared_plan_branch_id",
                table: "direction_prepared_plan",
                column: "branch_id");
        }
    }
}

