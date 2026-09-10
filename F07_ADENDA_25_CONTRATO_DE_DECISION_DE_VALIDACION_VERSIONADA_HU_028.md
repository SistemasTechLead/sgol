# SGOL — Adenda 25 a F07: contrato de decisión de validación versionada para `HU-028`

## 1. Control del documento

| Campo | Valor |
|---|---|
| Tipo | Adenda normativa a `F07_BACKLOG_DE_IMPLEMENTACION.md` |
| Estado | `APROBADA ÍNTEGRAMENTE` |
| Fecha | 2026-09-10 |
| Historia | `HU-028 — Superior inmediato emite/sustituye validación separada` |
| Corte y épica | `CV-04` / `EP-08` |
| Efecto propuesto | Precisar exclusivamente el contrato ejecutable de `HU-028` sin adelantar `HU-029`, `HU-031`, `HU-032` ni historias posteriores |
| Precedencia | `HU-027` reconocida como `Terminada` efectiva: commit implementado `72d4658136e8d2dfdfc30b4faac93658769b8db6`, PR `#47` merged, pipeline `SUCCESS` run `34525053653`, aprobación humana, merge `c1fc6595de21095186a54d15617a4e07c4142be2` y ascendencia verificada en `origin/master` |
| Conservación | Mantiene sin cambios los documentos F00–F07 aprobados y `Fuentes/` |
| Aprobación | Aprobada íntegramente por el responsable el 2026-09-10 antes de producir código; no autoriza commit, publicación de rama, pull request ni merge |

El siguiente identificador libre verificado es `F07_ADENDA_25`: en la raíz existen adendas numeradas hasta `F07_ADENDA_24` y no existe otra Adenda 25.

## 2. Hechos documentales que no se reabren

1. `HU-028`, `CAP-033`, `CA-028`, `CP-028-P`, `CP-028-N`, `RN-020` a `RN-024`, `PRM-006`, `PRM-007` y `CPE-002` exigen una decisión humana separada de la ejecución.
2. Sólo se valida una obligación `CONCLUIDA`; validar no modifica `execution_status`, `execution_result` ni el snapshot de conclusión.
3. Los únicos resultados son `CUMPLIDA`, `INCOMPLETA` y `NO_CUMPLIDA`.
4. La autoridad ordinaria es `SUPERIOR_INMEDIATO` conforme a la versión exacta capturada en `work_obligation.validation_policy_version_id`.
5. Un nivel posterior actúa sólo por escalamiento y con motivo.
6. Nadie se autovalida salvo `DIRECCION`; la excepción debe auditarse.
7. Sólo existe una decisión `VIGENTE`; sustituir exige autoridad y motivo y conserva la anterior como `SUSTITUIDA`.
8. Una decisión desfavorable no reabre, cancela ni crea automáticamente otra obligación.
9. `HU-027` dejó nullable e inmutable `work_obligation.validation_policy_version_id`, sin backfill, y reservó para `HU-028` la materialización de `validation_requirement` y `validation_decision_version`.
10. Una sustitución de evidencia posterior a la conclusión no modifica el snapshot de conclusión ni una decisión de validación ya emitida.

La matriz congelada por `HU-027` se reutiliza literalmente:

| `taskCode` | `executorRole` | `validatorRole` |
|---|---|---|
| `TAR-0005` | `SUBCOORDINACION` | `ADMINISTRACION` |
| `TAR-0007` | `PISO_VENTAS` | `SUBCOORDINACION` |
| `TAR-0008` | `SUBCOORDINACION` | `ADMINISTRACION` |
| `TAR-0011` | `SUBCOORDINACION` | `ADMINISTRACION` |
| `TAR-0018` | `PISO_VENTAS` | `SUBCOORDINACION` |
| `TAR-0026` | `ADMINISTRACION` | `DIRECCION` |
| `TAR-0092` | `SUBCOORDINACION` | `ADMINISTRACION` |
| `TAR-0093` | `SUBCOORDINACION` | `ADMINISTRACION` |

## 3. Carencia contractual comprobada

### 3.1 Hechos

F05 y F06 fijan la historia, permisos, tres rutas de decisión/historia, estados conceptuales, separación de ejecución y garantías transversales. No fijan de manera exacta:

- cuándo se crea `validation_requirement` ni qué ocurre con obligaciones cuyo snapshot de política sea `NULL`;
- DTOs completos, respuestas, ETag, idempotencia, orden histórico o códigos de error;
- instante y algoritmo exactos de autoridad ordinaria, escalamiento, sustitución y autovalidación de Dirección;
- forma y límites de `foundation`, `reason` y `escalationReason`;
- snapshot exacto de evidencia ni el efecto de una sustitución de evidencia posterior;
- restricciones, índices, FKs, guardas, concurrencia y eventos de auditoría; ni
- si `GET /api/v1/validations/pending` corresponde a esta historia o a `HU-031`.

### 3.2 Inferencias que no autorizan código

Los patrones ya aprobados de evidencia, conclusión, snapshots, ETag, idempotencia, autorización y auditoría son técnicamente reutilizables, pero no autorizan trasladar silenciosamente sus DTOs, límites, scopes, errores o semántica a validación. La fila conceptual de F06 tampoco decide si el requisito debe crearse retroactivamente ni cuál de varias fotografías de evidencia sustenta la decisión.

### 3.3 Propuesta cerrada

Las secciones 4 a 17 resuelven únicamente esas carencias. Toda su eficacia depende de una aprobación íntegra y explícita posterior.

## 4. Alcance y exclusiones

### 4.1 Incluido

- materialización de `validation_requirement` para obligaciones con política congelada no nula;
- emisión ordinaria, escalada o por autovalidación excepcional de Dirección;
- sustitución versionada y motivada;
- historial por obligación;
- snapshot exacto de evidencia vigente al decidir;
- autorización, anti-IDOR, ETag, idempotencia, concurrencia, auditoría y pruebas;
- una migración forward-only para requisito y decisiones; y
- trazabilidad mínima de `HU-028`.

### 4.2 Excluido

- `GET /api/v1/validations/pending`, reservado expresamente a `HU-031` junto con identificación y supervisión de validaciones pendientes;
- `HU-029`, `HU-031`, `HU-032`, bandejas, supervisión, indicadores y tableros;
- UI, navegador, avisos, mensajería, Worker, scheduler, outbox, broker, Redis, microservicio o integración externa;
- cambio de la política aprobada en `HU-027`, backfill de `work_obligation.validation_policy_version_id` o selección de la política actualmente vigente como sustituto;
- reapertura, cancelación, mutación del resultado de ejecución o creación automática de tareas correctivas; y
- cambios en `Fuentes/`.

No se añade UI: ninguna fuente aprobada define una superficie visual de `HU-028`.

## 5. Materialización de `validation_requirement`

1. Una conclusión nueva crea, dentro de la misma transacción de `POST /api/v1/obligations/{id}/conclusion`, un `validation_requirement` `PENDIENTE` cuando `work_obligation.validation_policy_version_id` no es nulo. La creación ocurre después de comprobar la conclusión y antes de confirmar idempotencia y auditoría; si falla, se revierte también la conclusión.
2. La conclusión conserva su DTO y ETag actuales. Crear el requisito no incrementa `work_obligation.row_version`, porque no cambia la ejecución.
3. Para una obligación ya `CONCLUIDA`, con política congelada no nula y sin requisito, la primera emisión autorizada materializa el requisito y la primera decisión en una sola transacción. `created_at` del requisito es el `concluded_at` ya persistido; no se fabrica un instante nuevo de pendencia.
4. Dos intentos concurrentes de materialización sólo pueden producir un requisito por la unicidad de `obligation_id`.
5. No existe migración de datos ni lectura con efecto lateral. El histórico GET no crea requisitos.
6. Una obligación con `validation_policy_version_id = NULL` no recibe requisito ni decisión. No se consulta la política vigente, no se reescribe la FK y se devuelve `409 POLITICA_VALIDACION_NO_DISPONIBLE`.
7. Una obligación `PENDIENTE` no recibe requisito por esta historia; la emisión devuelve `409 OBLIGACION_NO_CONCLUIDA`.

La ausencia de backfill conserva literalmente `HU-027`. Las obligaciones históricas con FK nula sólo podrían habilitarse mediante otra decisión documental y una migración explícita posterior, fuera de `HU-028`.

## 6. Reglas comunes de texto y forma JSON

Los cuerpos aceptan exactamente las propiedades indicadas; una propiedad desconocida, repetida, omitida, `null` no permitido o tipo distinto devuelve `400 SOLICITUD_VALIDACION_INVALIDA`.

`foundation` es obligatorio, de texto libre —no existe catálogo documental aprobado— y se normaliza a Unicode NFC con recorte de extremos. Debe medir entre 1 y 1000 caracteres. No admite controles C0/C1 salvo tabulación, HTML, URLs, secretos, nombres de archivo, claves de objeto, hashes, contenido literal de evidencia ni saltos de línea.

`reason` y `escalationReason`, cuando son obligatorios, se normalizan del mismo modo, miden entre 1 y 500 caracteres y aplican las mismas prohibiciones. Un valor vacío después de normalizar equivale a ausente. No se intercambian: `escalationReason` explica por qué actúa un nivel posterior; `reason` explica por qué se sustituye una decisión vigente.

## 7. Emisión: contrato HTTP exacto

```http
POST /api/v1/obligations/{id}/validation-decisions
Idempotency-Key: 00000000-0000-7000-8000-000000000001
If-Match: "7"
Content-Type: application/json
```

```json
{
  "result": "INCOMPLETA",
  "foundation": "La evidencia vigente no acredita completamente el criterio aplicable.",
  "escalationReason": null
}
```

`Idempotency-Key` e `If-Match` son obligatorios. Si el requisito ya existe, el ETag representa `validation_requirement.row_version`; si todavía no existe para una obligación concluida con política no nula, representa `work_obligation.row_version`. El cliente obtiene ese valor mediante el GET de la sección 10 o el detalle de obligación ya aprobado. El servidor vuelve a determinar cuál agregado existe dentro de la transacción; no acepta comodín ni ETag débil.

La emisión ordinaria exige `escalationReason = null`. La emisión escalada exige un motivo válido. La autovalidación de Dirección exige `escalationReason = null` y se clasifica separadamente, no como escalamiento.

La primera decisión devuelve `201 Created`, `Location: /api/v1/obligations/{obligationId}/validations` y el ETag del requisito resultante:

```json
{
  "data": {
    "obligationId": "01900000-0000-7000-8000-000000000001",
    "executionStatus": "CONCLUIDA",
    "validationRequirement": {
      "requirementId": "01900000-0000-7000-8000-000000000010",
      "policyVersionId": "01900000-0000-7000-8000-000000000020",
      "status": "RESUELTA",
      "createdAt": "2026-09-10T18:00:00Z",
      "resolvedAt": "2026-09-10T19:00:00Z",
      "rowVersion": 1
    },
    "decision": {
      "decisionVersionId": "01900000-0000-7000-8000-000000000030",
      "versionNo": 1,
      "result": "INCOMPLETA",
      "foundation": "La evidencia vigente no acredita completamente el criterio aplicable.",
      "status": "VIGENTE",
      "authorityType": "ORDINARIA",
      "validatorUserId": "01900000-0000-7000-8000-000000000040",
      "validatorRole": "ADMINISTRACION",
      "decidedAt": "2026-09-10T19:00:00Z",
      "reason": null,
      "supersedesDecisionVersionId": null,
      "evidenceReviewSnapshotId": "01900000-0000-7000-8000-000000000050",
      "evidenceVersionIds": ["01900000-0000-7000-8000-000000000060"]
    }
  },
  "meta": {
    "correlationId": "01900000-0000-7000-8000-000000000099"
  }
}
```

## 8. Autoridad de emisión

Se captura una sola vez `decidedAt` UTC después de bloquear la obligación. Todas las vigencias se evalúan con ese mismo instante. Se exige sesión y MFA completos, cuenta y empleo vigentes, un único rol canónico vigente en `LOR-001`, permiso aplicable y asignación `VIGENTE` de la obligación.

La política congelada debe pertenecer a la misma TAR y versión exacta capturada por la obligación, estar publicada, requerir validación, contener `SUPERIOR_INMEDIATO`, la combinación de roles exacta de la sección 2 y los tres resultados. No se usa puesto textual, nombre, política actual, creador de la tarea ni parecido de rol.

Los modos son excluyentes:

- `ORDINARIA`: `PER-VALIDACION-EMITIR`; el actor no es el responsable, su rol vigente coincide exactamente con `validatorRole`, el rol vigente del responsable coincide con `executorRole` y existe superioridad estricta canónica.
- `ESCALAMIENTO`: `PER-VALIDACION-ESCALAR`; el actor no es el responsable, su rol vigente es estrictamente superior a `validatorRole`, también es superior al rol vigente del responsable y presenta `escalationReason`.
- `AUTOVALIDACION_DIRECCION`: el actor y el responsable son la misma persona, el actor tiene rol vigente `DIRECCION` y `PER-VALIDACION-EMITIR`; ésta es la única excepción a la separación de personas y queda auditada expresamente. La excepción no modifica la política ni concede autovalidación a otro rol.

Actor sin permiso base recibe `403`. Recurso inexistente, otra sucursal, par, inferior, actor con rol no exacto para emisión ordinaria, autoridad obsoleta, puesto textual o relación fuera de alcance converge en `404 OBLIGACION_NO_ENCONTRADA`. Una autovalidación visible de un rol distinto de Dirección devuelve `422 AUTOVALIDACION_NO_PERMITIDA` y no produce efecto.

## 9. Sustitución: contrato HTTP y autoridad

```http
POST /api/v1/validation-decisions/{id}/replacements
Idempotency-Key: 00000000-0000-7000-8000-000000000002
If-Match: "1"
Content-Type: application/json
```

```json
{
  "result": "CUMPLIDA",
  "foundation": "La evidencia vigente acredita el criterio aplicable.",
  "reason": "Se corrige la decisión tras revisar la versión vigente de evidencia."
}
```

El cuerpo contiene exactamente esas tres propiedades y `reason` siempre es obligatorio. `{id}` debe ser la decisión actualmente `VIGENTE`; una versión histórica no sirve como atajo para sustituir. `If-Match` representa `validation_requirement.row_version`.

Puede sustituir únicamente:

- `SUSTITUCION_ORIGINAL`: el mismo `validator_user_id` de la decisión vigente, con cuenta, empleo, rol y `PER-VALIDACION-SUSTITUIR` vigentes, dentro del mismo alcance; si su rol dejó de ser canónico o ya no conserva alcance, la autoridad es obsoleta y se rechaza; o
- `SUSTITUCION_SUPERIOR`: un actor con `PER-VALIDACION-SUSTITUIR`, rol vigente estrictamente superior al `validator_role` capturado por la decisión vigente y al rol vigente del responsable, dentro de `LOR-001`.

La sustitución crea una nueva versión, marca la anterior `SUSTITUIDA`, incrementa `validation_requirement.row_version`, conserva el requisito `RESUELTA` y actualiza `resolved_at` al nuevo `decidedAt`. Devuelve `201 Created`, el mismo envelope de la sección 7 con la sucesora, `Location` al histórico y el nuevo ETag. No existe actualización in-place.

## 10. Consulta del requisito e histórico

```http
GET /api/v1/obligations/{id}/validations
```

No admite query string. Requiere `PER-TAREA-VER` y reutiliza exactamente el alcance de `HU-023`: tarea propia; nivel estrictamente inferior; o Dirección dentro de `LOR-001`. Inexistencia, otra sucursal o fuera de alcance converge en `404 OBLIGACION_NO_ENCONTRADA`.

La respuesta es `200` con el envelope de la sección 7, excepto que la propiedad singular `decision` se sustituye por `decisions`, ordenada por `versionNo` descendente y `decisionVersionId` ascendente. No hay paginación porque existe una única cadena lineal por obligación. Cada elemento tiene exactamente la forma de `decision` mostrada.

Si la obligación está concluida, su política no es nula y todavía no se materializó el requisito, `validationRequirement` es `null`, `decisions` es `[]` y el ETag es `work_obligation.row_version`. La lectura no escribe. Si existe requisito, el ETag es su `row_version`. Si la política es nula se devuelve la misma proyección vacía, sin inferir o exponer una política sustituta. Una obligación pendiente devuelve su proyección vacía y no se vuelve validable por consultar.

No se devuelve contenido o payload estructurado de evidencia, nombre de archivo, hash, URL, clave, bucket, scanner, auditoría general, puesto textual ni datos ajenos.

## 11. Snapshot de evidencia y efecto de sustituciones

Cada emisión o sustitución reevalúa dentro de su transacción la evidencia `VIGENTE` mediante el contrato exacto de `HU-026`. Reutiliza un `evidence_review_snapshot` sólo si obligación, política de evidencia, entrada canónica, huella, proyección y conjunto ordenado de `evidence_version.id` coinciden; de otro modo materializa uno nuevo. Una inconsistencia impide decidir con `409 EVIDENCIA_VALIDACION_NO_DISPONIBLE`.

`validation_decision_version.evidence_review_snapshot_id` apunta de manera inmutable al snapshot exacto usado. `evidenceVersionIds` de la respuesta se proyecta desde ese snapshot, no desde una consulta posterior. El resultado humano no se deriva automáticamente del resultado estructural `COMPLETA/INCOMPLETA`: el snapshot sustenta qué se revisó y `foundation` sustenta la decisión humana.

Una sustitución posterior de evidencia no altera requisito, decisión, resultado, fundamento, snapshot ni auditoría anteriores. Sólo una sustitución explícita de la decisión vuelve a evaluar la evidencia vigente y crea otra decisión con otro snapshot. Una carrera evidencia/decisión se serializa: la decisión usa íntegramente el conjunto anterior o el sucesor, nunca una mezcla.

## 12. Idempotencia, equivalencia y ETag

Scopes exactos:

```text
validation:emit:{actorUserId:D}:{obligationId:D}
validation:replace:{actorUserId:D}:{currentDecisionVersionId:D}
```

El hash SHA-256 minúsculo usa método, ruta canónica, actor, recurso, ETag esperado y cuerpo normalizado en el orden de propiedades de las secciones 7 o 9. `correlationId`, cookies, CSRF y encabezados de transporte no forman parte.

- misma clave, scope y hash recupera identidad, cuerpo, código, `Location` y ETag originales sin nueva decisión, snapshot ni auditoría de éxito;
- misma clave y scope con otro contenido o ETag devuelve `409 IDEMPOTENCY_CONFLICT`;
- una segunda emisión secuencial con clave distinta, aun semánticamente equivalente, devuelve `409 DECISION_VALIDACION_YA_EXISTE`;
- dos emisiones concurrentes con el mismo ETag dejan una sola vigente; el perdedor devuelve `412 VERSION_CONFLICT`, incluido un escalamiento concurrente con emisión ordinaria;
- dos sustituciones con el mismo ETag dejan una sola sucesora; el perdedor devuelve `412 VERSION_CONFLICT`;
- una sustitución semánticamente equivalente con clave distinta sigue siendo una nueva intención: con ETag vigente crea una versión histórica nueva; con ETag obsoleto devuelve `412`; y
- sólo respuestas confirmadas `201` consumen durablemente la clave. Los rechazos no crean idempotencia.

Un replay reevalúa autenticación y autoridad antes de devolver datos; una clave nunca concede acceso posterior.

## 13. Transacción, bloqueos y carreras

Emisión y sustitución usan PostgreSQL `SERIALIZABLE` con un máximo de tres intentos totales y capturan un único `decidedAt`. El orden común es:

1. `work_obligation` por ID `FOR UPDATE`;
2. `assignment_version` `VIGENTE` de la obligación `FOR UPDATE`;
3. requisito existente por obligación `FOR UPDATE` o su ausencia protegida por predicado y unicidad;
4. política de validación congelada;
5. actor, responsable, empleos y roles vigentes en orden estable de ID;
6. ítems y versiones de evidencia vigentes en orden de requisito e ID;
7. snapshot de evidencia coincidente o nuevo;
8. decisión vigente, si existe, `FOR UPDATE`;
9. requisito y decisión nueva/transiciones;
10. `idempotency_record`; y
11. `audit_event`.

El flujo de conclusión adquiere primero la misma obligación antes de crear el requisito. El flujo de sustitución de evidencia también adquiere primero la obligación. Por ello, conclusión, evidencia y validación se ordenan antes o después sin mezclar autoridad, estado o snapshots.

`40001`, `40P01` y violaciones únicas esperables sobre requisito, vigente, sucesora o idempotencia se reintentan de forma acotada. Sólo se recupera un ganador si scope, hash, actor, obligación, requisito, decisión, ETag y snapshot coinciden exactamente. Agotado el límite se devuelve `409 VALIDACION_CONCURRENCIA_CONFLICTO`. No hay efectos parciales.

## 14. Persistencia y guardas PostgreSQL

El módulo nuevo `Validation` es propietario de las dos tablas; `Execution` sólo invoca su contrato interno para crear el requisito al concluir y no escribe sus tablas directamente.

### 14.1 `validation_requirement`

| Columna | Regla |
|---|---|
| `id uuid` | PK; UUID v7 generado por aplicación. |
| `obligation_id uuid` | FK `RESTRICT` a `work_obligation.id`; único y no nulo. |
| `policy_version_id uuid` | FK `RESTRICT` a `validation_policy_version.id`; coincide con la FK congelada de la obligación. |
| `status varchar(16)` | Sólo `PENDIENTE` o `RESUELTA`. |
| `created_at timestamptz` | Exactamente el `concluded_at` de la obligación. |
| `resolved_at timestamptz null` | Nulo en `PENDIENTE`; instante de la decisión vigente en `RESUELTA`. |
| `row_version bigint` | Mayor a cero; inicia en 1 y aumenta exactamente uno por sustitución. |

Índices: único por `obligation_id`; `(status, created_at, id)` para consumo futuro sin crear la bandeja; `(policy_version_id, created_at, id)`. El índice no autoriza el endpoint pendiente.

### 14.2 `validation_decision_version`

| Columna | Regla |
|---|---|
| `id uuid` | PK; UUID v7 generado por aplicación. |
| `requirement_id uuid` | FK `RESTRICT`; no nulo. |
| `version_no integer` | Mayor a cero, secuencial por requisito. |
| `result varchar(16)` | Sólo los tres resultados aprobados por la política congelada. |
| `foundation varchar(1000)` | Texto normalizado obligatorio de la sección 6. |
| `status varchar(16)` | `VIGENTE` o `SUSTITUIDA`. |
| `authority_type varchar(32)` | `ORDINARIA`, `ESCALAMIENTO`, `AUTOVALIDACION_DIRECCION`, `SUSTITUCION_ORIGINAL` o `SUSTITUCION_SUPERIOR`. |
| `validator_user_id uuid` | FK `RESTRICT` a `app_user.id`. |
| `validator_person_id uuid` | FK `RESTRICT` a la persona exacta del actor al decidir. |
| `validator_role varchar(32)` | Rol canónico vigente capturado al decidir. |
| `assignment_version_id uuid` | FK `RESTRICT` a la asignación responsable observada. |
| `responsible_person_id uuid` | FK `RESTRICT` a la persona responsable observada. |
| `decided_at timestamptz` | Instante UTC único de la operación. |
| `reason varchar(500) null` | Sólo nulo en `ORDINARIA` y `AUTOVALIDACION_DIRECCION`; obligatorio en los otros modos. |
| `supersedes_id uuid null` | FK `RESTRICT` a la predecesora inmediata del mismo requisito. |
| `evidence_review_snapshot_id uuid` | FK `RESTRICT` al snapshot exacto de la misma obligación. |

Restricciones y guardas mínimas:

- únicos `(requirement_id, version_no)` y `supersedes_id`; clave candidata `(id, requirement_id)`;
- índice parcial único por requisito donde `status='VIGENTE'`;
- predecesora inmediata, misma cadena y `version_no + 1`;
- checks cerrados de resultado, estado, autoridad, textos, motivo y primera/sucesoras;
- FKs `RESTRICT` y comprobaciones cruzadas de obligación, política, asignación y snapshot;
- requisito `PENDIENTE` si y sólo si no tiene decisión, y `RESUELTA` si y sólo si tiene exactamente una `VIGENTE`, mediante constraint trigger diferible;
- guarda que impide `DELETE`; impide cambiar campos semánticos; y sólo permite `VIGENTE → SUSTITUIDA` cuando se inserta la sucesora válida en la misma transacción;
- guarda que impide `UPDATE` o `DELETE` del snapshot de evidencia y de toda decisión ya `SUSTITUIDA`; y
- guardas que impiden modificar `execution_status`, `execution_result` o snapshot de conclusión como efecto de validar.

Se genera una sola migración forward-only propuesta como `AddValidationDecisions`. Crea tablas, FKs, índices, checks y guardas, pero no hace backfill de política, requisito o decisión. Archivo y Designer usan LF, no contienen BOM y `Down()` lanza la excepción de reversión bloqueada. No se edita una migración histórica.

## 15. Auditoría, atomicidad y datos permitidos

Acciones allowlist:

| `action` | `outcome` | Uso |
|---|---|---|
| `VALIDATION_DECISION_ISSUED` | `SUCCESS` | Emisión ordinaria. |
| `VALIDATION_DECISION_ESCALATED` | `SUCCESS` | Emisión por nivel posterior. |
| `VALIDATION_DIRECTION_SELF_VALIDATED` | `SUCCESS` | Excepción de Dirección sobre tarea propia. |
| `VALIDATION_DECISION_REPLACED` | `SUCCESS` | Sustitución por original o superior. |
| `VALIDATION_ACCESS_DENIED` | `DENIED` | Actor autenticado sin autoridad. |
| `VALIDATION_REJECTED` | `REJECTED` | Estado, DTO, ETag, idempotencia, evidencia o concurrencia rechazados. |

En éxito, requisito, snapshot nuevo si aplica, decisiones, idempotencia y auditoría se confirman en la misma transacción. Si cualquier escritura o auditoría falla, todo se revierte. Un replay no crea segunda auditoría de éxito. Un rechazo no deja requisito, snapshot, decisión, cambio de ejecución, tarea, aviso u outbox.

`before_data` y `after_data` esquema v1 incluyen sólo obligación, requisito, política, decisión/predecesora, resultado, estado, `authorityType`, roles canónicos, asignación, responsable, snapshot, conteo de versiones de evidencia, `rowVersion` e instantes. El evento de autovalidación incluye `selfValidation = true`. No incluye `foundation`, `reason`, contenido, listas de evidencia, payload estructurado, nombre, hash, URL, clave, bucket, puesto textual, secreto ni cuerpo HTTP.

## 16. Errores exactos y no efecto

Todos usan `application/problem+json` con `status`, `code`, `title`, `instance` y `correlationId`; el detalle es genérico.

| HTTP | `code` | Uso |
|---:|---|---|
| 400 | `OBLIGACION_ID_INVALIDO` / `DECISION_ID_INVALIDO` | UUID de ruta inválido. |
| 400 | `SOLICITUD_VALIDACION_INVALIDA` | JSON, query, forma, propiedad o texto inválido. |
| 400 | `IDEMPOTENCY_KEY_INVALIDA` | Cabecera ausente o no UUID. |
| 400 | `IF_MATCH_REQUERIDO` / `IF_MATCH_INVALIDO` | ETag ausente o inválido. |
| 401 | `AUTENTICACION_REQUERIDA` | Sesión ausente, expirada o MFA incompleto. |
| 403 | `ACCESO_DENEGADO` | Falta permiso base, cuenta/empleo/rol vigente o `LOR-001`, antes de resolver el recurso. |
| 404 | `OBLIGACION_NO_ENCONTRADA` | Inexistente, otra sucursal, fuera de alcance o autoridad jerárquica no válida. |
| 404 | `DECISION_VALIDACION_NO_ENCONTRADA` | Decisión inexistente, histórica, ajena o no visible para sustitución. |
| 409 | `OBLIGACION_NO_CONCLUIDA` | La obligación visible no está `CONCLUIDA`. |
| 409 | `POLITICA_VALIDACION_NO_DISPONIBLE` | FK nula, política incoherente, no publicada o distinta de la TAR/versión congelada. |
| 409 | `EVIDENCIA_VALIDACION_NO_DISPONIBLE` | Snapshot exacto no puede materializarse o reutilizarse coherentemente. |
| 409 | `DECISION_VALIDACION_YA_EXISTE` | Segunda emisión directa secuencial. |
| 409 | `IDEMPOTENCY_CONFLICT` | Clave reutilizada con otra intención normalizada. |
| 409 | `VALIDACION_CONCURRENCIA_CONFLICTO` | Reintentos transaccionales agotados. |
| 412 | `VERSION_CONFLICT` | ETag obsoleto o carrera de emisión/sustitución. |
| 422 | `RESULTADO_VALIDACION_INVALIDO` | Resultado fuera de los tres aprobados. |
| 422 | `FUNDAMENTO_INVALIDO` | Fundamento ausente o fuera del contrato. |
| 422 | `MOTIVO_REQUERIDO` | Falta motivo de escalamiento o sustitución. |
| 422 | `AUTOVALIDACION_NO_PERMITIDA` | Responsable intenta decidir su obligación sin ser Dirección. |
| 500 | `ERROR_INTERNO` | Falla no controlada, sin detalle sensible ni efecto parcial. |

Cada prueba negativa comprueba ausencia de requisito parcial, snapshot nuevo, decisión, sustitución, idempotencia, cambio de ejecución, tarea, aviso y auditoría de éxito. La auditoría de rechazo permitida no cuenta como efecto funcional.

## 17. Pruebas, gates, trazabilidad y cierre

Después de una aprobación íntegra, las pruebas deben demostrar al menos:

1. materialización al concluir y materialización atómica tardía para concluida con política no nula;
2. rechazo cerrado y sin backfill para política nula;
3. autoridad ordinaria exacta para las ocho TAR usando la política congelada;
4. emisión sólo sobre `CONCLUIDA`, tres resultados y ejecución/snapshot de conclusión inalterados;
5. escalamiento de nivel posterior con motivo y rechazo sin motivo;
6. autovalidación de Dirección auditada y rechazo de cualquier otra;
7. denegaciones de sesión, permiso aislado, par, inferior, rol/autoridad obsoletos, puesto textual y fuera de alcance;
8. una sola vigente, segunda emisión directa rechazada y sustitución motivada por original/superior autorizado;
9. cadena, snapshots y versiones sustituidas inmutables;
10. evidencia vigente exacta, sustitución posterior sin efecto retroactivo y carrera serializada;
11. replay, conflicto de clave, ETag obsoleto, emisión ordinaria contra escalada y sustituciones concurrentes;
12. auditoría atómica, fallo de auditoría y no efecto de cada rechazo;
13. `CPE-002`: `NO_CUMPLIDA` deja la ejecución `CONCLUIDA`;
14. ausencia de reapertura, cancelación, nueva obligación, aviso, outbox, UI, bandeja, supervisión e indicadores; y
15. restricciones, FKs, checks, índices, guardas e invariantes diferibles en PostgreSQL real.

Durante la implementación sólo se ejecutan filtros dirigidos. Una sola vez al final se ejecutan restore locked fuera del aislamiento, build Release, suite local completa sin integración PostgreSQL, format, arquitectura/contrato/seguridad/migración, modelo EF sin cambios pendientes, vulnerabilidades NuGet, espejo y protección de `Fuentes/` y `git diff --check`. Luego se solicita una única corrida PostgreSQL externa consolidada.

`docs/traceability/IMPLEMENTATION_STATUS.md` se actualizará en el mismo cambio como `Propuesta implementada`, nunca como `Terminada`. `HU-028` sólo será `Terminada` después de commit exacto, pipeline requerido exitoso, aprobación humana, merge, ascendencia verificada en `origin/master`, PostgreSQL externo satisfactorio, protección de `Fuentes/` y cero defectos bloqueantes. Ninguna condición autoriza por sí sola la siguiente acción Git.

## 18. Decisiones propuestas y aprobación íntegra solicitada

Se solicita aprobar o rechazar conjuntamente:

1. materialización al concluir o atómicamente con la primera emisión, sin lectura con efecto lateral ni backfill, y rechazo cerrado de FK nula;
2. exclusión de `GET /api/v1/validations/pending`, reservado a `HU-031`;
3. DTOs, respuestas, histórico y ETag de las secciones 7, 9 y 10;
4. autoridad ordinaria, escalamiento, sustitución y autovalidación de Dirección de las secciones 8 y 9;
5. textos libres normalizados y límites de fundamento/motivos de la sección 6;
6. snapshot exacto y efecto no retroactivo de sustituciones de evidencia de la sección 11;
7. scopes, replay, equivalencia, carreras y bloqueo de las secciones 12 y 13;
8. relación, tablas, restricciones, guardas y migración sin backfill de la sección 14;
9. auditoría y errores cerrados de las secciones 15 y 16; y
10. pruebas, gates, trazabilidad y exclusiones de la sección 17.

La pregunta de aprobación es: **¿se aprueba íntegramente la Adenda 25, sin cambios ni aprobación parcial, para autorizar después la implementación local de `HU-028`?**

Si cualquier punto requiere cambio, la adenda debe corregirse y volver a aprobarse íntegramente antes de producir código.
