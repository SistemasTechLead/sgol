using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_sequence_stage_state",
                table: "operational_sequence_stage");

            migrationBuilder.AddColumn<bool>(
                name: "IsFinalAudit",
                table: "operational_sequence_step",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "OutputItemId",
                table: "operational_sequence_step",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OutputTargetTaskId",
                table: "operational_sequence_step",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CalculatedLoadMinutes",
                table: "operational_sequence_stage",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstimatedLoadMinutes",
                table: "operational_sequence_stage",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFinalAudit",
                table: "operational_sequence_stage",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "LoadSourceAttemptId",
                table: "operational_sequence_stage",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OmittedByAttemptId",
                table: "operational_sequence_stage",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OutputItemMinutes",
                table: "operational_sequence_stage",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutputItemName",
                table: "operational_sequence_stage",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OutputItemRateId",
                table: "operational_sequence_stage",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CapacityExceptionJson",
                table: "operational_sequence_attempt",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CapacityResolution",
                table: "operational_sequence_attempt",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContinuationAttemptId",
                table: "operational_sequence_attempt",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ItemQuantity",
                table: "operational_sequence_attempt",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OmittedStagesJson",
                table: "operational_sequence_attempt",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItemRatesJson",
                table: "operational_process_plan_start",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ItemLoadSourceAttemptId",
                table: "operational_assignment",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "process_item",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_process_item", x => x.Id);
                    table.CheckConstraint("CK_process_item", "length(trim(\"Name\")) > 0 AND \"Revision\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "process_item_rate",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Minutes = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_process_item_rate", x => x.Id);
                    table.CheckConstraint("CK_process_item_rate", "\"Minutes\" > 0 AND \"Revision\" > 0");
                    table.ForeignKey(
                        name: "FK_process_item_rate_app_user_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_process_item_rate_process_item_ItemId",
                        column: x => x.ItemId,
                        principalTable: "process_item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_step_OutputItemId",
                table: "operational_sequence_step",
                column: "OutputItemId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_stage_OutputItemRateId",
                table: "operational_sequence_stage",
                column: "OutputItemRateId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_stage_RunId_LoadSourceAttemptId",
                table: "operational_sequence_stage",
                columns: new[] { "RunId", "LoadSourceAttemptId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_sequence_stage_state",
                table: "operational_sequence_stage",
                sql: "\"Status\" IN ('EN_ESPERA','PROGRAMADA','DISPONIBLE','PENDIENTE_ASIGNACION','FUERA_DE_PLAZO','CONCLUIDA','CANCELADA','OMITIDA') AND (\"Status\" NOT IN ('DISPONIBLE','CONCLUIDA') OR (\"CurrentAttemptId\" IS NOT NULL AND \"EnabledAt\" IS NOT NULL))");

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_attempt_RunId_StageId_ContinuationAtte~",
                table: "operational_sequence_attempt",
                columns: new[] { "RunId", "StageId", "ContinuationAttemptId" });

            migrationBuilder.CreateIndex(
                name: "IX_process_item_rate_CreatedBy",
                table: "process_item_rate",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_process_item_rate_ItemId_Revision",
                table: "process_item_rate",
                columns: new[] { "ItemId", "Revision" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_operational_sequence_attempt_operational_sequence_attempt_R~",
                table: "operational_sequence_attempt",
                columns: new[] { "RunId", "StageId", "ContinuationAttemptId" },
                principalTable: "operational_sequence_attempt",
                principalColumns: new[] { "RunId", "StageId", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_operational_sequence_stage_operational_sequence_attempt_Run~",
                table: "operational_sequence_stage",
                columns: new[] { "RunId", "LoadSourceAttemptId" },
                principalTable: "operational_sequence_attempt",
                principalColumns: new[] { "RunId", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_operational_sequence_stage_process_item_rate_OutputItemRate~",
                table: "operational_sequence_stage",
                column: "OutputItemRateId",
                principalTable: "process_item_rate",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_operational_sequence_step_process_item_OutputItemId",
                table: "operational_sequence_step",
                column: "OutputItemId",
                principalTable: "process_item",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_operational_sequence_attempt_operational_sequence_attempt_R~",
                table: "operational_sequence_attempt");

            migrationBuilder.DropForeignKey(
                name: "FK_operational_sequence_stage_operational_sequence_attempt_Run~",
                table: "operational_sequence_stage");

            migrationBuilder.DropForeignKey(
                name: "FK_operational_sequence_stage_process_item_rate_OutputItemRate~",
                table: "operational_sequence_stage");

            migrationBuilder.DropForeignKey(
                name: "FK_operational_sequence_step_process_item_OutputItemId",
                table: "operational_sequence_step");

            migrationBuilder.DropTable(
                name: "process_item_rate");

            migrationBuilder.DropTable(
                name: "process_item");

            migrationBuilder.DropIndex(
                name: "IX_operational_sequence_step_OutputItemId",
                table: "operational_sequence_step");

            migrationBuilder.DropIndex(
                name: "IX_operational_sequence_stage_OutputItemRateId",
                table: "operational_sequence_stage");

            migrationBuilder.DropIndex(
                name: "IX_operational_sequence_stage_RunId_LoadSourceAttemptId",
                table: "operational_sequence_stage");

            migrationBuilder.DropCheckConstraint(
                name: "CK_sequence_stage_state",
                table: "operational_sequence_stage");

            migrationBuilder.DropIndex(
                name: "IX_operational_sequence_attempt_RunId_StageId_ContinuationAtte~",
                table: "operational_sequence_attempt");

            migrationBuilder.DropColumn(
                name: "IsFinalAudit",
                table: "operational_sequence_step");

            migrationBuilder.DropColumn(
                name: "OutputItemId",
                table: "operational_sequence_step");

            migrationBuilder.DropColumn(
                name: "OutputTargetTaskId",
                table: "operational_sequence_step");

            migrationBuilder.DropColumn(
                name: "CalculatedLoadMinutes",
                table: "operational_sequence_stage");

            migrationBuilder.DropColumn(
                name: "EstimatedLoadMinutes",
                table: "operational_sequence_stage");

            migrationBuilder.DropColumn(
                name: "IsFinalAudit",
                table: "operational_sequence_stage");

            migrationBuilder.DropColumn(
                name: "LoadSourceAttemptId",
                table: "operational_sequence_stage");

            migrationBuilder.DropColumn(
                name: "OmittedByAttemptId",
                table: "operational_sequence_stage");

            migrationBuilder.DropColumn(
                name: "OutputItemMinutes",
                table: "operational_sequence_stage");

            migrationBuilder.DropColumn(
                name: "OutputItemName",
                table: "operational_sequence_stage");

            migrationBuilder.DropColumn(
                name: "OutputItemRateId",
                table: "operational_sequence_stage");

            migrationBuilder.DropColumn(
                name: "CapacityExceptionJson",
                table: "operational_sequence_attempt");

            migrationBuilder.DropColumn(
                name: "CapacityResolution",
                table: "operational_sequence_attempt");

            migrationBuilder.DropColumn(
                name: "ContinuationAttemptId",
                table: "operational_sequence_attempt");

            migrationBuilder.DropColumn(
                name: "ItemQuantity",
                table: "operational_sequence_attempt");

            migrationBuilder.DropColumn(
                name: "OmittedStagesJson",
                table: "operational_sequence_attempt");

            migrationBuilder.DropColumn(
                name: "ItemRatesJson",
                table: "operational_process_plan_start");

            migrationBuilder.DropColumn(
                name: "ItemLoadSourceAttemptId",
                table: "operational_assignment");

            migrationBuilder.AddCheckConstraint(
                name: "CK_sequence_stage_state",
                table: "operational_sequence_stage",
                sql: "\"Status\" IN ('EN_ESPERA','PROGRAMADA','DISPONIBLE','PENDIENTE_ASIGNACION','FUERA_DE_PLAZO','CONCLUIDA','CANCELADA') AND (\"Status\" NOT IN ('DISPONIBLE','CONCLUIDA') OR (\"CurrentAttemptId\" IS NOT NULL AND \"EnabledAt\" IS NOT NULL))");
        }
    }
}
