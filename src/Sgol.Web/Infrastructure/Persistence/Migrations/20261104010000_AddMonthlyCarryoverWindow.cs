using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Sgol.Web.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SgolDbContext))]
[Migration("20261104010000_AddMonthlyCarryoverWindow")]
public sealed class AddMonthlyCarryoverWindow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint("CK_carryover_interval", "operational_carryover_request");
        migrationBuilder.AddCheckConstraint("CK_carryover_interval", "operational_carryover_request",
            "\"Minutes\" > 0 AND ((\"Kind\" = 'INCIDENTE' AND \"IncidentEnd\" = \"IncidentStart\" + \"Minutes\" * interval '1 minute' AND \"From\" IS NULL AND \"Through\" IS NULL) OR (\"Kind\" = 'POSPONER' AND \"From\" IS NOT NULL AND \"Through\" IS NOT NULL AND \"Through\" BETWEEN \"From\" AND CASE WHEN \"AlgorithmVersion\" >= 4 THEN ((\"From\" - 1) + interval '1 month')::date ELSE \"From\" + 6 END))");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM operational_carryover_request WHERE \"Kind\" = 'POSPONER' AND \"Through\" > \"From\" + 6) THEN RAISE EXCEPTION 'Monthly carryover history prevents downgrade'; END IF; END $$;");
        migrationBuilder.DropCheckConstraint("CK_carryover_interval", "operational_carryover_request");
        migrationBuilder.AddCheckConstraint("CK_carryover_interval", "operational_carryover_request",
            "\"Minutes\" > 0 AND ((\"Kind\" = 'INCIDENTE' AND \"IncidentEnd\" = \"IncidentStart\" + \"Minutes\" * interval '1 minute' AND \"From\" IS NULL AND \"Through\" IS NULL) OR (\"Kind\" = 'POSPONER' AND \"From\" IS NOT NULL AND \"Through\" IS NOT NULL AND \"Through\" BETWEEN \"From\" AND \"From\" + 6))");
    }
}
