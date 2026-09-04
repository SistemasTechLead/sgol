# F07 Adenda 06 — Contrato de elegibilidad para HU-016

## 1. Estado y alcance

Decisión aprobada por el responsable el 2026-09-03 para desbloquear exclusivamente `HU-016 — SGOL calcula candidatos y explica exclusiones`. Esta adenda no autoriza ranking, carga activa, desempate, ganador, asignación ni planificación.

## 2. Fecha e instante

- `eligibility_date` es la fecha local usada para consultar disponibilidad.
- El disparador interno la proporciona: una solicitud manual usa la fecha local de su aceptación y una recurrencia usa la fecha local de la ocurrencia programada.
- Un cliente y `GET /api/v1/obligations/{id}/eligibility` no suministran ni sustituyen la fecha.
- `evaluated_at` es el instante UTC real en que comienza la evaluación.
- Persona, empleo y rol se resuelven en `evaluated_at`; disponibilidad se resuelve exactamente para `eligibility_date`.
- La política se resuelve por `work_obligation.task_definition_version_id` y por el intervalo de vigencia que contiene `generation_request.requested_at`; nunca se sustituye por la política actualmente vigente.
- El snapshot conserva la fecha y su origen literal: `MANUAL_REQUEST` o `SCHEDULED_OCCURRENCE`.

## 3. Identidad, reintento e inmutabilidad

- El comando interno recibe `evaluation_request_id`.
- El mismo identificador y contenido recupera la misma evaluación.
- El mismo identificador con contenido distinto es conflicto.
- Otro identificador crea una reevaluación nueva e inmutable; no existe unicidad por obligación, política o fecha.
- La consulta devuelve la última evaluación confirmada, ordenada por `evaluated_at` y después por `id`.
- El `GET` es sólo lectura y nunca calcula una evaluación.

## 4. Universo y razones

Se evalúan todas las personas registradas en el universo organizacional de la obligación y se acumulan todos los incumplimientos en orden estable. El catálogo literal aprobado es:

- `PERSONA_INACTIVA`
- `EMPLEO_NO_VIGENTE`
- `SUCURSAL_NO_COINCIDE`
- `ROL_ACTIVO_AUSENTE`
- `ROL_REQUERIDO_NO_COINCIDE`
- `DISPONIBILIDAD_AUSENTE`
- `DISPONIBILIDAD_NO_POSITIVA`
- `TURNO_NO_COINCIDE`

La política ausente, duplicada o no correspondiente a la versión TAR exacta aborta la evaluación completa; no se registra como incumplimiento de una persona.

## 5. Resultado y campos reservados

`eligibility_evaluation.result` admite solamente `CANDIDATOS_ELEGIBLES` y `SIN_CANDIDATO_ELEGIBLE`.

En `HU-016`, `winner_person_id`, `active_load`, `last_auto_assignment_at` y `rank` permanecen nulos. `stable_code` se copia como entrada del snapshot y no constituye ranking.

`SIN_CANDIDATO_ELEGIBLE` conserva la evaluación, todas las exclusiones y la auditoría, mantiene la obligación `PENDIENTE` y no crea responsable ni `assignment_version`.

## 6. Consulta y seguridad

`GET /api/v1/obligations/{id}/eligibility` exige `PER-ASIGNACION-EXPLICAR` y aplica alcance jerárquico en servidor. Devuelve evaluación, fecha, instante, política exacta, rol y turno requeridos, resultado, candidatos, exclusiones, entradas utilizadas y razones.

Una obligación inexistente, fuera del alcance o sin snapshot visible devuelve `404` sin revelar cuál de esas condiciones ocurrió. Falta de autenticación devuelve `401`; una sesión sin el permiso requerido devuelve `403`.

## 7. Transacción

La evaluación es un comando interno posterior a la materialización de la obligación. Evaluación, candidatos, registro de idempotencia y auditoría se confirman en una sola transacción PostgreSQL. Una falla antes del commit no deja ninguno de esos efectos parciales y no modifica la obligación ni sus fuentes de elegibilidad.
