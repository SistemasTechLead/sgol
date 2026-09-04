# F07 Adenda 08 — Contrato de asignación automática para HU-018

## 1. Estado y alcance

Decisión aprobada por el responsable el 2026-09-04 para desbloquear exclusivamente `HU-018 — SGOL asigna por carga y desempates aprobados`.

La aprobación autoriza iniciar la implementación en `codex/hu-018`, pero no autoriza migración no prevista, commit, publicación, pull request ni merge. Tampoco modifica los contratos cerrados de `HU-004` o `HU-016` ni autoriza correcciones de `HU-019`, planificación, conclusión, evidencia, validación, recurrencias, UI o endpoints públicos.

## 2. Comando interno e identidad idempotente

La única entrada de escritura de `HU-018` es el comando interno `AssignObligationCommand`, ejecutado por el actor sistema después de que `HU-016` haya confirmado una evaluación. No forma parte de la API pública y no convierte la evaluación y la asignación en una sola operación.

El comando contiene exactamente:

| Campo | Tipo | Regla |
|---|---|---|
| `assignment_request_id` | `uuid`, no nulo | Identidad idempotente del intento de asignación. |
| `obligation_id` | `uuid`, no nulo | Obligación que se pretende asignar. |
| `eligibility_evaluation_id` | `uuid`, no nulo | Snapshot exacto de `HU-016` que se consume. |
| `correlation_id` | `uuid`, no nulo | Correlación operativa y de auditoría; no forma parte del contenido idempotente. |

El hash idempotente incluye `obligation_id` y `eligibility_evaluation_id`, con serialización canónica y alcance interno `assignment:automatic`.

- El mismo `assignment_request_id` y el mismo contenido devuelve los mismos identificadores y ganador. Si la ejecución inicial creó una asignación, el reintento devuelve `RECUPERADA`; si terminó sin candidato, vuelve a devolver `SIN_CANDIDATO_ELEGIBLE`.
- El mismo `assignment_request_id` con contenido diferente se rechaza con `ASSIGNMENT_IDEMPOTENCY_CONFLICT`, sin modificar obligación, evaluación o asignación.
- Otro `assignment_request_id` para una obligación ya asignada recupera la asignación `VIGENTE/AUTOMATICA` únicamente cuando su explicación referencia la misma `eligibility_evaluation_id`.
- Si la obligación ya tiene una asignación `VIGENTE` producida con otra evaluación o de otro tipo, se rechaza con `ASSIGNMENT_ALREADY_EXISTS`; nunca se crea una segunda versión.
- Los resultados terminales `CREADA`, `RECUPERADA` y `SIN_CANDIDATO_ELEGIBLE` son recuperables por idempotencia. Un conflicto conserva su auditoría, pero no sustituye el registro perteneciente a una clave ya ocupada.

La respuesta interna contiene `result`, `assignmentRequestId`, `obligationId`, `eligibilityEvaluationId`, `assignmentId` anulable, `winnerPersonId` anulable, `assignedAt` anulable y `errorCode` anulable. No establece un contrato HTTP.

## 3. Evaluación de elegibilidad consumida

El comando consume por ID una evaluación confirmada e inmutable de `HU-016`. Después de bloquear la obligación debe comprobar que:

1. la evaluación existe y pertenece exactamente a `obligation_id`;
2. es la última evaluación confirmada de esa obligación, ordenada por `evaluated_at` descendente y después por `id` descendente;
3. su resultado es `CANDIDATOS_ELEGIBLES` o `SIN_CANDIDATO_ELEGIBLE`;
4. un resultado `CANDIDATOS_ELEGIBLES` contiene al menos una fila con `is_eligible = true`; y
5. un resultado `SIN_CANDIDATO_ELEGIBLE` no contiene ninguna fila con `is_eligible = true`.

Una evaluación anterior se rechaza con `ASSIGNMENT_EVALUATION_STALE`. Una evaluación de otra obligación se rechaza con `ASSIGNMENT_EVALUATION_MISMATCH`. Una evaluación inexistente se rechaza con `ASSIGNMENT_EVALUATION_NOT_FOUND`. Un resultado o cardinalidad incompatibles se rechazan con `ASSIGNMENT_EVALUATION_INCOMPATIBLE`.

Sólo participan las filas de esa evaluación con `is_eligible = true`. Las personas excluidas nunca entran al ranking.

La elegibilidad no se recalcula al asignar. Cambios posteriores a `evaluated_at` en persona, empleo, cuenta, rol, disponibilidad, turno o política no alteran el snapshot consumido. Esta regla no impide recalcular la carga activa ni derivar la última asignación automática en el instante de selección.

## 4. Inmutabilidad de HU-016

`HU-018` no actualiza `eligibility_evaluation.winner_person_id` ni `eligibility_candidate.active_load`, `eligibility_candidate.last_auto_assignment_at` o `eligibility_candidate.rank`. Esos campos permanecen nulos y se conserva la protección PostgreSQL incorporada por `HU-016`.

No se crea una segunda evaluación ni un snapshot adicional de elegibilidad. La carga, la última asignación automática, el orden y el ganador se conservan exclusivamente en `assignment_version.explanation`.

## 5. Instante de corte, carga y última asignación automática

Después de adquirir todos los bloqueos de selección se captura `IClock.UtcNow` exactamente una vez. Ese instante UTC es simultáneamente `calculatedAt` y `assignedAt`.

La carga activa se vuelve a leer dentro de la transacción conforme a `F07_ADENDA_07_CONTRATO_DE_CARGA_ACTIVA_HU_004.md`: cuenta obligaciones `PENDIENTE` con asignación `VIGENTE`, una vez por obligación. Una obligación `CONCLUIDA` y una asignación `SUSTITUIDA` cuentan cero. La consulta interna reutiliza la misma definición y no persiste un contador ni modifica `eligibility_candidate`.

Para cada persona elegible, `lastAutoAssignmentAt` es el máximo `assigned_at` no posterior a `calculatedAt` entre todas sus versiones con `assignment_type = AUTOMATICA`, tanto `VIGENTE` como `SUSTITUIDA`. Una `CORRECCION` nunca sustituye esa fecha.

La ausencia de asignaciones automáticas se representa con `lastAutoAssignmentAt = null` y significa la mayor espera posible. Por tanto, a igual carga, una persona nunca asignada automáticamente precede a cualquier persona con fecha; entre fechas no nulas, la más antigua precede a la más reciente.

## 6. Ranking determinista

Todos los candidatos elegibles se ordenan por la tupla siguiente:

1. `activeLoad`, entero ascendente;
2. indicador `lastAutoAssignmentAt is null`, con nulos primero;
3. `lastAutoAssignmentAt`, instante ascendente para valores no nulos;
4. `stableCode` copiado por `HU-016`, comparación ordinal ascendente.

`person.stable_code` es único, por lo que no se agrega otro desempate técnico. El primer elemento es el ganador. Un único candidato elegible recibe rango 1 directamente, sin omitir el cálculo ni la explicación.

## 7. Concurrencia y orden de bloqueos

La operación usa una sola transacción PostgreSQL `READ COMMITTED`, de acuerdo con el aislamiento ordinario de F06. Las preconsultas sólo pueden optimizar; nunca son autoridad de idempotencia, unicidad o selección.

El orden obligatorio es:

1. bloquear `work_obligation` mediante `SELECT ... FOR UPDATE`;
2. comprobar que existe, pertenece a `LOR-001`, permanece `PENDIENTE` y no tiene otra asignación `VIGENTE` incompatible;
3. cargar y validar la evaluación exacta y sus candidatos elegibles;
4. bloquear las filas `person` de todos los candidatos elegibles mediante `SELECT ... FOR UPDATE`, en orden `stableCode` ordinal y después `person_id`;
5. capturar una vez el instante de corte;
6. volver a leer en PostgreSQL la carga activa y la última asignación automática de todos los candidatos;
7. ordenar, insertar `assignment_version`, idempotencia y auditoría; y
8. confirmar.

Los bloqueos ordenados de todas las personas candidatas serializan dos selecciones concurrentes cuyos universos se superponen y obligan a la segunda a releer las cargas después de la primera confirmación. No se bloquean ni actualizan filas de `eligibility_candidate`. Las asignaciones existentes se leen dentro de la transacción; el bloqueo de la obligación y el índice único parcial de `assignment_version` resuelven solicitudes concurrentes sobre la misma obligación.

El índice único parcial de HU-004 sigue siendo la autoridad final de una sola asignación `VIGENTE` por obligación. Las violaciones de unicidad se resuelven recuperando el resultado compatible o devolviendo `ASSIGNMENT_ALREADY_EXISTS`.

Los SQLSTATE `40P01` y `40001` permiten como máximo dos reintentos adicionales —tres intentos totales— con el mismo `assignment_request_id`. Agotarlos devuelve `ASSIGNMENT_CONCURRENCY_CONFLICT`, sin efectos parciales.

## 8. Ausencia de candidato y demás rechazos

Cuando la evaluación exacta tiene resultado `SIN_CANDIDATO_ELEGIBLE`, el comando devuelve:

- `result = SIN_CANDIDATO_ELEGIBLE`;
- `errorCode = SIN_CANDIDATO_ELEGIBLE`;
- `assignmentId = null`;
- `winnerPersonId = null`; y
- `assignedAt = null`.

El resultado se conserva en el registro de idempotencia y en auditoría. No crea `assignment_version`, no introduce tabla o estado adicional y mantiene la obligación `PENDIENTE`. Un reintento idéntico recupera el mismo resultado.

Una obligación inexistente devuelve `ASSIGNMENT_OBLIGATION_NOT_FOUND`; una obligación fuera de `LOR-001`, no `PENDIENTE` o no asignable devuelve `ASSIGNMENT_OBLIGATION_NOT_ASSIGNABLE`. Todos los rechazos carecen de efectos sobre obligación, evaluación, candidatos, personas, organización, políticas y planes.

## 9. Esquema exacto de `assignment_version.explanation`

La explicación es un objeto JSON `camelCase`, con `schemaVersion = 1`, y contiene exactamente esta forma:

```json
{
  "schemaVersion": 1,
  "eligibilityEvaluationId": "019...",
  "calculatedAt": "2026-09-04T18:00:00Z",
  "orderingRules": [
    "ACTIVE_LOAD_ASC",
    "NEVER_AUTOMATICALLY_ASSIGNED_FIRST",
    "LAST_AUTO_ASSIGNMENT_AT_ASC",
    "STABLE_CODE_ORDINAL_ASC"
  ],
  "candidates": [
    {
      "personId": "019...",
      "stableCode": "EMP-001",
      "activeLoad": 0,
      "lastAutoAssignmentAt": null,
      "rank": 1
    }
  ],
  "winner": {
    "personId": "019...",
    "stableCode": "EMP-001",
    "rank": 1
  },
  "decisiveRule": "ONLY_ELIGIBLE_CANDIDATE"
}
```

`candidates` incluye todos y sólo los elegibles, ya ordenados, con rango entero consecutivo desde 1. `lastAutoAssignmentAt` es un instante UTC ISO 8601 o `null`.

`decisiveRule` admite exclusivamente `ONLY_ELIGIBLE_CANDIDATE`, `ACTIVE_LOAD`, `LAST_AUTO_ASSIGNMENT_AT` o `STABLE_CODE`; identifica la primera regla que distingue al ganador del segundo candidato. La explicación no copia razones de exclusión, empleo, puesto, rol, disponibilidad, política completa ni contenido de `input_snapshot`, pues permanecen referenciados por `eligibilityEvaluationId`.

## 10. Asignación creada

La fila creada usa:

- `status = VIGENTE`;
- `assignment_type = AUTOMATICA`;
- `person_id` igual al ganador;
- `assigned_at` igual a `calculatedAt`;
- `reason = null`;
- `assigned_by = null`; y
- `supersedes_id = null`.

La obligación permanece `PENDIENTE`. No se crea corrección, plan, versión de plan, asociación a plan, recurrencia ni otro efecto funcional.

## 11. Auditoría y actor sistema

Todos los eventos usan `actor_type = SYSTEM`, `actor_user_id = null`, el `correlation_id` del comando y `request_id = assignment_request_id` en formato UUID canónico.

| Resultado | `action` | `resource_type` | `outcome` |
|---|---|---|---|
| Creación | `AUTOMATIC_ASSIGNMENT_CREATED` | `ASSIGNMENT_VERSION` | `CREADA` |
| Recuperación | `AUTOMATIC_ASSIGNMENT_RECOVERED` | `ASSIGNMENT_VERSION` | `RECUPERADA` |
| Sin candidato | `AUTOMATIC_ASSIGNMENT_NOT_CREATED` | `WORK_OBLIGATION` | `SIN_CANDIDATO_ELEGIBLE` |
| Rechazo o conflicto | `AUTOMATIC_ASSIGNMENT_REJECTED` | `WORK_OBLIGATION` | Código literal del error |

Para creación, `beforeData` contiene únicamente `schemaVersion`, `obligationId` y `assignmentId = null`; `afterData` contiene `schemaVersion`, `assignmentId`, `obligationId`, `personId`, `eligibilityEvaluationId`, `status` y `assignmentType`.

Para recuperación, ausencia de candidato o rechazo, `beforeData` es nulo y `afterData` contiene únicamente `schemaVersion`, `obligationId`, `eligibilityEvaluationId`, `assignmentId` anulable y `result` o `errorCode`. No se registran snapshots completos, nombres, textos laborales, razones de exclusión ni la explicación completa.

La creación de asignación, su registro idempotente y el evento de creación se confirman o revierten en la misma transacción PostgreSQL. `SIN_CANDIDATO_ELEGIBLE` confirma conjuntamente idempotencia y auditoría sin asignación. Una recuperación o rechazo confirma únicamente su auditoría y, cuando la clave todavía no existe, su resultado idempotente; nunca altera la asignación existente.

## 12. Integridad, efectos prohibidos y migración

La implementación debe reutilizar `assignment_version`, sus restricciones e índices de HU-004, `AuditTransaction`, `IClock` y la infraestructura común de idempotencia. No duplica la definición de carga activa cuando pueda exponerse como consulta interna segura del módulo `Assignment`.

No modifica obligación, evaluación, candidatos, persona, empleo, cuenta, rol, disponibilidad o política. Puesto, nombre laboral, turno no configurado y texto parecido no participan en elegibilidad ni ranking.

No se prevé migración. Una necesidad de esquema descubierta durante la implementación requiere detenerse y obtener una decisión adicional; no autoriza eliminar o relajar restricciones de HU-004 o HU-016.

## 13. Pruebas y eficacia

Las pruebas de HU-018 deben cubrir los casos positivos, negativos, idempotentes, de no efecto, explicación, auditoría y concurrencia exigidos por su instrucción de implementación. Persistencia, restricciones, bloqueos y concurrencia se verifican exclusivamente con PostgreSQL real.

Esta adenda es la fuente contractual específica de HU-018 junto con F05, F06 y las adendas 06 y 07. Commit, publicación, pull request y merge permanecen como decisiones independientes.
