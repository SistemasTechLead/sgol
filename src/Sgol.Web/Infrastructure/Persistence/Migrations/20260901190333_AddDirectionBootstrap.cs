using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectionBootstrap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "branch",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    timezone = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branch", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "direction_bootstrap",
                columns: table => new
                {
                    singleton = table.Column<bool>(type: "boolean", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_direction_bootstrap", x => x.singleton);
                    table.CheckConstraint("CK_direction_bootstrap_singleton", "singleton");
                });

            migrationBuilder.CreateTable(
                name: "person",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    stable_code = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "app_user",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    must_change_password = table.Column<bool>(type: "boolean", nullable: false),
                    mfa_enrolled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    security_stamp = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_user", x => x.id);
                    table.ForeignKey(
                        name: "FK_app_user_person_person_id",
                        column: x => x.person_id,
                        principalTable: "person",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "employment_version",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    position_text = table.Column<string>(type: "text", nullable: true),
                    shift_text = table.Column<string>(type: "text", nullable: true),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    valid_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    supersedes_id = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employment_version", x => x.id);
                    table.ForeignKey(
                        name: "FK_employment_version_branch_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_employment_version_employment_version_supersedes_id",
                        column: x => x.supersedes_id,
                        principalTable: "employment_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_employment_version_person_person_id",
                        column: x => x.person_id,
                        principalTable: "person",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "identity_credential",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_name = table.Column<string>(type: "text", nullable: false),
                    normalized_user_name = table.Column<string>(type: "text", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_credential", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_identity_credential_app_user_user_id",
                        column: x => x.user_id,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role_assignment_version",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_code = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    valid_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    supersedes_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_assignment_version", x => x.id);
                    table.CheckConstraint("CK_role_assignment_version_role_code", "role_code IN ('DIRECCION', 'ADMINISTRACION', 'SUBCOORDINACION', 'PISO_VENTAS')");
                    table.ForeignKey(
                        name: "FK_role_assignment_version_app_user_user_id",
                        column: x => x.user_id,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_role_assignment_version_branch_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_role_assignment_version_role_assignment_version_supersedes_~",
                        column: x => x.supersedes_id,
                        principalTable: "role_assignment_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "branch",
                columns: new[] { "id", "code", "name", "status", "timezone" },
                values: new object[] { new Guid("019d2d67-2c00-7000-8000-000000000001"), "LOR-001", "Loretta", "ACTIVA", "America/Mexico_City" });

            migrationBuilder.CreateIndex(
                name: "IX_app_user_person_id",
                table: "app_user",
                column: "person_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_branch_code",
                table: "branch",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_employment_version_branch_id",
                table: "employment_version",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_employment_version_person_id_branch_id",
                table: "employment_version",
                columns: new[] { "person_id", "branch_id" },
                unique: true,
                filter: "status = 'ACTIVA'");

            migrationBuilder.CreateIndex(
                name: "IX_employment_version_supersedes_id",
                table: "employment_version",
                column: "supersedes_id");

            migrationBuilder.CreateIndex(
                name: "IX_identity_credential_normalized_user_name",
                table: "identity_credential",
                column: "normalized_user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_person_stable_code",
                table: "person",
                column: "stable_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_role_assignment_version_branch_id",
                table: "role_assignment_version",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_role_assignment_version_supersedes_id",
                table: "role_assignment_version",
                column: "supersedes_id");

            migrationBuilder.CreateIndex(
                name: "IX_role_assignment_version_user_id_branch_id",
                table: "role_assignment_version",
                columns: new[] { "user_id", "branch_id" },
                unique: true,
                filter: "status = 'ACTIVO'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $block$
                BEGIN
                    RAISE EXCEPTION 'direction bootstrap migration is forward-only';
                END;
                $block$;
                """);
        }
    }
}
