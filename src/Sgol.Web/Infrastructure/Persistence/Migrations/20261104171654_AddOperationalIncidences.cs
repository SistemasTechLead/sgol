using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalIncidences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_sequence_stage_state",
                table: "operational_sequence_stage");

            migrationBuilder.CreateTable(
                name: "operational_incidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IntentKey = table.Column<Guid>(type: "uuid", nullable: false),
                    IntentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ReceiptJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_incidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_operational_incidence_person_PersonId",
                        column: x => x.PersonId,
                        principalTable: "person",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "operational_incidence_disposition",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IncidenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageId = table.Column<Guid>(type: "uuid", nullable: true),
                    ObligationId = table.Column<Guid>(type: "uuid", nullable: true),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvaluationDay = table.Column<DateOnly>(type: "date", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OverloadAuthorized = table.Column<bool>(type: "boolean", nullable: false),
                    BeforeJson = table.Column<string>(type: "jsonb", nullable: false),
                    AfterJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_incidence_disposition", x => x.Id);
                    table.ForeignKey(
                        name: "FK_operational_incidence_disposition_operational_incidence_Inc~",
                        column: x => x.IncidenceId,
                        principalTable: "operational_incidence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_sequence_stage_state",
                table: "operational_sequence_stage",
                sql: "\"Status\" IN ('EN_ESPERA','PROGRAMADA','DISPONIBLE','PENDIENTE_ASIGNACION','FUERA_DE_PLAZO','CONCLUIDA','CANCELADA','OMITIDA','PENDIENTE_REPROGRAMACION') AND (\"Status\" NOT IN ('DISPONIBLE','CONCLUIDA') OR (\"CurrentAttemptId\" IS NOT NULL AND \"EnabledAt\" IS NOT NULL))");

            migrationBuilder.CreateIndex(
                name: "IX_operational_incidence_ActorId_IntentKey",
                table: "operational_incidence",
                columns: new[] { "ActorId", "IntentKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_operational_incidence_PersonId_Day",
                table: "operational_incidence",
                columns: new[] { "PersonId", "Day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_operational_incidence_disposition_IncidenceId_AssignmentId",
                table: "operational_incidence_disposition",
                columns: new[] { "IncidenceId", "AssignmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_operational_incidence_disposition_ObligationId",
                table: "operational_incidence_disposition",
                column: "ObligationId");
            migrationBuilder.Sql("""
                CREATE FUNCTION sgol_guard_incidence_assignment() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF NEW."RetiredAt" IS NOT NULL THEN RETURN NEW; END IF;
                  IF TG_OP = 'UPDATE' AND NEW."PersonId" = OLD."PersonId" AND NEW."StartsAt" = OLD."StartsAt" AND NEW."DueAt" = OLD."DueAt" THEN RETURN NEW; END IF;
                  PERFORM pg_advisory_xact_lock(734651001);
                  IF EXISTS (SELECT 1 FROM operational_incidence i WHERE i."PersonId" = NEW."PersonId"
                    AND i."Day" = (NEW."StartsAt" AT TIME ZONE 'America/Mexico_City')::date) THEN
                    RAISE EXCEPTION 'Assignment % conflicts with a confirmed absence; review recipient/date', NEW."Id" USING ERRCODE = '23514', CONSTRAINT = 'CK_incidence_assignment_absence';
                  END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER trg_incidence_assignment BEFORE INSERT OR UPDATE ON operational_assignment
                  FOR EACH ROW EXECUTE FUNCTION sgol_guard_incidence_assignment();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN IF EXISTS (SELECT 1 FROM operational_incidence) THEN
                  RAISE EXCEPTION 'Incidence history prevents downgrade'; END IF; END $$;
                DROP TRIGGER trg_incidence_assignment ON operational_assignment;
                DROP FUNCTION sgol_guard_incidence_assignment();
                """);
            migrationBuilder.DropTable(
                name: "operational_incidence_disposition");

            migrationBuilder.DropTable(
                name: "operational_incidence");

            migrationBuilder.DropCheckConstraint(
                name: "CK_sequence_stage_state",
                table: "operational_sequence_stage");

            migrationBuilder.AddCheckConstraint(
                name: "CK_sequence_stage_state",
                table: "operational_sequence_stage",
                sql: "\"Status\" IN ('EN_ESPERA','PROGRAMADA','DISPONIBLE','PENDIENTE_ASIGNACION','FUERA_DE_PLAZO','CONCLUIDA','CANCELADA','OMITIDA') AND (\"Status\" NOT IN ('DISPONIBLE','CONCLUIDA') OR (\"CurrentAttemptId\" IS NOT NULL AND \"EnabledAt\" IS NOT NULL))");
        }
    }
}
