# Bandeja propia y avisos internos de HU-030

El contrato normativo es `F07_ADENDA_22_CONTRATO_DE_BANDEJA_PROPIA_Y_AVISOS_INTERNOS_HU_030.md`. El host expone únicamente `GET /api/v1/me/inbox` y `POST /api/v1/me/notices/{id}/read`; no existe UI en este corte.

## Lectura de bandeja

La consulta requiere sesión autenticada con MFA, cuenta, empleo y rol vigentes en `LOR-001`, y `PER-BANDEJA-PROPIA`. `/me` nunca amplía alcance por jerarquía. El lector captura una sola vez el reloj y abre una transacción PostgreSQL `REPEATABLE READ, READ ONLY`. Las tareas se filtran por asignación `VIGENTE` de la persona autenticada; los avisos, por `recipient_user_id`.

Las páginas de tareas y avisos tienen cursores y límites independientes. La política congelada de cada obligación se evalúa mediante `EvidenceReviewEvaluator` y sólo con evidencia `VIGENTE`; la consulta no crea snapshots, auditoría, resultados ni efectos de dominio y no lee contenido de archivos. Las respuestas llevan `Cache-Control: private, no-store`.

## Producción y lectura de avisos

El catálogo contiene sólo `OBLIGATION_ASSIGNED` sobre `ASSIGNMENT_VERSION`. Los servicios de asignación automática y corrección agregan el aviso para la cuenta activa del nuevo responsable dentro de su transacción existente. PostgreSQL exige una fila coherente y única por asignación nueva; no hay backfill, outbox, scheduler ni canal externo.

La marcación de lectura exige CSRF en sesión de navegador, usa `SERIALIZABLE` y bloquea el aviso propio `FOR UPDATE`. La primera llamada confirma `read_at` y `INTERNAL_NOTICE_READ` en una sola transacción; una repetición devuelve el instante original sin otra escritura. No se admiten cuerpo, query, `Idempotency-Key`, ETag ni `If-Match`.

## Observabilidad y privacidad

Las métricas `sgol_inbox_queries_total`, `sgol_inbox_query_duration_seconds` y `sgol_internal_notices_total` usan sólo etiquetas de resultado u operación de baja cardinalidad. No deben registrarse usuarios, recursos, tareas, evidencia, cursores, payloads, URLs, hashes, claves, cookies o tokens CSRF.

La comprobación PostgreSQL externa se documenta en `docs/operations/postgresql-local.md`. No debe ejecutarse con SQLite ni requiere S3, SeaweedFS o ClamAV.
