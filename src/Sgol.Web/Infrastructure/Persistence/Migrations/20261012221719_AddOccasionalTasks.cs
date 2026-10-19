using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOccasionalTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_task_period",
                table: "operational_task");

            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_task_requirement",
                table: "operational_task");

            migrationBuilder.DropCheckConstraint(
                name: "CK_direction_plan_period",
                table: "direction_prepared_plan");

            migrationBuilder.AddColumn<Guid>(
                name: "OccasionId",
                table: "operational_assignment",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "operational_occasion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Instructions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    PublicationText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Channels = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_occasion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_operational_occasion_direction_prepared_plan_PlanId",
                        column: x => x.PlanId,
                        principalTable: "direction_prepared_plan",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "operational_reference_file",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OccasionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_reference_file", x => x.Id);
                    table.ForeignKey(
                        name: "FK_operational_reference_file_operational_occasion_OccasionId",
                        column: x => x.OccasionId,
                        principalTable: "operational_occasion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_task_period",
                table: "operational_task",
                sql: "\"PeriodType\" IN ('SEMANAL','MENSUAL','TRIMESTRAL','ANUAL','OCASIONAL')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_task_requirement",
                table: "operational_task",
                sql: "\"Requirement\" IN ('FOTOGRAFIA','CAPTURA_PANTALLA','COMENTARIO','DOCUMENTO','NINGUNO')");

            migrationBuilder.CreateIndex(
                name: "IX_operational_assignment_OccasionId",
                table: "operational_assignment",
                column: "OccasionId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_direction_plan_period",
                table: "direction_prepared_plan",
                sql: "(period_number IS NOT NULL AND ((period_type = 'SEMANAL' AND period_number BETWEEN 1 AND 53) OR (period_type = 'MENSUAL' AND period_number BETWEEN 1 AND 12) OR (period_type = 'TRIMESTRAL' AND period_number BETWEEN 1 AND 4) OR (period_type = 'SEMESTRAL' AND period_number BETWEEN 1 AND 2))) OR (period_type IN ('ANUAL','OCASIONAL') AND period_number IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_operational_occasion_PlanId",
                table: "operational_occasion",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_reference_file_OccasionId_Position",
                table: "operational_reference_file",
                columns: new[] { "OccasionId", "Position" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_operational_assignment_operational_occasion_OccasionId",
                table: "operational_assignment",
                column: "OccasionId",
                principalTable: "operational_occasion",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_operational_assignment_operational_occasion_OccasionId",
                table: "operational_assignment");

            migrationBuilder.DropTable(
                name: "operational_reference_file");

            migrationBuilder.DropTable(
                name: "operational_occasion");

            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_task_period",
                table: "operational_task");

            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_task_requirement",
                table: "operational_task");

            migrationBuilder.DropIndex(
                name: "IX_operational_assignment_OccasionId",
                table: "operational_assignment");

            migrationBuilder.DropCheckConstraint(
                name: "CK_direction_plan_period",
                table: "direction_prepared_plan");

            migrationBuilder.DropColumn(
                name: "OccasionId",
                table: "operational_assignment");

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_task_period",
                table: "operational_task",
                sql: "\"PeriodType\" IN ('SEMANAL','MENSUAL','TRIMESTRAL','ANUAL')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_task_requirement",
                table: "operational_task",
                sql: "\"Requirement\" IN ('FOTOGRAFIA','COMENTARIO','DOCUMENTO','NINGUNO')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_direction_plan_period",
                table: "direction_prepared_plan",
                sql: "(period_number IS NOT NULL AND ((period_type = 'SEMANAL' AND period_number BETWEEN 1 AND 53) OR (period_type = 'MENSUAL' AND period_number BETWEEN 1 AND 12) OR (period_type = 'TRIMESTRAL' AND period_number BETWEEN 1 AND 4) OR (period_type = 'SEMESTRAL' AND period_number BETWEEN 1 AND 2))) OR (period_type = 'ANUAL' AND period_number IS NULL)");
        }
    }
}
