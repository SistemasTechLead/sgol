-- Run only inside sgol-performance-postgres-1 after the synthetic load fixture.
DO $$
DECLARE perf_person uuid := gen_random_uuid(); perf_user uuid := gen_random_uuid();
BEGIN
  IF current_database() <> 'sgol_task_checks_performance' THEN RAISE EXCEPTION 'Dedicated performance database required'; END IF;
  IF EXISTS (SELECT 1 FROM identity_credential WHERE normalized_user_name='PERF-BROWSER') THEN RETURN; END IF;
  INSERT INTO person(id,stable_code,display_name,created_at)
    VALUES(perf_person,'perf-browser','Synthetic browser performance',now()-interval '1 year');
  INSERT INTO app_user(id,person_id,status,must_change_password,security_stamp)
    VALUES(perf_user,perf_person,'ACTIVA',false,gen_random_uuid()::text);
  INSERT INTO identity_credential(user_id,user_name,normalized_user_name,password_hash)
    SELECT perf_user,'perf-browser','PERF-BROWSER',password_hash FROM identity_credential WHERE normalized_user_name='PERF-WORKER';
  INSERT INTO employment_version(id,person_id,branch_id,status,valid_from,row_version)
    SELECT gen_random_uuid(),perf_person,branch_id,'ACTIVA',now()-interval '1 year',1 FROM employment_version WHERE valid_to IS NULL LIMIT 1;
  INSERT INTO role_assignment_version(id,user_id,branch_id,role_code,status,valid_from)
    SELECT gen_random_uuid(),perf_user,branch_id,'DIRECCION','ACTIVO',now()-interval '1 year' FROM role_assignment_version LIMIT 1;
END $$;
