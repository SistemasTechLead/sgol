using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCameraPhotographSets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "MaximumPhotoBytes",
                table: "operational_task",
                type: "bigint",
                nullable: false,
                defaultValue: 31457280L);

            migrationBuilder.AddColumn<int>(
                name: "MaximumPhotos",
                table: "operational_task",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<int>(
                name: "MinimumPhotos",
                table: "operational_task",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<long>(
                name: "MaximumPhotoBytes",
                table: "operational_assignment",
                type: "bigint",
                nullable: false,
                defaultValue: 31457280L);

            migrationBuilder.AddColumn<int>(
                name: "MaximumPhotos",
                table: "operational_assignment",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<int>(
                name: "MinimumPhotos",
                table: "operational_assignment",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "capture_expires_at",
                table: "file_object",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "capture_session_id",
                table: "file_object",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "received_object_key",
                table: "file_object",
                type: "character varying(73)",
                maxLength: 73,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "received_sha256",
                table: "file_object",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "received_size_bytes",
                table: "file_object",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "evidence_version_file",
                columns: table => new
                {
                    evidence_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_object_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evidence_version_file", x => new { x.evidence_version_id, x.file_object_id });
                    table.CheckConstraint("CK_evidence_version_file_position", "position BETWEEN 0 AND 4");
                    table.ForeignKey(
                        name: "FK_evidence_version_file_evidence_version_evidence_version_id",
                        column: x => x.evidence_version_id,
                        principalTable: "evidence_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_evidence_version_file_file_object_file_object_id",
                        column: x => x.file_object_id,
                        principalTable: "file_object",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "operational_capture_session",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UploadAttempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_capture_session", x => x.Id);
                    table.ForeignKey(
                        name: "FK_operational_capture_session_app_user_ActorId",
                        column: x => x.ActorId,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_operational_capture_session_operational_assignment_Assignme~",
                        column: x => x.AssignmentId,
                        principalTable: "operational_assignment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "operational_photograph",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaptureSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    ReceivedBytes = table.Column<long>(type: "bigint", nullable: false),
                    ReceivedSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AttachedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_photograph", x => x.Id);
                    table.CheckConstraint("CK_operational_photo_position", "\"Position\" BETWEEN 0 AND 4");
                    table.CheckConstraint("CK_operational_photo_size", "octet_length(\"Content\") BETWEEN 1 AND 15728640 AND \"ReceivedBytes\" BETWEEN 1 AND 15728640");
                    table.CheckConstraint("CK_operational_photo_type", "\"ContentType\" IN ('image/jpeg','image/png')");
                    table.ForeignKey(
                        name: "FK_operational_photograph_operational_assignment_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "operational_assignment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_operational_photograph_operational_capture_session_CaptureS~",
                        column: x => x.CaptureSessionId,
                        principalTable: "operational_capture_session",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0005-7000-8000-000000000005"),
                columns: new[] { "MaximumPhotoBytes", "MaximumPhotos", "MinimumPhotos" },
                values: new object[] { 31457280L, 5, 1 });

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0007-7000-8000-000000000007"),
                columns: new[] { "MaximumPhotoBytes", "MaximumPhotos", "MinimumPhotos" },
                values: new object[] { 31457280L, 5, 1 });

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0008-7000-8000-000000000008"),
                columns: new[] { "MaximumPhotoBytes", "MaximumPhotos", "MinimumPhotos" },
                values: new object[] { 31457280L, 5, 1 });

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0011-7000-8000-000000000011"),
                columns: new[] { "MaximumPhotoBytes", "MaximumPhotos", "MinimumPhotos" },
                values: new object[] { 31457280L, 5, 1 });

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0018-7000-8000-000000000018"),
                columns: new[] { "MaximumPhotoBytes", "MaximumPhotos", "MinimumPhotos" },
                values: new object[] { 31457280L, 5, 1 });

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0026-7000-8000-000000000026"),
                columns: new[] { "MaximumPhotoBytes", "MaximumPhotos", "MinimumPhotos" },
                values: new object[] { 31457280L, 5, 1 });

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0092-7000-8000-000000000092"),
                columns: new[] { "MaximumPhotoBytes", "MaximumPhotos", "MinimumPhotos" },
                values: new object[] { 31457280L, 5, 1 });

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0093-7000-8000-000000000093"),
                columns: new[] { "MaximumPhotoBytes", "MaximumPhotos", "MinimumPhotos" },
                values: new object[] { 31457280L, 5, 1 });

            migrationBuilder.CreateIndex(
                name: "IX_evidence_version_file_evidence_version_id_position",
                table: "evidence_version_file",
                columns: new[] { "evidence_version_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_evidence_version_file_file_object_id",
                table: "evidence_version_file",
                column: "file_object_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_operational_capture_session_ActorId_CreatedAt",
                table: "operational_capture_session",
                columns: new[] { "ActorId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_capture_session_AssignmentId",
                table: "operational_capture_session",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_capture_session_ExpiresAt",
                table: "operational_capture_session",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_operational_photograph_AssignmentId_Position",
                table: "operational_photograph",
                columns: new[] { "AssignmentId", "Position" },
                unique: true,
                filter: "\"AttachedAt\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_operational_photograph_CaptureSessionId",
                table: "operational_photograph",
                column: "CaptureSessionId");

            // Preserve the complete binary history as ordered collections without rewriting evidence.
            migrationBuilder.Sql("""
                INSERT INTO evidence_version_file (evidence_version_id, file_object_id, position)
                SELECT id, file_object_id, 0 FROM evidence_version WHERE file_object_id IS NOT NULL;
                INSERT INTO operational_photograph ("Id", "AssignmentId", "CaptureSessionId", "Position", "FileName", "ContentType", "Content", "ReceivedBytes", "ReceivedSha256", "Sha256", "CreatedAt", "AttachedAt")
                SELECT "Id", "Id", NULL, 0, 'foto-historica-' || "Id"::text || CASE WHEN "ContentType" = 'image/png' THEN '.png' ELSE '.jpg' END,
                    "ContentType", "FileContent", octet_length("FileContent"), encode(sha256("FileContent"), 'hex'), encode(sha256("FileContent"), 'hex'),
                    COALESCE("CompletedAt", now()), COALESCE("CompletedAt", now())
                FROM operational_assignment WHERE "Requirement" = 'FOTOGRAFIA' AND "FileContent" IS NOT NULL AND "ContentType" IN ('image/jpeg','image/png');
                UPDATE operational_assignment SET "FileContent" = NULL, "ContentType" = NULL
                WHERE "Id" IN (SELECT "AssignmentId" FROM operational_photograph);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A rollback must not silently discard a set of photos or a normalized object's provenance.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM operational_photograph WHERE "AttachedAt" IS NOT NULL GROUP BY "AssignmentId" HAVING count(*) > 1)
                        OR EXISTS (SELECT 1 FROM evidence_version_file GROUP BY evidence_version_id HAVING count(*) > 1)
                        OR EXISTS (SELECT 1 FROM file_object WHERE received_sha256 IS NOT NULL)
                    THEN RAISE EXCEPTION 'Camera evidence exists. Disable Evidence:Camera:Enabled instead of downgrading the schema.';
                    END IF;
                END $$;
                UPDATE operational_assignment a SET "FileContent" = p."Content", "ContentType" = p."ContentType"
                FROM operational_photograph p WHERE p."AssignmentId" = a."Id" AND p."AttachedAt" IS NOT NULL;
                """);
            migrationBuilder.DropTable(
                name: "evidence_version_file");

            migrationBuilder.DropTable(
                name: "operational_photograph");

            migrationBuilder.DropTable(
                name: "operational_capture_session");

            migrationBuilder.DropColumn(
                name: "MaximumPhotoBytes",
                table: "operational_task");

            migrationBuilder.DropColumn(
                name: "MaximumPhotos",
                table: "operational_task");

            migrationBuilder.DropColumn(
                name: "MinimumPhotos",
                table: "operational_task");

            migrationBuilder.DropColumn(
                name: "MaximumPhotoBytes",
                table: "operational_assignment");

            migrationBuilder.DropColumn(
                name: "MaximumPhotos",
                table: "operational_assignment");

            migrationBuilder.DropColumn(
                name: "MinimumPhotos",
                table: "operational_assignment");

            migrationBuilder.DropColumn(
                name: "capture_expires_at",
                table: "file_object");

            migrationBuilder.DropColumn(
                name: "capture_session_id",
                table: "file_object");

            migrationBuilder.DropColumn(
                name: "received_object_key",
                table: "file_object");

            migrationBuilder.DropColumn(
                name: "received_sha256",
                table: "file_object");

            migrationBuilder.DropColumn(
                name: "received_size_bytes",
                table: "file_object");
        }
    }
}
