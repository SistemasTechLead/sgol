using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations;

public partial class RemoveContinuityModule : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The retired tables remain as historical data outside the active EF model.
        // Removing screens and application code does not require erasing stored records.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The historical tables were retained, so no schema change is required.
    }
}