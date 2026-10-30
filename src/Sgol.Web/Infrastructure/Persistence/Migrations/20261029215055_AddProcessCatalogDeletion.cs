using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessCatalogDeletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "operational_sequence",
                type: "timestamp with time zone",
                nullable: true);

            // Disabled definitions may diverge from their frozen historical version.
            // Publishing and enabling still require current catalogue membership.
            migrationBuilder.Sql(PublicationGuard);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM operational_sequence WHERE "DeletedAt" IS NOT NULL)
                    OR EXISTS (SELECT 1 FROM operational_sequence p
                      JOIN operational_sequence_step s ON s."VersionId" = p."PublishedVersionId"
                      JOIN operational_task t ON t."Id" = s."TaskId" WHERE t."ProcessId" <> p."Id") THEN
                    RAISE EXCEPTION 'Cannot downgrade after process deletion or task removal';
                  END IF;
                END $$;
                """);
            migrationBuilder.Sql(PublicationGuard.Replace("require_membership := NEW.\"Enabled\";", "require_membership := true;"));
            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "operational_sequence");
        }

        private const string PublicationGuard = """
            CREATE OR REPLACE FUNCTION sgol_process_publication_valid() RETURNS trigger LANGUAGE plpgsql AS $body$
            DECLARE seq uuid; version uuid; require_membership boolean := true;
            BEGIN
              IF TG_TABLE_NAME = 'operational_sequence_version' THEN
                seq := NEW."SequenceId"; version := NEW."Id";
                IF NEW."PublishedAt" IS NULL THEN RETURN NEW; END IF;
              ELSE
                seq := NEW."Id"; version := NEW."PublishedVersionId";
                require_membership := NEW."Enabled";
                IF version IS NULL THEN RETURN NEW; END IF;
              END IF;
              IF NOT EXISTS (SELECT 1 FROM operational_sequence_version WHERE "Id" = version AND "SequenceId" = seq AND "PublishedAt" IS NOT NULL)
                OR NOT EXISTS (SELECT 1 FROM operational_sequence_step WHERE "VersionId" = version)
                OR EXISTS (SELECT 1 FROM operational_sequence_step s JOIN operational_task t ON t."Id" = s."TaskId"
                  WHERE s."VersionId" = version AND (s."TaskSnapshot" IS NULL OR
                    (require_membership AND (t."ProcessId" <> seq OR t."Deleted" OR NOT t."Active"))))
                OR EXISTS (SELECT "TaskId" FROM operational_sequence_step WHERE "VersionId" = version GROUP BY "TaskId" HAVING count(*) > 1)
                OR (SELECT count(*) FROM operational_sequence_step WHERE "VersionId" = version) <>
                   (SELECT max("Position") FROM operational_sequence_step WHERE "VersionId" = version) THEN
                RAISE EXCEPTION 'Published process membership or rules are inconsistent' USING ERRCODE = '23514';
              END IF;
              RETURN NEW;
            END $body$;
            """;
    }
}
