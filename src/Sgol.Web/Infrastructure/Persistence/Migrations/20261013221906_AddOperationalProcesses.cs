using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalProcesses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $body$ BEGIN
                  IF EXISTS (SELECT 1 FROM operational_sequence_run) THEN
                    RAISE EXCEPTION 'Existing sequence executions require an explicit configuration capture before catalog migration.';
                  END IF;
                END $body$;
                """);
            migrationBuilder.AddColumn<Guid>(
                name: "ProcessId",
                table: "operational_task",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000010"));

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "operational_sequence_version",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "operational_sequence_version",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PublishedAt",
                table: "operational_sequence_version",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PublishedBy",
                table: "operational_sequence_version",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Revision",
                table: "operational_sequence_version",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<bool>(
                name: "StartHourConfirmed",
                table: "operational_sequence_version",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TaskSnapshot",
                table: "operational_sequence_step",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "operational_sequence",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsGeneral",
                table: "operational_sequence",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "PublishedVersionId",
                table: "operational_sequence",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_operational_sequence_version_SequenceId_Id",
                table: "operational_sequence_version",
                columns: new[] { "SequenceId", "Id" });

            migrationBuilder.Sql("""
                UPDATE operational_sequence_version v SET "Name" = p."Name" FROM operational_sequence p WHERE p."Id" = v."SequenceId";
                UPDATE operational_sequence SET "Enabled" = false;
                """);

            migrationBuilder.InsertData(
                table: "operational_sequence",
                columns: new[] { "Id", "Description", "Enabled", "IsGeneral", "Name", "PublishedVersionId", "Revision" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), "", false, true, "General", null, 1L });

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0005-7000-8000-000000000005"),
                column: "ProcessId",
                value: new Guid("00000000-0000-0000-0000-000000000010"));

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0007-7000-8000-000000000007"),
                column: "ProcessId",
                value: new Guid("00000000-0000-0000-0000-000000000010"));

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0008-7000-8000-000000000008"),
                column: "ProcessId",
                value: new Guid("00000000-0000-0000-0000-000000000010"));

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0011-7000-8000-000000000011"),
                column: "ProcessId",
                value: new Guid("00000000-0000-0000-0000-000000000010"));

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0018-7000-8000-000000000018"),
                column: "ProcessId",
                value: new Guid("00000000-0000-0000-0000-000000000010"));

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0026-7000-8000-000000000026"),
                column: "ProcessId",
                value: new Guid("00000000-0000-0000-0000-000000000010"));

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0092-7000-8000-000000000092"),
                column: "ProcessId",
                value: new Guid("00000000-0000-0000-0000-000000000010"));

            migrationBuilder.UpdateData(
                table: "operational_task",
                keyColumn: "Id",
                keyValue: new Guid("019d3a10-0093-7000-8000-000000000093"),
                column: "ProcessId",
                value: new Guid("00000000-0000-0000-0000-000000000010"));

            migrationBuilder.CreateIndex(
                name: "IX_operational_task_ProcessId",
                table: "operational_task",
                column: "ProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_version_PublishedBy",
                table: "operational_sequence_version",
                column: "PublishedBy");

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_version_SequenceId",
                table: "operational_sequence_version",
                column: "SequenceId",
                unique: true,
                filter: "\"PublishedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_Id_PublishedVersionId",
                table: "operational_sequence",
                columns: new[] { "Id", "PublishedVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_IsGeneral",
                table: "operational_sequence",
                column: "IsGeneral",
                unique: true,
                filter: "\"IsGeneral\"");

            migrationBuilder.AddForeignKey(
                name: "FK_operational_sequence_operational_sequence_version_Id_Publis~",
                table: "operational_sequence",
                columns: new[] { "Id", "PublishedVersionId" },
                principalTable: "operational_sequence_version",
                principalColumns: new[] { "SequenceId", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_operational_sequence_version_app_user_PublishedBy",
                table: "operational_sequence_version",
                column: "PublishedBy",
                principalTable: "app_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_operational_task_operational_sequence_ProcessId",
                table: "operational_task",
                column: "ProcessId",
                principalTable: "operational_sequence",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
                ALTER TABLE operational_sequence ADD CONSTRAINT "CK_process_general"
                  CHECK (("IsGeneral" AND "Id" = '00000000-0000-0000-0000-000000000010' AND "Name" = 'General' AND NOT "Enabled" AND "PublishedVersionId" IS NULL)
                    OR (NOT "IsGeneral" AND "Id" <> '00000000-0000-0000-0000-000000000010' AND (NOT "Enabled" OR "PublishedVersionId" IS NOT NULL)));
                ALTER TABLE operational_sequence_version ADD CONSTRAINT "CK_process_publication"
                  CHECK ("Revision" > 0 AND (("PublishedAt" IS NULL AND "PublishedBy" IS NULL)
                    OR ("PublishedAt" IS NOT NULL AND "PublishedBy" IS NOT NULL AND ("StartKind" <> 'SEMESTRAL' OR "StartHourConfirmed"))));

                CREATE OR REPLACE FUNCTION sgol_sequence_version_immutable() RETURNS trigger LANGUAGE plpgsql AS $body$
                DECLARE v_id uuid;
                BEGIN
                  IF TG_TABLE_NAME = 'operational_sequence_version' THEN
                    v_id := OLD."Id";
                    IF OLD."PublishedAt" IS NOT NULL THEN
                      RAISE EXCEPTION 'Published process versions are immutable' USING ERRCODE = '23514';
                    END IF;
                  ELSE
                    IF TG_OP = 'DELETE' THEN v_id := OLD."VersionId"; ELSE v_id := NEW."VersionId"; END IF;
                    IF EXISTS (SELECT 1 FROM operational_sequence_version WHERE "Id" = v_id AND "PublishedAt" IS NOT NULL)
                      OR (TG_OP = 'UPDATE' AND EXISTS (SELECT 1 FROM operational_sequence_version WHERE "Id" = OLD."VersionId" AND "PublishedAt" IS NOT NULL)) THEN
                      RAISE EXCEPTION 'Published process steps are immutable' USING ERRCODE = '23514';
                    END IF;
                  END IF;
                  IF EXISTS (SELECT 1 FROM operational_sequence_run WHERE "VersionId" = v_id) THEN
                    RAISE EXCEPTION 'A version used by a sequence execution is immutable' USING ERRCODE = '23514';
                  END IF;
                  IF TG_TABLE_NAME = 'operational_sequence_step' AND TG_OP = 'UPDATE' THEN
                    IF EXISTS (SELECT 1 FROM operational_sequence_run WHERE "VersionId" = OLD."VersionId") THEN
                      RAISE EXCEPTION 'A version used by a sequence execution is immutable' USING ERRCODE = '23514';
                    END IF;
                  END IF;
                  IF TG_OP = 'DELETE' THEN RETURN OLD; END IF; RETURN NEW;
                END $body$;

                CREATE FUNCTION sgol_process_general_guard() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN
                  IF OLD."IsGeneral" THEN RAISE EXCEPTION 'General is a reserved system process' USING ERRCODE = '23514'; END IF;
                  IF TG_OP = 'DELETE' THEN RETURN OLD; END IF; RETURN NEW;
                END $body$;
                CREATE TRIGGER process_general_guard BEFORE UPDATE OR DELETE ON operational_sequence
                  FOR EACH ROW EXECUTE FUNCTION sgol_process_general_guard();

                CREATE FUNCTION sgol_process_publication_valid() RETURNS trigger LANGUAGE plpgsql AS $body$
                DECLARE seq uuid; version uuid;
                BEGIN
                  IF TG_TABLE_NAME = 'operational_sequence_version' THEN
                    seq := NEW."SequenceId"; version := NEW."Id";
                    IF NEW."PublishedAt" IS NULL THEN RETURN NEW; END IF;
                  ELSE
                    seq := NEW."Id"; version := NEW."PublishedVersionId";
                    IF version IS NULL THEN RETURN NEW; END IF;
                  END IF;
                  IF NOT EXISTS (SELECT 1 FROM operational_sequence_version WHERE "Id" = version AND "SequenceId" = seq AND "PublishedAt" IS NOT NULL)
                    OR NOT EXISTS (SELECT 1 FROM operational_sequence_step WHERE "VersionId" = version)
                    OR EXISTS (SELECT 1 FROM operational_sequence_step s JOIN operational_task t ON t."Id" = s."TaskId"
                      WHERE s."VersionId" = version AND (t."ProcessId" <> seq OR t."Deleted" OR NOT t."Active" OR s."TaskSnapshot" IS NULL))
                    OR EXISTS (SELECT "TaskId" FROM operational_sequence_step WHERE "VersionId" = version GROUP BY "TaskId" HAVING count(*) > 1)
                    OR (SELECT count(*) FROM operational_sequence_step WHERE "VersionId" = version) <>
                       (SELECT max("Position") FROM operational_sequence_step WHERE "VersionId" = version) THEN
                    RAISE EXCEPTION 'Published process membership or rules are inconsistent' USING ERRCODE = '23514';
                  END IF;
                  RETURN NEW;
                END $body$;
                CREATE CONSTRAINT TRIGGER process_publication_valid AFTER INSERT OR UPDATE ON operational_sequence_version
                  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION sgol_process_publication_valid();
                CREATE CONSTRAINT TRIGGER process_current_version_valid AFTER INSERT OR UPDATE ON operational_sequence
                  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION sgol_process_publication_valid();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $body$ BEGIN
                  IF EXISTS (SELECT 1 FROM operational_sequence WHERE NOT "IsGeneral")
                    OR EXISTS (SELECT 1 FROM operational_task WHERE "ProcessId" <> '00000000-0000-0000-0000-000000000010') THEN
                    RAISE EXCEPTION 'Process catalog data exists. Restore a verified backup instead of discarding membership and versions.';
                  END IF;
                END $body$;
                DROP TRIGGER process_general_guard ON operational_sequence;
                DROP TRIGGER process_publication_valid ON operational_sequence_version;
                DROP TRIGGER process_current_version_valid ON operational_sequence;
                DROP FUNCTION sgol_process_general_guard();
                DROP FUNCTION sgol_process_publication_valid();
                ALTER TABLE operational_sequence DROP CONSTRAINT "CK_process_general";
                ALTER TABLE operational_sequence_version DROP CONSTRAINT "CK_process_publication";
                CREATE OR REPLACE FUNCTION sgol_sequence_version_immutable() RETURNS trigger LANGUAGE plpgsql AS $body$
                DECLARE v_id uuid;
                BEGIN
                  IF TG_TABLE_NAME = 'operational_sequence_version' THEN v_id := OLD."Id";
                  ELSE IF TG_OP = 'DELETE' THEN v_id := OLD."VersionId"; ELSE v_id := NEW."VersionId"; END IF; END IF;
                  IF EXISTS (SELECT 1 FROM operational_sequence_run WHERE "VersionId" = v_id) THEN
                    RAISE EXCEPTION 'A version used by a sequence execution is immutable' USING ERRCODE = '23514';
                  END IF;
                  IF TG_TABLE_NAME = 'operational_sequence_step' AND TG_OP = 'UPDATE' THEN
                    IF EXISTS (SELECT 1 FROM operational_sequence_run WHERE "VersionId" = OLD."VersionId") THEN
                      RAISE EXCEPTION 'A version used by a sequence execution is immutable' USING ERRCODE = '23514';
                    END IF;
                  END IF;
                  IF TG_OP = 'DELETE' THEN RETURN OLD; END IF; RETURN NEW;
                END $body$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_operational_sequence_operational_sequence_version_Id_Publis~",
                table: "operational_sequence");

            migrationBuilder.DropForeignKey(
                name: "FK_operational_sequence_version_app_user_PublishedBy",
                table: "operational_sequence_version");

            migrationBuilder.DropForeignKey(
                name: "FK_operational_task_operational_sequence_ProcessId",
                table: "operational_task");

            migrationBuilder.DropIndex(
                name: "IX_operational_task_ProcessId",
                table: "operational_task");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_operational_sequence_version_SequenceId_Id",
                table: "operational_sequence_version");

            migrationBuilder.DropIndex(
                name: "IX_operational_sequence_version_PublishedBy",
                table: "operational_sequence_version");

            migrationBuilder.DropIndex(
                name: "IX_operational_sequence_version_SequenceId",
                table: "operational_sequence_version");

            migrationBuilder.DropIndex(
                name: "IX_operational_sequence_Id_PublishedVersionId",
                table: "operational_sequence");

            migrationBuilder.DropIndex(
                name: "IX_operational_sequence_IsGeneral",
                table: "operational_sequence");

            migrationBuilder.DeleteData(
                table: "operational_sequence",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"));

            migrationBuilder.DropColumn(
                name: "ProcessId",
                table: "operational_task");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "operational_sequence_version");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "operational_sequence_version");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "operational_sequence_version");

            migrationBuilder.DropColumn(
                name: "PublishedBy",
                table: "operational_sequence_version");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "operational_sequence_version");

            migrationBuilder.DropColumn(
                name: "StartHourConfirmed",
                table: "operational_sequence_version");

            migrationBuilder.DropColumn(
                name: "TaskSnapshot",
                table: "operational_sequence_step");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "operational_sequence");

            migrationBuilder.DropColumn(
                name: "IsGeneral",
                table: "operational_sequence");

            migrationBuilder.DropColumn(
                name: "PublishedVersionId",
                table: "operational_sequence");
        }
    }
}
