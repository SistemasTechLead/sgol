using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TransferProgressUnits",
                table: "operational_assignment",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TransferRemainingMinutes",
                table: "operational_assignment",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "operational_transfer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FromPersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ToPersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PreviousStartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PreviousDueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NewDueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProgressUnits = table.Column<int>(type: "integer", nullable: false),
                    RemainingMinutes = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_transfer", x => x.Id);
                    table.CheckConstraint("CK_transfer_values", "\"FromPersonId\" <> \"ToPersonId\" AND \"RemainingMinutes\" BETWEEN 1 AND 4800 AND \"ProgressUnits\" >= 0 AND \"NewDueAt\" > \"At\"");
                    table.ForeignKey(
                        name: "FK_operational_transfer_app_user_ActorId",
                        column: x => x.ActorId,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_operational_transfer_operational_review_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "operational_review",
                        principalColumn: "AssignmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_operational_transfer_person_FromPersonId",
                        column: x => x.FromPersonId,
                        principalTable: "person",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_operational_transfer_person_ToPersonId",
                        column: x => x.ToPersonId,
                        principalTable: "person",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_assignment_transfer_load",
                table: "operational_assignment",
                sql: "\"TransferProgressUnits\" BETWEEN 0 AND \"ProgressUnits\" AND (\"TransferRemainingMinutes\" IS NULL OR \"TransferRemainingMinutes\" BETWEEN 1 AND 4800)");

            migrationBuilder.CreateIndex(
                name: "IX_operational_transfer_ActorId",
                table: "operational_transfer",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_transfer_AssignmentId_At",
                table: "operational_transfer",
                columns: new[] { "AssignmentId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_transfer_FromPersonId_At",
                table: "operational_transfer",
                columns: new[] { "FromPersonId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_transfer_ToPersonId_At",
                table: "operational_transfer",
                columns: new[] { "ToPersonId", "At" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "operational_transfer");

            migrationBuilder.DropCheckConstraint(
                name: "CK_assignment_transfer_load",
                table: "operational_assignment");

            migrationBuilder.DropColumn(
                name: "TransferProgressUnits",
                table: "operational_assignment");

            migrationBuilder.DropColumn(
                name: "TransferRemainingMinutes",
                table: "operational_assignment");
        }
    }
}
