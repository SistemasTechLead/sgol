using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Sgol.Web.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SgolDbContext))]
[Migration("20261015231000_PersonalScheduleIntegrity")]
public sealed class PersonalScheduleIntegrity : Migration
{
    protected override void Up(MigrationBuilder b) => b.Sql("""
        CREATE EXTENSION IF NOT EXISTS btree_gist;
        CREATE FUNCTION sgol_work_intervals_valid(spans jsonb) RETURNS boolean LANGUAGE plpgsql IMMUTABLE AS $$
        DECLARE span jsonb; entry_min integer; exit_min integer; previous_exit integer := -1;
        BEGIN
            IF jsonb_typeof(spans) <> 'array' OR jsonb_array_length(spans) > 4 THEN RETURN false; END IF;
            FOR span IN SELECT value FROM jsonb_array_elements(spans) ORDER BY (value->>'Entry')::integer LOOP
                entry_min := (span->>'Entry')::integer; exit_min := (span->>'Exit')::integer;
                IF entry_min IS NULL OR exit_min IS NULL OR entry_min < 0 OR exit_min > 1440 OR entry_min >= exit_min OR entry_min < previous_exit THEN RETURN false; END IF;
                previous_exit := exit_min;
            END LOOP;
            RETURN true;
        EXCEPTION WHEN OTHERS THEN RETURN false; END $$;
        CREATE FUNCTION sgol_work_week_valid(days jsonb) RETURNS boolean LANGUAGE plpgsql IMMUTABLE AS $$
        DECLARE day record;
        BEGIN
            IF jsonb_typeof(days) <> 'object' THEN RETURN false; END IF;
            FOR day IN SELECT * FROM jsonb_each(days) LOOP
                IF day.key NOT IN ('0','1','2','3','4','5','6') OR NOT sgol_work_intervals_valid(day.value) THEN RETURN false; END IF;
            END LOOP;
            RETURN true;
        EXCEPTION WHEN OTHERS THEN RETURN false; END $$;
        ALTER TABLE personal_schedule_version ADD CONSTRAINT "CK_schedule_days" CHECK (sgol_work_week_valid("DaysJson") AND "Revision" > 0 AND "Zone" = 'America/Mexico_City');
        ALTER TABLE personal_schedule_version ADD CONSTRAINT "EX_schedule_effective" EXCLUDE USING gist ("PersonId" WITH =, daterange("From", "Through", '[]') WITH &&) WHERE (NOT "Superseded") DEFERRABLE INITIALLY DEFERRED;
        ALTER TABLE personal_schedule_exception ADD CONSTRAINT "CK_schedule_exception_intervals" CHECK (sgol_work_intervals_valid("IntervalsJson") AND "Revision" > 0 AND length(btrim("Reason")) > 0);
        ALTER TABLE closing_coverage_version ADD CONSTRAINT "CK_closing_coverage_reason" CHECK ("Revision" > 0 AND length(btrim("Reason")) > 0);
        ALTER TABLE temporal_assignment_state ADD CONSTRAINT "CK_temporal_state_singleton" CHECK ("Id" = 1 AND "Revision" > 0);
        ALTER TABLE closing_rotation_version ADD CONSTRAINT "CK_rotation_participants" CHECK ("Revision" > 0 AND jsonb_typeof("ParticipantsJson") = 'array' AND jsonb_array_length("ParticipantsJson") > 0 AND extract(dow from "Anchor") = "WeekDay");
        ALTER TABLE personal_schedule_version ADD CONSTRAINT "FK_schedule_author" FOREIGN KEY ("ChangedBy") REFERENCES app_user(id) ON DELETE RESTRICT;
        ALTER TABLE personal_schedule_exception ADD CONSTRAINT "FK_schedule_exception_author" FOREIGN KEY ("ChangedBy") REFERENCES app_user(id) ON DELETE RESTRICT;
        ALTER TABLE closing_rotation_version ADD CONSTRAINT "FK_rotation_author" FOREIGN KEY ("ChangedBy") REFERENCES app_user(id) ON DELETE RESTRICT;
        ALTER TABLE closing_coverage_version ADD CONSTRAINT "FK_coverage_author" FOREIGN KEY ("ChangedBy") REFERENCES app_user(id) ON DELETE RESTRICT;
        CREATE FUNCTION sgol_schedule_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'Schedule history is immutable' USING ERRCODE='23514'; END IF;
            IF TG_TABLE_NAME <> 'personal_schedule_version' OR (to_jsonb(OLD) - ARRAY['Through','Superseded']) <> (to_jsonb(NEW) - ARRAY['Through','Superseded']) THEN
                RAISE EXCEPTION 'Create another schedule revision' USING ERRCODE='23514';
            END IF;
            RETURN NEW;
        END $$;
        CREATE TRIGGER personal_schedule_immutable BEFORE UPDATE OR DELETE ON personal_schedule_version FOR EACH ROW EXECUTE FUNCTION sgol_schedule_immutable();
        CREATE TRIGGER personal_schedule_exception_immutable BEFORE UPDATE OR DELETE ON personal_schedule_exception FOR EACH ROW EXECUTE FUNCTION sgol_schedule_immutable();
        CREATE TRIGGER closing_rotation_immutable BEFORE UPDATE OR DELETE ON closing_rotation_version FOR EACH ROW EXECUTE FUNCTION sgol_schedule_immutable();
        CREATE TRIGGER closing_coverage_immutable BEFORE UPDATE OR DELETE ON closing_coverage_version FOR EACH ROW EXECUTE FUNCTION sgol_schedule_immutable();
        """);
    protected override void Down(MigrationBuilder b) => b.Sql("""
        DO $$ BEGIN
          IF EXISTS(SELECT 1 FROM personal_schedule_version) OR EXISTS(SELECT 1 FROM personal_schedule_exception)
             OR EXISTS(SELECT 1 FROM closing_rotation_version) OR EXISTS(SELECT 1 FROM closing_coverage_version)
             OR EXISTS(SELECT 1 FROM operational_assignment WHERE "TemporalDecisionJson" IS NOT NULL OR "TemporalPolicyJson" IS NOT NULL) THEN
            RAISE EXCEPTION 'Preserve schedule history: disable temporal policy instead of downgrading';
          END IF;
        END $$;
        DROP TRIGGER personal_schedule_immutable ON personal_schedule_version;
        DROP TRIGGER personal_schedule_exception_immutable ON personal_schedule_exception;
        DROP TRIGGER closing_rotation_immutable ON closing_rotation_version;
        DROP TRIGGER closing_coverage_immutable ON closing_coverage_version;
        DROP FUNCTION sgol_schedule_immutable();
        ALTER TABLE personal_schedule_version DROP CONSTRAINT "CK_schedule_days", DROP CONSTRAINT "EX_schedule_effective", DROP CONSTRAINT "FK_schedule_author";
        ALTER TABLE personal_schedule_exception DROP CONSTRAINT "CK_schedule_exception_intervals", DROP CONSTRAINT "FK_schedule_exception_author";
        ALTER TABLE closing_rotation_version DROP CONSTRAINT "CK_rotation_participants", DROP CONSTRAINT "FK_rotation_author";
        ALTER TABLE closing_coverage_version DROP CONSTRAINT "CK_closing_coverage_reason", DROP CONSTRAINT "FK_coverage_author";
        ALTER TABLE temporal_assignment_state DROP CONSTRAINT "CK_temporal_state_singleton";
        DROP FUNCTION sgol_work_week_valid(jsonb);
        DROP FUNCTION sgol_work_intervals_valid(jsonb);
        """);
}
