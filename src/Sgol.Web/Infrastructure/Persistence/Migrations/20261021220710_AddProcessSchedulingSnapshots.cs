using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessSchedulingSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AgendaVersion",
                table: "operational_sequence_version",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClosureMarginPercent",
                table: "operational_sequence_stage",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NominalStartsAt",
                table: "operational_sequence_stage",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperationalCriticality",
                table: "operational_sequence_stage",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SchedulingPolicyId",
                table: "operational_sequence_stage",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgendaJson",
                table: "operational_process_plan_start",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_stage_SchedulingPolicyId",
                table: "operational_sequence_stage",
                column: "SchedulingPolicyId");

            migrationBuilder.AddForeignKey(
                name: "FK_operational_sequence_stage_operational_scheduling_policy_Sc~",
                table: "operational_sequence_stage",
                column: "SchedulingPolicyId",
                principalTable: "operational_scheduling_policy",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DO $ BEGIN IF EXISTS (SELECT 1 FROM operational_sequence_version WHERE \"AgendaVersion\" IS NOT NULL) OR EXISTS (SELECT 1 FROM operational_process_plan_start WHERE \"AgendaJson\" IS NOT NULL) THEN RAISE EXCEPTION 'Published process scheduling contracts must be retained'; END IF; END $;");
            migrationBuilder.DropForeignKey(
                name: "FK_operational_sequence_stage_operational_scheduling_policy_Sc~",
                table: "operational_sequence_stage");

            migrationBuilder.DropIndex(
                name: "IX_operational_sequence_stage_SchedulingPolicyId",
                table: "operational_sequence_stage");

            migrationBuilder.DropColumn(
                name: "AgendaVersion",
                table: "operational_sequence_version");

            migrationBuilder.DropColumn(
                name: "ClosureMarginPercent",
                table: "operational_sequence_stage");

            migrationBuilder.DropColumn(
                name: "NominalStartsAt",
                table: "operational_sequence_stage");

            migrationBuilder.DropColumn(
                name: "OperationalCriticality",
                table: "operational_sequence_stage");

            migrationBuilder.DropColumn(
                name: "SchedulingPolicyId",
                table: "operational_sequence_stage");

            migrationBuilder.DropColumn(
                name: "AgendaJson",
                table: "operational_process_plan_start");
        }
    }
}
