using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenCameraPolicyLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_task_photo_limits",
                table: "operational_task",
                sql: "\"MinimumPhotos\" BETWEEN 1 AND 5 AND \"MaximumPhotos\" BETWEEN \"MinimumPhotos\" AND 5 AND \"MaximumPhotoBytes\" BETWEEN 1048576 AND 31457280");

            migrationBuilder.AddCheckConstraint(
                name: "CK_operational_assignment_photo_limits",
                table: "operational_assignment",
                sql: "\"MinimumPhotos\" BETWEEN 1 AND 5 AND \"MaximumPhotos\" BETWEEN \"MinimumPhotos\" AND 5 AND \"MaximumPhotoBytes\" BETWEEN 1048576 AND 31457280");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_task_photo_limits",
                table: "operational_task");

            migrationBuilder.DropCheckConstraint(
                name: "CK_operational_assignment_photo_limits",
                table: "operational_assignment");
        }
    }
}
