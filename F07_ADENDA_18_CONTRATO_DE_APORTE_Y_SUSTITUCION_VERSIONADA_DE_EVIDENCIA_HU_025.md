# SGOL — Adenda 18 a F07: contrato de aporte y sustitución versionada de evidencia para `HU-025`

## 1. Control del documento

| Campo | Valor |
|---|---|
| Tipo | Propuesta de adenda normativa a `F07_BACKLOG_DE_IMPLEMENTACION.md` |
| Estado | `APROBADA PARA IMPLEMENTACIÓN` |
| Fecha | 2026-09-07 |
| Historia | `HU-025 — Responsable/superior aporta o sustituye evidencia conservando versiones` |
| Corte y épica | `CV-03` / `EP-07` |
| Efecto propuesto | Precisar el contrato ejecutable de `HU-025` sin alterar su orden ni habilitar historias posteriores |
| Precedencia comprobada | `TECH-EVID-001` reconocida como `Terminada` efectiva por la regla condicional de `docs/traceability/IMPLEMENTATION_STATUS.md`: PR `#40`, commit `eb29656f7d2ced765924115520119f36529fc0fc`, pipeline requerido `TECH-BASE-003 / PR gates` `SUCCESS` run `34153966707`, aprobación humana, suite externa SeaweedFS/ClamAV `1/1`, merge `c0c07fff5dd0a05be41d6993eb1abf10b0e74706`, ambos SHA ancestros de `origin/master` y cero defectos bloqueantes conocidos; PostgreSQL no aplicó |
| Conservación | Mantiene sin cambios F00–F07, `Fuentes/`, `docs/design/logo.svg`, `docs/design/mapa-pantallas.md` y los tres Designer ajenos identificados en el encargo |
| Aprobación | Versión inicial y revisiones materiales de las secciones 22, 23 y 24 aprobadas íntegramente por el responsable el 2026-09-07 |

La aprobación íntegra registrada habilita exclusivamente la implementación y las pruebas de `HU-025`. No autoriza commit, publicación de rama, pull request ni merge.

## 2. Fuentes, interpretación y carencia cerrada

La propuesta se limita a `HU-025`, `CAP-030`, `CA-025`, `CP-025-P`, `CP-025-N`, `RN-018`, `RN-023`, `RN-027`, `RN-028`, `DEC-067`, `DEC-068`, `DEC-069`, `ADR-007`, los contratos aprobados de `HU-023`, `HU-024`, `TECH-EVID-001` y las primitivas de Worker/outbox de `TECH-JOBS-001`.

De esas fuentes se mantienen como hechos aprobados:

1. la evidencia usa objetos privados S3-compatible, cuarentena, SHA-256, validación técnica y escaneo antimalware;
2. sólo JPEG, PNG y PDF, de 1 a 15 MiB inclusive, pueden superar la inspección técnica;
3. ningún objeto distinto de `LIMPIO` puede vincularse ni salir de cuarentena;
4. el responsable sustituye antes de concluir y después sólo un superior jerárquico estricto con motivo;
5. todas las versiones permanecen y una validación existente no cambia automáticamente;
6. la política capturada por la obligación es la única fuente de requisitos aplicables; y
7. cada escritura crítica comparte transacción PostgreSQL con idempotencia, auditoría y outbox cuando corresponda.

Las fuentes no contienen esquemas ejecutables para `REGISTRO_DIGITAL`, `DATO_ESTRUCTURADO` ni `CHECKLIST_ESTRUCTURADO`. Esta propuesta no los inventa. `structured_payload` se reserva como `NULL` y cualquier intento de aportar esas clases se rechaza con `422 TIPO_EVIDENCIA_NO_IMPLEMENTADO`. Por tanto, esta adenda cierra la porción binaria de `HU-025`, suficiente para probar su versionado, pero **no habilita `HU-026`** para evaluar políticas que contengan requisitos estructurados. Habilitar esas clases requiere otra decisión contractual explícita antes de `HU-026`.

## 3. Resultado cerrado y exclusiones

### 3.1 Incluido

- intención de carga privada ligada desde su creación a una obligación y a un requisito de su política capturada;
- carga directa mediante URL firmada temporal exclusivamente a cuarentena;
- confirmación idempotente y solicitud transaccional de inspección al outbox existente;
- inspección por el Worker existente, con tamaño real, tipo real, estructura, SHA-256, ClamAV y promoción segura;
- aporte de primera versión binaria y sustitución histórica;
- consulta autorizada del estado técnico y de todas las versiones funcionales;
- `file_object`, `evidence_item`, `evidence_version`, una migración y guardas PostgreSQL;
- autorización en servidor, concurrencia, idempotencia, auditoría, métricas y limpieza exclusiva de objetos técnicos no vinculados; y
- pruebas unitarias, API, arquitectura, PostgreSQL real y externas S3/ClamAV directamente afectadas.

### 3.2 Excluido

- `HU-026`, evaluación `COMPLETA/INCOMPLETA`, faltantes o `evidence_review_snapshot`;
- `HU-022`, conclusión, reapertura o cambio de `work_obligation.execution_status`;
- `HU-028`, creación, emisión, sustitución o actualización automática de validaciones;
- UI, Razor Pages, componentes, CSS, JavaScript y Playwright;
- descarga, `GET /api/v1/files/{id}/download`, URL permanente, CDN o acceso público;
- aportes estructurados, texto libre, formularios dinámicos o motor general de reglas;
- borrado funcional de evidencia, ítems o versiones y deduplicación funcional por SHA-256;
- SVG, HTML, Office, macros, ZIP, ejecutables o cualquier tipo distinto de JPEG, PNG y PDF;
- réplica, respaldo, restauración, despliegue productivo y retención legal de objetos rechazados;
- permisos nuevos, autenticación nueva, broker, Redis, otro Worker, microservicios, scheduler o Kubernetes; y
- cualquier cambio en F00–F07 o `Fuentes/`.

## 4. Clases binarias admitidas y metadatos

La aportación funcional de esta historia sólo admite estas combinaciones de la política capturada:

| Clase del requisito | Tipos admitidos | Regla |
|---|---|---|
| `FOTOGRAFIA` | `image/jpeg` con `.jpg`/`.jpeg`; `image/png` con `.png` | La firma y estructura deben corresponder al tipo declarado. |
| `DOCUMENTO_REFERENCIADO` | `application/pdf` con `.pdf` | El PDF es el documento privado aportado; no se extrae ni interpreta contenido. |

`FORMULARIO_REFERENCIADO`, `REGISTRO_DIGITAL`, `DATO_ESTRUCTURADO` y `CHECKLIST_ESTRUCTURADO` no se convierten implícitamente en archivos. Para `TAR-0092 / DOCUMENTO_RECEPCION`, el único metadato funcional adicional es `documentSubtype`, obligatorio y cerrado a `NOTA`, `REMISION` o `FACTURA`. Ese campo debe estar ausente para cualquier otro requisito.

El nombre original:

- se reduce al basename;
- se normaliza a Unicode NFC y se recortan extremos;
- debe contener entre 1 y 255 caracteres Unicode;
- no admite controles C0/C1, NUL, separadores de ruta ni segmentos `.` o `..`;
- conserva únicamente la extensión aprobada; y
- nunca participa en la clave S3, autorización, hash, log, métrica, error o correlación.

No se aceptan metadatos arbitrarios, descripción, etiqueta, comentario, EXIF aportado por el cliente, ruta local, URL, referencia externa ni JSON libre.

## 5. Endpoints exclusivos

`HU-025` agrega exactamente estas rutas bajo `/api/v1`:

1. `POST /api/v1/files/upload-intents`;
2. `POST /api/v1/files/{id}/complete`;
3. `GET /api/v1/files/{id}/status`;
4. `POST /api/v1/obligations/{id}/evidence`;
5. `POST /api/v1/obligations/{id}/evidence/{itemId}/replacements`; y
6. `GET /api/v1/obligations/{id}/evidence`.

No se agregan alias ni rutas adicionales. Las mutaciones requieren sesión vigente, MFA completo, token CSRF, `Content-Type: application/json` e `Idempotency-Key` UUID. La sustitución requiere además `If-Match`. Los GET no escriben auditoría, idempotencia, outbox ni estado.

Todas las respuestas usan `{ "data": ..., "meta": { "correlationId": ... } }`; los errores usan `application/problem+json`, `code` y `correlationId`. UUID se serializa en formato `D` minúsculo e instantes en RFC 3339 UTC con `Z`.

## 6. Intención de carga

### 6.1 Solicitud

```http
POST /api/v1/files/upload-intents
Idempotency-Key: 00000000-0000-7000-8000-000000000001
Content-Type: application/json
```

```json
{
  "obligationId": "019...",
  "requirementCode": "FOTOGRAFIA_FINAL",
  "originalFileName": "resultado-final.jpg",
  "declaredMediaType": "image/jpeg",
  "sizeBytes": 482103,
  "sha256": "64-caracteres-hexadecimales-minusculos",
  "documentSubtype": null
}
```

El cuerpo contiene exactamente esos campos. `sizeBytes` debe estar entre `1` y `15,728,640`; `sha256` debe tener exactamente 64 caracteres hexadecimales minúsculos. La combinación requisito/clase/media/extensión y `documentSubtype` se valida antes de crear la intención.

HU-025 sólo admite requisitos congelados con `condition_code = SIEMPRE`. El requisito `FOTO_DIFERENCIA_DANO` de `TAR-0092`, cuya condición es `DIFERENCIA_O_DANO`, se rechaza con `422 REQUISITO_CONDICIONAL_NO_EVALUABLE`: las fuentes aprobadas no definen una proyección ejecutable para evaluar esa condición y F07_ADENDA_16 reserva esa evaluación para HU-026. No se infieren campos desde `input_payload`, no se acepta una declaración del cliente y no se crea una intención de carga para ese requisito.

### 6.2 Autorización y vínculo previo

La intención se liga inmutablemente a obligación, versión de política, requisito y actor:

- si la obligación está `PENDIENTE`, sólo su responsable `VIGENTE`, con cuenta, empleo y rol vigentes en `LOR-001` y `PER-EVIDENCIA-APORTAR`, puede crearla;
- si está `CONCLUIDA`, sólo un actor con `PER-EVIDENCIA-SUSTITUIR` cuyo rol vigente sea estrictamente superior al rol vigente del responsable actual puede crearla, y el `evidence_item` debe existir;
- un par, inferior, responsable histórico, actor de otra sucursal o superior de un recurso ajeno queda fuera de alcance;
- `DIRECCION` puede actuar sobre cualquier responsable de nivel inferior en `LOR-001`, pero nadie puede ser superior estricto de una obligación cuyo responsable vigente sea `DIRECCION`; y
- la autorización se vuelve a evaluar al confirmar y al vincular; la intención no es una credencial de negocio.

Un UUID inexistente, de otra sucursal o fuera de alcance converge en `404 OBLIGACION_NO_ENCONTRADA`. Sólo después de autorizar la obligación se informa que el requisito no existe, no pertenece a su política capturada, no es aplicable o no admite binario.

### 6.3 URL firmada y respuesta

La aplicación crea `file_object` y reserva una clave opaca `v1/{aa}/{bb}/{token}` mediante la fábrica aprobada de `TECH-EVID-001`. La URL:

- expira exactamente 10 minutos después de `createdAt`;
- sólo autoriza `PUT` sobre esa clave exacta del bucket de cuarentena;
- firma `Content-Type`, longitud exacta, SHA-256 declarado, media type técnica e `If-None-Match: *`;
- no permite GET, listado, overwrite, otro objeto, otro bucket, ACL ni cambio de metadatos;
- no expone credencial permanente y nunca se persiste ni registra; y
- sólo puede usarse desde los orígenes SGOL configurados en `Evidence:Storage:AllowedUploadOrigins`. La lista requiere al menos un origen absoluto, sin ruta, query, fragmento ni wildcard. Fuera de `Development` y `CI`, cada origen debe usar HTTPS; en `Development` y `CI` sólo se admiten loopback o direcciones de red privada. CORS S3 permite exclusivamente `PUT`, los encabezados firmados y la ventana de 10 minutos, sin credenciales de navegador ni wildcard de origen.

La generación de la firma es local al adaptador y no crea el objeto. La fila, idempotencia y auditoría `EVIDENCE_UPLOAD_INTENT_CREATED` se confirman en una transacción; una falla al generar la firma no crea fila. Respuesta `201`:

```json
{
  "data": {
    "fileId": "019...",
    "status": "PENDIENTE_CARGA",
    "upload": {
      "method": "PUT",
      "url": "https://valor-firmado-temporal",
      "expiresAt": "2026-09-07T20:10:00Z",
      "headers": {
        "Content-Type": "image/jpeg",
        "Content-Length": "482103",
        "If-None-Match": "*",
        "x-amz-meta-sgol-sha256": "64-caracteres-hexadecimales-minusculos",
        "x-amz-meta-sgol-media-type": "Jpeg",
        "x-amz-meta-sgol-size-bytes": "482103"
      }
    }
  },
  "meta": { "correlationId": "019..." }
}
```

Un replay con la misma clave y cuerpo antes de expirar devuelve el mismo `fileId`, vencimiento y autorización equivalente. Después de expirar devuelve `410 INTENCION_CARGA_EXPIRADA`; se requiere una clave nueva para una intención nueva. Misma clave con cuerpo distinto devuelve `409 IDEMPOTENCY_CONFLICT`.

Se aplica un límite persistente de 30 intenciones por usuario en los 60 minutos inmediatamente anteriores. La transacción adquiere `pg_advisory_xact_lock` sobre una clave derivada de `uploaded_by`, cuenta filas de `file_object` del actor con `created_at > now() - interval '60 minutes'` e inserta sólo cuando el conteo es menor que 30. PostgreSQL serializa así intenciones concurrentes del mismo actor sin Redis, memoria de proceso ni una cuarta tabla. `429 LIMITE_INTENCIONES_EXCEDIDO` no crea fila, objeto, auditoría funcional ni outbox.

## 7. Confirmación y solicitud de inspección

### 7.1 Contrato

```http
POST /api/v1/files/{id}/complete
Idempotency-Key: 00000000-0000-7000-8000-000000000002
Content-Type: application/json
```

El cuerpo debe ser exactamente `{}`. Sólo el creador vigente y todavía autorizado puede confirmar. La petición debe recibirse a más tardar en `upload_expires_at`; no renueva ni vuelve a firmar la carga.

Dentro de una transacción PostgreSQL se bloquean el `file_object` y la obligación. Antes de escribir, el adaptador consulta por `HEAD` el objeto exacto de cuarentena y exige:

- existencia única;
- tamaño exacto declarado;
- los tres metadatos técnicos firmados exactos; y
- ausencia de una copia limpia previa incompatible.

El `HEAD` no acredita tipo real ni hash real. Esas verificaciones leen los bytes en el Worker. Objeto ausente devuelve `409 CARGA_NO_ENCONTRADA` sin mutación. Objeto con longitud o metadatos discordantes deja el archivo `INVALIDO`, conserva cuarentena, audita el rechazo y no crea outbox.

Una confirmación válida fija `upload_completed_at`, inserta `EVIDENCE.FILE_INSPECTION_REQUESTED.V1` con sólo `fileObjectId`, registra `EVIDENCE_UPLOAD_COMPLETED`, guarda idempotencia y confirma todo junto. Falla de auditoría u outbox revierte todas esas filas. Respuesta `202`:

```json
{
  "data": {
    "fileId": "019...",
    "status": "PENDIENTE_ESCANEO",
    "statusUrl": "/api/v1/files/019.../status"
  },
  "meta": { "correlationId": "019..." }
}
```

El replay devuelve el mismo resultado. Confirmaciones concurrentes no crean más de un evento elegible: existe un índice parcial único sobre `(event_type, aggregate_id)` para `EVIDENCE.FILE_INSPECTION_REQUESTED.V1` no procesado y con intentos disponibles. Un evento agotado no permite convertir un archivo terminal en pendiente.

## 8. Worker, inspección, reintentos y recuperación

### 8.1 Reutilización del host

Se registra un único manejador `EVIDENCE.FILE_INSPECTION_REQUESTED.V1` en `Sgol.Worker outbox`. No se agrega host, cola, broker ni política de reintento interna. El contexto de entrega existente se amplía sólo con el número de intento actual para que el manejador aplique el límite aprobado.

El evento usa `outbox_event.id` como identidad idempotente y los cinco intentos existentes, con demoras de 1, 5, 15 y 60 minutos. El adaptador S3 y el adaptador ClamAV mantienen exactamente un intento de red por invocación.

### 8.2 Procesamiento

Para un `file_object` `PENDIENTE` confirmado, el manejador:

1. bloquea la fila y valida evento, obligación y metadatos persistidos;
2. abre el objeto de cuarentena; si una promoción previa quedó confirmada externamente pero no en PostgreSQL, abre la copia limpia exacta;
3. lee como máximo `15,728,641` bytes, valida firma y estructura, obtiene tipo real y calcula SHA-256;
4. compara tamaño, tipo y hash contra la intención firmada;
5. sólo si todo coincide transmite el temporal validado a ClamAV;
6. sólo ante `LIMPIO` promueve y vuelve a verificar tamaño y hash en limpio;
7. persiste estado terminal, auditoría y confirmación del outbox en la misma transacción; y
8. nunca incluye binario, nombre, hash, clave, bucket, URL, firma de malware ni respuesta cruda en outbox, auditoría, log o métrica.

Resultados:

| Resultado | Estado y objeto |
|---|---|
| Objeto ausente, vacío, excesivo, alterado, duplicado incompatible, tipo/firma/estructura discordante o hash distinto | `INVALIDO`; permanece o se considera aislado en cuarentena; nunca se promueve. |
| Respuesta completa `stream: <firma> FOUND` | `INFECTADO`; permanece en cuarentena; alerta crítica. |
| Respuesta completa `stream: OK` y promoción verificada | `LIMPIO`; copia verificada en limpio y cuarentena eliminada. |
| Timeout, indisponibilidad, límite o respuesta desconocida | intentos 1–4 revierten el manejador y reprograman outbox; el intento 5 persiste `ERROR_ESCANEO`, auditoría y alerta; permanece en cuarentena. |
| Cancelación del host | revierte el intento y no inventa veredicto. |

`INFECTADO`, `INVALIDO` y `ERROR_ESCANEO` son terminales para ese `file_object`; no pueden volver a `PENDIENTE` ni `LIMPIO`. El usuario debe crear otra intención con otra clave. Ningún error, ausencia, timeout o respuesta desconocida se interpreta como limpio.

Si el proceso cae después de copiar a limpio y antes del commit PostgreSQL, el reintento valida y vuelve a escanear la copia limpia; sólo entonces converge a `LIMPIO`. Si cuarentena y limpio existen, ambas deben coincidir con el metadata esperado; una diferencia elimina únicamente la copia limpia parcial identificable, conserva cuarentena y termina `INVALIDO`. Si falla la transacción final, no queda estado funcional, auditoría ni outbox confirmado; la entrega puede repetirse.

## 9. Estado de archivo

```http
GET /api/v1/files/{id}/status
```

Puede consultar el creador si conserva la relación y permiso aplicables, o cualquier actor que satisfaga la visibilidad de evidencia de la sección 12. Recurso inexistente, ajeno o fuera de alcance converge en `404 ARCHIVO_NO_ENCONTRADO`.

Respuesta `200`:

```json
{
  "data": {
    "fileId": "019...",
    "status": "PENDIENTE_CARGA|PENDIENTE_ESCANEO|LIMPIO|INFECTADO|INVALIDO|ERROR_ESCANEO",
    "originalFileName": "resultado-final.jpg",
    "declaredMediaType": "image/jpeg",
    "detectedMediaType": "image/jpeg",
    "sizeBytes": 482103,
    "sha256": "64-caracteres-hexadecimales-minusculos",
    "createdAt": "2026-09-07T20:00:00Z",
    "uploadExpiresAt": "2026-09-07T20:10:00Z",
    "uploadedAt": "2026-09-07T20:04:00Z",
    "scannedAt": "2026-09-07T20:05:00Z",
    "failureCode": null,
    "linkedEvidenceItemId": null
  },
  "meta": { "correlationId": "019..." }
}
```

Los campos todavía desconocidos son `null`. `failureCode` usa una allowlist funcional y nunca contiene excepción o detalle del proveedor. La respuesta no incluye clave, bucket, host, puerto, URL, credencial, versión de firmas ni respuesta del escáner.

## 10. Aporte de primera versión

```http
POST /api/v1/obligations/{id}/evidence
Idempotency-Key: 00000000-0000-7000-8000-000000000003
Content-Type: application/json
```

```json
{
  "requirementCode": "FOTOGRAFIA_FINAL",
  "fileId": "019..."
}
```

El cuerpo contiene exactamente esos campos. Sólo el responsable vigente de una obligación `PENDIENTE`, con `PER-EVIDENCIA-APORTAR`, puede crear la primera versión. Se exige que:

- la obligación capture una política no nula;
- el requisito pertenezca exactamente a esa versión, sea aplicable y admita la combinación binaria de la sección 4;
- el archivo pertenezca al mismo actor, obligación, política y requisito;
- el archivo esté `LIMPIO`, no expirado, no vinculado y verificado en el bucket limpio; y
- todavía no exista `evidence_item` para ese par obligación/requisito.

La transacción bloquea obligación, asignación vigente y archivo; crea `evidence_item`, crea `evidence_version` número 1 `VIGENTE`, fija el vínculo irreversible del archivo al ítem, guarda idempotencia y registra `EVIDENCE_CONTRIBUTED`. Respuesta `201` devuelve el DTO de la sección 12 y `ETag` de `evidence_item.row_version`.

No se crea el ítem para requisito inexistente, no aplicable, ajeno, estructurado o con archivo no limpio. Un archivo nunca puede alimentar dos versiones, ítems, obligaciones o requisitos.

## 11. Sustitución

```http
POST /api/v1/obligations/{id}/evidence/{itemId}/replacements
Idempotency-Key: 00000000-0000-7000-8000-000000000004
If-Match: "2"
Content-Type: application/json
```

```json
{
  "fileId": "019...",
  "reason": "Daño visible en la primera fotografía; se aporta una toma legible."
}
```

El cuerpo contiene exactamente `fileId` y `reason`; este último admite `null` sólo antes de conclusión. `If-Match` es obligatorio y representa `evidence_item.row_version`.

Autorización:

- `PENDIENTE`: sólo el responsable vigente con `PER-EVIDENCIA-APORTAR`; el motivo es opcional;
- `CONCLUIDA`: sólo actor con `PER-EVIDENCIA-SUSTITUIR`, rol vigente estrictamente superior al responsable vigente y motivo obligatorio;
- el superior no puede sustituir una obligación pendiente; el responsable no puede sustituirla concluida; y
- par, inferior, responsable histórico, puesto textual, usuario fuera de `LOR-001` o actor sin relación actual se rechazan.

El motivo se normaliza a NFC, se recortan extremos, debe tener entre 1 y 500 caracteres cuando se proporciona y no admite controles C0/C1, HTML ni saltos de línea. No puede contener nombre de archivo, URL, clave, secreto ni contenido de evidencia. Antes de conclusión, un motivo vacío se normaliza a `null`; después produce `422 MOTIVO_REQUERIDO`.

La transacción bloquea en orden obligación, asignación vigente, `evidence_item`, versión `VIGENTE` y archivo. Vuelve a evaluar estado y autoridad, compara ETag, marca la anterior `SUSTITUIDA`, crea la sucesora con `version_no + 1`, `supersedes_id`, actor, fecha y motivo, vincula el archivo, incrementa `evidence_item.row_version`, guarda idempotencia y audita `EVIDENCE_REPLACED`. Todo se confirma o revierte junto.

Una sustitución nunca actualiza conclusión, política, revisión, validación ni auditoría anterior. Si en el futuro existe una validación, permanece literal hasta que `HU-028` autorice una sustitución explícita de esa decisión.

## 12. Consulta de evidencia e historia

```http
GET /api/v1/obligations/{id}/evidence?requirementCode=FOTOGRAFIA_FINAL&status=VIGENTE&limit=25&cursor=...
```

Requiere `PER-TAREA-VER` y aplica `DEC-069` con la misma expresión SQL de alcance de `HU-023`: responsable propio, superiores sobre niveles estrictamente inferiores y `DIRECCION` sobre `LOR-001`. El listado nunca carga primero recursos ajenos. UUID inexistente, otra sucursal o fuera de alcance converge en `404 OBLIGACION_NO_ENCONTRADA`.

Los únicos filtros son `requirementCode`, `status`, `limit` y `cursor`. `status` acepta `VIGENTE` o `SUSTITUIDA`; `limit` predeterminado 25 y máximo 100. Parámetro desconocido, repetido, vacío o inválido devuelve `400 FILTRO_EVIDENCIA_INVALIDO`.

Cada fila de `data` contiene exactamente:

```json
{
  "evidenceItemId": "019...",
  "itemRowVersion": 2,
  "requirement": {
    "requirementVersionId": "019...",
    "requirementCode": "FOTOGRAFIA_FINAL",
    "kind": "FOTOGRAFIA"
  },
  "version": {
    "evidenceVersionId": "019...",
    "versionNo": 2,
    "status": "VIGENTE",
    "submittedByUserId": "019...",
    "submittedAt": "2026-09-07T20:20:00Z",
    "reason": null,
    "supersedesEvidenceVersionId": "019..."
  },
  "file": {
    "fileId": "019...",
    "originalFileName": "resultado-final.jpg",
    "mediaType": "image/jpeg",
    "sizeBytes": 482103,
    "sha256": "64-caracteres-hexadecimales-minusculos",
    "documentSubtype": null
  }
}
```

El orden total es ordinal del requisito ascendente, `versionNo` descendente y `evidenceVersionId` ascendente. El cursor Base64URL es opaco, versionado, ligado a obligación y filtros y no contiene nombres, hash, PII ni autoridad. `meta.count` cuenta sólo la página y `meta.nextCursor` sólo existe cuando hay otra fila visible. No se devuelve contenido, `structured_payload`, clave, bucket, URL, scanner, auditoría general ni validaciones.

## 13. Idempotencia, concurrencia y no efecto

- El alcance de idempotencia es actor + operación + recurso. Misma clave/cuerpo devuelve recurso, estado, ETag y código semánticos originales; cuerpo distinto devuelve `409 IDEMPOTENCY_CONFLICT`.
- La intención, confirmación, aporte y sustitución tienen scopes separados. Una clave no puede cruzar operaciones ni recursos.
- Dos aportes iniciales concurrentes con claves distintas producen una sola primera versión; la otra petición devuelve `409 EVIDENCIA_YA_EXISTE`.
- Dos sustituciones con el mismo ETag producen una ganadora y `412 VERSION_CONFLICT`; permanece una sola `VIGENTE`.
- El orden de bloqueo común es obligación, asignación vigente, ítem, versión vigente y archivo. `HU-022` deberá usar primero la misma obligación para resolver conclusión simultánea sin snapshot incoherente.
- `23505`, `40P01` y `40001` siguen el tratamiento acotado existente; PostgreSQL, no una comprobación previa, decide unicidad final.
- Un fallo de auditoría, idempotencia u outbox revierte la mutación relacional completa. Un fallo después de una operación S3 sólo puede dejar una copia técnica recuperable; nunca crea evidencia funcional.
- Una respuesta perdida después del commit se recupera por idempotencia sin nueva fila, nueva versión ni nuevo outbox.

## 14. Persistencia cerrada

La futura implementación crea una sola migración hacia adelante. No modifica migraciones históricas. El archivo de migración y Designer no llevan BOM, usan LF y `Down()` lanza la excepción de reversión bloqueada. Se actualizan snapshot EF e inventario de migraciones.

### 14.1 `file_object`

| Columna | Tipo | Regla |
|---|---|---|
| `id` | `uuid` | PK, UUID v7. |
| `branch_id` | `uuid` | FK `RESTRICT`; sólo `LOR-001`. |
| `obligation_id` | `uuid` | FK `RESTRICT` a la obligación autorizada. |
| `evidence_policy_version_id` | `uuid` | Snapshot inmutable de la obligación. |
| `requirement_version_id` | `uuid` | Requisito exacto del snapshot. |
| `requirement_code` | `varchar(64)` | Código estable exacto. |
| `requirement_kind` | `varchar(32)` | Sólo `FOTOGRAFIA` o `DOCUMENTO_REFERENCIADO` en esta historia. |
| `document_subtype` | `varchar(16) null` | Sólo `NOTA`, `REMISION`, `FACTURA` para `DOCUMENTO_RECEPCION`. |
| `bucket_class` | `varchar(16)` | `QUARANTINE` o `CLEAN`. |
| `object_key` | `varchar(73)` | Única, patrón opaco aprobado. |
| `original_name` | `varchar(255)` | Basename sanitizado; nunca clave. |
| `declared_media_type` | `varchar(32)` | `image/jpeg`, `image/png` o `application/pdf`. |
| `detected_media_type` | `varchar(32) null` | Se fija por inspección; coincide al quedar limpio. |
| `size_bytes` | `bigint` | Entre 1 y 15,728,640; valor esperado y luego verificado. |
| `sha256` | `char(64)` | Hexadecimal minúsculo esperado y luego verificado. |
| `scan_status` | `varchar(16)` | `PENDIENTE`, `LIMPIO`, `INFECTADO`, `INVALIDO`, `ERROR_ESCANEO`. |
| `scan_engine` | `varchar(64) null` | Versión sanitizada sólo cuando ClamAV respondió. |
| `scan_error_code` | `varchar(64) null` | Allowlist; nunca respuesta cruda. |
| `scanned_at` | `timestamptz null` | Instante UTC de veredicto técnico. |
| `uploaded_by` | `uuid` | FK `RESTRICT` a `app_user`. |
| `created_at` | `timestamptz` | Instante único de intención. |
| `upload_expires_at` | `timestamptz` | Exactamente `created_at + 10 minutos`. |
| `upload_completed_at` | `timestamptz null` | Confirmación válida de objeto. |
| `link_expires_at` | `timestamptz null` | Para limpio no vinculado: `scanned_at + 24 horas`. |
| `linked_evidence_item_id` | `uuid null` | FK `RESTRICT`; se fija una vez al vincular y no cambia. |
| `replicated_at` | `timestamptz null` | Reservado y siempre `NULL` en `HU-025`. |
| `row_version` | `bigint` | Inicia en 1 y aumenta en transición válida. |

Restricciones e índices:

- PK `id`; `object_key` es única; `linked_evidence_item_id` tiene índice no único cuando no es nulo, porque todas las versiones conservadas del mismo ítem mantienen su propio archivo ligado al mismo `evidence_item`;
- FK compuestas garantizan que obligación, política y requisito pertenecen al mismo snapshot;
- checks de clave, hash, tamaño, media/extensión, subtipo, fechas y coherencia estado/bucket;
- transición permitida `PENDIENTE → LIMPIO|INFECTADO|INVALIDO|ERROR_ESCANEO`; excepcionalmente `LIMPIO → INVALIDO` sólo si sigue sin vínculo y vence `link_expires_at`;
- identidad, alcance, requisito, actor, clave, nombre, declaración, tamaño y hash son inmutables;
- índices `(scan_status, created_at)`, `(obligation_id, requirement_version_id, scan_status)`, `(uploaded_by, created_at)` y parcial de expiración técnica; y
- `ON DELETE RESTRICT` en todas las FK históricas.

### 14.2 `evidence_item`

| Columna | Tipo | Regla |
|---|---|---|
| `id` | `uuid` | PK, UUID v7. |
| `obligation_id` | `uuid` | FK `RESTRICT`. |
| `evidence_policy_version_id` | `uuid` | Debe ser la capturada por la obligación. |
| `requirement_version_id` | `uuid` | FK al requisito exacto. |
| `requirement_code` | `varchar(64)` | Debe coincidir con el requisito. |
| `requirement_kind` | `varchar(32)` | Clase binaria aprobada. |
| `created_at` | `timestamptz` | Instante UTC de primera aportación. |
| `row_version` | `bigint` | ETag; inicia en 1 y aumenta por sustitución. |

Existe unicidad `(obligation_id, requirement_version_id)`, FK compuestas de coherencia con obligación/política/requisito e índices por obligación y requisito. No se borra ni cambia de requisito.

### 14.3 `evidence_version`

| Columna | Tipo | Regla |
|---|---|---|
| `id` | `uuid` | PK, UUID v7. |
| `evidence_item_id` | `uuid` | FK `RESTRICT`. |
| `version_no` | `integer` | Mayor a cero y creciente por ítem. |
| `file_object_id` | `uuid` | FK `RESTRICT`, única; sólo archivo limpio ligado al mismo ítem. |
| `structured_payload` | `jsonb null` | Check obliga `NULL` en esta historia. |
| `status` | `varchar(16)` | `VIGENTE` o `SUSTITUIDA`. |
| `submitted_by` | `uuid` | FK `RESTRICT` a `app_user`. |
| `submitted_at` | `timestamptz` | Instante UTC único de la versión. |
| `reason` | `varchar(500) null` | Normalizado; obligatorio para sustitución posterior a conclusión. |
| `supersedes_id` | `uuid null` | FK `RESTRICT` a la predecesora inmediata del mismo ítem. |
| `row_version` | `bigint` | Inicia en 1; la predecesora aumenta al sustituirse. |

Existen unicidad `(evidence_item_id, version_no)`, índice único parcial de una `VIGENTE` por ítem, unicidad de `file_object_id` y de `supersedes_id` no nulo, y checks de primera versión/cadena. Una guarda PostgreSQL confirma al insertar que el archivo está `LIMPIO`, en `CLEAN`, ligado al mismo ítem y no expirado. Otra guarda sólo permite `VIGENTE → SUSTITUIDA` con sucesora inmediata; rechaza DELETE, reversión de estado, cambio de archivo, actor, fecha, motivo o cadena.

No se crea `evidence_review_snapshot`, `execution_result`, `validation_requirement`, `validation_decision_version` ni otra tabla.

## 15. Limpieza técnica sin borrado funcional

Se registra `CLEAN_EXPIRED_EVIDENCE_UPLOADS` como job del `Sgol.Worker run-job` existente; no se crea scheduler. El invocador operativo futuro decidirá cuándo ejecutarlo. El job usa `scheduled_for` como corte UTC, advisory lock y `scheduled_job_run` existentes.

Sólo trata filas sin `linked_evidence_item_id`:

- intención `PENDIENTE` no confirmada cuyo `upload_expires_at` ya pasó: elimina, si existe, la clave exacta de cuarentena y marca `INVALIDO/UPLOAD_EXPIRED`;
- archivo `LIMPIO` no vinculado cuyo `link_expires_at` ya pasó: elimina la copia limpia exacta verificada y marca `INVALIDO/UNLINKED_EXPIRED`; y
- caída después del borrado externo y antes del commit: la reejecución acepta ausencia del objeto exacto y completa estado/auditoría de forma idempotente.

Procesa lotes máximos de 100 en orden `(fecha_de_expiración, id)` y conserva checkpoint. Nunca selecciona un archivo vinculado ni borra `evidence_item`, `evidence_version`, objetos de una versión funcional, objetos `INFECTADO`, `ERROR_ESCANEO` o rechazados sujetos a investigación. La retención o eliminación posterior de estos últimos permanece bloqueada por la política legal/seguridad futura.

## 16. Auditoría, privacidad y observabilidad

Acciones allowlist:

- `EVIDENCE_UPLOAD_INTENT_CREATED`;
- `EVIDENCE_UPLOAD_COMPLETED`;
- `EVIDENCE_UPLOAD_REJECTED`;
- `EVIDENCE_FILE_INSPECTED`;
- `EVIDENCE_CONTRIBUTED`;
- `EVIDENCE_REPLACED`; y
- `EVIDENCE_ORPHAN_CLEANED`.

La auditoría registra actor humano o `SYSTEM`, instante, `LOR-001`, recurso, correlación, resultado y sólo IDs/estados antes-después necesarios. La sustitución conserva el motivo normalizado. Nunca guarda binario, nombre original, hash, media metadata, clave, bucket, URL firmada, firma detectada, respuesta del scanner, excepción, SQL o secreto.

Las métricas existentes se reutilizan y amplían sin etiquetas de alta cardinalidad:

- `sgol.evidence.upload_intents`, `sgol.evidence.upload_completions`;
- `sgol.evidence.quarantine.entered`, `sgol.evidence.quarantine.exited`;
- `sgol.evidence.scans`, `sgol.evidence.scan_failures`, `sgol.evidence.promotions`;
- `sgol.evidence.links`, `sgol.evidence.replacements`, `sgol.evidence.rejections`; y
- histogramas de carga lógica, espera de escaneo, inspección y promoción.

Etiquetas permitidas: `operation`, `mediaType`, `result`, `attempt`, `service` y `requirementKind`. Se prohíben ID de usuario/obligación/archivo, nombre, hash, clave, bucket, URL, host, puerto, motivo y contenido.

Configuración de evidencia ausente o inválida falla cerrada al activar los endpoints o el manejador. No existe fallback a memoria, filesystem, scanner simulado ni vínculo sin inspección.

## 17. Errores normalizados y anti-IDOR

| HTTP | Código | Uso |
|---:|---|---|
| 400 | `SOLICITUD_EVIDENCIA_INVALIDA` | JSON, campo, UUID, nombre, hash o valor mal formado. |
| 400 | `IDEMPOTENCY_KEY_INVALIDA` / `IF_MATCH_INVALIDO` | Cabecera ausente o mal formada según la operación. |
| 400 | `CSRF_INVALIDO` | Sesión autenticada sin par antiforgery válido conforme a la sección 24. |
| 400 | `FILTRO_EVIDENCIA_INVALIDO` | Filtro/cursor desconocido, repetido o inválido. |
| 401 | `AUTENTICACION_REQUERIDA` | Sesión ausente, expirada o MFA incompleto. |
| 403 | `ACCESO_DENEGADO` | Falta el permiso requerido antes de resolver un recurso. |
| 404 | `OBLIGACION_NO_ENCONTRADA` | UUID inexistente, sucursal ajena o fuera de alcance. |
| 404 | `ARCHIVO_NO_ENCONTRADO` / `EVIDENCIA_NO_ENCONTRADA` | Recurso de esa ruta inexistente o no visible. |
| 409 | `IDEMPOTENCY_CONFLICT` | Misma clave, cuerpo distinto. |
| 409 | `CARGA_NO_ENCONTRADA` | No existe objeto exacto al confirmar. |
| 409 | `EVIDENCIA_YA_EXISTE` / `ARCHIVO_YA_VINCULADO` | Conflicto funcional sin sobrescritura. |
| 410 | `INTENCION_CARGA_EXPIRADA` | Ventana de carga/confirmación vencida. |
| 412 | `VERSION_CONFLICT` | ETag desactualizado. |
| 413 | `ARCHIVO_DEMASIADO_GRANDE` | Tamaño declarado sobre 15 MiB. |
| 415 | `TIPO_ARCHIVO_NO_ADMITIDO` | Media type/extensión no admitidos. |
| 422 | `REQUISITO_EVIDENCIA_INVALIDO` | Requisito inexistente, ajeno o no aplicable tras autorizar la obligación. |
| 422 | `REQUISITO_CONDICIONAL_NO_EVALUABLE` | Requisito con condición distinta de `SIEMPRE` cuya evaluación ejecutable pertenece a HU-026. |
| 422 | `TIPO_EVIDENCIA_NO_IMPLEMENTADO` | Clase estructurada o formulario sin contrato ejecutable. |
| 422 | `ARCHIVO_NO_LIMPIO` | Intento de vínculo con estado distinto de `LIMPIO`. |
| 422 | `MOTIVO_REQUERIDO` / `SUSTITUCION_NO_PERMITIDA` | Guarda de estado, autoridad o motivo. |
| 429 | `LIMITE_INTENCIONES_EXCEDIDO` | Límite 30/h por usuario. |
| 503 | `INFRAESTRUCTURA_EVIDENCIA_NO_DISPONIBLE` | Configuración o dependencia requerida no disponible. |

Los detalles públicos son genéricos. No distinguen existencia de obligación, ítem o archivo fuera de alcance; no incluyen estado interno, clave, proveedor, stack, SQL, ruta, URL, contenido ni secreto. El tiempo y los conteos de listados no confirman recursos ajenos.

## 18. Pruebas obligatorias posteriores a la aprobación

### 18.1 Sin Docker

- contrato exacto de las seis rutas, payloads, cabeceras, respuestas, errores y ausencia de endpoints adicionales;
- intención autorizada, enlace inmutable a obligación/requisito, expiración 10 minutos, rate limit y URL limitada a cuarentena;
- firma sin credencial permanente, URL nunca persistida/logueada y CORS allowlist sin wildcard;
- confirmación idempotente, objeto ausente/duplicado/alterado/discordante y rollback de auditoría/outbox;
- JPEG, PNG y PDF sintéticos válidos; vacío, exceso, firma, estructura, tipo, extensión y SHA discordantes;
- excepción, timeout, indisponibilidad y respuesta desconocida nunca `LIMPIO`;
- `INFECTADO`, `INVALIDO`, `ERROR_ESCANEO` y pendiente nunca vinculables ni promovibles;
- primera versión, una `VIGENTE`, sustitución conserva ambas, cadena y actor/fecha/motivo;
- responsable sólo antes de conclusión; superior estricto sólo después y con motivo; par, inferior, superior de pendiente y fuera de alcance rechazados;
- sustitución no invoca ni modifica contrato de validación, conclusión o evaluación;
- concurrencia, ETag e idempotencia sin duplicar ítem, versión, archivo, auditoría ni outbox elegible;
- limpieza sólo de objetos no vinculados y ninguna eliminación funcional;
- logs, métricas, auditoría y Problem Details sin binario, nombre, hash, clave, URL o secreto; y
- arquitectura: dominio sin EF/ASP.NET/S3/ClamAV, adaptadores fuera del dominio, único Worker/outbox y ausencia de UI/HU posteriores.

### 18.2 PostgreSQL real externa

- migración desde cero y sobre la base vigente; aplicación repetida sin cambio;
- todas las FK compuestas, checks, índices, `ON DELETE RESTRICT` y guardas de transición;
- una versión `VIGENTE`, una rama histórica lineal y archivo limpio ligado al mismo ítem;
- requisito pertenece a la política capturada de la obligación;
- aportes/sustituciones concurrentes e idempotentes;
- conclusión simultánea simulada contra el bloqueo común sin estado incoherente;
- auditoría, idempotencia, evidencia y outbox atómicos; fallo inyectado revierte todo; y
- usuario de aplicación incapaz de borrar historia o alterar transiciones protegidas.

### 18.3 S3-compatible y ClamAV externa

- PUT firmado sobre objeto exacto, cuarentena privada, overwrite y acceso anónimo denegados;
- metadata y longitud firmadas, confirmación y lectura real acotada;
- SHA-256 estable, tipo real y corpus JPEG/PNG/PDF;
- `LIMPIO` promovido y releído; `INFECTADO`, `INVALIDO` y `ERROR_ESCANEO` sólo en cuarentena;
- EICAR opt-in aislado, timeouts, caída y respuesta desconocida;
- recuperación tras copia limpia previa a commit y eliminación segura de parcial incompatible;
- cinco entregas máximas sin retries multiplicativos; y
- limpieza de objeto técnico expirado sin tocar evidencia vinculada.

No se usa SQLite. El desarrollador ejecuta externamente, en una sola solicitud de gate, las suites con PostgreSQL, SeaweedFS, ClamAV, Docker o Testcontainers y comunica comando, total, errores, omitidas, duración, imágenes/digests y fecha de firmas cuando esté disponible.

## 19. Archivos previstos después de la aprobación

La implementación podrá modificar únicamente:

- `src/Modules/Evidence/` para contratos y reglas funcionales/técnicas aprobadas;
- adaptadores de evidencia, API y persistencia directamente necesarios en `src/Sgol.Web/`;
- composición y manejador del outbox/job aprobado en `src/Sgol.Worker/` y la mínima ampliación de contexto en `Sgol.JobInfrastructure`;
- una migración nueva, su Designer, snapshot EF e inventario de migraciones;
- pruebas unitarias, arquitectura, API, PostgreSQL e integración externa directamente afectadas;
- configuración no secreta mínima de CORS S3 y documentación externa de evidencia;
- `docs/traceability/README.md`, `docs/traceability/IMPLEMENTATION_STATUS.md` y documentación técnica mínima afectada; y
- esta adenda después de recibir aprobación íntegra.

No se editarán migraciones históricas, los tres Designer ajenos, `docs/design/logo.svg`, `docs/design/mapa-pantallas.md`, F00–F07 ni `Fuentes/`.

## 20. Gates y cierre

Después de aprobar, implementar y revisar el diff completo, se ejecutan una sola vez y en este orden:

1. `dotnet restore SGOL.slnx --locked-mode` fuera del aislamiento;
2. `dotnet build SGOL.slnx --no-restore --configuration Release`;
3. suite unitaria completa;
4. suite de arquitectura completa;
5. pruebas enfocadas de `HU-025` sin Docker;
6. suites externas afectadas con PostgreSQL, SeaweedFS y ClamAV ejecutadas por el desarrollador;
7. `dotnet format SGOL.slnx --verify-no-changes --no-restore`;
8. `./scripts/ci/Assert-NoVulnerablePackages.ps1`;
9. modelo EF sin cambios pendientes;
10. `./scripts/ci/verify-fuentes-protection.ps1`;
11. `./scripts/ci/verify-fuentes-mirror.ps1`; y
12. `git diff --check`.

Los gates 10 y 11 son secuenciales. Un gate omitido, compuesto o pendiente se informa como no verificado.

La rama del PR registrará `HU-025` como propuesta. Ese mismo registro adquirirá estado efectivo `Terminada` en `master`, sin commit administrativo posterior, sólo cuando el commit exacto tenga pipeline requerido verde, aprobación humana, merge, ascendencia verificada en `origin/master`, PostgreSQL satisfactorio, integración S3/ClamAV satisfactoria, `Fuentes/` protegida y cero defectos bloqueantes conocidos.

La aprobación contractual no autoriza commit, publicación, PR ni merge. Cada acción requiere autorización independiente.

## 21. Decisión solicitada

Se solicita aprobar o rechazar íntegramente esta propuesta, incluidos sus límites deliberados:

1. las seis rutas exactas y ausencia de descarga;
2. URL PUT firmada de 10 minutos y confirmación antes de expirar;
3. aporte binario sólo para `FOTOGRAFIA` y `DOCUMENTO_REFERENCIADO`;
4. rechazo explícito de evidencia estructurada y, por ello, `HU-026` todavía no habilitada para esas clases;
5. aporte/sustitución previa sólo por responsable y sustitución posterior sólo por superior estricto con motivo;
6. las tres tablas, columnas, guardas, índices y única migración;
7. outbox de cinco intentos, recuperación y job de limpieza sin scheduler; y
8. retención técnica de limpio no vinculado por 24 horas, sin purga automática de rechazados.

Cualquier cambio material requiere revisar esta propuesta agrupada antes de editar implementación.

## 22. Revisión material posterior a la aprobación inicial

Al traducir la versión inicialmente aprobada a persistencia y adaptadores se detectaron tres puntos todavía insuficientes para una implementación determinista. No se retuvo scaffolding de producción basado en supuestos.

1. **Aplicabilidad condicional.** HU-025 acepta únicamente `condition_code = SIEMPRE`. `DIFERENCIA_O_DANO` se rechaza con `REQUISITO_CONDICIONAL_NO_EVALUABLE`; no se interpreta `input_payload` ni una declaración del cliente. Esta decisión evita adelantar la evaluación reservada a HU-026.
2. **Rate limiting consistente entre instancias.** El límite es una ventana móvil persistente de 60 minutos calculada sobre `file_object` y serializada por actor con `pg_advisory_xact_lock`. No se agrega Redis, almacenamiento en memoria ni una tabla adicional.
3. **CORS de carga cerrado.** `Evidence:Storage:AllowedUploadOrigins` es obligatorio y contiene orígenes absolutos sin wildcard. Producción exige HTTPS; `Development` y `CI` admiten sólo loopback o red privada. La política del almacenamiento permite únicamente el `PUT` firmado y sus encabezados durante 10 minutos.

La aprobación inicial del 2026-09-07 no cubría estas decisiones nuevas. La revisión material fue aprobada íntegramente por el responsable el 2026-09-07 y habilita la implementación bajo este contrato, sin autorizar commit, publicación, PR ni merge.

## 23. Segunda revisión material: cardinalidad del vínculo archivo–ítem

Durante la revisión integral del modelo se detectó una contradicción entre la historia obligatoria y la restricción propuesta para `file_object.linked_evidence_item_id`:

- conservar todas las versiones exige conservar un `file_object` distinto por cada `evidence_version`;
- todas esas versiones pertenecen al mismo `evidence_item`; y
- una unicidad sobre `linked_evidence_item_id` impediría que el segundo archivo se ligara al ítem, por lo que ninguna sustitución podría confirmarse.

La corrección propuesta elimina únicamente la unicidad de `file_object.linked_evidence_item_id` y conserva un índice parcial no único para consultas por ítem. La autoridad contra reutilizar un archivo permanece en la unicidad de `evidence_version.file_object_id`; cada `file_object` sigue admitiendo un solo `linked_evidence_item_id`, fijado una vez e inmutable. No cambia ningún endpoint, payload, permiso, estado, tabla, columna, flujo técnico ni límite de alcance.

La aprobación anterior no cubría esta corrección material. La segunda revisión fue aprobada íntegramente por el responsable el 2026-09-07 y habilita su implementación, sin autorizar commit, publicación, PR ni merge.

## 24. Tercera revisión material: frontera CSRF y aprovisionamiento CORS

Durante la revisión completa previa a gates se comprobó que dos obligaciones ya aprobadas aún no fijan una frontera ejecutable segura. La mención genérica de «token CSRF» no determina su transporte ni su error, y configurar CORS desde el adaptador operativo exigiría ampliar permanentemente su credencial con autoridad administrativa sobre el bucket. Esta revisión propone cerrar ambos puntos sin agregar endpoints ni elevar privilegios de escritura de la aplicación.

### 24.1 CSRF enlazado a sesión

- Las cuatro mutaciones de la sección 5 validan el antiforgery estándar de ASP.NET Core después de confirmar que existe una identidad autenticada y antes de deserializar el cuerpo o invocar servicios funcionales.
- El token de petición viaja exclusivamente en una cabecera única `X-CSRF-TOKEN`; el token complementario usa cookie `__Host-SGOL-CSRF`, `Secure=Always`, `HttpOnly=true`, `SameSite=Strict` y `Path=/`.
- La adquisición del par de tokens pertenece a la frontera transversal de sesión y a la futura interfaz autenticada; `HU-025` no agrega endpoint, cookie de sesión, login, bootstrap ni UI para emitirlo. Las pruebas HTTP generan el par mediante `IAntiforgery` dentro del host de prueba, sin introducir una ruta productiva.
- Una sesión autenticada con token ausente, repetido, alterado o no ligado a su cookie recibe `400 CSRF_INVALIDO` en `application/problem+json`; no se invoca negocio ni se escribe estado. Una petición no autenticada conserva precedencia y devuelve `401 AUTENTICACION_REQUERIDA` sin exigir ni emitir tokens.
- Los dos GET no requieren ni emiten token CSRF y permanecen sin efectos de escritura.

### 24.2 CORS del bucket sin privilegio administrativo operativo

- La credencial normal de Web/Worker no recibe `PutBucketCors` ni otra capacidad administrativa. La política CORS es infraestructura externa declarativa, aplicada por la credencial efímera de aprovisionamiento antes de habilitar carga, conforme a que el despliegue productivo permanece fuera de `HU-025`.
- La política exacta del bucket de cuarentena contiene una sola regla `sgol-evidence-upload`: orígenes iguales a `Evidence:Storage:AllowedUploadOrigins`, método único `PUT`, encabezados únicos `Content-Type`, `Content-Length`, `If-None-Match`, `x-amz-meta-sgol-sha256`, `x-amz-meta-sgol-media-type` y `x-amz-meta-sgol-size-bytes`, cero credenciales de navegador, cero encabezados expuestos y `MaxAgeSeconds=600`. No se configura CORS en el bucket limpio.
- Antes de emitir la primera URL firmada de cada proceso, el adaptador realiza una verificación de sólo lectura de esa política y memoriza únicamente el resultado satisfactorio. Política ausente, regla adicional, wildcard, origen, método, encabezado o duración discordante produce fallo cerrado `503 INFRAESTRUCTURA_EVIDENCIA_NO_DISPONIBLE`; no crea `file_object`, auditoría ni idempotencia y nunca intenta reparar el bucket.
- La suite externa aprovisiona la regla exacta con su identidad administrativa efímera, comprueba que la identidad operativa sólo pueda verificarla y usar el `PUT` firmado, y prueba el fallo cerrado ante política ausente o discordante. No se versionan credenciales ni se ejecuta aprovisionamiento productivo en esta historia.

Esta tercera revisión no cambia rutas, payloads funcionales, tablas, estados, permisos de negocio, cardinalidad, historia ni alcance. Fue aprobada íntegramente por el responsable el 2026-09-07 y habilita la implementación de CSRF y la verificación CORS, sin autorizar commit, publicación, PR ni merge.

## 25. Cuarta revisión material propuesta: límite de autorización CORS de SeaweedFS

Durante la ejecución externa posterior a la tercera revisión se comprobó que SeaweedFS 4.45 autoriza `PutObject`, `PutBucketCors` y `DeleteBucketCors` mediante la misma acción gruesa `Write`. La identidad operativa necesita `Write` para cargar en cuarentena, promover al bucket limpio y aplicar la limpieza técnica; por tanto, la configuración estática de este proveedor no permite otorgar esas operaciones de objeto y denegar simultáneamente la mutación CORS. La suite confirmó la carga firmada y falló correctamente al intentar demostrar una separación que el proveedor no expresa.

Se propone cerrar esta limitación sin elevar la autoridad ejercida por SGOL ni simular una garantía inexistente:

- Web y Worker conservan únicamente la credencial operativa; ningún flujo, endpoint, servicio, manejador o job de SGOL invoca `PutBucketCors`, `DeleteBucketCors` ni otra mutación de configuración del bucket.
- Una identidad efímera distinta continúa aprovisionando la regla CORS exacta antes de habilitar la carga. El adaptador operativo sólo ejecuta `GetBucketCors`, memoriza únicamente una verificación satisfactoria y falla cerrado ante ausencia o discordancia.
- La suite SeaweedFS verifica las identidades distintas, el aprovisionamiento administrativo, la lectura operativa, el `PUT` firmado, la política exacta y el fallo cerrado por deriva. No afirma que SeaweedFS deniegue `PutBucketCors` a una identidad con `Write`, y elimina exclusivamente esa aserción imposible.
- Una prueba de arquitectura conserva como autoridad sobre el código SGOL la ausencia de llamadas a `PutBucketCors` y `DeleteBucketCors` fuera del proyecto de pruebas externas.
- El despliegue productivo sigue fuera de `HU-025`. Antes de habilitarlo, la revisión operativa debe registrar una de estas garantías: proveedor o gateway S3 que separe mutaciones de configuración de operaciones sobre objetos; política externa equivalente; o aceptación explícita y documentada del riesgo residual de la autorización gruesa de SeaweedFS. Sin una de ellas no se declara verificada en producción la separación de privilegios CORS.
- Esta excepción no permite al runtime reparar CORS, no reduce la comparación exacta, no habilita wildcard, no expone credenciales y no convierte una política ausente o alterada en estado disponible.

La corrección del esquema de la URL firmada (`HTTP` sólo para el endpoint privado permitido en `Development`/`CI`; `HTTPS` en los demás casos) no depende de esta revisión y permanece dentro del contrato aprobado. La cuarta revisión no cambia rutas, payloads funcionales, tablas, estados, permisos de negocio, historia ni alcance de HU-025. Fue aprobada íntegramente por el responsable el 2026-09-07 y habilita este ajuste de pruebas y documentación, sin autorizar commit, publicación, PR ni merge.
