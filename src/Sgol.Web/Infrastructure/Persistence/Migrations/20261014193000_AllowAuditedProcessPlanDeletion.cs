using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Sgol.Web.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SgolDbContext))]
[Migration("20261014193000_AllowAuditedProcessPlanDeletion")]
public sealed class AllowAuditedProcessPlanDeletion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE OR REPLACE FUNCTION sgol_sequence_append_only() RETURNS trigger LANGUAGE plpgsql AS $body$
        BEGIN
          IF TG_OP = 'DELETE' AND EXISTS (
            SELECT 1 FROM operational_sequence_run r WHERE r."Id" = OLD."RunId"
              AND r."PlanId"::text = current_setting('sgol.plan_delete_id', true)
          ) AND NULLIF(current_setting('sgol.plan_delete_key', true), '') IS NOT NULL THEN
            RETURN OLD;
          END IF;
          RAISE EXCEPTION 'Sequence documents and events are append only' USING ERRCODE = '23514';
        END $body$;

        -- The narrowly scoped exception may commit only with the whole plan gone
        -- and its deletion audit recorded. Individual history deletion stays forbidden.
        CREATE FUNCTION sgol_sequence_plan_delete_audited() RETURNS trigger LANGUAGE plpgsql AS $body$
        DECLARE plan_id text := current_setting('sgol.plan_delete_id', true);
        BEGIN
          IF EXISTS (SELECT 1 FROM direction_prepared_plan WHERE id::text = plan_id)
            OR EXISTS (SELECT 1 FROM operational_sequence_run WHERE "Id" = OLD."RunId")
            OR NOT EXISTS (SELECT 1 FROM audit_event WHERE resource_id::text = plan_id
              AND action = 'DIRECTION_PLAN_DELETE' AND outcome = 'DELETED'
              AND request_id = current_setting('sgol.plan_delete_key', true)) THEN
            RAISE EXCEPTION 'Process history deletion requires complete audited plan deletion' USING ERRCODE = '23514';
          END IF;
          RETURN OLD;
        END $body$;
        CREATE CONSTRAINT TRIGGER sequence_document_delete_audited AFTER DELETE ON operational_sequence_document
          DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION sgol_sequence_plan_delete_audited();
        CREATE CONSTRAINT TRIGGER sequence_event_delete_audited AFTER DELETE ON operational_sequence_event
          DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION sgol_sequence_plan_delete_audited();
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TRIGGER sequence_document_delete_audited ON operational_sequence_document;
        DROP TRIGGER sequence_event_delete_audited ON operational_sequence_event;
        DROP FUNCTION sgol_sequence_plan_delete_audited();
        CREATE OR REPLACE FUNCTION sgol_sequence_append_only() RETURNS trigger LANGUAGE plpgsql AS $body$
        BEGIN
          RAISE EXCEPTION 'Sequence documents and events are append only' USING ERRCODE = '23514';
        END $body$;
        """);
}
