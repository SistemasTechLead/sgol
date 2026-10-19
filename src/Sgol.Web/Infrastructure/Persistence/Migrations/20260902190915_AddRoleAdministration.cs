using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "row_version",
                table: "role_assignment_version",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddCheckConstraint(
                name: "CK_role_assignment_version_interval",
                table: "role_assignment_version",
                sql: "(status = 'ACTIVO' AND valid_to IS NULL) OR (status = 'SUSTITUIDO' AND valid_to IS NOT NULL AND valid_to >= valid_from)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_role_assignment_version_status",
                table: "role_assignment_version",
                sql: "status IN ('ACTIVO', 'SUSTITUIDO')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "Rollback is blocked because role assignment history and concurrency metadata must be preserved.");
        }
    }
}
