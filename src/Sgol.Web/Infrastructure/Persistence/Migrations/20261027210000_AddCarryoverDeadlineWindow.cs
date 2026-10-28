using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Sgol.Web.Infrastructure.Persistence.Migrations;

public partial class AddCarryoverDeadlineWindow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint("CK_carryover_interval", "operational_carryover_request");
        migrationBuilder.AddCheckConstraint("CK_carryover_interval", "operational_carryover_request", "\"Minutes\" > 0 AND ((\"Kind\" = 'INCIDENTE' AND \"IncidentEnd\" = \"IncidentStart\" + \"Minutes\" * interval '1 minute' AND \"From\" IS NULL AND \"Through\" IS NULL) OR (\"Kind\" = 'POSPONER' AND \"From\" IS NOT NULL AND \"Through\" IS NOT NULL AND \"Through\" BETWEEN \"From\" AND \"From\" + 6))");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint("CK_carryover_interval", "operational_carryover_request");
        migrationBuilder.AddCheckConstraint("CK_carryover_interval", "operational_carryover_request", "\"Minutes\" > 0 AND ((\"Kind\" = 'INCIDENTE' AND \"IncidentEnd\" = \"IncidentStart\" + \"Minutes\" * interval '1 minute' AND \"From\" IS NULL AND \"Through\" IS NULL) OR (\"Kind\" = 'POSPONER' AND \"From\" IS NOT NULL AND \"Through\" IS NOT NULL AND \"Through\" = \"From\" + 6))");
    }
}
