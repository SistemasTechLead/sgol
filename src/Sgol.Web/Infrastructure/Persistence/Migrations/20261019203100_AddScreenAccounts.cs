using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScreenAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "screen_account",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    display_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    purpose = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_screen_account", x => x.id);
                    table.CheckConstraint("CK_screen_account_code", "code ~ '^[A-Z][A-Z0-9-]{2,63}$'");
                    table.CheckConstraint("CK_screen_account_name", "length(btrim(display_name)) BETWEEN 1 AND 120");
                    table.CheckConstraint("CK_screen_account_purpose", "purpose = 'TABLERO_LECTURA'");
                    table.CheckConstraint("CK_screen_account_status", "status = 'PENDIENTE_VINCULACION'");
                    table.ForeignKey(
                        name: "FK_screen_account_app_user_created_by",
                        column: x => x.created_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_screen_account_branch_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_screen_account_branch_id",
                table: "screen_account",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_screen_account_code",
                table: "screen_account",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_screen_account_created_by",
                table: "screen_account",
                column: "created_by");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM screen_account) THEN
                        RAISE EXCEPTION 'Preserve screen identities: a populated registry cannot be downgraded';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "screen_account");
        }
    }
}
