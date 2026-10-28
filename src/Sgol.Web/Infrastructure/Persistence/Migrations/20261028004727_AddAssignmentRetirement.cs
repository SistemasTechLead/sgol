using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentRetirement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_operational_assignment_ScheduleKey",
                table: "operational_assignment");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RetiredAt",
                table: "operational_assignment",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_operational_assignment_ScheduleKey",
                table: "operational_assignment",
                column: "ScheduleKey",
                unique: true,
                filter: "\"RetiredAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM operational_assignment WHERE "RetiredAt" IS NOT NULL) THEN
                    RAISE EXCEPTION 'Cannot remove retirement state while retired assignments exist; preserve their history explicitly first.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropIndex(
                name: "IX_operational_assignment_ScheduleKey",
                table: "operational_assignment");

            migrationBuilder.DropColumn(
                name: "RetiredAt",
                table: "operational_assignment");

            migrationBuilder.CreateIndex(
                name: "IX_operational_assignment_ScheduleKey",
                table: "operational_assignment",
                column: "ScheduleKey",
                unique: true);
        }
    }
}
