using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalCarryover : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "operational_carryover_request",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    IntentKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Cause = table.Column<string>(type: "text", nullable: false),
                    CauseDescription = table.Column<string>(type: "text", nullable: false),
                    CauseVersion = table.Column<int>(type: "integer", nullable: false),
                    AlgorithmVersion = table.Column<int>(type: "integer", nullable: false),
                    Explanation = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    IncidentStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IncidentEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Minutes = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Fingerprint = table.Column<string>(type: "text", nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    ScheduleFingerprint = table.Column<string>(type: "text", nullable: false),
                    DecidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecisionComment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ResultsJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_carryover_request", x => x.Id);
                    table.CheckConstraint("CK_carryover_interval", "\"Minutes\" > 0 AND \"IncidentEnd\" = \"IncidentStart\" + \"Minutes\" * interval '1 minute'");
                    table.CheckConstraint("CK_carryover_state", "\"Revision\" > 0 AND ((\"State\" = 'PENDIENTE' AND \"DecidedBy\" IS NULL AND \"DecidedAt\" IS NULL) OR (\"State\" IN ('APROBADA','RECHAZADA') AND \"DecidedBy\" IS NOT NULL AND \"DecidedAt\" IS NOT NULL AND length(\"DecisionComment\") > 0))");
                    table.ForeignKey(
                        name: "FK_operational_carryover_request_person_PersonId",
                        column: x => x.PersonId,
                        principalTable: "person",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_operational_carryover_request_BranchId_State_SubmittedAt",
                table: "operational_carryover_request",
                columns: new[] { "BranchId", "State", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_carryover_request_IntentKey",
                table: "operational_carryover_request",
                column: "IntentKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_operational_carryover_request_PersonId_IncidentStart_Incide~",
                table: "operational_carryover_request",
                columns: new[] { "PersonId", "IncidentStart", "IncidentEnd" });

            migrationBuilder.Sql("""
                CREATE FUNCTION protect_carryover_history() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'Carryover history cannot be deleted'; END IF;
                  IF OLD."State" <> 'PENDIENTE' OR
                     (to_jsonb(NEW) - ARRAY['State','Revision','DecidedBy','DecidedAt','DecisionComment','ResultsJson']) <>
                     (to_jsonb(OLD) - ARRAY['State','Revision','DecidedBy','DecidedAt','DecisionComment','ResultsJson']) OR
                     NEW."State" NOT IN ('APROBADA','RECHAZADA') OR NEW."Revision" <> OLD."Revision" + 1
                  THEN RAISE EXCEPTION 'Carryover snapshot and terminal decisions are immutable'; END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER carryover_history_immutable BEFORE UPDATE OR DELETE ON operational_carryover_request
                  FOR EACH ROW EXECUTE FUNCTION protect_carryover_history();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "operational_carryover_request");
            migrationBuilder.Sql("DROP FUNCTION protect_carryover_history();");
        }
    }
}
