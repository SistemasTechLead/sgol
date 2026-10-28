using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonAvatars : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "person_avatar",
                columns: table => new
                {
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Variant = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Palette = table.Column<int>(type: "integer", nullable: false),
                    Seed = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_avatar", x => x.PersonId);
                    table.CheckConstraint("CK_person_avatar_options", "\"Variant\" IN ('marble','beam','pixel','sunset','ring','bauhaus') AND \"Palette\" BETWEEN 0 AND 3 AND \"Seed\" BETWEEN 0 AND 999999");
                    table.ForeignKey(
                        name: "FK_person_avatar_person_PersonId",
                        column: x => x.PersonId,
                        principalTable: "person",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "person_avatar");
        }
    }
}
