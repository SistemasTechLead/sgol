using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConfirmSundayOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SundayTaskIdsJson",
                table: "temporal_assignment_state",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS(SELECT 1 FROM temporal_assignment_state WHERE "SundayTaskIdsJson" <> '[]'::jsonb) THEN
                    RAISE EXCEPTION 'Preserve Sunday policy history: disable temporal policy instead of downgrading';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropColumn(
                name: "SundayTaskIdsJson",
                table: "temporal_assignment_state");
        }
    }
}
