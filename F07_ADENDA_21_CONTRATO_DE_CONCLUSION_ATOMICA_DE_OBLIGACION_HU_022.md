# SGOL — Adenda 21 a F07: contrato de conclusión atómica de obligación para `HU-022`

## 1. Control del documento

| Campo | Valor |
|---|---|
| Tipo | Propuesta de adenda normativa a `F07_BACKLOG_DE_IMPLEMENTACION.md` |
| Estado | `APROBADA ÍNTEGRAMENTE; IMPLEMENTACIÓN EN CURSO` |
| Fecha | 2026-09-08 |
| Historia | `HU-022 — Responsable concluye sólo con evidencia completa` |
| Corte y épica | `CV-03` / `EP-06` |
| Criterios | `CA-022`, `CP-022-P`, `CP-022-N`, `RN-016`, `RN-017`, `RN-018`, `RN-019` |
| Efecto pretendido | Precisar el contrato ejecutable de `HU-022` sin modificar ni renumerar el backlog aprobado |
| Base verificada | `origin/master = 8d28d654388fd532b9b85a6d29242f4a71494985`, merge de `HU-026`; rama de trabajo `codex/hu-022` creada exactamente desde ese corte |
| Conservación | Mantiene sin cambios F00–F07, `Fuentes/`, `docs/design/logo.svg`, `docs/design/mapa-pantallas.md` y los tres Designer ajenos identificados para esta tarea |
| Aprobación | Aprobación íntegra recibida del responsable el 2026-09-08 |

La aprobación íntegra autoriza exclusivamente la implementación aquí descrita. No autoriza commit, publicación, pull request ni merge.

## 2. Precedencia y orden efectivo

Se reconocen como hechos de cierre ya satisfechos:

1. `HU-019`: PR `#32`, commit implementado `ca62f925d8505715775a0f245ef818d1d292812f`, pipeline `TECH-BASE-003 / PR gates` `SUCCESS` run `33906693457` sobre ese SHA, aprobación humana, merge `0e58cc10ef66d54681eadfd63858941eb584e44b`, ambos SHA ancestros de `origin/master` y cero defectos bloqueantes conocidos.
2. `HU-024`, `TECH-EVID-001`, `HU-025` y `TECH-EVID-002`: cerradas conforme a sus registros y adendas aprobadas.
3. `HU-026`: Adenda 19 aprobada íntegramente; PR `#43`, commit implementado `bddf95fe2957fbea4b3b65bd0a29a17bb736a34d`, pipeline requerido `TECH-BASE-003 / PR gates` `SUCCESS` run `34277376838` sobre ese SHA, aprobación humana, PostgreSQL externo `3/3`, merge `8d28d654388fd532b9b85a6d29242f4a71494985`, `origin/master` exactamente en ese merge, ambos SHA ancestros de `origin/master` y cero defectos bloqueantes conocidos.

La tabla `Tareas insertadas por adenda` no contiene una tarea pendiente que bloquee `HU-022`. La tabla `Dependencias de infraestructura por momento` no agrega otra dependencia para este tramo. No existe una adenda posterior a `F07_ADENDA_20` en la base verificada antes de crear esta propuesta.

El orden efectivo es:

```text
HU-026 → HU-022 → HU-030 → HU-027 → HU-028
```

Esta adenda no adelanta `HU-030`, `HU-027`, `HU-028`, `HU-029`, `HU-031` ni otra historia posterior.

## 3. Fuentes y límites interpretativos

La propuesta se limita a la fila 26 de `F07_BACKLOG_DE_IMPLEMENTACION.md`, `CV-03`, `HU-022`, `CAP-027`, `CA-022`, `CP-022-P`, `CP-022-N`, `RN-016` a `RN-019`, `DEC-021`, `DEC-022`, `DEC-066`, los límites F04 de conclusión ordinaria, los contratos F06 directamente aplicables, las adendas 15 a 20 y la implementación fusionada de obligación, asignación vigente, autorización, política congelada, evidencia versionada, evaluación y snapshot de `HU-026`, idempotencia, auditoría, PostgreSQL y observabilidad.

Se conservan estos hechos:

- la obligación sólo persiste `PENDIENTE` o `CONCLUIDA`;
- `VENCIDA` es una bandera derivada y no una transición;
- no existe inicio explícito ni medición de duración en el MVP;
- toda evidencia configurada y aplicable es obligatoria;
- la política capturada por `work_obligation.evidence_policy_version_id` es la única política autorizada para esa obligación;
- sólo una versión funcional `VIGENTE` puede satisfacer un requisito;
- `F_ENT_001` vigente es la única autoridad de `DIFERENCIA_O_DANO` para `TAR-0092`;
- evaluación, conclusión y validación son hechos separados; y
- excepción, cierre forzado y reapertura quedan fuera del MVP actual.

## 4. Contradicciones y resolución propuesta

### 4.1 Estado legacy `EN_PROCESO`

`CAP-027` en F02 describía conclusión sólo desde `EN_PROCESO`. F04 dejó `CAP-026` —inicio explícito y medición temporal— para una versión posterior. Después, `RN-016`, la fila de `HU-022`, `TR-015` y `TP-008` aprobaron expresamente la transición directa `PENDIENTE → CONCLUIDA` y prohibieron `PENDIENTE → INICIADA` en el MVP.

Para `HU-022` prevalece esa especificación posterior y aprobada. No se crea `EN_PROCESO`, `INICIADA`, fecha de inicio ni duración.

### 4.2 Momento de crear `validation_requirement`

F05 indica que una validación requerida entra en `PENDIENTE` al concluir y F06 enumera `validation_requirement` como una fila por obligación concluida validable. Sin embargo, `HU-027` —que versionará la política de validación— todavía no está implementada y depende de `HU-022`; por tanto, `HU-022` no dispone de una política aprobada que pueda referenciar sin inventarla. El alcance expreso de esta tarea también prohíbe anticipar persistencia de validación.

La resolución propuesta es:

1. `HU-022` no crea `validation_requirement`, `validation_decision_version` ni configuración de validación;
2. la conclusión queda completamente separada y reconstruible mediante `execution_result` y auditoría;
3. `HU-027` definirá la política sin reescribir conclusiones; y
4. `HU-028` deberá cerrar, antes de implementarse, cómo materializa de forma idempotente el requisito de validación para una obligación ya concluida usando la política aprobada, sin alterar `execution_result` ni el snapshot de conclusión.

La aprobación íntegra de esta adenda constituye la decisión humana que resuelve exclusivamente ese momento técnico. Si se exige crear `validation_requirement` durante `HU-022`, esta propuesta se rechaza y la implementación permanece detenida hasta aprobar un contrato de política de validación compatible.

### 4.3 Ocultación anti-IDOR

F06 contempla `403` como respuesta genérica a falta de permiso, pero este comando sólo existe sobre un recurso propio y la lista de respuestas exigida para `HU-022` no incluye `403`. Para este endpoint, toda denegación autenticada por permiso, sucursal, relación de responsabilidad o recurso oculto converge en `404 OBLIGACION_NO_ENCONTRADA`. Esta regla específica evita distinguir inexistencia de existencia no autorizada.

## 5. Resultado cerrado

`HU-022` agrega exclusivamente:

```http
POST /api/v1/obligations/{id}/conclusion
```

El comando concluye una sola vez una obligación propia `PENDIENTE` cuando la evaluación actual de su política congelada es `COMPLETA`. En una sola transacción crea un `execution_result`, lo vincula al snapshot completo exacto, actualiza la obligación, incrementa su versión y registra la auditoría de éxito.

No aporta, sustituye, inspecciona, descarga ni promueve evidencia. No valida, no crea avisos y no emite outbox.

## 6. Contrato de solicitud

### 6.1 Ruta, query y cuerpo

- La ruta aprobada es únicamente `POST /api/v1/obligations/{id}/conclusion`.
- `{id}` debe ser un UUID canónico no vacío.
- No se acepta query string.
- La solicitud no tiene cuerpo. Debe llegar con cuerpo de longitud cero; `{}`, `null`, cualquier JSON, bytes adicionales o `Content-Type` acompañado de contenido devuelven `400 SOLICITUD_CONCLUSION_INVALIDA`.
- No se agregan alias, ruta masiva, operación administrativa ni parámetro de resultado.

### 6.2 Encabezados obligatorios

- `Idempotency-Key`: exactamente una ocurrencia, UUID canónico no vacío.
- `If-Match`: exactamente una ocurrencia, ETag fuerte con la forma `"<rowVersion>"`, donde `rowVersion` es entero decimal positivo. `*`, ETag débil, cero, negativo, lista o valor sin comillas se rechazan.
- La sesión de navegador usa cookie segura. La mutación exige token CSRF válido ligado a esa sesión. Ausencia, repetición o invalidez devuelve `400 CSRF_INVALIDO` antes de invocar el servicio.

El endpoint no acepta identidad, responsable, snapshot, política, evidencia, actor, instante, estado o resultado enviados por el cliente.

## 7. Respuesta exitosa

Tanto la creación como la recuperación idempotente devuelven `200 OK`, `Content-Type: application/json` y el ETag nuevo de la obligación:

```http
ETag: "8"
```

```json
{
  "data": {
    "obligationId": "019...",
    "executionStatus": "CONCLUIDA",
    "concludedAt": "2026-09-08T22:00:00Z",
    "concludedBy": "019...",
    "rowVersion": 8,
    "executionResult": {
      "id": "019...",
      "resultCode": "CONCLUIDA",
      "resultPayload": {
        "schemaVersion": 1
      },
      "evidenceReviewSnapshotId": "019...",
      "recordedBy": "019...",
      "recordedAt": "2026-09-08T22:00:00Z"
    }
  },
  "meta": {
    "correlationId": "019..."
  }
}
```

`concludedBy` y `recordedBy` son el mismo `app_user.id`; `concludedAt` y `recordedAt` son el mismo instante UTC. `resultCode` sólo puede ser `CONCLUIDA`. `resultPayload` es exactamente `{"schemaVersion":1}` y no admite observación, motivo, porcentaje, texto libre, datos de evidencia ni extensiones.

La recuperación devuelve los mismos IDs, estado, instante, actor, snapshot, payload y `rowVersion` de la primera respuesta. Sólo `meta.correlationId` refleja la petición actual.

## 8. Errores permitidos

Todo error usa `application/problem+json`, incluye `type`, `title`, `status`, `code`, `instance` y `correlationId`, y no expone detalles internos.

| HTTP | Código | Uso exacto |
|---:|---|---|
| `400` | `OBLIGACION_ID_INVALIDO` | UUID de ruta inválido o vacío. |
| `400` | `SOLICITUD_CONCLUSION_INVALIDA` | Query, cuerpo o forma de solicitud no permitida. |
| `400` | `IDEMPOTENCY_KEY_REQUERIDA` | Falta `Idempotency-Key`. |
| `400` | `IDEMPOTENCY_KEY_INVALIDA` | Clave repetida, no canónica, vacía o no UUID. |
| `400` | `IF_MATCH_REQUERIDO` | Falta `If-Match`. |
| `400` | `IF_MATCH_INVALIDO` | ETag repetido, débil, múltiple o con formato/valor inválido. |
| `400` | `CSRF_INVALIDO` | Token CSRF ausente o inválido para la sesión de navegador. |
| `401` | `AUTENTICACION_REQUERIDA` | Falta sesión individual válida o MFA completo. |
| `404` | `OBLIGACION_NO_ENCONTRADA` | Inexistente, otra sucursal, permiso/identidad vigente insuficiente, recurso ajeno o actor distinto del responsable vigente. |
| `409` | `IDEMPOTENCY_CONFLICT` | La misma clave del mismo scope ya concluyó con otro hash canónico. |
| `409` | `OBLIGACION_YA_CONCLUIDA` | La obligación ya está `CONCLUIDA` y no corresponde a una recuperación de la clave original. |
| `409` | `CONCLUSION_INCONSISTENTE` | Política, requisitos, evidencia, snapshot o vínculos incoherentes; colisión/desacuerdo de snapshot; o restricción PostgreSQL que impide afirmar coherencia. |
| `409` | `CONCLUSION_CONCURRENCIA_CONFLICTO` | Se agotó el máximo acotado de recuperación por `40001`, `40P01` o carrera única. |
| `412` | `VERSION_CONFLICT` | La clave no es replay y `If-Match` no coincide con `work_obligation.row_version`. |
| `422` | `EVIDENCIA_FALTANTE` | La evaluación actual es `INCOMPLETA`; la obligación permanece `PENDIENTE`. |

`EVIDENCIA_FALTANTE` añade exclusivamente:

```json
{
  "errors": [
    {
      "field": "evidence",
      "code": "MISSING",
      "reference": "FOTO_FINAL"
    }
  ]
}
```

`errors` contiene sólo los `requirementCode` aplicables faltantes, en el orden canónico de `HU-026`. No incluye IDs de versiones, contenido, payload estructurado, metadatos de archivo ni detalles de una inconsistencia. Una condición `NO_RESUELTA` produce `EVIDENCIA_FALTANTE`; una condición o historia corrupta produce `CONCLUSION_INCONSISTENTE`.

No se usa `403`, `201`, `202`, `204`, `413`, `415`, `423`, `429` ni `503` en este endpoint.

## 9. Autorización y estado

La única autoridad funcional es `PER-TAREA-EJECUTAR`. No se crea otro permiso y ninguno de `PER-TAREA-VER`, `PER-EVIDENCIA-APORTAR`, `PER-EVIDENCIA-SUSTITUIR`, `PER-ASIGNACION-CORREGIR`, un rol superior o Dirección concede por sí mismo conclusión de una obligación ajena.

En un único `concludedAt = IClock.UtcNow`, el actor debe tener:

1. sesión individual con MFA completo;
2. `app_user` `ACTIVA` ligada a una persona;
3. empleo `ACTIVA` y vigente en `LOR-001`;
4. exactamente un rol canónico `ACTIVO` y vigente en `LOR-001`;
5. `PER-TAREA-EJECUTAR`; y
6. coincidencia exacta entre su `person_id` y el `person_id` de la única `assignment_version` `VIGENTE` de la obligación.

Los cuatro roles canónicos pueden ejercer `PER-TAREA-EJECUTAR` únicamente sobre su propia asignación vigente. Responsabilidad histórica, par, superior, inferior, puesto textual, turno, publicación, creador de la obligación o aportante de evidencia no conceden autoridad.

La obligación debe estar inicialmente `PENDIENTE`. Se permite concluir antes de `due_at`; no existe guarda de “no iniciar antes”. Estar vencida no bloquea, permite ni provoca conclusión y no modifica el resultado.

No existe cierre forzado, reapertura, excepción administrativa, dispensa de evidencia ni transición distinta de `PENDIENTE → CONCLUIDA`.

## 10. Orden de validación y ocultación

El orden observable es:

1. validar método/ruta, ausencia de query/cuerpo y formatos de encabezados;
2. validar sesión, MFA y CSRF;
3. abrir la transacción y bloquear la obligación de `LOR-001` por ID;
4. bloquear su asignación `VIGENTE` y revalidar cuenta, persona, empleo, rol, permiso y responsabilidad actual;
5. si cualquiera de los pasos 3 o 4 falla, devolver el mismo `404 OBLIGACION_NO_ENCONTRADA` sin consultar idempotencia, versión, estado, política, evidencia o snapshot;
6. consultar el registro de idempotencia del scope exacto;
7. si existe y el hash coincide, recuperar la respuesta original antes de comparar el estado o la versión actuales;
8. si existe y el hash difiere, devolver `409 IDEMPOTENCY_CONFLICT`;
9. si no existe, comparar `If-Match`; una diferencia devuelve `412 VERSION_CONFLICT`;
10. exigir estado `PENDIENTE`; `CONCLUIDA` devuelve `409 OBLIGACION_YA_CONCLUIDA`; y
11. revalidar política, evidencia y snapshot antes de escribir.

Así, una repetición auténtica después de concluir recupera la primera respuesta; una clave nueva no puede volver a concluir. La idempotencia no evita que la autorización actual se revalide y nunca revela un resultado a quien dejó de estar autorizado.

## 11. Evidencia, política y snapshot

### 11.1 Autoridad de evaluación

La única política es `work_obligation.evidence_policy_version_id`. No se consulta como sustituto la política actualmente `VIGENTE` de la TAR, no se hace backfill y una FK nula o incoherente falla cerrada con `409 CONCLUSION_INCONSISTENTE`.

La conclusión reutiliza literalmente `EvidenceReviewEvaluator` y el catálogo cerrado aprobados en `HU-026`:

- todos los requisitos configurados y aplicables son obligatorios;
- sólo evidencia `VIGENTE` del mismo ítem, requisito, política y obligación cuenta;
- `SUSTITUIDA` nunca satisface;
- binario sólo satisface si el vínculo persistido ya es `LIMPIO/CLEAN`;
- estructurado debe cumplir el contrato cerrado y las tres relaciones nominales aprobadas;
- para `TAR-0092`, `F_ENT_001` vigente resuelve exclusivamente `DIFERENCIA_O_DANO`; y
- política, requisito, historia o payload incoherente falla cerrado.

La conclusión no vuelve a inspeccionar archivos ni llama S3, SeaweedFS o ClamAV. Sólo lee los hechos funcionales ya persistidos.

### 11.2 Snapshot consumido

Dentro de la misma transacción se vuelve a calcular la entrada canónica y su huella con las versiones bloqueadas. La evaluación debe ser `COMPLETA`.

- Si existe un `evidence_review_snapshot` `COMPLETA` con la misma obligación, política, entrada canónica, huella, proyección y conjunto exacto de `evidence_version.id`, se reutiliza.
- Si no existe y la evaluación actual es `COMPLETA`, el módulo `Evidence` materializa el snapshot exacto dentro de la transacción de conclusión.
- Si la evaluación es `INCOMPLETA`, no se crea snapshot y se devuelve `422 EVIDENCIA_FALTANTE`.
- Un snapshot anterior cuya huella difiere es obsoleto y no puede vincularse.
- Un snapshot anterior `INCOMPLETA` nunca puede vincularse aunque su obligación coincida.

`execution_result.evidence_review_id` apunta inmutablemente al snapshot `COMPLETA` exacto utilizado. Una sustitución posterior a la conclusión no modifica ese snapshot ni el resultado; conserva la fotografía histórica del cierre. Una sustitución que se confirma antes de la conclusión obliga a recalcular con la sucesora. Una sustitución concurrente se serializa antes o después y nunca produce mezcla de versiones.

La conclusión es independiente de cualquier validación posterior: un resultado de validación futuro no cambia `execution_status`, `execution_result` ni el snapshot de cierre.

## 12. Idempotencia

El scope exacto es:

```text
obligation:conclusion:{actorUserId:D}:{obligationId:D}
```

El hash SHA-256 minúsculo se calcula sobre UTF-8 con saltos LF y representación canónica exacta:

```text
POST
/api/v1/obligations/{obligationId:D}/conclusion
{expectedRowVersion en decimal invariante}
```

Actor, operación y recurso ya forman parte del scope; `correlationId`, cookies, token CSRF y orden físico de encabezados no forman parte del hash. Como no existe cuerpo, `If-Match` es el único contenido variable de la intención.

Sólo una conclusión confirmada consume durablemente la clave y guarda un `idempotency_record` `COMPLETED` con `resource_type = EXECUTION_RESULT`, `resource_id = execution_result.id` y `response_code = 200`. Las solicitudes `400`, `401`, `404`, `409`, `412` o `422` no crean ni modifican idempotencia y pueden reintentarse tras corregir su causa.

Misma clave, mismo scope y mismo hash recupera la respuesta persistida. Misma clave y mismo scope con hash distinto devuelve `409 IDEMPOTENCY_CONFLICT`. Una clave usada por otro actor o recurso pertenece a otro scope y no concede acceso ni produce conflicto observable fuera del recurso autorizado.

## 13. Transacción y concurrencia

La operación usa PostgreSQL `SERIALIZABLE` y como máximo tres intentos totales. Cada intento vuelve a ejecutar autorización, lectura, evaluación y escritura completas. El orden determinista es:

1. `work_obligation` por ID `FOR UPDATE`;
2. `assignment_version` `VIGENTE` de esa obligación `FOR UPDATE`;
3. actor, persona, empleo y rol vigentes en orden estable de ID cuando requieran bloqueo;
4. política congelada y requisitos inmutables en `ordinal, id`;
5. `evidence_item` en `ordinal, id` `FOR UPDATE`;
6. `evidence_version` `VIGENTE` en el mismo orden `FOR UPDATE` y sus `file_object` vinculados cuando apliquen;
7. snapshot coincidente o nuevo;
8. `execution_result`;
9. transición de obligación e incremento de `row_version`;
10. `idempotency_record`; y
11. `audit_event`.

Los flujos fusionados de corrección de asignación y aporte/sustitución de evidencia también bloquean primero la obligación. Por ello:

- si el cambio de responsable confirma antes, el responsable anterior recibe `404`;
- si la conclusión obtiene primero el bloqueo, confirma y la corrección posterior encuentra la obligación `CONCLUIDA`, por lo que no puede cambiar la asignación;
- si la sustitución confirma antes, la conclusión usa sólo la versión sucesora;
- si la conclusión obtiene primero el bloqueo, usa la versión todavía vigente y la sustitución posterior conserva como histórico el snapshot exacto del cierre.

`40001`, `40P01` y las violaciones únicas esperables sobre idempotencia, snapshot o `execution_result` se recuperan releyendo al ganador sólo si scope, hash, obligación, actor, snapshot y proyección coinciden exactamente; de otro modo se reintenta la transacción completa. Agotado el límite se devuelve `409 CONCLUSION_CONCURRENCIA_CONFLICTO`. No hay efectos parciales.

## 14. Persistencia de `work_obligation`

La transición autorizada actualiza conjuntamente:

| Columna | Valor |
|---|---|
| `execution_status` | `CONCLUIDA` |
| `concluded_at` | `concludedAt` UTC capturado una vez |
| `concluded_by` | `actorUserId` autorizado |
| `row_version` | valor anterior más uno, exactamente |

Se agrega FK `RESTRICT` de `work_obligation.concluded_by` a `app_user.id`. La guarda PostgreSQL conserva `PENDIENTE` con campos de conclusión nulos y `CONCLUIDA` con ambos no nulos; además impide revertir, cambiar actor/instante, saltar versión o repetir la transición.

## 15. Persistencia de `execution_result`

El módulo `Execution` es propietario de la única tabla nueva:

| Columna | Tipo PostgreSQL | Regla |
|---|---|---|
| `id` | `uuid` | PK; UUID v7 generado por aplicación. |
| `obligation_id` | `uuid` | FK `RESTRICT` a `work_obligation.id`; único y no nulo. |
| `result_code` | `varchar(16)` | Exactamente `CONCLUIDA`. |
| `result_payload` | `jsonb` | Exactamente el objeto `{"schemaVersion":1}`. |
| `evidence_review_id` | `uuid` | FK `RESTRICT` a `evidence_review_snapshot.id`; no nulo y único. |
| `recorded_by` | `uuid` | FK `RESTRICT` a `app_user.id`; no nulo. |
| `recorded_at` | `timestamptz` | Instante UTC no nulo. |

Índices y unicidades:

- PK `execution_result(id)`;
- único `execution_result(obligation_id)`;
- único `execution_result(evidence_review_id)`; y
- índice `execution_result(recorded_by, recorded_at, id)`.

Checks y guardas PostgreSQL impiden:

- código o payload distinto del catálogo cerrado;
- resultado de otra obligación;
- snapshot distinto de `COMPLETA`, obsoleto o con política/entrada/versiones incoherentes;
- actor distinto del responsable `VIGENTE` autorizado;
- diferencia entre `recorded_by`/`recorded_at` y `concluded_by`/`concluded_at`;
- una obligación `CONCLUIDA` sin exactamente un resultado o un resultado cuyo estado no sea `CONCLUIDA` al final de la transacción;
- `UPDATE` o `DELETE` de `execution_result`; y
- reversión o reescritura de la conclusión.

Las comprobaciones cruzadas que requieren observar el estado final se implementan mediante constraint triggers diferibles y se validan al confirmar la transacción. PostgreSQL es la autoridad final aunque la aplicación tenga una falla.

No se crean `validation_requirement`, `validation_decision_version`, `internal_notice`, tabla de bandeja, tabla de reapertura, tabla de excepción ni outbox.

## 16. Auditoría

La primera conclusión crea exactamente un `audit_event` en la misma transacción:

- `action = OBLIGATION_CONCLUDED`;
- `resource_type = WORK_OBLIGATION`;
- `resource_id = obligationId`;
- `actor_type = USER` y `actor_user_id = concludedBy`;
- `request_id = Idempotency-Key` canónica;
- `occurred_at = concludedAt`;
- `before_data`: `schemaVersion`, `executionStatus = PENDIENTE`, `rowVersion` anterior y `assignmentVersionId`;
- `after_data`: `schemaVersion`, `executionStatus = CONCLUIDA`, `rowVersion` nuevo, `executionResultId`, `evidenceReviewSnapshotId`, `concludedBy` y `concludedAt`; y
- `outcome = CONCLUIDA`.

No incluye lista de evidencia, requisitos, payload estructurado, nombres, URLs, claves, buckets, hashes de archivos, contenido ni datos de validación. Si insertar auditoría falla, se revierten snapshot nuevo, resultado, idempotencia y transición.

Una recuperación idempotente no crea un segundo evento de conclusión. Una solicitud rechazada no crea auditoría de éxito, snapshot, `execution_result`, transición, outbox ni otro efecto de negocio.

## 17. Seguridad, privacidad y observabilidad

Se aplica denegación por defecto y autorización dentro de la transacción. El servidor no confía en identidad, alcance, estado, política, ETag, evidencia o snapshot aportados por el cliente.

Respuesta, logs y métricas no exponen contenido de evidencia, `structured_payload`, `result_payload` privado distinto del objeto cerrado, nombre de archivo, media type, tamaño, URL, clave S3, bucket, SHA-256 de archivo, credencial, cookie, CSRF, secreto ni detalle de una inconsistencia.

Los logs estructurados contienen como máximo operación, resultado de baja cardinalidad y `correlationId`. No registran `Idempotency-Key` completa. Los fallos inesperados devuelven una referencia correlacionada sin stack trace ni SQL.

La métrica exacta es:

```text
sgol_obligation_conclusions_total{result}
```

`result` sólo admite `success`, `incomplete`, `version_conflict`, `idempotency_conflict`, `inconsistency` o `failure`. No se etiquetan actor, obligación, TAR, requisito, snapshot, archivo, clave ni correlation ID. Los `404` se agrupan en `failure` sin distinguir inexistencia de denegación.

Toda inconsistencia falla cerrada. No se invoca infraestructura de archivos como recuperación.

## 18. Pruebas obligatorias

### 18.1 Unitarias, HTTP y arquitectura sin Docker

- responsable vigente concluye una obligación propia `PENDIENTE` con evaluación actual `COMPLETA`;
- se crea un único `execution_result` con código/payload cerrados y el snapshot completo exacto;
- estado, `concluded_at`, `concluded_by` y `row_version` cambian una sola vez;
- ejecución anticipada funciona y vencimiento no concluye ni bloquea por sí solo;
- evidencia incompleta devuelve `422`, mantiene `PENDIENTE` y no crea snapshot, resultado ni auditoría de éxito;
- TAR-0092 aplicable exige la foto y no aplicable no la exige;
- política congelada gobierna aunque exista otra política vigente;
- snapshot obsoleto y evidencia `SUSTITUIDA` no permiten concluir;
- sustitución concurrente no mezcla versiones;
- cambio concurrente de responsable impide al responsable anterior concluir;
- `If-Match` vigente funciona; desactualizado devuelve `412`; ausente o inválido devuelve `400`;
- `Idempotency-Key` ausente o inválida devuelve `400`;
- misma clave y solicitud recupera la misma respuesta; contenido distinto devuelve `409`;
- carreras con claves diferentes crean un resultado y una conclusión;
- par, superior, inferior y ajeno reciben el mismo `404` que un ID inexistente;
- fallos de política, requisitos, evidencia o snapshot devuelven inconsistencia sin efectos;
- auditoría comparte transacción y su fallo revierte todo;
- no se crean validaciones, avisos ni outbox;
- no existe UI ni ruta adicional; y
- no hay referencias ni invocaciones nuevas a S3, SeaweedFS o ClamAV.

### 18.2 PostgreSQL real externa

- migración desde cero y desde la base vigente;
- PK, FK `RESTRICT`, checks, índices, unicidades y constraint triggers son autoridad final;
- `execution_result` rechaza código, payload, obligación, snapshot, actor o instante incoherentes;
- una obligación concluida sin resultado, un resultado sobre pendiente, `UPDATE`, `DELETE`, reapertura o segunda conclusión se rechazan;
- carreras con igual y distinta clave producen una sola transición y un solo resultado;
- sustitución y corrección concurrentes respetan el orden aprobado;
- una falla de auditoría revierte resultado, snapshot nuevo, idempotencia y obligación; y
- cero pruebas omitidas.

No se usa SQLite. Las suites PostgreSQL, Docker o Testcontainers las ejecuta externamente el desarrollador; la sesión no ejecuta ni diagnostica Docker.

## 19. Gates y trazabilidad posteriores a la aprobación

Después de aprobar íntegramente el contrato e implementar el cambio, los gates se ejecutan una sola vez y en este orden:

1. `dotnet restore SGOL.slnx --locked-mode` fuera del aislamiento;
2. `dotnet build SGOL.slnx --no-restore --configuration Release`;
3. suite unitaria completa;
4. suite de arquitectura completa;
5. pruebas enfocadas `HU-022` sin Docker;
6. solicitud única al desarrollador de las suites PostgreSQL afectadas y espera de su resultado;
7. `dotnet format SGOL.slnx --verify-no-changes --no-restore`;
8. `./scripts/ci/Assert-NoVulnerablePackages.ps1`;
9. modelo EF sin cambios pendientes;
10. `./scripts/ci/verify-fuentes-protection.ps1`;
11. `./scripts/ci/verify-fuentes-mirror.ps1`, después y no en paralelo con el anterior; y
12. `git diff --check`.

En el mismo cambio se actualizarán la Adenda 21 aprobada, `docs/traceability/README.md`, `docs/traceability/IMPLEMENTATION_STATUS.md`, la documentación técnica mínima, el inventario de migraciones y la documentación de la suite PostgreSQL.

La rama registrará `HU-022` como propuesta. Sólo adquiere eficacia `Terminada` en `master` cuando el commit exacto tenga pipeline requerido verde, aprobación humana, merge, ascendencia verificada en `origin/master`, PostgreSQL satisfactorio y cero defectos bloqueantes conocidos.

Edición, commit, publicación, apertura de PR y merge son autorizaciones independientes.

## 20. Fuera de alcance

No se implementan ni simulan:

- `HU-030`, `HU-027`, `HU-028`, `HU-029`, `HU-031` u otra historia posterior;
- aporte, sustitución, inspección, descarga, promoción o borrado de evidencia;
- S3, SeaweedFS, ClamAV, URL firmada o adaptador de archivos;
- `validation_requirement`, `validation_decision_version`, configuración o decisión de validación;
- `internal_notice`, bandejas o mensajería;
- excepción, cancelación, postergación, traslado, cierre forzado o reapertura;
- UI, página Razor, componente, CSS o JavaScript de pantalla;
- outbox, broker, Redis, microservicio, scheduler u otro Worker;
- motor general de reglas;
- paquete, herramienta, navegador o servicio nuevo;
- cambio de CI no exigido por este contrato; ni
- cambios en F00–F07 o `Fuentes/`.

## 21. Decisión íntegra solicitada

Aprobar esta adenda significa aprobar conjuntamente:

1. la ruta única, ausencia de cuerpo, encabezados, CSRF, respuesta, ETag y catálogo cerrado de errores;
2. `resultCode = CONCLUIDA` y `resultPayload = {"schemaVersion":1}` como única representación de `execution_result`;
3. autorización exclusiva del responsable `VIGENTE` con `PER-TAREA-EJECUTAR` y convergencia `404` contra IDOR;
4. transición única `PENDIENTE → CONCLUIDA`, ejecución anticipada y vencimiento puramente derivado;
5. política congelada, algoritmo de `HU-026`, evidencia `VIGENTE`, condición TAR-0092 y snapshot `COMPLETA` exacto;
6. scope, hash, recuperación y precedencia de idempotencia frente a versión y estado;
7. transacción `SERIALIZABLE`, orden de bloqueo, recuperación acotada y ausencia de efectos parciales;
8. tabla `execution_result`, cambio de `work_obligation`, FK, checks, índices, constraint triggers e inmutabilidad;
9. auditoría atómica única, privacidad, métricas de baja cardinalidad y fallo cerrado;
10. resolución de no crear persistencia de validación durante `HU-022` y diferir su materialización contractual a `HU-027`/`HU-028`; y
11. pruebas, gates, trazabilidad, cierre condicional y todas las exclusiones.

No se interpreta como aprobación íntegra la existencia de este archivo, una aprobación parcial, una autorización de edición ni la aprobación de una adenda anterior. Cualquier cambio material posterior exige una nueva decisión antes de implementar.

**Decisión recibida:** `Apruebo la adenda integramente`.
