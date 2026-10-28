using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OperationalCriticality",
                table: "operational_task",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClosureMarginPercent",
                table: "operational_assignment",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NominalStartsAt",
                table: "operational_assignment",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperationalCriticality",
                table: "operational_assignment",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SchedulingPolicyId",
                table: "operational_assignment",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SchedulingPolicyId",
                table: "direction_prepared_plan",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "operational_scheduling_policy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    MarginPercent = table.Column<int>(type: "integer", nullable: false),
                    FormulaVersion = table.Column<int>(type: "integer", nullable: false),
                    AgendaVersion = table.Column<int>(type: "integer", nullable: false),
                    ChangedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_scheduling_policy", x => x.Id);
                    table.CheckConstraint("CK_scheduling_policy", "\"MarginPercent\" BETWEEN 0 AND 500 AND \"Revision\" > 0 AND \"FormulaVersion\" = 1 AND \"AgendaVersion\" = 1");
                });

            migrationBuilder.CreateTable(
                name: "operational_scheduling_settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    CurrentPolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_scheduling_settings", x => x.Id);
                    table.CheckConstraint("CK_scheduling_settings", "\"Id\" = 1 AND \"Revision\" > 0");
                    table.ForeignKey(
                        name: "FK_operational_scheduling_settings_operational_scheduling_poli~",
                        column: x => x.CurrentPolicyId,
                        principalTable: "operational_scheduling_policy",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "operational_scheduling_policy",
                columns: new[] { "Id", "AgendaVersion", "ChangedAt", "ChangedBy", "FormulaVersion", "MarginPercent", "Revision" },
                values: new object[] { new Guid("d2a5d1af-5886-4b9c-bfb1-590399c8f734"), 1, new DateTimeOffset(new DateTime(2026, 10, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1, 50, 1L });

            migrationBuilder.InsertData(
                table: "operational_scheduling_settings",
                columns: new[] { "Id", "CurrentPolicyId", "Revision" },
                values: new object[] { 1, new Guid("d2a5d1af-5886-4b9c-bfb1-590399c8f734"), 1L });

            migrationBuilder.AddCheckConstraint(
                name: "CK_task_criticality",
                table: "operational_task",
                sql: "\"OperationalCriticality\" IS NULL OR \"OperationalCriticality\" IN ('CRITICA','IMPORTANTE','FLEXIBLE')");

            migrationBuilder.CreateIndex(
                name: "IX_operational_assignment_PersonId_StartsAt_DueAt",
                table: "operational_assignment",
                columns: new[] { "PersonId", "StartsAt", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_assignment_SchedulingPolicyId",
                table: "operational_assignment",
                column: "SchedulingPolicyId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_assignment_scheduling",
                table: "operational_assignment",
                sql: "(\"SchedulingPolicyId\" IS NULL AND \"ClosureMarginPercent\" IS NULL AND \"NominalStartsAt\" IS NULL) OR (\"SchedulingPolicyId\" IS NOT NULL AND \"ClosureMarginPercent\" IS NOT NULL AND \"ClosureMarginPercent\" BETWEEN 0 AND 500 AND \"NominalStartsAt\" IS NOT NULL AND \"StartsAt\" >= \"NominalStartsAt\" AND \"OperationalCriticality\" IS NOT NULL AND \"OperationalCriticality\" IN ('CRITICA','IMPORTANTE','FLEXIBLE'))");

            migrationBuilder.CreateIndex(
                name: "IX_direction_prepared_plan_SchedulingPolicyId",
                table: "direction_prepared_plan",
                column: "SchedulingPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_scheduling_policy_Revision",
                table: "operational_scheduling_policy",
                column: "Revision",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_operational_scheduling_settings_CurrentPolicyId",
                table: "operational_scheduling_settings",
                column: "CurrentPolicyId");

            migrationBuilder.AddForeignKey(
                name: "FK_direction_prepared_plan_operational_scheduling_policy_Sched~",
                table: "direction_prepared_plan",
                column: "SchedulingPolicyId",
                principalTable: "operational_scheduling_policy",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_operational_assignment_operational_scheduling_policy_Schedu~",
                table: "operational_assignment",
                column: "SchedulingPolicyId",
                principalTable: "operational_scheduling_policy",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DO $ BEGIN IF EXISTS (SELECT 1 FROM operational_assignment WHERE \"SchedulingPolicyId\" IS NOT NULL) THEN RAISE EXCEPTION 'Automatic assignments must retain their scheduling policy'; END IF; END $;");
            migrationBuilder.DropForeignKey(
                name: "FK_direction_prepared_plan_operational_scheduling_policy_Sched~",
                table: "direction_prepared_plan");

            migrationBuilder.DropForeignKey(
                name: "FK_operational_assignment_operational_scheduling_policy_Schedu~",
                table: "operational_assignment");

            migrationBuilder.DropTable(
                name: "operational_scheduling_settings");

            migrationBuilder.DropTable(
                name: "operational_scheduling_policy");

            migrationBuilder.DropCheckConstraint(
                name: "CK_task_criticality",
                table: "operational_task");

            migrationBuilder.DropIndex(
                name: "IX_operational_assignment_PersonId_StartsAt_DueAt",
                table: "operational_assignment");

            migrationBuilder.DropIndex(
                name: "IX_operational_assignment_SchedulingPolicyId",
                table: "operational_assignment");

            migrationBuilder.DropCheckConstraint(
                name: "CK_assignment_scheduling",
                table: "operational_assignment");

            migrationBuilder.DropIndex(
                name: "IX_direction_prepared_plan_SchedulingPolicyId",
                table: "direction_prepared_plan");

            migrationBuilder.DropColumn(
                name: "OperationalCriticality",
                table: "operational_task");

            migrationBuilder.DropColumn(
                name: "ClosureMarginPercent",
                table: "operational_assignment");

            migrationBuilder.DropColumn(
                name: "NominalStartsAt",
                table: "operational_assignment");

            migrationBuilder.DropColumn(
                name: "OperationalCriticality",
                table: "operational_assignment");

            migrationBuilder.DropColumn(
                name: "SchedulingPolicyId",
                table: "operational_assignment");

            migrationBuilder.DropColumn(
                name: "SchedulingPolicyId",
                table: "direction_prepared_plan");
        }
    }
}
