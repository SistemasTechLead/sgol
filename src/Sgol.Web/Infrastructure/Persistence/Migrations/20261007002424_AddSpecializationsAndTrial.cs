using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSpecializationsAndTrial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOnTrial",
                table: "person",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "SpecializationId",
                table: "operational_task",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SpecializationId",
                table: "operational_assignment",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpecializationName",
                table: "operational_assignment",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "specialization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_specialization", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "person_specialization",
                columns: table => new
                {
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpecializationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_specialization", x => new { x.PersonId, x.SpecializationId });
                    table.ForeignKey(
                        name: "FK_person_specialization_person_PersonId",
                        column: x => x.PersonId,
                        principalTable: "person",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_person_specialization_specialization_SpecializationId",
                        column: x => x.SpecializationId,
                        principalTable: "specialization",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0005-7000-8000-000000000005"),
                column: "SpecializationId",
                value: null);

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0007-7000-8000-000000000007"),
                column: "SpecializationId",
                value: null);

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0008-7000-8000-000000000008"),
                column: "SpecializationId",
                value: null);

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0011-7000-8000-000000000011"),
                column: "SpecializationId",
                value: null);

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0018-7000-8000-000000000018"),
                column: "SpecializationId",
                value: null);

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0026-7000-8000-000000000026"),
                column: "SpecializationId",
                value: null);

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0092-7000-8000-000000000092"),
                column: "SpecializationId",
                value: null);

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0093-7000-8000-000000000093"),
                column: "SpecializationId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_operational_task_SpecializationId",
                table: "operational_task",
                column: "SpecializationId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_assignment_SpecializationId",
                table: "operational_assignment",
                column: "SpecializationId");

            migrationBuilder.CreateIndex(
                name: "IX_person_specialization_SpecializationId",
                table: "person_specialization",
                column: "SpecializationId");

            migrationBuilder.CreateIndex(
                name: "IX_specialization_NormalizedName",
                table: "specialization",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_operational_assignment_specialization_SpecializationId",
                table: "operational_assignment",
                column: "SpecializationId",
                principalTable: "specialization",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_operational_task_specialization_SpecializationId",
                table: "operational_task",
                column: "SpecializationId",
                principalTable: "specialization",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_operational_assignment_specialization_SpecializationId",
                table: "operational_assignment");

            migrationBuilder.DropForeignKey(
                name: "FK_operational_task_specialization_SpecializationId",
                table: "operational_task");

            migrationBuilder.DropTable(
                name: "person_specialization");

            migrationBuilder.DropTable(
                name: "specialization");

            migrationBuilder.DropIndex(
                name: "IX_operational_task_SpecializationId",
                table: "operational_task");

            migrationBuilder.DropIndex(
                name: "IX_operational_assignment_SpecializationId",
                table: "operational_assignment");

            migrationBuilder.DropColumn(
                name: "IsOnTrial",
                table: "person");

            migrationBuilder.DropColumn(
                name: "SpecializationId",
                table: "operational_task");

            migrationBuilder.DropColumn(
                name: "SpecializationId",
                table: "operational_assignment");

            migrationBuilder.DropColumn(
                name: "SpecializationName",
                table: "operational_assignment");
        }
    }
}
