using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPreparedProcessPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "operational_process_plan_start",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Participants = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_process_plan_start", x => x.Id);
                    table.CheckConstraint("CK_process_plan_start", "\"Status\" IN ('PREPARADO','ACTIVADO') AND \"Revision\" > 0");
                    table.ForeignKey(
                        name: "FK_operational_process_plan_start_direction_prepared_plan_Plan~",
                        column: x => x.PlanId,
                        principalTable: "direction_prepared_plan",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_operational_process_plan_start_operational_sequence_version~",
                        columns: x => new { x.ProcessId, x.VersionId },
                        principalTable: "operational_sequence_version",
                        principalColumns: new[] { "SequenceId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_operational_process_plan_start_PlanId",
                table: "operational_process_plan_start",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_process_plan_start_ProcessId_VersionId",
                table: "operational_process_plan_start",
                columns: new[] { "ProcessId", "VersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_process_plan_start_StartKey",
                table: "operational_process_plan_start",
                column: "StartKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $guard$ BEGIN
                  IF EXISTS (SELECT 1 FROM operational_process_plan_start) THEN
                    RAISE EXCEPTION 'Prepared process plans exist. Restore a verified backup instead of discarding their version and reservations.';
                  END IF;
                END $guard$;
                """);
            migrationBuilder.DropTable(
                name: "operational_process_plan_start");
        }
    }
}
