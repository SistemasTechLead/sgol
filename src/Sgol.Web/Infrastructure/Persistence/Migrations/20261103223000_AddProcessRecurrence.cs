using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Sgol.Web.Infrastructure.Persistence.Migrations;
[DbContext(typeof(SgolDbContext))]
[Migration("20261103223000_AddProcessRecurrence")]
public sealed class AddProcessRecurrence : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<string>(name: "StartScheduleJson", table: "operational_sequence_version", type: "jsonb", nullable: true);
        m.DropCheckConstraint("CK_sequence_calendar", "operational_sequence_version");
        m.AddCheckConstraint("CK_sequence_calendar", "operational_sequence_version", Rule(true));
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropCheckConstraint("CK_sequence_calendar", "operational_sequence_version");
        m.AddCheckConstraint("CK_sequence_calendar", "operational_sequence_version", Rule(false));
        m.DropColumn("StartScheduleJson", "operational_sequence_version");
    }
    private static string Rule(bool expanded) => "\"Number\" > 0 AND \"StartKind\" IN (" + (expanded ? "'SEMESTRAL','OCASIONAL','SEMANAL','MENSUAL','TRIMESTRAL','ANUAL'" : "'SEMESTRAL','OCASIONAL'") + ") AND \"FirstMonth\" BETWEEN 1 AND 6 AND \"WindowFromDay\" BETWEEN 1 AND 28 AND \"WindowThroughDay\" BETWEEN \"WindowFromDay\" AND 28 AND \"PreferredWeekDay\" BETWEEN 0 AND 6 AND \"StartHour\" BETWEEN 0 AND 23";
}
