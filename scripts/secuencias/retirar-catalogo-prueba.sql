-- Solo después de revisar la vista previa y restaurar el respaldo actualizado.
-- Baja lógica: no elimina asignaciones, entregas, revisiones, planes ni evidencia.
BEGIN;
SELECT pg_advisory_xact_lock(734651001);
SELECT set_config('sgol.expected_catalog_hash', :'expected_catalog_hash', true);
DO $guard$
DECLARE actual text;
BEGIN
  SELECT md5(coalesce(string_agg(row_to_json(t)::text, '' ORDER BY t."Id"), '')) INTO actual FROM operational_task t WHERE NOT t."Deleted";
  IF actual <> current_setting('sgol.expected_catalog_hash') THEN
    RAISE EXCEPTION 'El catálogo cambió desde la vista previa. No se retiró ninguna tarea.';
  END IF;
END $guard$;
INSERT INTO audit_event(id,occurred_at,actor_user_id,actor_type,action,resource_type,resource_id,branch_id,correlation_id,request_id,before_data,after_data,reason,outcome)
SELECT gen_random_uuid(),clock_timestamp(),NULL,'SYSTEM','TEST_TASK_CATALOG_RETIRED','OPERATIONAL_TASK',t."Id",
  (SELECT branch_id FROM employment_version LIMIT 1),gen_random_uuid(),'retirar-catalogo-prueba-20261013',to_jsonb(t),
  jsonb_build_object('Active',false,'Deleted',true,'Revision',t."Revision"+1),
  'Solicitud del usuario: retirar las definiciones de prueba y conservar asignaciones, documentos e historial. Las tareas nuevas ajenas al ejemplo no se cargan.',
  'SUCCESS'
FROM operational_task t WHERE NOT t."Deleted";
UPDATE operational_task SET "Deleted"=true,"Active"=false,"Revision"="Revision"+1 WHERE NOT "Deleted";
COMMIT;
