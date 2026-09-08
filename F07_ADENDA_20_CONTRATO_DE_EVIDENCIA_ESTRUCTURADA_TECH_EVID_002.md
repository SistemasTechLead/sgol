# SGOL — Adenda 20 a F07: contrato de evidencia estructurada para `TECH-EVID-002`

## 1. Control del documento

| Campo | Valor |
|---|---|
| Tipo | Adenda normativa a `F07_BACKLOG_DE_IMPLEMENTACION.md` |
| Estado | `APROBADA ÍNTEGRAMENTE; IMPLEMENTACIÓN EN CURSO` |
| Fecha | 2026-09-07 |
| Identificador | `TECH-EVID-002 — Aporte estructurado cerrado y hecho condicional de TAR-0092` |
| Corte y épica | Infraestructura funcional previa a `HU-026`; `CV-03` / `EP-07` |
| Efecto | Insertar `TECH-EVID-002` inmediatamente después de `HU-025` y antes de `HU-026`, sin renumerar historias |
| Precedencia | `HU-025` reconocida como `Terminada` efectiva: PR `#41`, commit `9f754ea68569786054bee63261ebdb3ee2565d4f`, check `TECH-BASE-003 / PR gates` `SUCCESS` run `34171002437`, aprobación humana, PostgreSQL y SeaweedFS/ClamAV satisfactorios, merge `e74c2e8d077d39ebf1605cd6e6f5b44ad1ff1d1f`, ambos SHA ancestros de `origin/master` y cero defectos bloqueantes conocidos |
| Conservación | Mantiene sin cambios F00–F07, `Fuentes/`, `docs/design/logo.svg`, `docs/design/mapa-pantallas.md` y los tres Designer ajenos identificados |
| Aprobación | Aprobación íntegra recibida del responsable el 2026-09-07 |

La aprobación íntegra autoriza exclusivamente la implementación aquí descrita; commit, publicación, pull request y merge siguen requiriendo autorizaciones independientes.

## 2. Motivo de inserción y relación con `HU-026`

`F07_ADENDA_18` cerró `HU-025` sólo para `FOTOGRAFIA` y `DOCUMENTO_REFERENCIADO`, reservó `structured_payload = NULL` y rechazó `FORMULARIO_REFERENCIADO`, `REGISTRO_DIGITAL`, `DATO_ESTRUCTURADO` y `CHECKLIST_ESTRUCTURADO`. Las ocho políticas de `F07_ADENDA_16` contienen al menos uno de esos cuatro tipos, por lo que ninguna puede producir `COMPLETA` mediante el flujo funcional vigente.

Esta tarea propuesta cierra únicamente ese hueco. Habilita aportación y sustitución estructurada bajo los contratos y permisos ya aprobados y fija el hecho canónico de `DIFERENCIA_O_DANO`. No evalúa `COMPLETA/INCOMPLETA`, no crea `evidence_review_snapshot` y no implementa `HU-026`.

Si se aprueba, la tabla `Tareas insertadas por adenda` deberá registrar:

| Tarea insertada | Adenda de origen | Ejecutar antes de | Estado inicial |
|---|---|---|---|
| `TECH-EVID-002` | `F07_ADENDA_20_CONTRATO_DE_EVIDENCIA_ESTRUCTURADA_TECH_EVID_002.md` | `HU-026` | `Propuesta`; no habilita `HU-026` hasta su cierre efectivo |

## 3. Fuentes y límites interpretativos

La propuesta consume exclusivamente el catálogo de las ocho TAR, `F05-JP-001` a `F05-JP-008`, `CAT-001` a `CAT-008`, `RN-017`, `RN-018`, `RN-023`, `RN-027`, `RN-028`, las filas `HU-024` a `HU-026`, las decisiones de evidencia e historia aplicables, los límites F06 y las adendas 15 a 19.

Los contratos estructurados de esta adenda son cerrados y versionados. No se interpreta contenido binario, no se extraen datos desde PDF/JPEG/PNG, no se consulta integración externa y no se admite JSON arbitrario. Las reglas funcionales distintas de presencia, forma y las tres relaciones expresas de la sección 9 permanecen para conclusión o validación posteriores.

## 4. Resultado cerrado

`TECH-EVID-002` permitirá que los mismos actores de `HU-025` aporten o sustituyan evidencia funcional para:

- `REGISTRO_DIGITAL`;
- `DATO_ESTRUCTURADO`;
- `CHECKLIST_ESTRUCTURADO`; y
- `FORMULARIO_REFERENCIADO`.

La tarea:

1. reutiliza `evidence_item` y `evidence_version`;
2. amplía exclusivamente la validación de aplicabilidad en intención/confirmación y los endpoints de aporte, sustitución y consulta existentes;
3. conserva `VIGENTE/SUSTITUIDA`, ETag, idempotencia, autorización, historia y auditoría de `HU-025`;
4. exige exactamente uno de `fileId` o `structuredPayload` por versión;
5. valida por `taskCode + requirementCode + kind + schemaVersion`, sin motor dinámico;
6. fija la versión `VIGENTE` de `F_ENT_001` como autoridad exclusiva de `DIFERENCIA_O_DANO`; y
7. reutiliza sin cambios el almacenamiento, escaneo, cuarentena y promoción de `HU-025`; no modifica adaptadores, protocolos, configuración ni descarga.

## 5. Superficie HTTP exacta

No se crea ninguna ruta. Se amplían únicamente:

```text
POST /api/v1/files/upload-intents
POST /api/v1/files/{id}/complete
POST /api/v1/obligations/{id}/evidence
POST /api/v1/obligations/{id}/evidence/{itemId}/replacements
GET  /api/v1/obligations/{id}/evidence
```

`POST /api/v1/files/upload-intents` conserva exactamente su cuerpo y respuesta; sólo deja de rechazar `FOTO_DIFERENCIA_DANO` cuando la versión `VIGENTE` de `F_ENT_001` de la misma obligación fija `DIFERENCIA_O_DANO=true`. `POST /api/v1/files/{id}/complete` conserva cuerpo y respuesta y vuelve a comprobar ese hecho antes de consultar el objeto o crear outbox. `GET /api/v1/files/{id}/status` permanece literalmente sin cambios. Tampoco se agrega alias, comando interno enrutable ni endpoint de catálogo.

Las dos mutaciones conservan sesión vigente, MFA, CSRF, `Idempotency-Key`, `Content-Type: application/json` y las reglas de estado/autoridad de `HU-025`. La sustitución conserva `If-Match`. El GET conserva `PER-TAREA-VER`, alcance jerárquico, paginación y 404 convergente.

## 6. Cuerpos discriminados y compatibilidad

### 6.1 Primera aportación

Binaria, sin cambios:

```json
{
  "requirementCode": "FOTOGRAFIA_FINAL",
  "fileId": "019..."
}
```

Estructurada:

```json
{
  "requirementCode": "CHECKLIST_COMPLETO",
  "structuredPayload": {
    "schemaVersion": 1,
    "productCorrect": true,
    "zoneAndFamilyCorrect": true,
    "stableFormation": true,
    "labelsVisible": true,
    "alignmentConsistent": true,
    "occupancyJustified": true,
    "clean": true,
    "intact": true,
    "signageCorrect": true,
    "matchesPlanogramOrList": true
  }
}
```

El cuerpo contiene `requirementCode` y exactamente uno de `fileId` o `structuredPayload`. Ambos presentes, ambos ausentes o una propiedad desconocida producen `400 SOLICITUD_EVIDENCIA_INVALIDA`.

### 6.2 Sustitución

Binaria, sin cambios:

```json
{
  "fileId": "019...",
  "reason": null
}
```

Estructurada:

```json
{
  "structuredPayload": {
    "schemaVersion": 1,
    "formCode": "F-ENT-001",
    "formReference": "REC-2026-000123",
    "completedAt": "2026-09-07T20:15:00Z",
    "hasDifference": true,
    "hasDamage": false
  },
  "reason": "Se corrige la recepción al confirmar una diferencia documentada."
}
```

También exige exactamente uno de `fileId` o `structuredPayload`. El motivo conserva las reglas de `HU-025`: opcional mientras la obligación está `PENDIENTE`; obligatorio y normalizado cuando sustituye un superior después de `CONCLUIDA`.

## 7. Reglas comunes de payload estructurado

Todo `structuredPayload`:

- es un objeto JSON, nunca `null`, arreglo, escalar ni JSON anidado libre;
- contiene exactamente las propiedades del requisito y `schemaVersion = 1`;
- rechaza propiedades ausentes, adicionales, repetidas o con tipo distinto;
- usa instantes RFC 3339 UTC con `Z` y UUID canónico `D` minúsculo cuando corresponda;
- normaliza texto a Unicode NFC, recorta extremos y rechaza C0/C1, NUL, `<` y `>`;
- limita referencias a 1–120 caracteres y textos descriptivos a 1–500 caracteres;
- no admite HTML, Markdown, URL, ruta, nombre de archivo, hash, clave S3, bucket, contenido binario ni objeto externo;
- se canonicaliza con propiedades en el orden contractual, números decimales normalizados y UTF-8 antes de calcular idempotencia; y
- se almacena completo e inmutable en `evidence_version.structured_payload`.

No existe esquema suministrado por cliente, expresión, operador, campo configurable ni registro de tipos en base. El código contiene una allowlist exhaustiva por requisito y PostgreSQL repite la autoridad mínima mediante una función `CASE` cerrada.

## 8. Esquemas cerrados por requisito

### 8.1 `TAR-0005`

| Requisito | Payload exacto | Validez estructural |
|---|---|---|
| `CALCULO_AVANCE` | `schemaVersion`, `expectedTarget`, `actualSales`, `sourceReference` | `expectedTarget > 0`; `actualSales >= 0`; ambos decimal con máximo 15 enteros y 2 decimales; fuente no vacía |
| `ACCION_O_CONFORMIDAD` | `schemaVersion`, `outcome`, `actionDescription`, `responsiblePersonId`, `startsAt` | `outcome=ACCION` exige los otros tres; `outcome=CONFORMIDAD` exige los tres en `null` |

El porcentaje no lo aporta el cliente: SGOL calcula `actualSales / expectedTarget × 100` con precisión decimal. La relación cerrada de la sección 9 determina si `ACCION_O_CONFORMIDAD` satisface.

### 8.2 `TAR-0007`

| Requisito | Payload exacto | Validez estructural |
|---|---|---|
| `LIBERACION` | `schemaVersion`, `releasedAt`, `releaseReference` | instante UTC y referencia no vacía |
| `MERCANCIA` | `schemaVersion`, `merchandiseReference` | referencia no vacía |
| `FECHA_HORA` | `schemaVersion`, `occurredAt` | instante UTC |
| `RETORNO_EXHIBICION` | `schemaVersion`, `returnedAt`, `returnReference` | instante UTC y referencia no vacía |

La restricción temporal de conclusión de `TAR-0007` no se implementa en esta tarea.

### 8.3 `TAR-0008`

| Requisito | Payload exacto | Validez estructural |
|---|---|---|
| `SECUENCIA` | `schemaVersion`, `sequenceSummary` | texto de 1–500 caracteres |
| `DECISION` | `schemaVersion`, `decisionSummary`, `decidedAt` | texto de 1–500 e instante UTC |
| `FUNDAMENTO` | `schemaVersion`, `foundationSummary` | texto de 1–500 caracteres |
| `AVISO_INTERNO` | `schemaVersion`, `noticeReference`, `notifiedAt` | referencia local no URL e instante UTC |

`EXPEDIENTE` conserva su aporte PDF binario de `HU-025`. La cantidad de reclamantes y el objetivo de 30 minutos pertenecen a los datos/criterios de la obligación, no se duplican dentro de estos payloads.

### 8.4 `TAR-0011`

| Requisito | Payload exacto | Validez estructural |
|---|---|---|
| `EVALUACION` | `schemaVersion`, `assessmentSummary`, `assessedAt` | texto de 1–500 e instante UTC |
| `REPARACION_O_CAMBIO` | `schemaVersion`, `solutionType`, `solutionReference`, `completedAt` | `solutionType=REPARACION|CAMBIO`, referencia e instante UTC |
| `ENTREGA` | `schemaVersion`, `deliveryReference`, `deliveredAt` | referencia e instante UTC |

`AUTORIZACION` y `COMPROBANTES` conservan su aporte PDF binario. La correspondencia material y el objetivo de siete días hábiles permanecen para conclusión/validación posteriores.

### 8.5 `TAR-0018`

`CHECKLIST_COMPLETO` contiene exactamente `schemaVersion` y los diez booleanos del ejemplo de la sección 6.1. Todos deben ser `true` para que el requisito esté estructuralmente satisfecho. Un `false` conserva una versión de evidencia válida e histórica, pero `HU-026` deberá tratar el requisito como no satisfecho; no se elimina ni altera la evidencia.

`FOTOGRAFIA_FINAL` y `PLANOGRAMA_O_LISTA` conservan los aportes binarios aprobados.

### 8.6 `TAR-0026`

| Requisito | Payload exacto | Validez estructural |
|---|---|---|
| `FORM_ADM_02` | `schemaVersion`, `formCode`, `formReference`, `completedAt` | `formCode=FORM-ADM-02`, referencia e instante UTC |

`COMPROBANTE_LOCALIZABLE` conserva su aporte PDF binario. Oportunidad del pago y vencimiento ajustado permanecen fuera de esta tarea.

### 8.7 `TAR-0092`

| Requisito | Payload exacto | Validez estructural |
|---|---|---|
| `F_ENT_001` | `schemaVersion`, `formCode`, `formReference`, `completedAt`, `hasDifference`, `hasDamage` | `formCode=F-ENT-001`, referencia, instante UTC y dos booleanos obligatorios |

La versión `VIGENTE` de `F_ENT_001` perteneciente a la misma obligación y política es la única autoridad para `DIFERENCIA_O_DANO`. El valor es verdadero si `hasDifference || hasDamage`; es falso sólo si ambos son `false`. Ausencia o inconsistencia produce `UNRESOLVED`, nunca falso implícito.

`DOCUMENTO_RECEPCION` conserva el PDF y subtipo `NOTA|REMISION|FACTURA`. `FOTO_DIFERENCIA_DANO` conserva JPEG/PNG y sólo puede iniciar intención, aportarse o sustituirse cuando el hecho vigente es verdadero.

### 8.8 `TAR-0093`

| Requisito | Payload exacto | Validez estructural |
|---|---|---|
| `ANOTACION_F_ENT_001` | `schemaVersion`, `formCode`, `formReference`, `annotationReference`, `recordedAt` | `formCode=F-ENT-001`, dos referencias locales e instante UTC |
| `CONSTANCIA_AVISO_INTERNO` | `schemaVersion`, `noticeReference`, `notifiedAt` | referencia local e instante UTC |

`FOTOGRAFIA_INCIDENCIA` conserva el aporte binario. Esta tarea no crea `internal_notice`, mensaje externo ni vínculo nuevo entre obligaciones.

## 9. Tres relaciones estructurales permitidas

No se introduce un motor general. Sólo se codifican estas relaciones nominales:

1. `TAR-0005`: la versión `VIGENTE` de `CALCULO_AVANCE` calcula el porcentaje. Menor a 90 exige `ACCION_O_CONFORMIDAD.outcome=ACCION` y sus campos completos; mayor o igual a 90 exige `outcome=CONFORMIDAD` y campos de acción nulos.
2. `TAR-0018`: `CHECKLIST_COMPLETO` satisface únicamente cuando los diez valores son `true`.
3. `TAR-0092`: `DIFERENCIA_O_DANO` se obtiene únicamente de `F_ENT_001` `VIGENTE`; verdadero vuelve aplicable la foto, falso la vuelve no aplicable y `UNRESOLVED` bloquea una evaluación `COMPLETA`.

Esta tarea valida la forma y conserva los hechos. Sólo la tercera relación controla además si puede aportarse la foto condicional. La evaluación agregada y el reporte de faltantes siguen reservados a `HU-026`.

Sustituir `F_ENT_001` puede cambiar el hecho de verdadero a falso o viceversa. No altera ni borra una fotografía ya aportada, no sustituye otra evidencia y no cambia conclusión o validación. `HU-026` usará exclusivamente las versiones vigentes del mismo corte.

## 10. Autorización, estado y alcance

No se crean permisos. Se reutilizan literalmente:

- obligación `PENDIENTE`: responsable vigente con `PER-EVIDENCIA-APORTAR` aporta o sustituye;
- obligación `CONCLUIDA`: superior jerárquico estricto con `PER-EVIDENCIA-SUSTITUIR` y motivo sustituye un ítem existente; y
- consulta: `PER-TAREA-VER` con el alcance de `HU-023`.

Un superior no aporta ni sustituye una pendiente; el responsable no sustituye una concluida. Par, inferior, responsabilidad histórica, puesto textual, otra sucursal o recurso ajeno se rechazan. Recurso inexistente o fuera de alcance converge en `404 OBLIGACION_NO_ENCONTRADA` antes de revelar requisito, clase, ítem o condición.

La aportación estructurada exige política congelada no nula, requisito exacto de esa política, clase estructurada aprobada y condición aplicable. Tanto la intención como el aporte o sustitución de `FOTO_DIFERENCIA_DANO` exigen hecho verdadero; `UNRESOLVED` devuelve `422 CONDICION_EVIDENCIA_NO_RESUELTA` y falso devuelve `422 REQUISITO_EVIDENCIA_NO_APLICABLE`. Una intención creada cuando el hecho era verdadero vuelve a comprobarlo al confirmar y al vincular; si cambió, no se crea evidencia funcional y el objeto conserva el tratamiento técnico no vinculado de `HU-025`.

## 11. Historia, consulta y privacidad

Cada primera aportación crea `evidence_item` y versión 1 `VIGENTE`. Cada sustitución vuelve la anterior `SUSTITUIDA`, crea una sucesora `VIGENTE`, conserva la cadena, actor, instante y motivo y aumenta el ETag del ítem. No existe sobrescritura ni borrado funcional.

El DTO de `GET /api/v1/obligations/{id}/evidence` conserva `requirement` y `version` y devuelve exactamente uno de:

- `file`: DTO aprobado de `HU-025`, `structuredPayload: null`; o
- `file: null`, `structuredPayload`: objeto cerrado almacenado.

El orden y cursor no cambian. La respuesta no incluye contenido binario, clave, bucket, URL, escáner, auditoría general ni validaciones. El payload estructurado es información confidencial: sólo aparece en este GET autorizado, nunca en listados generales, Problem Details, logs, métricas, outbox o auditoría completa.

## 12. Idempotencia y concurrencia

La normalización de `structuredPayload` forma parte del request hash. Misma clave, operación, recurso y cuerpo canónico recupera la misma versión; misma clave con payload diferente devuelve `409 IDEMPOTENCY_CONFLICT`.

Se conserva `SERIALIZABLE` y el orden de bloqueo de `HU-025`: obligación, asignación vigente, ítem, versión vigente. Para `TAR-0005` se bloquean además los dos ítems en ordinal canónico; para `TAR-0092` se bloquea `F_ENT_001` antes del archivo o ítem condicional. Dos primeras aportaciones crean una sola versión; dos sustituciones con el mismo ETag producen una ganadora.

La sustitución concurrente de `F_ENT_001` y una intención/aportación de foto observa íntegramente el hecho anterior o posterior. Ninguna foto se vincula usando una versión de formulario que dejó de ser vigente antes del commit.

Fallo de validación, idempotencia, auditoría o persistencia revierte la mutación completa. No existe S3 ni outbox en una operación estructurada.

## 13. Persistencia y migración

No se crea tabla nueva. La única migración `20260908005832_EnableStructuredEvidence` modifica sólo `evidence_version` y las guardas directamente relacionadas.

### 13.1 `evidence_version`

- `file_object_id` cambia de `uuid NOT NULL` a `uuid NULL`; su FK `RESTRICT` y unicidad permanecen para valores no nulos;
- `structured_payload jsonb NULL` deja de estar forzado a `NULL`;
- un check XOR exige exactamente uno de `file_object_id` o `structured_payload`;
- evidencia binaria exige clase `FOTOGRAFIA|DOCUMENTO_REFERENCIADO`, archivo `LIMPIO/CLEAN` y `structured_payload IS NULL`;
- evidencia estructurada exige una de las cuatro clases aprobadas, `file_object_id IS NULL` y payload válido para el requisito; y
- estado, cadena, versión, actor, instante, motivo, predecesora y `row_version` conservan las restricciones existentes.

### 13.2 Autoridad PostgreSQL

Una función de trigger con lógica de esquema cerrada y versionada valida en `INSERT`:

1. vínculo de versión con el `evidence_item`, obligación, política y requisito;
2. correspondencia exacta de clase;
3. XOR archivo/payload;
4. propiedades y tipos JSON por cada uno de los 18 requisitos;
5. límites de texto, decimal, booleano, UUID e instante;
6. `formCode` exacto; y
7. relación condicional vigente para la foto cuando corresponda.

No almacena JSON Schema, expresiones o reglas en tablas. Una guarda rechaza cambio posterior de `structured_payload`, `file_object_id`, requisito o clase. DELETE continúa prohibido. Todas las FK usan `ON DELETE RESTRICT`.

La migración y Designer usan LF, no contienen BOM y `Down()` lanza la excepción de reversión bloqueada. No se modifica ninguna migración histórica ni los tres Designer ajenos.

## 14. Auditoría

Se reutilizan `EVIDENCE_CONTRIBUTED`, `EVIDENCE_REPLACED`, rechazos y denegaciones de `HU-025`. Evidencia, idempotencia y auditoría comparten transacción.

Para una versión estructurada, `after_data` contiene sólo IDs, código/clase de requisito, `schemaVersion`, estado, número de versión y una huella SHA-256 del payload canónico. No incluye el payload, textos, referencias, booleanos de condición, nombre, URL, archivo ni datos personales adicionales.

Una falla de auditoría revierte ítem, versión, sustitución e idempotencia. Una solicitud rechazada no crea evidencia ni auditoría de éxito.

## 15. Errores normalizados

Se conservan los errores de `HU-025` y se precisan:

| HTTP | `code` | Uso |
|---:|---|---|
| 400 | `SOLICITUD_EVIDENCIA_INVALIDA` | Discriminador, propiedad, JSON, tipo o forma inválida |
| 401 | `AUTENTICACION_REQUERIDA` | Sesión ausente, expirada o MFA incompleto |
| 403 | `ACCESO_DENEGADO` | Falta autoridad base antes de resolver recurso |
| 404 | `OBLIGACION_NO_ENCONTRADA` / `EVIDENCIA_NO_ENCONTRADA` | Inexistente o fuera de alcance |
| 409 | `EVIDENCIA_YA_EXISTE` / `IDEMPOTENCY_CONFLICT` | Conflicto funcional o de clave |
| 412 | `VERSION_CONFLICT` | ETag desactualizado |
| 422 | `REQUISITO_EVIDENCIA_INVALIDO` | Requisito ajeno, clase o esquema no aprobado |
| 422 | `PAYLOAD_EVIDENCIA_INVALIDO` | Payload no satisface el esquema cerrado |
| 422 | `CONDICION_EVIDENCIA_NO_RESUELTA` | Falta una versión vigente y válida de `F_ENT_001` |
| 422 | `REQUISITO_EVIDENCIA_NO_APLICABLE` | `F_ENT_001` vigente fija ambos booleanos en falso |
| 422 | `MOTIVO_REQUERIDO` / `SUSTITUCION_NO_PERMITIDA` | Estado, autoridad o motivo |
| 500 | `ERROR_INTERNO` | Falla no controlada sin efecto parcial |

Todos usan `application/problem+json`, `code` y `correlationId`. El detalle no incluye payload, referencia, condición, SQL, stack, ruta, archivo, hash, clave, URL ni secreto.

## 16. Observabilidad

Se amplían las métricas existentes con `operation=contribute|replace|query`, `requirementKind` y `result=success|rejected|failed`. No se etiqueta TAR, requisito, schemaVersion, usuario, obligación, ítem, versión, condición, referencia ni contenido.

Logs JSON incluyen sólo `correlationId`, operación, clase, resultado y código allowlist. No registran el payload ni su huella, referencias, booleanos, textos, IDs funcionales, nombres, archivos o secretos.

## 17. Pruebas requeridas tras aprobación

### 17.1 Sin Docker

- los 18 requisitos estructurados aceptan exclusivamente su esquema exacto;
- propiedad adicional/ausente, tipo erróneo, límite, HTML/URL o schemaVersion distinto se rechaza;
- XOR `fileId/structuredPayload` conserva compatibilidad binaria;
- primera versión, sustitución, historia, ETag e idempotencia funcionan para cada clase;
- `TAR-0005` calcula porcentaje y exige acción o conformidad coherente;
- checklist con diez `true` satisface; cualquier `false` conserva evidencia pero no satisface para `HU-026`;
- `F_ENT_001` verdadero habilita foto, falso la rechaza y ausente/inconsistente no se interpreta como falso;
- sustituir `F_ENT_001` cambia sólo el hecho vigente, conserva historia y no altera foto, conclusión o validación;
- permisos, estados, jerarquía y 404 convergente coinciden con `HU-025`/`HU-023`;
- rechazo y falla de auditoría no dejan ítem, versión ni idempotencia parcial;
- consulta devuelve exactamente uno de archivo/payload y no filtra contenido fuera de alcance;
- logs, errores, métricas y auditoría no contienen payload ni datos prohibidos;
- no existe endpoint, tabla, UI, Worker o dependencia externa nueva; y
- no se crea snapshot ni resultado `COMPLETA/INCOMPLETA`.

### 17.2 PostgreSQL real externa

- migración desde cero y sobre la base vigente; aplicación repetida sin cambio;
- XOR, FK `RESTRICT`, unicidades, cadena, una `VIGENTE` y guardas de inmutabilidad;
- los 18 esquemas y relaciones rechazados directamente por PostgreSQL cuando son incoherentes;
- versión binaria sigue exigiendo archivo limpio del mismo ítem;
- versión estructurada nunca referencia archivo;
- aportes/sustituciones concurrentes producen una sola historia lineal;
- sustitución concurrente de `F_ENT_001` y foto nunca usa hechos mezclados;
- auditoría, idempotencia e historia confirman o revierten juntas; y
- usuario de aplicación no puede mutar o borrar versiones históricas.

No se usa SQLite. El desarrollador ejecuta externamente una sola vez las suites PostgreSQL/Docker/Testcontainers afectadas. SeaweedFS/ClamAV no aplica porque no se modifica ni invoca esa infraestructura.

## 18. Archivos previstos tras aprobación

La implementación podrá modificar únicamente:

- `src/Modules/Evidence/` para DTO, validadores cerrados y contratos;
- endpoints, validación de intención/confirmación y persistencia de evidencia directamente necesarios en `src/Sgol.Web/`;
- una migración nueva, su Designer y el snapshot EF;
- pruebas unitarias, arquitectura, API y PostgreSQL directamente afectadas;
- inventario de migraciones/tablas y documentación técnica mínima;
- `docs/traceability/README.md` y `docs/traceability/IMPLEMENTATION_STATUS.md`;
- esta adenda para registrar la aprobación; y
- `F07_ADENDA_19_CONTRATO_DE_EVALUACION_ESTRUCTURAL_DE_EVIDENCIA_HU_026.md` sólo para sustituir el bloqueo por la precedencia efectiva una vez cerrado `TECH-EVID-002`.

No se modifican proyectos, paquetes, configuración, CI, Worker, adaptadores de archivo, F00–F07 ni `Fuentes/`.

## 19. Gates de salida

Después de aprobar, implementar y revisar el diff completo:

1. `dotnet restore SGOL.slnx --locked-mode` fuera del aislamiento;
2. `dotnet build SGOL.slnx --no-restore --configuration Release`;
3. suite unitaria completa;
4. suite de arquitectura completa;
5. pruebas enfocadas de `TECH-EVID-002` sin Docker;
6. solicitar al desarrollador una ejecución externa de las suites PostgreSQL afectadas y esperar el resultado;
7. `dotnet format SGOL.slnx --verify-no-changes --no-restore`;
8. `./scripts/ci/Assert-NoVulnerablePackages.ps1`;
9. modelo EF sin cambios pendientes;
10. `./scripts/ci/verify-fuentes-protection.ps1`;
11. `./scripts/ci/verify-fuentes-mirror.ps1`, después y no en paralelo con el anterior; y
12. `git diff --check`.

Un gate omitido, compuesto, pendiente o ejecutado sobre otro SHA se informa como no verificado. No se ejecutan Docker ni Testcontainers dentro de la sesión y no se diagnostica Docker.

## 20. Trazabilidad y cierre condicional

La rama registrará `TECH-EVID-002` como propuesta. Sólo será `Terminada` cuando el commit exacto que contiene implementación y trazabilidad tenga pipeline requerido verde, aprobación humana, merge, ascendencia verificada en `origin/master`, PostgreSQL satisfactorio, `Fuentes/` protegido y cero defectos bloqueantes conocidos.

Sólo entonces podrá corregirse la Adenda 19 para reconocer la predecesora cerrada y solicitar aprobación íntegra de `HU-026`. No se requiere commit administrativo retrospectivo para cambiar el encabezado de la propuesta; la regla condicional se aplica como en historias anteriores.

## 21. Fuera de alcance

No se implementan ni simulan:

- `HU-026`, evaluación agregada, faltantes o `evidence_review_snapshot`;
- `HU-022`, conclusión o cambio de `execution_status`;
- `HU-027`, `HU-028`, `HU-030`, validación, bandeja o avisos;
- UI;
- endpoint nuevo;
- clase, requisito o TAR fuera del catálogo cerrado;
- texto libre ilimitado, adjuntos dentro de JSON o motor general de reglas/formularios;
- extracción o transformación de PDF/JPEG/PNG;
- cambios en adaptadores, protocolos, configuración, permisos o topología S3/SeaweedFS/ClamAV; la foto condicional reutiliza literalmente el flujo binario aprobado;
- nuevas clases de archivo, URL permanente, cambio de duración firmada, descarga o acceso público;
- `internal_notice`, `execution_result`, tablas de validación o snapshot;
- permiso, autenticación o rol nuevos;
- borrado funcional;
- outbox, broker, Redis, microservicios, scheduler u otro Worker;
- integración externa, réplica, respaldo, restauración o despliegue productivo; y
- cambios en F00–F07 o `Fuentes/`.

## 22. Decisión íntegra solicitada

Aprobar esta adenda significa aprobar conjuntamente:

1. el ID tentativo `TECH-EVID-002` y su inserción antes de `HU-026`;
2. ampliación exclusiva de los cinco endpoints existentes de la sección 5, sin ruta nueva;
3. cuerpos discriminados con exactamente archivo o payload;
4. los 18 esquemas estructurados cerrados de la sección 8;
5. las tres relaciones nominales y ausencia de motor general;
6. `F_ENT_001` vigente como autoridad única de `DIFERENCIA_O_DANO`, incluida la salida `UNRESOLVED`;
7. historia, autorización, ETag, idempotencia y auditoría heredadas de `HU-025`;
8. visibilidad autorizada del payload estructurado en el GET existente;
9. nulabilidad de `file_object_id`, XOR, trigger cerrado y única migración;
10. no creación de tablas, endpoints, UI, Worker ni infraestructura externa; y
11. gates, trazabilidad y cierre condicional de las secciones 17 a 20.

No se interpreta aprobación parcial, autorización de preparación, memoria del chat o aceptación del nombre como aprobación íntegra. La aprobación íntegra fue recibida el 2026-09-07; cualquier cambio material exige una nueva decisión antes de implementar.
