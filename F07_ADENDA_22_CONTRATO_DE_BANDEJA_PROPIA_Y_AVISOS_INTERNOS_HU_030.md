# SGOL — Adenda 22 a F07: contrato de bandeja propia y avisos internos para `HU-030`

## 1. Control del documento

| Campo | Valor |
|---|---|
| Tipo | Propuesta de adenda normativa a `F07_BACKLOG_DE_IMPLEMENTACION.md` |
| Estado | `APROBADA ÍNTEGRAMENTE; IMPLEMENTACIÓN EN CURSO` |
| Fecha | 2026-09-09 |
| Historia | `HU-030 — Responsable ve bandeja propia y avisos internos` |
| Corte y épica | `CV-03` / `EP-06` |
| Criterios | `CA-030`, `CP-030-P`, `CP-030-N`, `RN-019`, `RN-025`, `RN-029` |
| Efecto pretendido | Precisar el contrato ejecutable de `HU-030` sin modificar ni renumerar el backlog aprobado |
| Base verificada | `origin/master = 10ef4c70515549d78f967ca5f70aa27be591c91f`, merge de `HU-022`; rama `codex/hu-030` creada desde ese corte |
| Conservación | Mantiene sin cambios F00–F07, `Fuentes/`, `docs/design/logo.svg`, `docs/design/mapa-pantallas.md` y los tres Designer ajenos identificados para esta tarea |
| Aprobación | Aprobación íntegra recibida del responsable el 2026-09-09 |

La aprobación íntegra autoriza exclusivamente la implementación aquí descrita; no autoriza commit, publicación, pull request ni merge.

## 2. Precedencia y orden efectivo

Se reconocen como hechos de cierre ya satisfechos:

1. `HU-023`: Adenda 15 aprobada; PR `#38`; commit `de8e9d746687e892e5d4baf1d23ed0745aa4865b`; check `TECH-BASE-003 / PR gates` `SUCCESS`, run `33986965205`, sobre ese SHA; aceptación humana; PostgreSQL `3/3`; merge `8a411596086fe1c0a95ec3c04f6c3ae8542b76c6`; commit y merge ancestros de `origin/master`; cero defectos bloqueantes conocidos.
2. `HU-024`, `TECH-EVID-001`, `HU-025`, `TECH-EVID-002` y `HU-026`: cerradas conforme a sus adendas, PR, checks exactos, pruebas externas, aceptación, merges y registros vigentes.
3. `HU-022`: Adenda 21 aprobada íntegramente; PR `#44` `MERGED`; commit `20849773725a63c976a9dcd636d1c3fc75bbf9a3`; check `TECH-BASE-003 / PR gates` `SUCCESS`, run `34300222101`, sobre ese SHA; aceptación humana; PostgreSQL `6/6`; merge `10ef4c70515549d78f967ca5f70aa27be591c91f`; `origin/master` exactamente en ese merge; commit y merge ancestros; cero defectos bloqueantes conocidos.

La tabla `Tareas insertadas por adenda` no contiene una tarea pendiente que bloquee `HU-030`. La tabla `Dependencias de infraestructura por momento` no agrega una dependencia anterior a esta historia. `TECH-E2E-CV-03` corresponde al cierre de `CV-03`, no es dependencia previa de `HU-030`. No existe una adenda posterior a `F07_ADENDA_21` en la base verificada.

El orden efectivo es:

```text
HU-026 → HU-022 → HU-030 → HU-027 → HU-028
```

Esta adenda no adelanta `HU-027`, `HU-028`, `HU-029`, `HU-031`, `HU-032`, `TECH-E2E-CV-03` ni otra historia posterior.

## 3. Fuentes, interpretación y permiso estable

La propuesta se limita a `HU-030`, `CAP-042`, `CA-030`, `CP-030-P`, `CP-030-N`, `RN-019`, `RN-025`, `RN-029`, `DEC-041`, `DEC-066`, `DEC-069`, los límites F04 directamente aplicables, los contratos F06 de bandeja, avisos, seguridad, persistencia y pruebas, las adendas 15 a 21 en lo aplicable y la implementación fusionada directamente reutilizable.

`CAP-042` conserva la referencia genérica legacy `CONSULTAR_OPERACION`. La especificación posterior de `HU-030` y el contrato F06 fijan el permiso estable `PER-BANDEJA-PROPIA`. Para esta historia prevalece `PER-BANDEJA-PROPIA`; `CONSULTAR_OPERACION` no se implementa como alias, permiso adicional ni mecanismo alternativo. Los permisos propios de acciones de dominio permanecen separados.

`DEC-069` no amplía `/me`: sólo gobierna consultas jerárquicas que expresamente lo permiten. `HU-030` es una bandeja personal. Un superior o `DIRECCION` no obtiene mediante `/me` tareas ni avisos de otra persona.

## 4. Resultado cerrado y exclusiones

`HU-030` agrega exclusivamente:

```http
GET  /api/v1/me/inbox
POST /api/v1/me/notices/{id}/read
```

La consulta proyecta tareas propias y avisos internos del destinatario. La marcación cambia una sola vez `read_at` y registra auditoría atómica. La historia agrega la tabla `internal_notice` y un único productor funcional vinculado a asignaciones confirmadas.

No se agrega tabla materializada de bandeja. No se concluye, valida, aporta, sustituye ni inspecciona evidencia; no se crea `evidence_review_snapshot`; no se reabre, cancela, posterga o traslada una obligación; no se crea `validation_requirement`, `validation_decision_version`, supervisión, indicadores, correo, SMS, push, webhook, outbox, scheduler, broker, Redis, microservicio ni motor general de reglas. No se invocan S3, SeaweedFS o ClamAV.

## 5. Actor, permiso y alcance propio

Cada petición captura exactamente una vez `queriedAt = IClock.UtcNow`. En ese instante el actor debe tener simultáneamente:

1. sesión individual autenticada con MFA completo;
2. `app_user` `ACTIVA` vinculada a una persona;
3. empleo `ACTIVA` y vigente en `LOR-001`;
4. exactamente un rol canónico `ACTIVO` y vigente en `LOR-001`; y
5. permiso efectivo `PER-BANDEJA-PROPIA`.

Los cuatro roles canónicos reciben este permiso exclusivamente para su propio recurso `/me`. Cuenta, empleo o rol ausente, inactivo, sustituido, múltiple, desconocido o fuera de vigencia produce `403 ACCESO_DENEGADO`. Puesto, turno, jerarquía, publicación, responsabilidad histórica o datos del cliente no conceden acceso.

La sección de tareas contiene sólo obligaciones cuya única `assignment_version` `VIGENTE` en `queriedAt` pertenece a la persona del actor. La sección de avisos contiene sólo filas cuyo `recipient_user_id` coincide con el actor. Pares, superiores, inferiores, `DIRECCION` y usuarios ajenos no amplían este universo.

La autorización se aplica en SQL antes de materializar tareas, evidencia o avisos. Un UUID de aviso inexistente, de otra sucursal, de otro destinatario o fuera del alcance converge indistinguiblemente en `404 AVISO_NO_ENCONTRADO`. Una acción proyectada nunca constituye autorización para ejecutar su endpoint; cada comando revalida permiso, recurso, responsable y estado.

## 6. Solicitud de bandeja y filtros

`GET /api/v1/me/inbox` no admite cuerpo, `Idempotency-Key`, `If-Match` ni CSRF. Acepta exclusivamente:

| Parámetro | Regla |
|---|---|
| `periodId` | UUID canónico de `week_period` de `LOR-001`; si se omite, se usa el período ISO que contiene la fecha local de `queriedAt` en `America/Mexico_City`, sin crearlo ni modificarlo |
| `taskState` | `FUTURA`, `DISPONIBLE`, `VENCIDA` o `CONCLUIDA`; opcional |
| `taskCursor` | Cursor opaco no vacío emitido para la misma sección y los mismos filtros |
| `taskLimit` | Entero de `1` a `100`; predeterminado `25` |
| `noticeStatus` | `ALL`, `UNREAD` o `READ`; predeterminado `ALL` |
| `noticeCursor` | Cursor opaco no vacío emitido para la misma sección y los mismos filtros |
| `noticeLimit` | Entero de `1` a `100`; predeterminado `25` |

Cada parámetro aparece como máximo una vez. Un parámetro desconocido, repetido, vacío, inválido, cursor de otra sección, versión o filtros, o límite fuera de rango devuelve `400 FILTRO_BANDEJA_INVALIDO` sin efectos.

Si el `periodId` válido no existe o no pertenece a `LOR-001`, la sección de tareas queda vacía sin confirmar existencia fuera de alcance; los avisos se consultan normalmente. Si se omite y la base no contiene un único período coherente para la semana calculada, se devuelve `409 BANDEJA_INCONSISTENTE` y no se ofrece una proyección parcial.

## 7. Respuesta exacta de bandeja

Una respuesta `200` tiene exactamente esta forma:

```json
{
  "data": {
    "period": {
      "periodId": "019...",
      "isoYear": 2026,
      "isoWeek": 37,
      "startsOn": "2026-09-07",
      "endsOn": "2026-09-13",
      "timeZone": "America/Mexico_City"
    },
    "tasks": { "items": [], "nextCursor": null, "count": 0, "isEmpty": true },
    "notices": { "items": [], "nextCursor": null, "count": 0, "isEmpty": true },
    "isEmpty": true
  },
  "meta": {
    "queriedAt": "2026-09-09T12:00:00Z",
    "correlationId": "019..."
  }
}
```

`period` tiene el objeto mostrado cuando el período existe y es `null` cuando se proporcionó un `periodId` canónico que no existe en `LOR-001`. `count` es sólo el número de elementos de la página correspondiente, nunca un total global. `data.isEmpty` es verdadero únicamente cuando ambas páginas están vacías. Instantes usan RFC 3339 UTC con `Z`, fechas `YYYY-MM-DD`, UUID canónico `D` minúsculo y propiedades JSON `camelCase`.

## 8. DTO exacto de tarea

Cada elemento de `tasks.items` contiene exactamente:

```text
obligationId: uuid
task: { taskDefinitionId: uuid, taskCode: TAR-####, name: string }
period: {
  periodId: uuid,
  isoYear: integer,
  isoWeek: integer,
  startsOn: local-date,
  endsOn: local-date,
  timeZone: America/Mexico_City
}
originKind: MANUAL|RECURRENTE
scheduled: boolean
dates: { dueAt: instant|null, dueLocalDate: local-date|null, concludedAt: instant|null }
executionStatus: PENDIENTE|CONCLUIDA
taskState: FUTURA|DISPONIBLE|VENCIDA|CONCLUIDA
evidence: {
  result: COMPLETA|INCOMPLETA,
  missingRequirements: missing-requirement[]
}
allowedActions: action-code[]
```

`scheduled` es verdadero sólo cuando `originKind = RECURRENTE`. “Programada” es procedencia, no un tercer estado de ejecución ni un estado temporal: una tarea programada puede ser futura, disponible, vencida o concluida.

Cada `missing-requirement` contiene exactamente `requirementVersionId`, `requirementCode`, `kind`, `conditionCode`, `ordinal` y `missingReason`. `missingReason` sólo admite `EVIDENCIA_VIGENTE_AUSENTE` o `EVIDENCIA_VIGENTE_NO_SATISFACE`.

`action-code` sólo admite, en este orden, `VIEW_TASK`, `CONTRIBUTE_EVIDENCE` y `CONCLUDE_TASK`:

- `VIEW_TASK` requiere el permiso actual `PER-TAREA-VER` y recurso propio;
- `CONTRIBUTE_EVIDENCE` requiere `PENDIENTE`, permiso `PER-EVIDENCIA-APORTAR`, asignación propia vigente y al menos un requisito faltante aportable;
- `CONCLUDE_TASK` requiere `PENDIENTE`, permiso `PER-TAREA-EJECUTAR`, asignación propia vigente y evaluación `COMPLETA`.

La lista es informativa y no contiene URLs ni autoridad delegada. No se proyectan acciones de sustitución, validación, supervisión, Dirección, reapertura, cancelación, postergación o traslado.

## 9. Estado temporal, vencimiento y período

La fecha operativa es `DateOnly` obtenida al convertir el único `queriedAt` a `America/Mexico_City`. `taskState` se calcula en este orden:

1. `CONCLUIDA` si `executionStatus = CONCLUIDA`;
2. `VENCIDA` si está `PENDIENTE`, `dueAt` no es nulo y `dueAt < queriedAt`;
3. `FUTURA` si está `PENDIENTE` y `period.startsOn` es posterior a la fecha operativa;
4. `DISPONIBLE` para cualquier otra obligación `PENDIENTE`.

La igualdad `dueAt == queriedAt` no es vencida. `dueAt = null` no se infiere. `VENCIDA` es una bandera derivada representada por `taskState`; no modifica `executionStatus`, no crea validación, cancelación, traslado ni excepción. `FUTURA` es clasificación de presentación y no prohíbe la conclusión anticipada ya aprobada por `HU-022`.

## 10. Evaluación informativa de evidencia

La autoridad es exclusivamente `work_obligation.evidence_policy_version_id`; nunca se sustituye por la política actualmente `VIGENTE` de la TAR. La proyección reutiliza literalmente `EvidenceReviewEvaluator`, los 27 requisitos cerrados, los contratos estructurados y las tres relaciones nominales aprobadas en las adendas 16, 19 y 20.

Para cada fila se usan únicamente ítems y versiones `VIGENTE` de la misma obligación, política y requisito. `SUSTITUIDA` nunca satisface. Un binario sólo satisface con vínculo funcional `LIMPIO/CLEAN`; un estructurado debe satisfacer su contrato cerrado. En `TAR-0092`, sólo el `F_ENT_001` estructurado vigente resuelve `DIFERENCIA_O_DANO`: verdadero hace aplicable la fotografía, falso la hace no aplicable y ausencia ordinaria mantiene la evaluación `INCOMPLETA` sin fabricar un faltante fotográfico falso.

La lectura no crea ni reutiliza como autoridad un `evidence_review_snapshot`; evalúa desde la fotografía PostgreSQL y devuelve sólo la proyección aprobada. No descarga, abre, inspecciona, aporta o sustituye archivos o payloads. No devuelve `evidenceVersionId`, contenido, `structuredPayload`, nombre, media type, tamaño, hash, URL, clave o bucket.

Política nula, catálogo incoherente, requisito desconocido o duplicado, historia vigente múltiple, payload vigente corrupto, vínculo cruzado o condición presente pero no resoluble falla cerrado para la petición completa con `409 BANDEJA_INCONSISTENTE`; no se mezclan filas confiables con filas dudosas.

## 11. Consistencia, consultas acotadas y concurrencia de evidencia

Cada GET usa una transacción PostgreSQL `REPEATABLE READ, READ ONLY`. Todas las secciones, autorizaciones, fechas, estados, acciones y evaluaciones usan la misma fotografía MVCC y el mismo `queriedAt`.

El lector aplica `AsNoTracking`, no expone `DbContext`, no llama `SaveChanges` y carga en lotes acotados las políticas, requisitos, ítems y versiones correspondientes exclusivamente a los IDs de la página visible. Se prohíbe N+1. Una sustitución concurrente queda ordenada antes o después de la fotografía de lectura; una fila nunca mezcla versiones anterior y sucesora.

Cada nueva petición, incluida una continuación, vuelve a autorizar cuenta, empleo, rol, permiso, asignación y destinatario. Un cursor nunca concede acceso. Con datos y autoridad sin cambios no hay duplicados ni omisiones; con cambios concurrentes la siguiente petición refleja su propia fotografía sin ampliar alcance.

## 12. Orden y cursores de tareas

El orden total de tareas es:

1. rango de `taskState`: `VENCIDA`, `DISPONIBLE`, `FUTURA`, `CONCLUIDA`;
2. `dueAt` ascendente, nulos al final;
3. `period.startsOn` ascendente;
4. `task.taskCode` ascendente ordinal; y
5. `obligationId` ascendente.

Se solicitan `taskLimit + 1` filas y se devuelven como máximo `taskLimit`. El cursor Base64URL versionado contiene las claves completas de orden y SHA-256 de `periodId` y `taskState` normalizados. No contiene nombres, contenido, PII, secretos ni autoridad.

## 13. DTO, filtros, orden y cursor de avisos

Cada elemento de `notices.items` contiene exactamente:

```text
noticeId: uuid
noticeType: OBLIGATION_ASSIGNED
status: UNREAD|READ
createdAt: instant
readAt: instant|null
resource: {
  resourceType: ASSIGNMENT_VERSION,
  resourceId: uuid,
  available: boolean,
  obligationId: uuid|null,
  taskCode: TAR-####|null,
  taskName: string|null
}
allowedActions: MARK_NOTICE_READ[]
```

`available` es verdadero y los tres datos funcionales se completan sólo si esa asignación sigue `VIGENTE`, pertenece a la persona del actor y la obligación sigue dentro de `LOR-001`. Si el recurso fue sustituido o ya no es propio, el aviso se conserva, `available=false`, `obligationId`, `taskCode` y `taskName` son nulos, y `allowedActions` sólo puede conservar `MARK_NOTICE_READ` porque esa acción pertenece al aviso, no a la tarea.

`allowedActions` contiene `MARK_NOTICE_READ` sólo para `UNREAD`; queda vacío para `READ`. El destinatario conserva acceso al aviso histórico, pero esa propiedad no concede acceso al recurso relacionado.

El orden total es `UNREAD` antes de `READ`, luego `createdAt` descendente y `noticeId` descendente. Se solicitan `noticeLimit + 1` filas. El cursor Base64URL versionado contiene esas claves y SHA-256 de `noticeStatus`; no contiene datos del recurso ni autoridad.

## 14. Estado vacío, caché y efectos de lectura

Una bandeja sin tareas ni avisos devuelve `200`, ambas colecciones vacías y los tres indicadores `isEmpty=true`. Un filtro sin coincidencias también devuelve `200`; el API no inventa un motivo textual.

Un GET es estrictamente lectura. No cambia `read_at`, obligación, asignación, evidencia, snapshot, resultado, idempotencia, auditoría ni outbox. Consultas repetidas pueden reflejar datos confirmados entre peticiones; no se promete una representación congelada entre páginas.

La respuesta usa `Cache-Control: private, no-store`. No emite ETag: combina varias fuentes mutables, autorización vigente y un instante de consulta. `If-None-Match` no forma parte del contrato.

## 15. Tipos, recursos y productores exactos

El catálogo de `notice_type` contiene exactamente `OBLIGATION_ASSIGNED`. El catálogo de `resource_type` contiene exactamente `ASSIGNMENT_VERSION`.

Cada inserción confirmada de una `assignment_version` `AUTOMATICA` o `CORRECCION` produce en la misma transacción un aviso para la única cuenta `ACTIVA` vinculada a la persona asignada en `assigned_at`:

| Transición productora | Propietario | Destinatario | Instante | Recurso |
|---|---|---|---|---|
| asignación automática creada | `Assignment` | cuenta activa de `assignment_version.person_id` | `assignment_version.assigned_at` UTC | nueva `assignment_version.id` |
| corrección de asignación creada | `Assignment` | cuenta activa de la nueva persona responsable | `assignment_version.assigned_at` UTC | nueva `assignment_version.id` |

El módulo `Notifications` posee la tabla y expone a `Assignment` un contrato interno de escritura; `Assignment` no escribe directamente la tabla. La escritura participa en la transacción ya abierta. Una repetición idempotente recuperada, un resultado sin candidato o una solicitud rechazada no crea aviso.

Si no puede resolverse exactamente una cuenta activa, si el recurso no coincide o si falla el aviso, se revierten juntos asignación, aviso, idempotencia y auditoría. No se agrega outbox porque productor y persistencia comparten base y transacción.

No se crean avisos por GET, evaluación, vencimiento, conclusión, marcación, migración o semilla de prueba. No se aprueban otros tipos porque `HU-027`, `HU-028`, `HU-031` y `HU-032` todavía no ofrecen productores autorizados. No hay reconstrucción, backfill ni migración histórica: las asignaciones existentes aparecen como tareas, sin avisos retroactivos.

## 16. Persistencia de `internal_notice`

El módulo `Notifications` es propietario de la tabla:

| Columna | Tipo PostgreSQL | Regla |
|---|---|---|
| `id` | `uuid` | PK; UUID v7 de aplicación |
| `recipient_user_id` | `uuid` | FK obligatoria `RESTRICT` a `app_user.id` |
| `notice_type` | `varchar(32)` | exactamente `OBLIGATION_ASSIGNED` |
| `resource_type` | `varchar(32)` | exactamente `ASSIGNMENT_VERSION` |
| `resource_id` | `uuid` | FK obligatoria `RESTRICT` a `assignment_version.id` |
| `created_at` | `timestamptz` | UTC; exactamente `assignment_version.assigned_at` |
| `read_at` | `timestamptz` | nulo o UTC no anterior a `created_at` |

Restricciones e índices:

- PK `internal_notice(id)`;
- unicidad `(notice_type, resource_type, resource_id)` para una fila por evento productor;
- índices `(recipient_user_id, read_at, created_at desc, id desc)` y `(recipient_user_id, created_at desc, id desc)`;
- checks de UUID no vacío, catálogos cerrados y `read_at >= created_at`;
- FK `RESTRICT` al destinatario y a la asignación;
- trigger de inserción que comprueba tipo/recurso, persona de asignación, cuenta destinataria activa en `created_at`, `LOR-001` e igualdad de instantes;
- constraint trigger diferible que exige al confirmar exactamente un aviso coherente por cada `assignment_version` insertada después de instalar la migración; y
- trigger que rechaza `DELETE` y todo `UPDATE` salvo `read_at: null → valor UTC`, manteniendo inmutables las demás columnas.

PostgreSQL es la autoridad final. La migración se llamará `AddInternalNotices`, será forward-only, sin BOM, con LF y `Down()` bloqueado mediante excepción. No modifica migraciones históricas.

## 17. Solicitud de marcación

`POST /api/v1/me/notices/{id}/read` no admite query ni cuerpo. El ID debe ser UUID canónico `D` no vacío. No usa `Idempotency-Key`: la transición monotónica proporciona idempotencia natural. No usa ETag ni `If-Match`: no existe edición reemplazable y los dos estados válidos convergen sin sobrescritura.

Para navegador, CSRF es obligatorio antes de invocar el servicio. Autenticación, MFA, cuenta, empleo, rol y `PER-BANDEJA-PROPIA` se revalidan con el único `queriedAt`. Sólo `recipient_user_id = actorUserId` permite resolver la fila.

## 18. Respuestas e idempotencia natural

La primera transición devuelve `200`:

```json
{
  "data": {
    "noticeId": "019...",
    "status": "READ",
    "readAt": "2026-09-09T12:00:00Z",
    "result": "MARKED_READ"
  },
  "meta": { "correlationId": "019..." }
}
```

Una repetición autorizada devuelve `200` con el `readAt` original y `result = ALREADY_READ`. No actualiza el instante, crea auditoría adicional ni consume idempotencia. Dos marcaciones concurrentes convergen: exactamente una obtiene `MARKED_READ`; la otra obtiene `ALREADY_READ` con el mismo `readAt`.

## 19. Errores normalizados

Todos los errores usan `application/problem+json`, con `status`, `code`, `title`, `instance` y `correlationId`.

| HTTP | `code` | Uso |
|---:|---|---|
| 400 | `FILTRO_BANDEJA_INVALIDO` | filtros, límites o cursores inválidos del GET |
| 400 | `AVISO_ID_INVALIDO` | UUID de ruta inválido |
| 400 | `SOLICITUD_LECTURA_AVISO_INVALIDA` | query, cuerpo o cabecera funcional no admitida |
| 400 | `CSRF_INVALIDO` | sesión de navegador sin token válido |
| 401 | `AUTENTICACION_REQUERIDA` | sesión ausente, expirada o MFA incompleto |
| 403 | `ACCESO_DENEGADO` | actor sin cuenta, empleo, rol o permiso base, antes de resolver recursos |
| 404 | `AVISO_NO_ENCONTRADO` | inexistente, ajeno, otra sucursal o fuera del destinatario |
| 409 | `BANDEJA_INCONSISTENTE` | período, política, evidencia o persistencia incoherente en GET |
| 409 | `LECTURA_AVISO_CONCURRENCIA_CONFLICTO` | agotamiento de reintentos serializables del POST |
| 500 | `ERROR_INTERNO` | falla inesperada sanitizada |

`412` no aplica: se prohíbe `If-Match`. `If-Match` o `Idempotency-Key` en el POST devuelve `400 SOLICITUD_LECTURA_AVISO_INVALIDA`. Toda solicitud rechazada deja sin cambios `read_at`, obligación, asignación, evidencia, snapshot, resultado, idempotencia, auditoría de éxito y outbox.

## 20. Transacción, bloqueos y recuperación

La marcación usa PostgreSQL `SERIALIZABLE` y como máximo tres intentos. Cada intento:

1. revalida actor, cuenta, empleo, rol y permiso;
2. selecciona por `id` y `recipient_user_id` `FOR UPDATE`;
3. devuelve `404` si no existe en ese alcance;
4. devuelve `ALREADY_READ` sin escribir si `read_at` existe;
5. transiciona condicionalmente, inserta auditoría y devuelve `MARKED_READ` si es nulo; y
6. confirma ambos efectos juntos.

Sólo existe un recurso; el orden de bloqueo es `internal_notice.id`. `40001` y `40P01` reintentan toda la transacción. Agotado el límite se devuelve `409 LECTURA_AVISO_CONCURRENCIA_CONFLICTO` sin efectos parciales.

En producción, el orden conserva el flujo propietario: obligación, asignación y aviso por `resource_id`. Una violación `23505` de deduplicación recupera al ganador sólo si destinatario, tipo, recurso e instante coinciden; una diferencia falla cerrada.

## 21. Auditoría

La primera marcación crea exactamente un `audit_event` en la misma transacción:

- `action = INTERNAL_NOTICE_READ`;
- `resource_type = INTERNAL_NOTICE`;
- `resource_id = noticeId`;
- `actor_type = USER`, `actor_user_id = recipientUserId`;
- `occurred_at = readAt`, `request_id = correlationId`;
- `before_data = { "schemaVersion": 1, "status": "UNREAD" }`;
- `after_data = { "schemaVersion": 1, "status": "READ", "readAt": ... }`; y
- `outcome = READ`.

No contiene tarea, asignación, nombre, evidencia, payload, URL, hash, clave, bucket ni secreto. Si auditoría o commit falla, `read_at` permanece nulo. Repetición, GET o rechazo no crea auditoría de éxito. La creación del aviso no duplica auditoría: el evento existente de asignación acredita el productor.

## 22. Seguridad, privacidad y observabilidad

Se aplica denegación por defecto, filtro de destinatario en SQL, convergencia anti-IDOR y salida mínima. Logs y métricas nunca incluyen contenido o requisito de evidencia, payload, nombres, tarea, asignación, ID de usuario/recurso, URL, hash, clave, bucket, cookie, CSRF, secreto, SQL o stack.

Métricas de baja cardinalidad:

```text
sgol_inbox_queries_total{result}
sgol_inbox_query_duration_seconds{result}
sgol_internal_notices_total{operation,result}
```

Para bandeja, `result` sólo admite `success`, `denied`, `inconsistent` o `failure`. Para avisos, `operation` sólo admite `create` o `read` y `result` sólo `success`, `replayed`, `conflict` o `failure`. No se etiquetan usuario, obligación, aviso, asignación, TAR, política, requisito, evidencia, cursor ni correlación. Toda inconsistencia falla cerrada.

## 23. Decisión de interfaz y brecha de diseño

`HU-030` no incluye interfaz Razor/MVC en este corte. Los documentos aprobados ofrecen tabla, botones, badges generales, navegación, carga, error, vacío, foco y tokens; no definen un componente de avisos, el estado accesible `UNREAD/READ`, su anuncio dinámico ni el patrón exacto para combinar tareas y avisos. Además, `docs/design/mapa-pantallas.md` es un cambio ajeno no rastreado y no puede usarse como fuente operativa.

Inventar esos elementos infringiría la regla de interfaz. No se crean página, ruta Razor, navegación, marcado, CSS, JavaScript ni pruebas Playwright. Una UI futura requiere aprobación documental previa. Esta exclusión no reduce los endpoints ni el estado vacío del API.

## 24. Pruebas requeridas después de una aprobación válida

### 24.1 Dominio, HTTP y arquitectura sin Docker

- responsable autenticado ve sólo tareas con asignación propia `VIGENTE`;
- superior o Dirección no amplía `/me`; pares, inferiores y ajenos tampoco;
- cuenta inactiva, empleo/rol inválido o falta de permiso se rechaza;
- programada se deriva sólo de origen recurrente;
- futuras, disponibles, vencidas y concluidas cumplen la sección 9;
- vencimiento no cambia `execution_status`;
- `queriedAt` se captura una vez y gobierna autorización, período y proyección;
- orden, desempates, límites y cursores son deterministas;
- cursor inválido se rechaza sin efectos;
- estado vacío y conteos de página cumplen el contrato;
- GET no cambia obligaciones, asignaciones, evidencia, avisos ni auditoría;
- GET no crea snapshot, resultado, idempotencia u outbox;
- política congelada y sólo evidencia `VIGENTE` gobiernan el resultado;
- `SUSTITUIDA` no satisface;
- `TAR-0092` aplicable muestra faltante y no aplicable no lo fabrica;
- sustitución concurrente no mezcla versiones;
- inconsistencia falla la petición completa;
- acciones se calculan sin conceder autoridad;
- cada productor crea exactamente un aviso;
- reintento, carrera, rechazo o ausencia de candidato no duplican avisos;
- ningún aviso se crea por GET, vencimiento, conclusión o lectura;
- sólo destinatario consulta o marca; inexistente y oculto convergen;
- primera lectura devuelve `MARKED_READ`; repetición, `ALREADY_READ` con el mismo instante;
- carreras producen un cambio y una auditoría;
- falla de auditoría revierte `read_at`;
- rechazo no cambia `read_at` ni crea auditoría de éxito;
- logs y métricas no filtran datos privados; y
- no se invocan archivos, canales externos, validación, supervisión o indicadores.

### 24.2 PostgreSQL real externa

La suite externa demuestra FKs `RESTRICT`, checks, índices, catálogos, inmutabilidad, coherencia asignación/destinatario/instante, obligación diferible de un aviso por asignación nueva, unicidad bajo carreras, transición única de `read_at`, una sola auditoría concurrente, rollback ante falla de auditoría, fotografía de evidencia coherente y ausencia de efectos en consultas y rechazos.

No se usa SQLite. El desarrollador ejecuta PostgreSQL/Testcontainers fuera de la sesión y comunica el resultado antes de los gates restantes.

### 24.3 UI y navegador

No aplican porque la UI queda excluida por la sección 23. Este gate es no aplicable al alcance propuesto, no aprobado ni omitido.

## 25. Archivos previstos, gates, trazabilidad, eficacia y decisión

Después de una aprobación íntegra, los cambios previstos se limitan a contratos del módulo `Notifications`, lector PostgreSQL, marcación transaccional, integración mínima de los dos productores, endpoints, dependencias, `SgolDbContext`, configuración EF, migración `AddInternalNotices`, telemetría, pruebas afectadas y trazabilidad (`README`, `IMPLEMENTATION_STATUS`, inventario de migraciones y suite PostgreSQL). Esta adenda cambiará de estado sólo para reflejar una aprobación recibida.

No se prevén paquetes, CI, Worker, adaptadores de archivo, páginas, estilos ni cambios en documentos F00–F07 congelados.

Los gates finales se ejecutarán una sola vez y en este orden:

1. `dotnet restore SGOL.slnx --locked-mode` fuera del aislamiento;
2. `dotnet build SGOL.slnx --no-restore --configuration Release`;
3. suite unitaria completa;
4. suite de arquitectura completa;
5. pruebas enfocadas `HU-030` sin Docker;
6. gate UI/navegador no aplicable por la sección 23;
7. suites PostgreSQL afectadas ejecutadas externamente por el desarrollador;
8. `dotnet format SGOL.slnx --verify-no-changes --no-restore`;
9. `./scripts/ci/Assert-NoVulnerablePackages.ps1`;
10. modelo EF sin cambios pendientes;
11. `./scripts/ci/verify-fuentes-protection.ps1`;
12. `./scripts/ci/verify-fuentes-mirror.ps1`, después y no en paralelo con el anterior; y
13. `git diff --check`.

En la rama, `HU-030` se registrará sólo como propuesta. Adquiere estado efectivo `Terminada` en `master` únicamente con pipeline exacto verde, aprobación humana, merge, ascendencia, PostgreSQL y gates aplicables satisfactorios y cero defectos bloqueantes. No se crea un commit administrativo autorreferencial posterior.

La aprobación íntegra fue recibida el 2026-09-09 y habilitó exclusivamente la implementación descrita. No autoriza commit, publicación, apertura de pull request ni merge; cada acción conserva su autorización independiente.
