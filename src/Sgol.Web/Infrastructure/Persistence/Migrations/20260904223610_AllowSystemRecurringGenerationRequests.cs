using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowSystemRecurringGenerationRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "requested_by",
                table: "generation_request",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddCheckConstraint(
                name: "CK_generation_request_actor",
                table: "generation_request",
                sql: "(requested_by IS NOT NULL AND origin_type = 'MANUAL_REFERENCE_V1') OR (requested_by IS NULL AND origin_type = 'WORKING_DAY_WINDOW_V1')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Rollback is blocked; apply a forward corrective migration.");
        }
    }
}
