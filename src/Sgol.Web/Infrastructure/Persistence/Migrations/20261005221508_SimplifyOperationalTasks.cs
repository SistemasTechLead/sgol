using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyOperationalTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_direction_plan_item_position",
                table: "direction_prepared_plan_item");

            migrationBuilder.DropCheckConstraint(
                name: "CK_direction_plan_status",
                table: "direction_prepared_plan");

            migrationBuilder.CreateTable(
                name: "operational_task",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Requirement = table.Column<string>(type: "text", nullable: false),
                    PeriodType = table.Column<string>(type: "text", nullable: false),
                    Repetitions = table.Column<int>(type: "integer", nullable: false),
                    DeadlineHours = table.Column<int>(type: "integer", nullable: false),
                    LoadMinutes = table.Column<int>(type: "integer", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_task", x => x.Id);
                    table.CheckConstraint("CK_operational_task_parameters", "\"Repetitions\" BETWEEN 1 AND 100 AND \"DeadlineHours\" BETWEEN 1 AND 8760 AND \"LoadMinutes\" BETWEEN 1 AND 4800");
                    table.CheckConstraint("CK_operational_task_period", "\"PeriodType\" IN ('SEMANAL','MENSUAL','TRIMESTRAL','ANUAL')");
                    table.CheckConstraint("CK_operational_task_requirement", "\"Requirement\" IN ('FOTOGRAFIA','COMENTARIO','DOCUMENTO','NINGUNO')");
                });

            migrationBuilder.CreateTable(
                name: "operational_assignment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskCode = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Requirement = table.Column<string>(type: "text", nullable: false),
                    LoadMinutes = table.Column<int>(type: "integer", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonName = table.Column<string>(type: "text", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Comment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    FileName = table.Column<string>(type: "text", nullable: true),
                    ContentType = table.Column<string>(type: "text", nullable: true),
                    FileContent = table.Column<byte[]>(type: "bytea", nullable: true),
                    PreviousLoadMinutes = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_assignment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_operational_assignment_direction_prepared_plan_PlanId",
                        column: x => x.PlanId,
                        principalTable: "direction_prepared_plan",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_operational_assignment_operational_task_TaskId",
                        column: x => x.TaskId,
                        principalTable: "operational_task",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_operational_assignment_person_PersonId",
                        column: x => x.PersonId,
                        principalTable: "person",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "operational_task",
                columns: new[] { "Id", "Active", "DeadlineHours", "Deleted", "LoadMinutes", "Name", "PeriodType", "Repetitions", "Requirement", "Revision", "TaskCode" },
                values: new object[,]
                {
                    { new Guid("019d3a10-0005-7000-8000-000000000005"), true, 24, false, 30, "Monitorear el avance de ventas y activar acciones correctivas", "SEMANAL", 1, "COMENTARIO", 1L, "TAR-0005" },
                    { new Guid("019d3a10-0007-7000-8000-000000000007"), true, 24, false, 30, "Liberar la mercancía al vencer un Separado de Cortesía", "SEMANAL", 1, "COMENTARIO", 1L, "TAR-0007" },
                    { new Guid("019d3a10-0008-7000-8000-000000000008"), true, 24, false, 30, "Resolver una controversia de asignación de venta con base en evidencia", "SEMANAL", 1, "COMENTARIO", 1L, "TAR-0008" },
                    { new Guid("019d3a10-0011-7000-8000-000000000011"), true, 24, false, 30, "Gestionar la reparación o el cambio de una garantía autorizada después de 90 días", "SEMANAL", 1, "COMENTARIO", 1L, "TAR-0011" },
                    { new Guid("019d3a10-0018-7000-8000-000000000018"), true, 24, false, 60, "Montar o actualizar exhibiciones conforme al planograma y la zonificación", "SEMANAL", 1, "FOTOGRAFIA", 1L, "TAR-0018" },
                    { new Guid("019d3a10-0026-7000-8000-000000000026"), true, 24, false, 30, "Programar y realizar el pago de servicios básicos", "MENSUAL", 1, "DOCUMENTO", 1L, "TAR-0026" },
                    { new Guid("019d3a10-0092-7000-8000-000000000092"), true, 24, false, 30, "Coordinar la recepción de mercancía contra la nota de envío", "SEMANAL", 1, "COMENTARIO", 1L, "TAR-0092" },
                    { new Guid("019d3a10-0093-7000-8000-000000000093"), true, 24, false, 30, "Documentar y notificar incidencia de recepción de mercancía", "SEMANAL", 1, "COMENTARIO", 1L, "TAR-0093" }
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_direction_plan_item_position",
                table: "direction_prepared_plan_item",
                sql: "position BETWEEN 0 AND 999");

            migrationBuilder.AddCheckConstraint(
                name: "CK_direction_plan_status",
                table: "direction_prepared_plan",
                sql: "status IN ('BORRADOR','ASIGNADO')");

            migrationBuilder.CreateIndex(
                name: "IX_operational_assignment_PersonId_CompletedAt",
                table: "operational_assignment",
                columns: new[] { "PersonId", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_assignment_PlanId",
                table: "operational_assignment",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_assignment_TaskId",
                table: "operational_assignment",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_task_TaskCode",
                table: "operational_task",
                column: "TaskCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "operational_assignment");

            migrationBuilder.DropTable(
                name: "operational_task");

            migrationBuilder.DropCheckConstraint(
                name: "CK_direction_plan_item_position",
                table: "direction_prepared_plan_item");

            migrationBuilder.DropCheckConstraint(
                name: "CK_direction_plan_status",
                table: "direction_prepared_plan");

            migrationBuilder.AddCheckConstraint(
                name: "CK_direction_plan_item_position",
                table: "direction_prepared_plan_item",
                sql: "position BETWEEN 0 AND 7");

            migrationBuilder.AddCheckConstraint(
                name: "CK_direction_plan_status",
                table: "direction_prepared_plan",
                sql: "status = 'BORRADOR'");
        }
    }
}
