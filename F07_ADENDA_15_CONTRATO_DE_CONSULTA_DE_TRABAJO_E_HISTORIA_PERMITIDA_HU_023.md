# F07 Adenda 15 — Contrato de consulta de trabajo e historia permitida para HU-023

## 1. Estado, decisión solicitada y alcance

| Campo | Valor |
|---|---|
| Tipo | Adenda normativa a `F07_BACKLOG_DE_IMPLEMENTACION.md` |
| Estado | `APROBADA` íntegramente por el responsable el 2026-09-05; habilita la implementación de `HU-023` |
| Fecha | 2026-09-05 |
| Historia | `HU-023` — Usuario consulta tarea, procedencia e historia permitida |
| Criterios | `CA-023`, `CP-023-P`, `CP-023-N`, `RN-002`, `RN-025`, `RN-027` |
| Efecto | Precisa el contrato reservado de `HU-023` sin modificar ni renumerar el backlog aprobado |
| Precedencia | `TECH-E2E-CV-02` `Terminada`: PR `#37`, commit `f74261988866819a1fc09c2a598d137538530fc5`, pipeline `SUCCESS` run `33983298961`, aprobación humana, merge `e2d059c11b9647e92e10c49eff00e148b87c6f7c` y ambos SHA verificados como ancestros de `origin/master` |
| Decisión agrupada | Aprobar o rechazar íntegramente las secciones 2 a 18 de esta adenda |

La propuesta autoriza, tras su aprobación íntegra, exclusivamente dos lecturas productivas: `GET /api/v1/obligations` y `GET /api/v1/obligations/{id}`. No autoriza `HU-022`, `HU-024` a `HU-032`, otros endpoints, nuevas entidades, migraciones, cambios de esquema, mutaciones, permisos, autenticación, Worker, jobs, UI ni infraestructura.

## 2. Inserción formal y orden efectivo

`HU-023` conserva su posición 22, sus dependencias `HU-007`, `HU-015` y `HU-019`, su pertenencia a `EP-06`/`CV-03` y su gate `Filtro servidor/IDOR y CA-023`. Esta adenda no inserta una tarea anterior ni altera el orden de F07.

Las tareas insertadas por adenda que podían bloquear este comienzo —`TECH-UI-001`, `TECH-VER-001`, `TECH-JOBS-001` y `TECH-E2E-CV-02`— constan como `Terminada`. Por tanto, una vez aprobada esta adenda, `HU-023` es la siguiente tarea efectiva. La aprobación del contrato no aprueba su implementación, publicación, pull request ni merge.

## 3. Instante único de consulta y autoridad del actor

Cada petición captura exactamente una vez `queriedAt = IClock.UtcNow` antes de evaluar autoridad o proyectar datos. Todas las comparaciones temporales, la condición vencida y los campos `meta.queriedAt` de esa respuesta usan ese mismo instante UTC.

En `queriedAt`, el actor debe tener simultáneamente:

1. sesión individual autenticada con identificador de usuario válido;
2. cuenta `ACTIVA` vinculada a una persona;
3. empleo `ACTIVA` y vigente en `LOR-001`, con `valid_from <= queriedAt` y `valid_to` nulo o `queriedAt < valid_to`;
4. exactamente una `role_assignment_version` `ACTIVO` y vigente en `LOR-001`, con la misma regla de intervalo; y
5. rol canónico definido. Los cuatro roles canónicos poseen `PER-TAREA-VER` para el alcance que esta adenda determina.

Cuenta, persona, empleo o rol ausente, inactivo, sustituido, no vigente, múltiple, desconocido o fuera de `LOR-001` produce `403 ACCESO_DENEGADO`. Puesto, turno, texto laboral, una asignación histórica, una publicación histórica o un dato enviado por el cliente nunca concede permiso ni nivel.

## 4. Universo visible y jerarquía

La autoridad se determina por la asignación `VIGENTE` y por el rol canónico vigente de su responsable en `queriedAt`:

| Rol vigente del actor | Obligaciones visibles |
|---|---|
| `PISO_VENTAS` | Las que tienen asignación `VIGENTE` a la propia persona del actor. |
| `SUBCOORDINACION` | Las propias y las asignadas actualmente a personas con rol vigente `PISO_VENTAS`. |
| `ADMINISTRACION` | Las propias y las asignadas actualmente a personas con rol vigente `SUBCOORDINACION` o `PISO_VENTAS`. |
| `DIRECCION` | Todas las obligaciones de `LOR-001`, incluidas las que carecen de asignación vigente. |

Para actores distintos de Dirección, una obligación sin asignación `VIGENTE`, cuyo responsable vigente no tenga cuenta, empleo y un único rol canónico vigentes en `LOR-001`, o cuyo responsable sea par o superior queda fuera del universo visible. Dirección no depende del estado del responsable para consultar el hecho perteneciente a `LOR-001`.

Una persona que fue responsable histórico pero ya no es la responsable vigente no obtiene visibilidad por ese antecedente. Si además es superior vigente del responsable actual, accede sólo por esa relación vigente; si no, la obligación queda oculta. Una corrección confirmada cambia inmediatamente el universo de peticiones posteriores, sin reescribir la historia.

La misma expresión de alcance se aplica dentro de la consulta PostgreSQL al listado, detalle, conteos, filtros y selección previa a historia o vínculos. Nunca se cargan primero obligaciones fuera de alcance para filtrarlas en memoria.

## 5. Publicación y estado de ejecución

La publicación en un plan no es una guarda de visibilidad de `HU-023`. Una obligación ya materializada es visible aunque todavía no aparezca en `plan_version_obligation`, siempre que cumpla la sección 4. Cuando exista publicación, se muestra únicamente la historia de versiones de plan que contiene esa obligación y que ya está persistida.

`executionStatus` conserva exclusivamente `PENDIENTE` o `CONCLUIDA`. `VENCIDA` no se persiste ni sustituye ese estado. `HU-023` no concluye, reabre, materializa, asigna, publica ni cambia una obligación.

## 6. Rutas, autenticación y ocultación anti-IDOR

Se conservan sin alias ni rutas adicionales:

- `GET /api/v1/obligations`;
- `GET /api/v1/obligations/{id}`.

Sin sesión o con MFA incompleto se responde `401 AUTENTICACION_REQUERIDA`. Un actor autenticado que no satisface la sección 3 recibe `403 ACCESO_DENEGADO`.

En el detalle, un UUID bien formado que no existe, pertenece a otra sucursal o está fuera del universo de la sección 4 devuelve indistinguiblemente `404 OBLIGACION_NO_ENCONTRADA`. No se devuelve `403` después de conocer la existencia del recurso. Un UUID de ruta inválido devuelve `400 OBLIGACION_ID_INVALIDO` antes de invocar el lector.

El listado omite silenciosamente los recursos fuera de alcance. No aparecen en `data`, `meta.count`, `meta.nextCursor`, tiempos deliberadamente diferenciados, mensajes, relaciones ni vínculos. Filtrar por un responsable fuera de alcance produce una página vacía, no una confirmación de existencia ni un error distinguible.

Todos los errores usan `application/problem+json`, incluyen `code` y `correlationId`, y excluyen stack, SQL, rutas físicas, secretos, PII innecesaria y valores internos del cursor.

## 7. Envolturas y metadatos

Una página satisfactoria devuelve:

```json
{
  "data": [],
  "meta": {
    "nextCursor": null,
    "count": 0,
    "queriedAt": "2026-09-05T18:00:00Z",
    "correlationId": "019..."
  }
}
```

`count` es exclusivamente la cantidad de elementos devueltos en esa página, nunca el total global ni el total previo al filtro de autorización. El detalle devuelve `{ "data": { ... }, "meta": { "historyNextCursor": ..., "historyCount": ..., "queriedAt": ..., "correlationId": ... } }`; sus metadatos de historia tampoco revelan totales fuera de la página autorizada.

Instantes se serializan como RFC 3339 UTC con sufijo `Z`; fechas operativas como `YYYY-MM-DD`; horas locales recurrentes como `HH:mm`; propiedades JSON en `camelCase`; UUID en formato canónico `D` minúsculo.

## 8. DTO exacto de listado

Cada elemento de `data` contiene exactamente:

```text
obligationId: uuid
task: {
  taskDefinitionId: uuid,
  taskCode: TAR-####,
  name: string,
  version: {
    taskDefinitionVersionId: uuid,
    versionNo: integer,
    status: string,
    effectiveFrom: instant|null,
    effectiveTo: instant|null,
    schemaVersion: integer
  }
}
origin: {
  kind: MANUAL|RECURRENTE,
  schema: MANUAL_REFERENCE_V1|WORKING_DAY_WINDOW_V1,
  reference: string,
  generationRequestId: uuid,
  activationRuleVersionId: uuid,
  requestedAt: instant
}
period: {
  periodId: uuid,
  isoYear: integer,
  isoWeek: integer,
  startsOn: local-date,
  endsOn: local-date,
  timeZone: America/Mexico_City
}
dates: {
  dueAt: instant|null,
  dueLocalDate: local-date|null,
  concludedAt: instant|null
}
executionStatus: PENDIENTE|CONCLUIDA
condition: VENCIDA|NO_VENCIDA
currentAssignment: assignment-summary|null
links: authorized-links
```

`assignment-summary` contiene exactamente `assignmentId`, `responsible` (`personId`, `stableCode`, `displayName`), `assignmentType`, `assignedAt`. No incluye explicación, motivo ni asignaciones anteriores.

`authorized-links` contiene siempre `self`. Puede contener `eligibility` únicamente si el actor posee además `PER-ASIGNACION-EXPLICAR` y el recurso es visible. No contiene vínculos de evidencia, descarga, conclusión, validación, avisos, supervisión, indicadores, auditoría general ni rutas todavía no implementadas.

No se serializan `taskPayload`, `inputPayload`, `requestHash`, `idempotencyKey`, `assignment.explanation`, snapshots completos, contenido de auditoría, cookies, credenciales, TOTP, códigos de recuperación, cadenas de conexión ni payloads completos.

## 9. DTO exacto de detalle

El objeto `data` del detalle contiene todos los campos del elemento de listado y agrega exactamente:

```text
generationRequest: {
  generationRequestId: uuid,
  result: ACEPTADA,
  requestedAt: instant,
  requestedByUserId: uuid|null
}
history: history-event[]
```

El detalle acepta exclusivamente `historyCursor` y `historyLimit`, además del UUID de ruta. `historyLimit` sigue la misma regla de `limit`: predeterminado `25`, mínimo `1` y máximo `100`. `historyCursor` continúa el orden de la sección 12 y sólo es válido para la misma obligación. Parámetros desconocidos, repetidos, vacíos o inválidos devuelven `400 FILTRO_HISTORIA_INVALIDO`.

La información de asignaciones y publicaciones se entrega dentro de los eventos tipados de `history`; no se crean colecciones duplicadas ni cargas históricas sin límite.

## 10. Procedencia y referencia sanitizada

La procedencia se deriva de `generation_request.origin_type`, nunca de texto libre ni del actor:

| Valor persistido | `origin.kind` | Regla de presentación |
|---|---|---|
| `MANUAL_REFERENCE_V1` | `MANUAL` | `reference` es la referencia persistida tras recortar extremos, rechazar caracteres de control y limitar la salida a 120 caracteres Unicode. No se interpreta ni se expone como HTML. |
| `WORKING_DAY_WINDOW_V1` | `RECURRENTE` | `reference` debe validar exactamente `LOR-001|yyyy-MM-dd|HH:mm`; se devuelve en esa forma canónica y su fecha/hora deben corresponder a `America/Mexico_City`. |

Cualquier otro esquema persistido es incompatible con el alcance implementado de `HU-023` y produce `500 CONSULTA_OBLIGACION_INCONSISTENTE` con detalle público genérico y telemetría sanitizada. `SERVICE_DUE_DATE_REFERENCE_V1` permanece reservado para una recurrencia no implementada; no se simula ni se presenta como dato válido de este corte.

La sanitización es una transformación de salida y no modifica el valor almacenado. Si la referencia manual excede 120 caracteres, se devuelven los primeros 119 más `…`; no se registra el valor original ni el truncado en logs. La interfaz futura deberá codificar el texto, pero esta historia no incluye UI.

## 11. Fechas y condición vencida

`period.startsOn` y `period.endsOn` proceden de `week_period`; nunca se materializa una semana durante la lectura. `dueAt` y `concludedAt` proceden de la obligación y se devuelven en UTC. `dueLocalDate`, cuando hay `dueAt`, es la fecha resultante de convertir ese instante a `America/Mexico_City`.

La condición se calcula una sola vez por elemento con el `queriedAt` de la petición:

```text
VENCIDA     si executionStatus = PENDIENTE y dueAt != null y dueAt < queriedAt
NO_VENCIDA  en cualquier otro caso
```

La igualdad `dueAt == queriedAt` todavía es `NO_VENCIDA`. Una obligación concluida nunca se presenta como `VENCIDA`. Una fecha nula no se infiere desde TAR, período, origen, plan o calendario.

La ruta productiva aceptada hasta `TECH-E2E-CV-02` permite que `work_obligation.due_at` sea nulo y `HU-015` no autorizó inferirlo. Por tanto, aprobar esta adenda acepta expresamente que `HU-023` sólo puede mostrar `VENCIDA` cuando el dato ya exista; esta lectura no corrige obligaciones anteriores ni completa vencimientos nulos. Si el responsable exige que toda obligación tenga vencimiento calculable, hará falta una decisión contractual separada sobre la escritura que origina `due_at`, fuera de `HU-023`.

## 12. Historia permitida y orden determinista

`history` es una proyección de hechos persistidos de la misma obligación, no la consulta general de `audit_event` de `HU-033`. Incluye únicamente:

1. `GENERACION_SOLICITADA`, con `eventId = generationRequestId`, `occurredAt = requestedAt`, `actorType = HUMAN` cuando `requestedByUserId` existe y `SYSTEM` cuando es nulo;
2. `ASIGNACION_AUTOMATICA`, por cada `assignment_version` de tipo `AUTOMATICA`;
3. `ASIGNACION_CORREGIDA`, por cada `assignment_version` de tipo `CORRECCION`, con su motivo autorizado; y
4. `PUBLICACION_INCLUIDA`, por cada `plan_version` que contiene la obligación.

No se inventa `OBLIGACION_CREADA`: el esquema aceptado no conserva un instante propio para ese hecho. Tampoco se incluyen consultas, evaluaciones de elegibilidad, candidatos, auditoría genérica, payloads antes/después, ejecución, evidencia, validación, avisos, indicadores o eventos futuros.

Cada `history-event` contiene exactamente `eventId`, `eventType`, `occurredAt`, `actorType`, `actorUserId`, `reason`, `assignment` y `publication`; los dos objetos finales son mutuamente excluyentes y admiten `null` cuando no aplican.

`assignment` contiene exactamente `assignmentId`, `status`, `assignmentType`, `responsible` (`personId`, `stableCode`, `displayName`) y `supersedesAssignmentId`. `reason` sólo se muestra para una corrección y conserva el motivo normalizado persistido. La explicación JSON no se expone.

`publication` contiene exactamente `planId`, `publicationId`, `versionNo`, `status`, `scopeRole`, `assignmentVersionId` y `supersedesPublicationId`. Sólo se incluyen versiones cuyo `plan_version_obligation.obligation_id` coincide con la obligación visible. No se devuelve el contenido completo de la versión ni identidades de otras obligaciones.

La colección se ordena por `occurredAt` ascendente, luego por precedencia `GENERACION_SOLICITADA`, `ASIGNACION_AUTOMATICA`, `ASIGNACION_CORREGIDA`, `PUBLICACION_INCLUIDA`, y finalmente por `eventId` ascendente. Se solicitan `historyLimit + 1` hechos y se devuelven como máximo `historyLimit`; `meta.historyNextCursor` sólo existe cuando hay otra página autorizada y `meta.historyCount` cuenta únicamente la página devuelta. El cursor de historia es Base64URL opaco, versionado, ligado a `obligationId` y compuesto por las tres claves de orden; no contiene texto, nombres, motivos, PII ni autoridad.

## 13. Filtros y validación

El listado acepta exclusivamente `periodId`, `taskCode`, `executionStatus`, `condition`, `responsiblePersonId`, `cursor` y `limit`. Cada parámetro puede aparecer como máximo una vez. Los filtros presentes se combinan con `AND` y siempre después de incorporar la expresión de autorización al mismo árbol de consulta.

| Parámetro | Valores válidos |
|---|---|
| `periodId` | UUID canónico no vacío de un `week_period` existente en `LOR-001`; si no existe, página vacía. |
| `taskCode` | Uno de los ocho códigos TAR del MVP, comparación ordinal exacta. |
| `executionStatus` | `PENDIENTE` o `CONCLUIDA`. |
| `condition` | `VENCIDA` o `NO_VENCIDA`, calculada con `queriedAt`. |
| `responsiblePersonId` | UUID canónico no vacío; coincide sólo con la asignación `VIGENTE`. |
| `limit` | Entero decimal de `1` a `100`; predeterminado `25`. |
| `cursor` | Un único cursor no vacío emitido previamente por este endpoint y compatible con los mismos filtros. |

Un parámetro desconocido, repetido, vacío, con formato inválido, TAR fuera del catálogo, estado/condición con otra capitalización, límite fuera de rango o cursor inválido devuelve `400 FILTRO_OBLIGACIONES_INVALIDO`. Todos los filtros válidos pueden combinarse con `AND`; incluso una combinación sin coincidencias devuelve `200` con página vacía.

## 14. Orden total, cursor y consistencia

El orden del listado es total y fijo:

1. `dueAt` ascendente, con valores nulos al final;
2. `period.startsOn` descendente;
3. `taskCode` ascendente ordinal; y
4. `obligationId` ascendente.

El lector solicita `limit + 1`, devuelve como máximo `limit` y sólo emite `nextCursor` si existe otro elemento visible. El cursor es Base64URL opaco para el cliente y contiene versión de formato, claves completas del último elemento y SHA-256 de los filtros normalizados. No contiene nombres, PII, secretos ni identidades de recursos ajenos; nunca se acepta como prueba de autoridad. Un cursor de listado no sirve como cursor de historia ni viceversa.

En cada página se reevalúan sesión, rol, empleo, responsable y alcance con el nuevo `queriedAt`. Un cambio de autorización puede reducir la página o invalidar la continuidad y nunca puede ampliar el alcance mediante un cursor antiguo. Con datos y autoridad sin cambios, las pruebas PostgreSQL demuestran ausencia de duplicados y omisiones entre páginas. La consulta de cada petición usa una transacción PostgreSQL `REPEATABLE READ, READ ONLY` para que `data`, `count`, vínculos e historia correspondan al mismo snapshot.

## 15. Lectura estricta y límites técnicos

La implementación usa contratos explícitos de `Execution` para consulta y un adaptador lector de infraestructura que proyecta las tablas poseídas por `Generation`, `Assignment`, `Planning`, `Configuration`, `Organization` e `Identity` sin escribirlas. El servicio lector:

- usa `AsNoTracking` en toda consulta de entidad;
- aplica autorización y filtros en SQL antes de materializar;
- no expone `DbContext`, entidades rastreadas ni SQL libre al endpoint;
- no llama ni puede llamar `SaveChanges`;
- no inserta auditoría por una consulta exitosa o denegada;
- no crea semana, obligación, evaluación, asignación, plan, publicación, evento, outbox ni otro hecho;
- no cambia estado, versión, `row_version`, vigencia o relación;
- evita N+1 mediante un número acotado de consultas por página/detalle y consultas por lotes limitadas a los IDs visibles de la página o a `historyLimit + 1`;
- nunca carga colecciones históricas para recursos fuera de alcance; y
- observa cancelación en todas las operaciones asíncronas.

Un interceptor o control de prueba rechazará cualquier `SaveChanges` iniciado desde el scope del lector. Las pruebas compararán huellas y conteos antes/después, incluidos `audit_event`, `idempotency_record`, `outbox_event` y `scheduled_job_run`.

## 16. Decisión de interfaz

`HU-023` no incluye UI en este corte. F05 y F06 reservan la capacidad y los dos endpoints, pero no fijan rutas Razor, navegación ni componentes visuales; inventarlos ampliaría el contrato. Por ello no se crean páginas, vistas, marcado, CSS, JavaScript, navegación ni pruebas Playwright.

`F07_ADENDA_04_CRITERIO_DE_INTERFAZ.md` permanece aplicable a una UI futura. Esa futura autorización deberá fijar primero rutas y componentes y exigir la lectura de `docs/design/tokens.md`, `componentes.md`, `estados-y-mensajes.md`, `estados-de-dominio.md` y `accesibilidad.md`. Esta adenda no autoriza abrir esos documentos ni resolver ahora una carencia visual.

## 17. Pruebas obligatorias posteriores a la aprobación

Se añadirán pruebas unitarias, API/componente, arquitectura y PostgreSQL real para demostrar al menos:

- responsable propio, superior sobre inferior y Dirección sobre todo `LOR-001`;
- par, inferior y usuario frente a obligación de superior rechazados u ocultos según las secciones 4 y 6;
- responsabilidad histórica sin visibilidad actual y corrección que cambia el universo posterior;
- UUID inexistente y existente fuera de alcance indistinguibles;
- recurso ajeno ausente de página, `count`, cursor, vínculos y errores;
- obligación visible antes de publicación y publicación histórica completa sin identidades de otras obligaciones;
- filtros individuales y combinados, desconocidos, repetidos, inválidos y responsable fuera de alcance;
- límite predeterminado 25, máximo 100, orden total y cursor sin duplicados ni omisiones;
- origen manual sanitizado y origen recurrente canónico; esquema futuro rechazado sin fabricar información;
- fechas UTC, fechas operativas de México, `VENCIDA` con reloj fijo, igualdad, fecha nula, no vencida y concluida;
- asignación vigente, sustituidas, motivos, correcciones sucesivas, publicaciones, paginación histórica y orden cronológico determinista;
- ausencia de `taskPayload`, `inputPayload`, explicación, auditoría completa y campos futuros de evidencia;
- `application/problem+json`, `correlationId`, mensajes sanitizados y ausencia de secretos o PII innecesaria;
- `AsNoTracking`, `READ ONLY`, ausencia de `SaveChanges`, auditoría nueva o cualquier otro efecto;
- ausencia de endpoints/UI fuera del contrato y dirección correcta de dependencias.

Autorización, filtros, cursores y proyecciones relacionales se prueban con PostgreSQL real. SQLite y una base simulada no sustituyen esos controles. Durante la implementación sólo se ejecutan localmente las pruebas enfocadas que no requieren Docker; la suite PostgreSQL afectada se solicita una sola vez al desarrollador en el gate acordado.

## 18. Gates, trazabilidad, límites y eficacia

Tras implementar y revisar el diff completo, los gates se ejecutan una sola vez en el orden solicitado para `HU-023`: restore bloqueado fuera del aislamiento, build Release, unitarias, arquitectura, pruebas enfocadas sin Docker, ejecución PostgreSQL externa única, formato, vulnerabilidades, modelo EF sin cambios, protección de `Fuentes/`, espejo de `Fuentes/` después del anterior y `git diff --check`. Playwright no aplica porque la sección 16 excluye UI. Un gate omitido o pendiente se informa como no verificado.

La implementación actualizará en el mismo cambio `docs/traceability/README.md`, `docs/traceability/IMPLEMENTATION_STATUS.md` y la documentación API mínima directamente afectada. En la rama del PR, `HU-023` será una propuesta y no `Terminada`.

Quedan expresamente fuera conclusión, requisitos o archivos de evidencia, revisión, validaciones, avisos, bandeja de `HU-030`, supervisión, indicadores, auditoría general de `HU-033`, nuevos permisos, autenticación, mutaciones, scheduler, Worker, jobs, broker, Redis, microservicios, Kubernetes, migraciones, cambios de esquema, nuevas entidades, datos reales, despliegue y `TECH-OPS-001`.

La aprobación íntegra de esta adenda autoriza iniciar la implementación mínima de `HU-023`; no autoriza commit, publicación de rama, apertura de PR ni merge. `HU-023` sólo podrá declararse `Terminada` después de pruebas PostgreSQL satisfactorias, pipeline requerido verde sobre el commit exacto, aprobación humana de implementación, merge en `master`, ascendencia verificada en `origin/master`, `Fuentes/` protegida y cero defectos bloqueantes conocidos.
