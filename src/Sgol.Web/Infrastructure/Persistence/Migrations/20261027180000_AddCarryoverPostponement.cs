using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Sgol.Web.Infrastructure.Persistence.Migrations;

public partial class AddCarryoverPostponement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint("CK_carryover_interval", "operational_carryover_request");
        migrationBuilder.AddColumn<string>("Kind", "operational_carryover_request", type: "character varying(12)", maxLength: 12, nullable: false, defaultValue: "INCIDENTE");
        migrationBuilder.AddColumn<DateOnly>("From", "operational_carryover_request", type: "date", nullable: true);
        migrationBuilder.AddColumn<DateOnly>("Through", "operational_carryover_request", type: "date", nullable: true);
        migrationBuilder.AddCheckConstraint("CK_carryover_interval", "operational_carryover_request",
            "\"Minutes\" > 0 AND ((\"Kind\" = 'INCIDENTE' AND \"IncidentEnd\" = \"IncidentStart\" + \"Minutes\" * interval '1 minute' AND \"From\" IS NULL AND \"Through\" IS NULL) OR (\"Kind\" = 'POSPONER' AND \"From\" IS NOT NULL AND \"Through\" IS NOT NULL AND \"Through\" = \"From\" + 6))");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Downgrade must not erase approved postponement history.
        migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM operational_carryover_request WHERE \"Kind\" = 'POSPONER') THEN RAISE EXCEPTION 'Postponement history prevents downgrade'; END IF; END $$;");
        migrationBuilder.DropCheckConstraint("CK_carryover_interval", "operational_carryover_request");
        migrationBuilder.DropColumn("Kind", "operational_carryover_request");
        migrationBuilder.DropColumn("From", "operational_carryover_request");
        migrationBuilder.DropColumn("Through", "operational_carryover_request");
        migrationBuilder.AddCheckConstraint("CK_carryover_interval", "operational_carryover_request", "\"Minutes\" > 0 AND \"IncidentEnd\" = \"IncidentStart\" + \"Minutes\" * interval '1 minute'");
    }
}
