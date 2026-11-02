using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskBonusAvailabilityHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "task_bonus_availability_fact",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Available = table.Column<bool>(type: "boolean", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_bonus_availability_fact", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_task_bonus_availability_fact_PersonId_Day_RecordedAt",
                table: "task_bonus_availability_fact",
                columns: new[] { "PersonId", "Day", "RecordedAt" });
            migrationBuilder.Sql("""
                INSERT INTO task_bonus_availability_fact ("Id", "PersonId", "Day", "Available", "RecordedAt")
                SELECT id, person_id, local_date, is_available, CURRENT_TIMESTAMP
                FROM availability_day_version WHERE status='VIGENTE' ON CONFLICT ("Id") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "task_bonus_availability_fact");
        }
    }
}
