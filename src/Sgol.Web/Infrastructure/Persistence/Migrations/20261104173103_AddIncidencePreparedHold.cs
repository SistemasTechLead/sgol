using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidencePreparedHold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IncidenceHoldId",
                table: "operational_process_plan_start",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN IF EXISTS (SELECT 1 FROM operational_incidence) THEN
                  RAISE EXCEPTION 'Incidence history prevents downgrade'; END IF; END $$;
                """);
            migrationBuilder.DropColumn(
                name: "IncidenceHoldId",
                table: "operational_process_plan_start");
        }
    }
}
