using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalSupervision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Conclusion",
                table: "operational_assignment",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PeriodType",
                table: "operational_assignment",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "SEMANAL");

            migrationBuilder.AddColumn<Guid>(
                name: "ReassignedFromId",
                table: "operational_assignment",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Repetitions",
                table: "operational_assignment",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "ReviewComment",
                table: "operational_assignment",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewStatus",
                table: "operational_assignment",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewedAt",
                table: "operational_assignment",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedBy",
                table: "operational_assignment",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Revision",
                table: "operational_assignment",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.Sql("""
                UPDATE operational_assignment AS a
                SET "PeriodType" = t."PeriodType", "Repetitions" = t."Repetitions",
                    "AssignedRole" = COALESCE(a."AssignedRole", t."AssignedRole"),
                    "ReviewStatus" = CASE WHEN a."CompletedAt" IS NOT NULL THEN 'PENDIENTE_REVISION' ELSE NULL END
                FROM operational_task AS t WHERE a."TaskId" = t."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_operational_assignment_ReassignedFromId",
                table: "operational_assignment",
                column: "ReassignedFromId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_operational_assignment_operational_assignment_ReassignedFro~",
                table: "operational_assignment",
                column: "ReassignedFromId",
                principalTable: "operational_assignment",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_operational_assignment_operational_assignment_ReassignedFro~",
                table: "operational_assignment");

            migrationBuilder.DropIndex(
                name: "IX_operational_assignment_ReassignedFromId",
                table: "operational_assignment");

            migrationBuilder.DropColumn(
                name: "Conclusion",
                table: "operational_assignment");

            migrationBuilder.DropColumn(
                name: "PeriodType",
                table: "operational_assignment");

            migrationBuilder.DropColumn(
                name: "ReassignedFromId",
                table: "operational_assignment");

            migrationBuilder.DropColumn(
                name: "Repetitions",
                table: "operational_assignment");

            migrationBuilder.DropColumn(
                name: "ReviewComment",
                table: "operational_assignment");

            migrationBuilder.DropColumn(
                name: "ReviewStatus",
                table: "operational_assignment");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "operational_assignment");

            migrationBuilder.DropColumn(
                name: "ReviewedBy",
                table: "operational_assignment");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "operational_assignment");
        }
    }
}
