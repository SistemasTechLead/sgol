using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Sgol.Web.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SgolDbContext))]
[Migration("20261103220000_AddCarryoverItemResolution")]
public sealed class AddCarryoverItemResolution : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION protect_carryover_history() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
              IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'Carryover history cannot be deleted'; END IF;
              IF OLD."State" <> 'PENDIENTE' OR
                (to_jsonb(NEW) - ARRAY['State','Revision','DecidedBy','DecidedAt','DecisionComment','ResultsJson']) <>
                (to_jsonb(OLD) - ARRAY['State','Revision','DecidedBy','DecidedAt','DecisionComment','ResultsJson']) OR
                NEW."Revision" <> OLD."Revision" + 1
              THEN RAISE EXCEPTION 'Carryover snapshot and terminal decisions are immutable'; END IF;
              IF NEW."State" IN ('PENDIENTE','RESUELTA') THEN
                IF NEW."Kind" <> 'POSPONER' OR jsonb_array_length(NEW."ResultsJson") <> jsonb_array_length(OLD."ResultsJson") + 1 OR
                   NOT NEW."ResultsJson" @> OLD."ResultsJson" OR
                   EXISTS (SELECT 1 FROM jsonb_array_elements(NEW."ResultsJson") r WHERE NOT EXISTS
                     (SELECT 1 FROM jsonb_array_elements(NEW."SnapshotJson") i WHERE i->>'AssignmentId' = r->>'AssignmentId')) OR
                   (SELECT count(DISTINCT r->>'AssignmentId') FROM jsonb_array_elements(NEW."ResultsJson") r) <> jsonb_array_length(NEW."ResultsJson") OR
                   (NEW."State" = 'RESUELTA') <> (jsonb_array_length(NEW."ResultsJson") = jsonb_array_length(NEW."SnapshotJson"))
                THEN RAISE EXCEPTION 'Individual resolutions must append exactly one immutable task result'; END IF;
              ELSIF NEW."State" NOT IN ('APROBADA','RECHAZADA') OR jsonb_array_length(OLD."ResultsJson") > 0 THEN
                RAISE EXCEPTION 'Integral decision cannot overwrite individual results';
              END IF;
              RETURN NEW;
            END $$;
            """);
        migrationBuilder.DropCheckConstraint("CK_carryover_state", "operational_carryover_request");
        migrationBuilder.AddCheckConstraint("CK_carryover_state", "operational_carryover_request",
            "\"Revision\" > 0 AND ((\"State\" = 'PENDIENTE' AND \"DecidedBy\" IS NULL AND \"DecidedAt\" IS NULL) OR (\"State\" IN ('APROBADA','RECHAZADA','RESUELTA') AND \"DecidedBy\" IS NOT NULL AND \"DecidedAt\" IS NOT NULL AND length(\"DecisionComment\") > 0))");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Never reinterpret individual decisions as an integral legacy approval.
        migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM operational_carryover_request WHERE \"State\" = 'RESUELTA' OR \"ResultsJson\"::text LIKE '%CommandHash%') THEN RAISE EXCEPTION 'Individual carryover decisions exist; downgrade requires explicit data reconciliation'; END IF; END $$;");
        migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION protect_carryover_history() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
              IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'Carryover history cannot be deleted'; END IF;
              IF OLD."State" <> 'PENDIENTE' OR
                 (to_jsonb(NEW) - ARRAY['State','Revision','DecidedBy','DecidedAt','DecisionComment','ResultsJson']) <>
                 (to_jsonb(OLD) - ARRAY['State','Revision','DecidedBy','DecidedAt','DecisionComment','ResultsJson']) OR
                 NEW."State" NOT IN ('APROBADA','RECHAZADA') OR NEW."Revision" <> OLD."Revision" + 1
              THEN RAISE EXCEPTION 'Carryover snapshot and terminal decisions are immutable'; END IF;
              RETURN NEW;
            END $$;
            """);
        migrationBuilder.DropCheckConstraint("CK_carryover_state", "operational_carryover_request");
        migrationBuilder.AddCheckConstraint("CK_carryover_state", "operational_carryover_request",
            "\"Revision\" > 0 AND ((\"State\" = 'PENDIENTE' AND \"DecidedBy\" IS NULL AND \"DecidedAt\" IS NULL) OR (\"State\" IN ('APROBADA','RECHAZADA') AND \"DecidedBy\" IS NOT NULL AND \"DecidedAt\" IS NOT NULL AND length(\"DecisionComment\") > 0))");
    }
}
