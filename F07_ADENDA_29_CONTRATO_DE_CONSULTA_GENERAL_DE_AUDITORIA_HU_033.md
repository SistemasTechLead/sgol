# F07 Adenda 29 — Contrato de consulta general de auditoría de HU-033

## 1. Control de la propuesta

| Campo | Valor |
|---|---|
| Identificador | `F07_ADENDA_29` |
| Historia | `HU-033` — Actor autorizado reconstruye auditoría sin borrado |
| Estado | Propuesta; requiere aprobación íntegra antes de código productivo |
| Fecha | 2026-09-11 |
| Alcance | Consulta general y detalle de hechos existentes, alcance jerárquico, paginación, minimización y rechazo auditado del intento de eliminación |
| Exclusiones rectoras | Sin rediseño de `TECH-AUD-001`, backfill, persistencia adicional, UI, idempotencia integral de `HU-034` ni continuidad o reconciliación de `HU-035` |

Esta adenda no modifica los documentos F00–F07 aprobados. Cierra únicamente las decisiones observables que éstos dejaron abiertas para implementar `HU-033`.

## 2. Hechos documentados

1. `TECH-AUD-001` está Terminada y proporciona `audit_event`, `AuditTransaction`, inserción transaccional, índices por recurso, actor y sucursal, y el trigger `audit_event_append_only` que rechaza `UPDATE` y `DELETE` con SQLSTATE `55000`.
2. `audit_event` conserva los 16 campos aprobados y es append-only. La cuenta de aplicación no puede modificar ni borrar sus filas.
3. `RN-025` dispone que el responsable consulta lo propio, los superiores consultan lo propio y niveles inferiores, y Dirección consulta todo.
4. `RN-027` prohíbe a todo usuario eliminar auditoría, evidencia, versiones, asignaciones o validaciones.
5. `RN-028` exige actor, fecha, alcance y antes/después aplicable para cambios de configuración, asignación, ejecución, evidencia, validación y publicación.
6. `PER-AUDITORIA-VER` está visible para los cuatro roles canónicos y sujeto a la misma regla jerárquica. `PER-AUDITORIA-ELIMINAR` no se concede a ningún rol.
7. `CP-033-P` exige reconstruir configuración → asignación → evidencia → validación y devolver actor, fecha y cambio.
8. `CP-033-N` exige rechazar y auditar también el intento de eliminación realizado por Dirección.
9. F06 fija REST JSON bajo `/api/v1`, envelopes `{ data, meta }`, cursores con límite predeterminado 25 y máximo 100, filtros desconocidos como `400`, errores `application/problem+json`, y `correlationId` en todas las respuestas.
10. F06 menciona `GET /audit-events` y `GET /audit-events/{id}`, permite conceptualmente filtros por actor, recurso, fecha y acción, limita cada consulta de auditoría a 31 días y exige datos minimizados sin secretos.
11. F06 declara que no existe `DELETE /audit-events` ni equivalente y que un intento autenticado a una ruta inexistente de eliminación sensible produce un evento de seguridad.
12. Los productores actuales son heterogéneos: `actorType`, `action`, `resourceType`, `outcome`, `beforeData` y `afterData` no forman catálogos globales cerrados ni un DTO uniforme.
13. Las obligaciones conservan vínculos persistidos a sus versiones congeladas de definición, política de evidencia y política de validación; asignaciones, evidencias y validaciones conservan relaciones persistidas con la obligación.
14. El backlog ubica `HU-033` en el orden 33, inmediatamente después de `HU-032`, con dependencia de todos los módulos usados; `HU-034` ocupa el orden 34 y `HU-035` depende de `HU-033`.
15. No existe tarea insertada por adenda pendiente que deba ejecutarse antes de `HU-033`. `F07_ADENDA_29` es el siguiente identificador libre.

## 3. Contradicciones y carencias que impiden implementar sin decisión

1. La matriz no decide si la jerarquía de un evento se determina por su actor, por el propietario del recurso, por la persona asignada, por la sucursal o por una combinación.
2. La cadena de `CP-033-P` atraviesa tipos de recurso diferentes y no tiene una correlación única obligatoria. `correlationId` identifica una solicitud, no toda la vida de una obligación.
3. Las rutas conceptuales de F06 no cierran prefijo versionado, parámetros, DTO, orden, cursor, snapshot o errores específicos.
4. F06 permite antes/después minimizado, pero no define una allowlist observable ni el tratamiento de claves históricas desconocidas.
5. La inexistencia de `DELETE /audit-events` no fija el status HTTP, el alcance de path que se observa, el evento de seguridad ni la convergencia anti-enumeración requerida por `CP-033-N`.
6. No está decidido si leer auditoría genera auditoría; hacerlo sin una regla explícita produciría crecimiento recursivo.
7. No se define cómo proyectar actores de sistema, usuarios inactivos, recursos archivados o vínculos cuya identidad vigente ya no existe.
8. `ViewAuditoriaEjecutiva` y `ViewTrazabilidadEntidad` no tienen correspondencia contractual inequívoca con los dos endpoints conceptuales.
9. La exclusión histórica de “pantalla” en `TECH-AUD-001` no constituye por sí misma un contrato aprobado de UI para `HU-033`.

Estas carencias cambian seguridad y comportamiento observable. Por ello no se autoriza código productivo antes de aprobar íntegramente esta adenda.

## 4. Inferencias limitadas

1. El prefijo `/api/v1` aprobado aplica a las dos rutas conceptuales de auditoría.
2. La autoridad debe depender de la identidad vigente del lector; la identidad histórica del actor del evento es un hecho mostrado, no una fuente de autoridad.
3. La jerarquía debe seguir al sujeto funcional del recurso y no al autor del evento. Usar al actor permitiría que una acción de Dirección sobre una tarea de Piso ocultara la historia al responsable o ampliara indebidamente la visibilidad de otros hechos de Dirección.
4. La cadena de `CP-033-P` debe enlazarse mediante relaciones persistidas ya existentes, no por coincidencia textual ni por una correlación inventada.
5. El evento histórico debe mostrarse como fue persistido. Consultar nombres, puestos o roles actuales para reescribirlo alteraría su significado.
6. Una lectura no es por sí misma una operación crítica ni una denegación sensible que deba crear otro `audit_event`; el intento de eliminar auditoría sí es la excepción indispensable y no recursiva requerida por `CP-033-N`.

Estas inferencias se convierten en decisiones sólo si la adenda se aprueba íntegramente.

## 5. Frontera exacta

### 5.1 Incluido

- `GET /api/v1/audit-events` como `ViewAuditoriaEjecutiva` paginada y filtrable;
- `GET /api/v1/audit-events/{id}` como detalle minimizado de un único evento;
- modo de trazabilidad de obligación dentro de la colección mediante `traceObligationId`;
- resolución jerárquica en servidor y convergencia anti-IDOR;
- proyección minimizada y cerrada de antes/después;
- cursor opaco ligado a actor, alcance, filtros, orden y cerca lógica de snapshot;
- rechazo uniforme y evento de seguridad para intentos autenticados de `DELETE` sobre las rutas exactas de auditoría;
- pruebas de lectura sin efectos y de append-only en PostgreSQL real.

### 5.2 Excluido

- cambiar `AuditEvent`, `AuditEventConfiguration`, `AuditTransaction`, la tabla, los tres índices o el trigger cerrados por `TECH-AUD-001`;
- crear tabla, columna, vista SQL, vista materializada, índice, migración, cache o proyección persistida;
- completar, corregir, normalizar o correlacionar retroactivamente eventos;
- modificar productores existentes para uniformar acciones o payloads;
- consultar o devolver estado actual como si fuera auditoría;
- exportación, KPI, UI, navegador, scheduler, Worker, outbox nuevo, broker o integración;
- idempotencia integral o consulta de conflictos de `HU-034`;
- respaldo, restauración, continuidad, compensación o reconciliación de `HU-035`.

Se autoriza como único productor nuevo el evento de seguridad de la sección 16, indispensable para `CP-033-N`. No modifica productores transaccionales existentes.

## 6. Actor lector y autorización vigente

Cada petición requiere sesión autenticada con MFA completo y exactamente una cuenta individual, persona, empleo y rol canónico vigentes en `LOR-001` al único `queriedAt` de la petición. También requiere `PER-AUDITORIA-VER`.

Los roles canónicos son, de mayor a menor: `DIRECCION`, `ADMINISTRACION`, `SUBCOORDINACION`, `PISO_VENTAS`.

- Dirección ve todo evento resoluble de `LOR-001` y eventos globales del sistema sin otra sucursal.
- Administración ve eventos cuyo sujeto es la propia persona o tiene nivel `SUBCOORDINACION` o `PISO_VENTAS`.
- Subcoordinación ve eventos cuyo sujeto es la propia persona o tiene nivel `PISO_VENTAS`.
- Piso ve únicamente eventos cuyo sujeto es su propia persona.

Puesto textual, turno, nombre, autor histórico, permiso aislado, rol sustituido, cuenta inactiva o empleo vencido no conceden autoridad. Pares ajenos y superiores se excluyen. Los filtros sólo reducen el universo ya autorizado.

La autoridad se evalúa con la identidad vigente del lector al `queriedAt`. No se exige vigencia actual del actor histórico ni se usa su rol actual para decidir visibilidad.

`PER-AUDITORIA-VER` es el permiso funcional necesario y suficiente para leer la proyección minimizada una vez aplicado el alcance anterior. Los permisos de mutación o consulta ordinaria del recurso no conceden auditoría por separado ni amplían su alcance; sólo delimitan la clasificación funcional de cada familia de recurso. `PER-DIRECCION-VER` tampoco sustituye `PER-AUDITORIA-VER`.

## 7. Sujeto y alcance histórico del evento

La visibilidad se resuelve en servidor antes de proyectar el DTO. Cada evento recibe exactamente una clasificación interna:

1. `RESOURCE_SUBJECT`: el recurso pertenece a una obligación, asignación, ejecución, evidencia, requisito o validación. El sujeto es la persona responsable según la versión de asignación vinculada y aplicable al hecho; si el propio hecho crea o sustituye la asignación, se usa la versión creada para el después y la sustituida para el antes.
2. `PERSON_SUBJECT`: el recurso es persona, cuenta, empleo, rol o disponibilidad. El sujeto es esa persona y el nivel es la versión de rol canónico efectiva en `occurredAt`.
3. `BRANCH_GOVERNANCE`: el recurso es sucursal, calendario, configuración, definición o política sin sujeto personal. Su nivel es `DIRECCION` y sólo Dirección lo ve en la lista general.
4. `SECURITY_ACTOR`: el evento es el intento de eliminar auditoría de la sección 16. El sujeto es la persona autenticada que realizó el intento y su rol vigente en el instante del intento.
5. `SYSTEM_GLOBAL`: `actorUserId` o `branchId` puede ser nulo y no existe sujeto resoluble. Sólo Dirección lo ve, siempre que el evento no identifique otra sucursal.
6. `UNRESOLVED`: la relación es ausente, ambigua o incoherente. Se oculta a roles no Dirección. Dirección lo recibe con `subjectPersonId`, `subjectLevel` y `relation` nulos, sin inferir identidad.

La resolución usa relaciones y versiones persistidas ya existentes, incluida la vigencia temporal en `occurredAt`. No usa texto libre de `beforeData` o `afterData` como fuente de autoridad. Un `branchId` distinto de `LOR-001` se excluye para todos. Un `branchId` nulo sólo es visible según `SYSTEM_GLOBAL` o cuando una relación persistida demuestra `LOR-001`.

Eventos con actor de sistema conservan `actor.userId = null`. Actores inactivos conservan su UUID histórico. No se devuelven nombre, código de persona, cuenta, puesto, turno, correo, teléfono ni rol actual del actor.

## 8. Reconstrucción de `CP-033-P`

“Reconstruir” significa devolver, en orden cronológico, eventos existentes relacionados por vínculos persistidos con una obligación visible. No significa calcular el estado actual, sintetizar eventos ausentes, afirmar causalidad no persistida ni completar huecos.

El cliente solicita el modo trazabilidad mediante `traceObligationId=<uuid>` en `GET /api/v1/audit-events`. El lector resuelve primero la obligación con la misma convergencia anti-IDOR de las consultas de obligación. Si no existe o el lector no puede ver íntegramente la trazabilidad conforme a esta sección, responde `404 AUDIT_EVENT_NOT_FOUND`.

La composición incluye únicamente eventos ya existentes cuyo recurso se enlaza mediante estas relaciones persistidas:

1. la propia `work_obligation` y su solicitud de generación;
2. la versión congelada de definición y el `configuration_release` que la publicó;
3. las versiones congeladas de política de evidencia y de validación y sus releases de publicación, cuando existen;
4. todas las `assignment_version` de la obligación;
5. todos los `evidence_item` y `evidence_version` de la obligación;
6. snapshots de revisión de evidencia vinculados a la obligación;
7. el `validation_requirement` y todas sus `validation_decision_version`;
8. eventos cuyo `resourceId` coincide con esos identificadores o cuya relación persistida directa apunta a ellos.

No se enlaza por `correlationId`, `requestId`, nombre, texto libre, cercanía temporal o coincidencia de una clave JSON desconocida. Una correlación puede filtrarse en modo general, pero no representa la cadena completa.

Los eventos `BRANCH_GOVERNANCE` que publicaron exactamente las versiones congeladas se incluyen como dependencias de la obligación visible y se proyectan con `scope.relation = "DEPENDENCY"`; no habilitan consultar otros recursos del mismo release ni la auditoría general de configuración. Los demás eventos conservan `OWN`, `LOWER` o `ALL` según el lector.

Para evitar una reconstrucción parcial engañosa, un lector no Dirección obtiene la traza sólo cuando cada evento personal de la cadena tiene como sujeto al propio lector o a un nivel inferior. Si una reasignación introduce un par ajeno, un superior o una relación irresoluble, toda la traza converge a `404`; nunca se omiten silenciosamente eslabones. Dirección puede ver la cadena completa de `LOR-001`.

Una cadena sin alguno de los cuatro hechos exigidos no se completa artificialmente: devuelve los eventos existentes y `meta.completeness` identifica de forma booleana `configuration`, `assignment`, `evidence` y `validation`. `CP-033-P` se satisface con datos sintéticos donde los cuatro son `true`.

Cada booleano es `true` si la página lógica completa —no sólo la página entregada— contiene al menos un evento enlazado de esa categoría. `configuration` cubre las versiones congeladas y su publicación; `assignment`, cualquier versión de asignación; `evidence`, cualquier aporte o sustitución de evidencia; y `validation`, cualquier decisión de validación. Los booleanos describen presencia de hechos, no éxito, vigencia ni estado actual.

## 9. API de colección

### 9.1 Ruta

`GET /api/v1/audit-events`

No se admite alias sin versión, ruta bajo otro recurso ni cuerpo de petición.

### 9.2 Parámetros exactos

| Parámetro | Regla |
|---|---|
| `from` | Obligatorio. Instante RFC 3339 con `Z`; extremo inclusivo. |
| `to` | Obligatorio. Instante RFC 3339 con `Z`; extremo exclusivo; debe ser posterior a `from`. |
| `actorUserId` | UUID canónico opcional; coincidencia exacta con `actor_user_id`. |
| `resourceType` | Texto opcional de 1–128 caracteres ASCII `[A-Z0-9_.:-]`; comparación ordinal exacta. |
| `resourceId` | UUID canónico opcional; exige `resourceType`. |
| `action` | Texto opcional con la misma forma de `resourceType`; comparación ordinal exacta. |
| `outcome` | Texto opcional con la misma forma de `resourceType`; comparación ordinal exacta. |
| `correlationId` | UUID canónico opcional; coincidencia exacta. |
| `branchCode` | Opcional; el único valor aceptado es `LOR-001`; ausente equivale a `LOR-001`. |
| `level` | Opcional; uno de los cuatro roles canónicos; filtra el nivel del sujeto, no el actor. |
| `traceObligationId` | UUID canónico opcional; activa exclusivamente el modo de la sección 8. |
| `cursor` | Base64URL opaco opcional; máximo 2048 caracteres. |
| `limit` | Entero decimal opcional, 1–100; predeterminado 25. |

El intervalo máximo es exactamente 31 días de 24 horas. No existe período predeterminado: `from` y `to` son obligatorios también con cursor y en modo trazabilidad. Los instantes se comparan en UTC; no se aplican semana ISO ni `America/Mexico_City` a esta consulta de hechos.

`traceObligationId` es incompatible con `actorUserId`, `resourceType`, `resourceId`, `correlationId` y `level`; puede combinarse con `action` y `outcome`. Todos los parámetros deben repetirse idénticos al usar un cursor.

Un parámetro ausente cuando es obligatorio, desconocido, repetido, vacío, con espacios laterales, inválido, fuera de rango o incompatible produce `400 AUDIT_FILTER_INVALID`. No se ignora ningún parámetro. Un filtro válido sin coincidencias devuelve `200` y colección vacía.

`actorType` no es filtro porque no existe catálogo cerrado y filtrar por actor humano/sistema no añade el vínculo requerido por F06. `sourceIpHash`, `requestId`, nombres y texto de motivo nunca son filtros públicos.

## 10. API de detalle

### 10.1 Ruta

`GET /api/v1/audit-events/{id}` donde `{id}` es el UUID del `audit_event`, no el identificador de un recurso.

No admite query parameters ni cuerpo. Cualquier query parameter produce `400 AUDIT_FILTER_INVALID`.

El evento se busca dentro del alcance jerárquico ya autorizado. Evento inexistente, de otra sucursal, fuera de jerarquía, de par/superior o irresoluble para un lector no Dirección converge en `404 AUDIT_EVENT_NOT_FOUND` con cuerpo idéntico. El detalle no consulta ni devuelve el estado actual del recurso.

## 11. DTO exacto de evento

La colección devuelve elementos y el detalle devuelve `data` con esta forma exacta:

```json
{
  "id": "01900000-0000-7000-8000-000000000201",
  "occurredAt": "2026-09-11T18:00:00Z",
  "actor": {
    "type": "USER",
    "userId": "01900000-0000-7000-8000-000000000010"
  },
  "action": "ASSIGNMENT_CORRECTED",
  "resource": {
    "type": "ASSIGNMENT_VERSION",
    "id": "01900000-0000-7000-8000-000000000301",
    "branchCode": "LOR-001"
  },
  "scope": {
    "subjectPersonId": "01900000-0000-7000-8000-000000000020",
    "subjectLevel": "PISO_VENTAS",
    "relation": "LOWER",
    "obligationId": "01900000-0000-7000-8000-000000000401"
  },
  "change": {
    "before": {
      "assignmentId": "01900000-0000-7000-8000-000000000300",
      "status": "VIGENTE"
    },
    "after": {
      "assignmentId": "01900000-0000-7000-8000-000000000301",
      "status": "VIGENTE"
    },
    "omittedFieldCount": 4
  },
  "reason": {
    "provided": true
  },
  "outcome": "SUCCESS",
  "correlationId": "01900000-0000-7000-8000-000000000099"
}
```

No existen otras propiedades. `actor.userId`, `resource.id`, los cuatro campos de `scope`, `change.before` y `change.after` pueden ser `null`. `actor.type`, `action`, `resource.type` y `outcome` se devuelven exactamente como fueron persistidos, sin convertirlos a un catálogo nuevo. Un valor histórico desconocido sigue siendo visible como texto opaco; no causa `500` ni se normaliza.

`scope.relation` es `OWN`, `LOWER`, `ALL`, `DEPENDENCY` o `null`. Dirección recibe `ALL`, salvo dependencias explícitas de trazabilidad. `scope.obligationId` sólo se llena cuando una relación persistida directa permite resolverla; no se extrae de texto libre.

`reason.provided` sólo informa si existió un motivo no vacío. El texto de `reason` no se expone porque es texto libre histórico y no existe garantía global de minimización. `requestId` y `sourceIpHash` tampoco se exponen a ningún rol.

Un recurso archivado, sustituido o ya no vigente conserva exactamente `resource.type` y `resource.id`. El lector no exige que el recurso actual exista, no devuelve su estado actual y no reemplaza el ID por el de una versión sucesora. Si las relaciones históricas persistidas bastan para resolver sucursal y sujeto, el evento conserva su visibilidad; si no bastan, aplica `UNRESOLVED` sin inventar el vínculo.

## 12. Allowlist de cambio y minimización

`beforeData` y `afterData` nunca se devuelven sin procesar. Cada uno se proyecta a un objeto JSON nuevo que conserva únicamente propiedades top-level con estos nombres exactos y únicamente cuando su valor es escalar (`string`, `number`, `boolean` o `null`):

```text
schemaVersion
obligationId
generationRequestId
taskDefinitionVersionId
configurationReleaseId
assignmentId
previousAssignmentId
evidenceItemId
evidenceVersionId
previousEvidenceVersionId
requirementId
decisionVersionId
previousDecisionVersionId
policyVersionId
evidenceReviewSnapshotId
taskCode
branchCode
versionNo
rowVersion
status
result
outcome
executionStatus
assignmentType
authorityType
validatorRole
requiredRole
effectiveFrom
effectiveTo
assignedAt
concludedAt
decidedAt
evidenceVersionCount
selfValidation
```

No se recorren objetos o arrays anidados. Una clave fuera de la allowlist, una clave permitida con valor objeto/array o un valor string mayor de 128 caracteres se omite. UUID, fecha y número conservan su representación JSON persistida; el lector no corrige valores históricos. `omittedFieldCount` es la suma de propiedades top-level omitidas en ambos documentos y no revela sus nombres.

Esta allowlist excluye, incluso si estuvieran presentes históricamente: contraseña o hash, semilla TOTP, códigos de recuperación, cookies, tokens, secretos, claves, cadenas de conexión, URLs firmadas, IP cruda o seudonimizada, binarios, contenido o hash de evidencia, payload completo de tarea, explicación de elegibilidad, nombre, código de persona, puesto, turno, contacto, texto libre, fundamento y contenido íntegro de evidencia.

La minimización ocurre en servidor después de la autorización y antes de serializar. El filtro no evalúa claves de `beforeData` o `afterData`. Los datos omitidos no aparecen en errores, logs, métricas o cursores.

## 13. Envelope y metadatos

La colección general devuelve exactamente:

```json
{
  "data": [],
  "meta": {
    "nextCursor": null,
    "count": 0,
    "queriedAt": "2026-09-11T18:00:00Z",
    "snapshot": {
      "upperOccurredAt": "2026-09-11T17:59:59Z",
      "upperEventId": "01900000-0000-7000-8000-000000000999"
    },
    "completeness": null,
    "correlationId": "01900000-0000-7000-8000-000000000099"
  }
}
```

En modo trazabilidad, `meta.completeness` es exactamente:

```json
{
  "configuration": true,
  "assignment": true,
  "evidence": true,
  "validation": true
}
```

En lista general es `null`. `meta.count` cuenta elementos de la página, no el universo total. No se devuelve total, porcentaje, estado actual ni indicador.

El detalle devuelve:

```json
{
  "data": {},
  "meta": {
    "queriedAt": "2026-09-11T18:00:00Z",
    "correlationId": "01900000-0000-7000-8000-000000000099"
  }
}
```

UUID se serializa en formato canónico `D` minúsculo e instantes en RFC 3339 UTC con `Z`.

## 14. Orden, cursor y snapshot lógico

La lista general se ordena por `occurredAt DESC, id DESC`. El modo `traceObligationId` se ordena por `occurredAt ASC, id ASC` para representar la secuencia histórica. El UUID es el desempate total y estable. Cada consulta solicita `limit + 1` y devuelve como máximo `limit`.

El cursor es Base64URL opaco, versionado y autenticado con protección de datos de la aplicación. Contiene únicamente versión, modo, claves de continuación, la pareja de cerca `upperOccurredAt/upperEventId` y SHA-256 del contexto normalizado. El contexto incluye ruta, actor, rol, persona, sucursal, alcance incluido, todos los filtros, período, orden y límite. No contiene nombres, texto libre, payload, secretos ni autoridad durable.

En la primera página, dentro del mismo snapshot PostgreSQL, se obtiene la mayor pareja `(occurredAt,id)` autorizada que satisface período y filtros. Esa pareja es la cerca lógica y se devuelve en `meta.snapshot`. Si no hay eventos, ambos campos son `null`. Las páginas siguientes sólo consideran filas que no exceden esa cerca y continúan después de la última pareja entregada según el orden del modo.

Una inserción concurrente con pareja posterior a la cerca nunca aparece en páginas subsiguientes y se observará en una consulta nueva. La cerca evita duplicar o mezclar eventos posteriores entre páginas; no pretende mantener abierta una transacción PostgreSQL entre solicitudes. Una fila insertada retroactivamente por un productor preexistente con pareja anterior a la cerca puede aparecer en una página posterior: esta limitación se informa y no autoriza impedir inserts, cambiar productores, añadir secuencia o persistir snapshots.

Cada página reevalúa autenticación y autoridad vigente. Si cambia identidad, rol, sucursal, alcance, filtros, período, orden o límite, el cursor produce `400 AUDIT_CURSOR_INVALID`; nunca amplía alcance. Un cursor alterado, expirado, de otra ruta o actor produce el mismo código. El cursor expira 15 minutos después de su emisión.

## 15. Consistencia, lectura sin efectos y cache

Cada petición usa una transacción PostgreSQL `REPEATABLE READ, READ ONLY`. Autoridad, alcance, relaciones, cerca, eventos y página pertenecen al mismo snapshot de esa petición. Todas las consultas EF usan `AsNoTracking`; filtros de sucursal, jerarquía, período y continuación se aplican en servidor antes de materializar.

No se promete el mismo snapshot PostgreSQL entre páginas; se promete la cerca lógica de la sección 14. La respuesta usa un único `queriedAt` obtenido del reloj inyectado.

Los GET no llaman `SaveChanges`, no insertan otro evento, no crean snapshots persistidos y no modifican auditoría, requisito, decisión, asignación, evidencia, ejecución, resultado, versión, ETag o `row_version`. Una consulta exitosa, vacía, inválida, denegada o fuera de alcance produce únicamente telemetría técnica sanitizada con instante, ruta normalizada, status, código, latencia y `correlationId`.

Las respuestas llevan `Cache-Control: private, no-store` y `Pragma: no-cache`. No emiten `ETag` ni aceptan `If-Match` o `Idempotency-Key`.

## 16. Intento autenticado de eliminar auditoría

No se registra endpoint funcional `DELETE`. Un componente transversal posterior a que la plataforma acepte la sesión y el MFA, y anterior a la respuesta, observa exclusivamente:

- `DELETE /api/v1/audit-events`;
- `DELETE /api/v1/audit-events/{id}` cuando `{id}` tiene forma UUID canónica.

Para cualquier principal cuya sesión y MFA hayan sido aceptados por la plataforma, tenga o no `PER-AUDITORIA-VER`, incluido Dirección, ambas formas responden `405 Method Not Allowed`, `Allow: GET` y `application/problem+json` con `code = AUDIT_NOT_DELETABLE`. La respuesta no consulta si el ID existe y es idéntica para ID inexistente, ajeno, fuera de sucursal o fuera de jerarquía. No interpreta body ni query; su presencia no cambia el resultado ni se registra.

Antes de completar la respuesta se inserta exactamente un evento de seguridad usando la tabla y transacción existentes:

| Campo | Valor |
|---|---|
| `actor_user_id` | UUID del principal aceptado por la sesión |
| `actor_type` | `USER` |
| `action` | `AUDIT_EVENT_DELETE_ATTEMPTED` |
| `resource_type` | `AUDIT_EVENT` |
| `resource_id` | UUID de la ruta de detalle; `null` para colección |
| `branch_id` | `LOR-001` cuando la identidad vigente lo resuelve; en otro caso `null` |
| `correlation_id` | Correlación de la petición |
| `request_id` | `null` |
| `before_data` | `null` |
| `after_data` | `null` |
| `reason` | `METHOD_NOT_ALLOWED` |
| `outcome` | `REJECTED` |
| `source_ip_hash` | `null`; HU-033 no introduce una clave ni un productor nuevo de seudonimización |

La inserción del evento es el único hecho de la transacción; no se busca, actualiza ni borra el evento objetivo. Si la inserción falla, se devuelve `500 AUDIT_SECURITY_EVENT_FAILED`, sin fingir que el intento quedó auditado y sin otro efecto funcional.

El propio insert de seguridad no atraviesa HTTP, no genera otro evento y no produce recursión. Consultar el evento creado tampoco genera auditoría. No se concede `PER-AUDITORIA-ELIMINAR` ni se agrega una acción funcional de borrado.

Una petición no autenticada a esas formas responde `401 AUTENTICACION_REQUERIDA`, no crea auditoría funcional porque no existe actor atribuible y sólo produce telemetría técnica sanitizada. Paths distintos, UUID mal formado o rutas posteriores no se convierten en contrato de `HU-033` y siguen el manejo normal de `404/405` sin evento funcional.

## 17. Errores exactos y anti-IDOR

Todos los errores incluyen `type`, `title`, `status`, `code`, `detail`, `instance` y `correlationId`; `errors` sólo aparece en `400` cuando puede identificar el nombre público del parámetro, nunca su valor sensible.

| HTTP | `code` | Uso |
|---:|---|---|
| 400 | `AUDIT_FILTER_INVALID` | Parámetro de lista o detalle inválido, desconocido, repetido, vacío, incompatible o fuera de rango. |
| 400 | `AUDIT_CURSOR_INVALID` | Cursor alterado, expirado o incompatible con actor, alcance, filtros, período, orden, límite o ruta. |
| 401 | `AUTENTICACION_REQUERIDA` | Sesión ausente, expirada o MFA incompleto. |
| 403 | `ACCESO_DENEGADO` | Cuenta, persona, empleo, rol, sucursal o `PER-AUDITORIA-VER` no vigente. |
| 404 | `AUDIT_EVENT_NOT_FOUND` | Evento/obligación inexistente o deliberadamente oculto por alcance. |
| 405 | `AUDIT_NOT_DELETABLE` | Intento autenticado de `DELETE` de la sección 16. |
| 500 | `AUDIT_SCOPE_INCONSISTENT` | Una traza autorizada exigiría una composición persistida inequívoca, pero contiene relaciones ausentes o contradictorias; la respuesta falla completa. La lista general de Dirección puede proyectar el evento aislado como `UNRESOLVED`. |
| 500 | `AUDIT_SECURITY_EVENT_FAILED` | No pudo persistirse el evento obligatorio del intento de eliminación. |
| 500 | `ERROR_INTERNO` | Falla no controlada sin detalle interno. |

Lista vacía no confirma identificadores. El detalle y el modo trazabilidad convergen entre inexistente, otra sucursal, par, superior y fuera de alcance. Ningún error devuelve actor, sujeto, recurso, filtros, conteos, payload o claves omitidas.

## 18. Persistencia, arquitectura y rendimiento

Se agrega un contrato interno de lectura de auditoría y un adaptador PostgreSQL read-only dentro de la composición Web, sin duplicar `AuditEvent` como agregado de dominio alternativo. Los contratos de salida son proyecciones inmutables; no exponen `SgolDbContext`, entidades rastreadas ni SQL libre al endpoint.

La consulta se calcula bajo demanda. Se prohíben N+1, carga indiscriminada de todos los productores, deserialización previa de todo el universo, evaluación jerárquica sólo en memoria, SQL dinámico construido con texto de filtros y consulta de binarios o contenido de evidencia.

No se requiere migración ni cambio de modelo EF. Se reutilizan los índices cerrados. Si las pruebas con la carga objetivo demuestran indispensable otro índice, vista o persistencia, se detiene la implementación y se solicita una nueva decisión; esta adenda no lo autoriza.

No se introduce purga ni retención física. Durante el piloto se conserva la política aprobada de ausencia de purga automática y el gate legal previo a producción plena; `HU-033` no decide su sucesora.

El objetivo específico aprobado es p95 menor de 3 segundos para una consulta de auditoría de hasta 31 días con la carga objetivo de F06. Los datos de prueba son sintéticos.

## 19. Interfaz y vistas conceptuales

`ViewAuditoriaEjecutiva` corresponde a la colección general de `GET /api/v1/audit-events`. `ViewTrazabilidadEntidad` corresponde al subconjunto directo `resourceType` + `resourceId` y, para la composición transversal exigida por `CP-033-P`, al modo `traceObligationId` de esa misma colección. `GET /api/v1/audit-events/{id}` es el detalle de un hecho, no una tercera vista funcional.

La palabra “pantalla” en la exclusión histórica de `TECH-AUD-001` sólo reservó una experiencia futura; no aprueba UI. `HU-033` se satisface con API. No se leen ni modifican documentos de diseño y no se crean Razor Pages, componentes, CSS, JavaScript o pruebas de navegador.

## 20. Pruebas obligatorias después de la aprobación

La implementación deberá demostrar al menos:

1. sesión ausente o MFA incompleto produce `401` sin exposición ni auditoría funcional;
2. permiso aislado sin cuenta, persona, empleo y rol canónico vigentes produce `403`;
3. puesto textual, rol sustituido, cuenta o empleo obsoleto no conceden acceso;
4. Dirección, Administración, Subcoordinación y Piso reciben exactamente el alcance de la sección 6;
5. Piso obtiene sólo recursos propios; pares y superiores se excluyen donde corresponde;
6. otra sucursal nunca aparece y los filtros no amplían autoridad;
7. detalle inexistente, ajeno y fuera de alcance converge en `404` idéntico;
8. actores de sistema, actores inactivos, recursos archivados y eventos irresolubles reciben el tratamiento aprobado;
9. una cadena sintética configuración → asignación → evidencia → validación devuelve los cuatro indicadores de completitud y cada evento con actor, fecha y cambio;
10. el enlace de la cadena usa FKs persistidas y no correlación, texto o proximidad temporal;
11. una cadena parcialmente no autorizada devuelve `404` completa y no omite eslabones;
12. cada evento aparece como máximo una vez aunque sea alcanzable por varias relaciones;
13. orden, desempate, límites y direcciones de ambos modos son deterministas;
14. parámetros ausentes, desconocidos, repetidos, vacíos, incompatibles e inválidos producen el error exacto;
15. el cursor está ligado a actor, rol, persona, sucursal, alcance, filtros, período, modo, orden y límite;
16. cursor alterado, expirado o reutilizado por otro actor produce `400` y no expone datos;
17. una inserción concurrente posterior a la cerca no mezcla páginas ni duplica eventos;
18. un valor histórico desconocido de acción, recurso, actor o resultado se devuelve opaco sin normalización;
19. `before` y `after` contienen sólo la allowlist y tipos escalares aprobados;
20. contraseña, hash, TOTP, códigos de recuperación, cookies, tokens, secretos, cadena de conexión, URL firmada, IP, binario, contenido/hash de evidencia y texto personal no aparecen;
21. `requestId`, `sourceIpHash` y motivo libre no aparecen en lista ni detalle;
22. la lectura exitosa, vacía, inválida, denegada o fuera de alcance no inserta otro `audit_event`;
23. la lectura no crea ni modifica requisito, decisión, asignación, evidencia, ejecución, resultado, versión, ETag o `row_version`;
24. `UPDATE audit_event` se rechaza en PostgreSQL con `55000`;
25. `DELETE audit_event` se rechaza en PostgreSQL con `55000`;
26. Dirección obtiene `405 AUDIT_NOT_DELETABLE` y exactamente un evento `AUDIT_EVENT_DELETE_ATTEMPTED`, sin lookup del objetivo, recursión o cambio parcial;
27. el intento no autenticado produce `401`, telemetría técnica y ningún evento funcional;
28. falla de persistencia del evento de seguridad produce `500 AUDIT_SECURITY_EVENT_FAILED`;
29. no existe permiso, endpoint o servicio funcional para borrar auditoría o versiones;
30. arquitectura confirma lector read-only, contratos explícitos, ausencia de dependencia de dominio hacia EF/ASP.NET y ausencia de persistencia nueva;
31. no aparecen consulta de conflictos de `HU-034`, continuidad, restauración o reconciliación de `HU-035`;
32. PostgreSQL real demuestra alcance, anti-IDOR, composición, paginación, cerca concurrente, minimización, ausencia de efectos y trigger append-only.

No aplican pruebas de UI, navegador, migración ni modelo EF porque no se autorizan esos cambios.

## 21. Gates, trazabilidad y eficacia

Después de la aprobación íntegra, durante el desarrollo se ejecutarán sólo pruebas enfocadas. Al final se ejecutarán una sola vez restore locked autorizado, build Release, suite local completa sin PostgreSQL real, format, arquitectura, API/contrato, seguridad, vulnerabilidades NuGet, espejo y protección de `Fuentes/` y `git diff --check`.

Cuando todos los gates locales estén listos se solicitará una única corrida externa consolidada de PostgreSQL. La implementación actualizará `docs/traceability/IMPLEMENTATION_STATUS.md` en el mismo cambio como `Propuesta implementada`, nunca como `Terminada`.

Presentar esta adenda no exige actualizar el registro de implementación: aún no existe implementación ni evidencia de cierre. Esta propuesta no autoriza commit, push, publicación de rama, PR o merge.

`HU-033` sólo será `Terminada` después de commit exacto, pipeline requerido exitoso sobre ese commit, aprobación humana, merge, ascendencia verificada en `origin/master`, PostgreSQL externo satisfactorio, protección de `Fuentes/` y cero defectos bloqueantes.

## 22. Preguntas cerradas por la propuesta

La aprobación íntegra responderá conjuntamente:

1. que las rutas son `GET /api/v1/audit-events` y `GET /api/v1/audit-events/{id}`;
2. que la colección admite exclusivamente los parámetros de la sección 9;
3. que `from` y `to` son obligatorios, UTC, `[from,to)` y máximo 31 días;
4. que el detalle identifica un evento y la trazabilidad se solicita con `traceObligationId`;
5. que la trazabilidad se enlaza sólo por relaciones persistidas y no por correlación inventada;
6. que la identidad vigente del lector concede autoridad y el sujeto histórico del recurso determina alcance;
7. que configuración sin sujeto es Dirección, salvo dependencia minimizada de una obligación visible;
8. que una traza parcialmente no autorizada converge a `404`, sin omisiones silenciosas;
9. que actores inactivos o de sistema se muestran sin reinterpretar identidad y sin nombres actuales;
10. que lista y detalle usan el DTO exacto de la sección 11;
11. que antes/después se proyecta con la allowlist exacta y motivo libre, `requestId` y `sourceIpHash` no se exponen;
12. que catálogos históricos desconocidos se conservan como texto opaco y no se normalizan;
13. que lista general ordena descendente y traza ascendente por instante e ID;
14. que el cursor se autentica, liga a actor/alcance/filtros y usa una cerca lógica entre páginas;
15. que cada petición usa PostgreSQL `REPEATABLE READ, READ ONLY`, `AsNoTracking`, un solo `queriedAt`, sin cache, ETag ni efectos;
16. que ninguna lectura genera auditoría funcional;
17. que el intento autenticado de DELETE no es endpoint funcional, responde `405` y genera exactamente el evento aprobado;
18. que un intento no autenticado no genera auditoría funcional;
19. que `HU-033` no necesita UI, migración, índice, vista SQL o persistencia adicional;
20. que `TECH-AUD-001` y productores existentes permanecen sin cambios;
21. que `HU-034` conserva idempotencia integral y conflictos; y
22. que `HU-035` conserva continuidad, restauración y reconciliación.

## 23. Decisiones solicitadas y aprobación íntegra

Se solicita aprobar o rechazar `F07_ADENDA_29` como una unidad. No existe aprobación parcial implícita. Si cualquier decisión no es aceptable, debe corregirse esta propuesta antes de producir código.

La pregunta de aprobación es: **¿se aprueba íntegramente `F07_ADENDA_29_CONTRATO_DE_CONSULTA_GENERAL_DE_AUDITORIA_HU_033.md`, sin cambios, para autorizar la implementación local de `HU-033` bajo este contrato?**
