using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SgolDbContext))]
[Migration("20260912120000_ExtendIdempotencyReplay")]
public sealed class ExtendIdempotencyReplay : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "UX_generation_request_idempotency_key",
            table: "generation_request");

        migrationBuilder.AddColumn<short>(
            name: "protocol_version",
            table: "idempotency_record",
            type: "smallint",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "response_etag",
            table: "idempotency_record",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "response_location",
            table: "idempotency_record",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<System.Text.Json.JsonDocument>(
            name: "response_payload",
            table: "idempotency_record",
            type: "jsonb",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_idempotency_record_protocol",
            table: "idempotency_record",
            sql: "protocol_version IS NULL OR (protocol_version = 1 AND status = 'COMPLETED' AND response_payload IS NOT NULL)");

        migrationBuilder.AddCheckConstraint(
            name: "CK_idempotency_record_response_location",
            table: "idempotency_record",
            sql: "response_location IS NULL OR (response_location LIKE '/api/v1/%' AND response_location !~ '://')");

        migrationBuilder.CreateIndex(
            name: "UX_generation_request_idempotency_key",
            table: "generation_request",
            columns: new[] { "requested_by", "idempotency_key" },
            unique: true)
            .Annotation("Npgsql:NullsDistinct", false);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("HU-034 idempotency replay persistence is forward-only.");
}
