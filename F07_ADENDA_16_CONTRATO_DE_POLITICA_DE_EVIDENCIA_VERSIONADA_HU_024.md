# SGOL — Adenda 16 a F07: contrato de política de evidencia versionada para `HU-024`

## 1. Control del documento

| Campo | Valor |
|---|---|
| Tipo | Adenda normativa a `F07_BACKLOG_DE_IMPLEMENTACION.md` |
| Estado | `APROBADA ÍNTEGRAMENTE` |
| Fecha | 2026-09-05 |
| Historia | `HU-024 — Dirección versiona evidencia requerida por TAR` |
| Corte y épica | `CV-03` / `EP-07` |
| Efecto | Inserta y precisa el contrato ejecutable de `HU-024` sin alterar su orden ni habilitar historias posteriores |
| Precedencia | `HU-023` reconocida como `Terminada` efectiva; `TECH-EVID-001` permanece antes de `HU-025`, no antes de `HU-024` |
| Conservación | Mantiene sin cambios los documentos F00–F07 aprobados y `Fuentes/` |
| Aprobación | Aprobada íntegramente por el responsable el 2026-09-05 antes de iniciar la implementación |

La aprobación autoriza la implementación y migración descritas. No autoriza commit, publicación de rama, pull request ni merge, que permanecen como decisiones independientes.

## 2. Resultado cerrado de `HU-024`

`HU-024` permite que una sesión vigente de rol canónico `DIRECCION`, permiso `PER-EVIDENCIA-CONFIG` y alcance `LOR-001` cree versiones de la política mínima de evidencia de las ocho TAR del MVP. La política:

1. usa exclusivamente el catálogo cerrado de esta adenda;
2. no admite requisitos, clases, condiciones, textos o esquemas libres;
3. considera obligatorio todo requisito configurado y aplicable;
4. se publica mediante el `configuration_release` existente;
5. conserva cada versión y su predecesora;
6. queda capturada por referencia inmutable en cada obligación nueva; y
7. confirma la versión, la sustitución y la auditoría de manera atómica.

No se implementan aportación, sustitución, almacenamiento, evaluación, revisión ni descarga de evidencia. Los códigos `FORM-ADM-02` y `F-ENT-001` son referencias lógicas; no representan archivos ni autorizan contenido binario.

## 3. Alcance y exclusiones

### 3.1 Incluido

- contratos de dominio y aplicación de la política de evidencia;
- catálogo cerrado de requisitos, clases y condiciones;
- un único endpoint nuevo: `PUT /api/v1/task-definitions/{taskCode}/evidence-policy`;
- integración de la política con el ciclo ya existente de `configuration_release`;
- persistencia mínima de catálogo, versión de política, requisitos de la versión y vínculo de snapshot de obligación;
- autorización de servidor, ETag, idempotencia, concurrencia y auditoría;
- pruebas unitarias, API, arquitectura y PostgreSQL directamente aplicables; y
- trazabilidad mínima de `HU-024`.

### 3.2 Excluido

- `HU-025`, `HU-026`, `HU-022`, `HU-027` a `HU-035` y cualquier otra historia de `CV-03` o `CV-04`;
- `TECH-EVID-001`, S3, buckets, intenciones o URL firmadas, cuarentena, SHA-256, escáner y descarga;
- `file_object`, `evidence_item`, `evidence_version` y `evidence_review_snapshot`;
- carga, sustitución, revisión, evaluación `COMPLETA/INCOMPLETA`, conclusión, validación, avisos, bandejas, supervisión e indicadores;
- endpoints adicionales de evidencia o archivos;
- motor genérico de formularios, reglas o condiciones;
- datos reales, integraciones externas, Worker, jobs o scheduler; y
- cambios en `Fuentes/`.

### 3.3 Interfaz

`HU-024` no incluye UI en este corte. No se agregan páginas, rutas de navegación, vistas, componentes, CSS, JavaScript ni Playwright. El contrato reservado y los criterios de aceptación sólo fijan la operación API; no existe una ruta o composición visual aprobada para esta historia. Por ello no corresponde abrir ni extender `docs/design` durante su implementación.

## 4. Modelo conceptual y vocabulario

| Concepto | Definición cerrada |
|---|---|
| Política lógica | La política de evidencia perteneciente a una identidad estable `task_definition`; existe una por TAR MVP. |
| Versión de política | Snapshot identificado por `evidence_policy_version.id`, con `version_no` creciente dentro de la política lógica. |
| Requisito configurado | Fila inmutable de `evidence_requirement_version` incluida en una versión de política. |
| Requisito aplicable | Requisito configurado cuya condición cerrada resulta verdadera para la obligación. `SIEMPRE` resulta verdadera; `DIFERENCIA_O_DANO` sólo cuando la recepción registra diferencia o daño. |
| Requisito obligatorio | Todo requisito configurado y aplicable. El cliente no envía ni puede alterar `isRequired`; el servidor y PostgreSQL lo fijan en `true`. |
| Elemento no configurado | Código que no aparece en la versión capturada. No produce faltante. En el catálogo v1 de esta adenda todos los códigos enumerados para la TAR forman su política mínima, por lo que una solicitud que omita cualquiera es incompleta y se rechaza. |
| Política capturada | FK persistida por la obligación hacia la versión exacta aplicable al instante de su materialización. No se vuelve a resolver contra la política vigente. |
| Referencia lógica | Identificador de formulario o documento sin archivo, contenido, URL ni metadatos de almacenamiento. |

No existen modos agregadores `CUALQUIERA` ni `AL_MENOS_N`. La expresión “acción o conformidad” de `TAR-0005` es el contenido cerrado de un único registro digital según el resultado del cálculo; no es una elección entre dos requisitos. “Nota/remisión/factura” de `TAR-0092` es una sola referencia documental cuyo subtipo cerrado será parte del contrato de aportación de `HU-025`; tampoco es un modo agregador.

## 5. Clases cerradas de requisito

| Código | Representación en `HU-024` | Límite |
|---|---|---|
| `REGISTRO_DIGITAL` | Referencia a un registro funcional estructurado identificado por el código del requisito. | No autoriza texto libre ilimitado, integración ni archivo. |
| `DOCUMENTO_REFERENCIADO` | Referencia lógica localizable a un documento aprobado. | No crea ni almacena binario. |
| `FOTOGRAFIA` | Declaración de que el requisito futuro se satisface con una fotografía. | No crea carga, objeto, hash ni estado de escaneo. |
| `FORMULARIO_REFERENCIADO` | Referencia lógica a un formulario canónico nombrado por el requisito. | `FORM-ADM-02` y `F-ENT-001` no son archivos en esta historia. |
| `CHECKLIST_ESTRUCTURADO` | Lista cerrada de ítems y respuesta estructurada. | Sólo el checklist de diez ítems de `TAR-0018`. |
| `DATO_ESTRUCTURADO` | Dato funcional tipado por el código estable del requisito. | Su payload de aportación no se define ni se persiste en `HU-024`. |

El `PUT` no recibe descripción, etiqueta, schema JSON, expresión, operador, campo arbitrario ni configuración específica de archivo. La combinación `(taskCode, requirementCode, kind, conditionCode)` debe coincidir exactamente con una fila del catálogo de la sección 7.

## 6. Condiciones cerradas

| Código persistido | Forma API | Semántica |
|---|---|---|
| `SIEMPRE` | `condition: null` | El requisito es aplicable a toda obligación que capture la política. |
| `DIFERENCIA_O_DANO` | `condition: { "code": "DIFERENCIA_O_DANO" }` | Sólo es verdadera cuando los datos estructurados de la recepción de `TAR-0092` indican al menos una diferencia o un daño. |

`DIFERENCIA_O_DANO` se admite exclusivamente para `TAR-0092 / FOTO_DIFERENCIA_DANO / FOTOGRAFIA`. No recibe operandos, nombres de campo, comparadores ni valores configurables. La fotografía final de `TAR-0018` usa `SIEMPRE`.

`HU-024` persiste y devuelve la condición, pero no la evalúa. La evaluación y el reporte de faltantes pertenecen exclusivamente a `HU-026`. El contrato posterior deberá consumir estos códigos sin cambiar su significado.

## 7. Catálogo cerrado por TAR

El orden de cada tabla es canónico y se devuelve como `ordinal` ascendente. Todos los requisitos indicados usan `isRequired=true`.

### 7.1 `TAR-0005`

| Ordinal | `requirementCode` | `kind` | Condición | Significado cerrado |
|---:|---|---|---|---|
| 1 | `CALCULO_AVANCE` | `REGISTRO_DIGITAL` | `SIEMPRE` | Registro del cálculo `venta/meta`. |
| 2 | `ACCION_O_CONFORMIDAD` | `REGISTRO_DIGITAL` | `SIEMPRE` | Registra acción, responsable e inicio si el avance es menor a 90 %, o conformidad sin acción si es mayor o igual a 90 %. |

### 7.2 `TAR-0007`

| Ordinal | `requirementCode` | `kind` | Condición | Significado cerrado |
|---:|---|---|---|---|
| 1 | `LIBERACION` | `REGISTRO_DIGITAL` | `SIEMPRE` | Liberación documentada. |
| 2 | `MERCANCIA` | `DATO_ESTRUCTURADO` | `SIEMPRE` | Identidad de mercancía vinculada a la liberación. |
| 3 | `FECHA_HORA` | `DATO_ESTRUCTURADO` | `SIEMPRE` | Fecha y hora documentadas de la liberación. |
| 4 | `RETORNO_EXHIBICION` | `REGISTRO_DIGITAL` | `SIEMPRE` | Constancia de retorno a exhibición. |

### 7.3 `TAR-0008`

| Ordinal | `requirementCode` | `kind` | Condición | Significado cerrado |
|---:|---|---|---|---|
| 1 | `EXPEDIENTE` | `DOCUMENTO_REFERENCIADO` | `SIEMPRE` | Referencia lógica del expediente. |
| 2 | `SECUENCIA` | `REGISTRO_DIGITAL` | `SIEMPRE` | Secuencia documentada de hechos. |
| 3 | `DECISION` | `REGISTRO_DIGITAL` | `SIEMPRE` | Decisión registrada. |
| 4 | `FUNDAMENTO` | `DATO_ESTRUCTURADO` | `SIEMPRE` | Fundamento vinculado a la decisión. |
| 5 | `AVISO_INTERNO` | `REGISTRO_DIGITAL` | `SIEMPRE` | Constancia de comunicación interna privada. |

### 7.4 `TAR-0011`

| Ordinal | `requirementCode` | `kind` | Condición | Significado cerrado |
|---:|---|---|---|---|
| 1 | `EVALUACION` | `REGISTRO_DIGITAL` | `SIEMPRE` | Evaluación documentada. |
| 2 | `AUTORIZACION` | `DOCUMENTO_REFERENCIADO` | `SIEMPRE` | Referencia de autorización previa. |
| 3 | `REPARACION_O_CAMBIO` | `REGISTRO_DIGITAL` | `SIEMPRE` | Solución ejecutada conforme a la autorización. |
| 4 | `COMPROBANTES` | `DOCUMENTO_REFERENCIADO` | `SIEMPRE` | Referencias localizables de comprobantes. |
| 5 | `ENTREGA` | `REGISTRO_DIGITAL` | `SIEMPRE` | Constancia de entrega. |

### 7.5 `TAR-0018`

| Ordinal | `requirementCode` | `kind` | Condición | Significado cerrado |
|---:|---|---|---|---|
| 1 | `CHECKLIST_COMPLETO` | `CHECKLIST_ESTRUCTURADO` | `SIEMPRE` | Los diez ítems aprobados en F05, sin adiciones: producto; zona/familia; formación; etiquetas; alineación; ocupación; limpieza; integridad; señalización; correspondencia con planograma/lista. |
| 2 | `FOTOGRAFIA_FINAL` | `FOTOGRAFIA` | `SIEMPRE` | Fotografía final obligatoria en todos los casos. |
| 3 | `PLANOGRAMA_O_LISTA` | `DOCUMENTO_REFERENCIADO` | `SIEMPRE` | Referencia lógica al planograma o lista vigente. |

### 7.6 `TAR-0026`

| Ordinal | `requirementCode` | `kind` | Condición | Significado cerrado |
|---:|---|---|---|---|
| 1 | `FORM_ADM_02` | `FORMULARIO_REFERENCIADO` | `SIEMPRE` | Referencia lógica a `FORM-ADM-02`. |
| 2 | `COMPROBANTE_LOCALIZABLE` | `DOCUMENTO_REFERENCIADO` | `SIEMPRE` | Referencia localizable del comprobante. |

### 7.7 `TAR-0092`

| Ordinal | `requirementCode` | `kind` | Condición | Significado cerrado |
|---:|---|---|---|---|
| 1 | `DOCUMENTO_RECEPCION` | `DOCUMENTO_REFERENCIADO` | `SIEMPRE` | Referencia lógica a nota, remisión o factura. |
| 2 | `F_ENT_001` | `FORMULARIO_REFERENCIADO` | `SIEMPRE` | Referencia lógica a `F-ENT-001`. |
| 3 | `FOTO_DIFERENCIA_DANO` | `FOTOGRAFIA` | `DIFERENCIA_O_DANO` | Fotografía aplicable exclusivamente si existe diferencia o daño. |

### 7.8 `TAR-0093`

| Ordinal | `requirementCode` | `kind` | Condición | Significado cerrado |
|---:|---|---|---|---|
| 1 | `FOTOGRAFIA_INCIDENCIA` | `FOTOGRAFIA` | `SIEMPRE` | Fotografía de la incidencia. |
| 2 | `ANOTACION_F_ENT_001` | `FORMULARIO_REFERENCIADO` | `SIEMPRE` | Anotación lógica vinculada a `F-ENT-001`. |
| 3 | `CONSTANCIA_AVISO_INTERNO` | `REGISTRO_DIGITAL` | `SIEMPRE` | Constancia de aviso únicamente interno. |

No existe un requisito genérico adicional. Una política vacía, una política que omite un código de su TAR, agrega otro, duplica un código, cambia clase o condición, o combina un código con otra TAR se rechaza como incompleta o inválida. El orden de entrada puede variar porque el servidor lo normaliza al orden canónico antes de validar, persistir y calcular el hash.

## 8. Versionado, release y vigencia

### 8.1 Identidad y relación

- `task_definition.id` identifica la TAR estable.
- `task_definition_version.id` identifica el snapshot operativo de la TAR.
- `evidence_policy_version.id` identifica una versión de política y pertenece a un único `task_definition.id`.
- `evidence_policy_version.task_definition_version_id` apunta a la versión TAR exacta consumida por esa política.
- `evidence_policy_version.release_id` apunta al `configuration_release` `BORRADOR` en que se prepara.
- `evidence_requirement_version` pertenece de forma inmutable a una única versión de política.
- `version_no` crece desde 1 por `task_definition.id`; no es global ni lo suministra el cliente.

Si la release contiene una versión TAR `BORRADOR` para el mismo código, la política debe apuntar a esa versión. Si no la contiene, puede apuntar únicamente a la versión TAR `VIGENTE` o `INACTIVA_NUEVAS` que ya resulte aplicable. No puede apuntar a una versión histórica distinta, inexistente o perteneciente a otra TAR.

### 8.2 Ciclo de vida

1. El `PUT` crea una versión `BORRADOR` completa con sus requisitos inmutables.
2. Una misma release admite como máximo un borrador de política por TAR.
3. La publicación existente de `configuration_release` publica los borradores incluidos en esa release.
4. La nueva versión pasa a `VIGENTE`, recibe `effective_from`, `reason`, `supersedes_id` y avanza `row_version`.
5. La anterior pasa a `SUSTITUIDA`, recibe `effective_to` igual al `effective_from` de la sucesora y conserva todos sus requisitos.
6. Sólo puede existir una versión `VIGENTE` por TAR y los intervalos publicados no pueden solaparse.

La primera publicación que incorpora políticas de evidencia debe dejar exactamente ocho políticas aplicables, una por TAR, cada una completa conforme a la sección 7. En publicaciones posteriores, las políticas no modificadas permanecen aplicables y una nueva versión sustituye sólo a la política de su TAR. Una release que dejaría una TAR sin política o vincularía una política con una versión TAR incompatible se rechaza íntegramente.

No se modifica ni reemplaza una versión publicada. Una corrección se expresa mediante otra versión en otra release `BORRADOR`.

## 9. Snapshot de obligación y transición histórica

### 9.1 Consecuencia contractual que requiere aprobación expresa

El modelo actual no puede garantizar `CP-024-N` sólo con `work_obligation.task_definition_version_id`: las políticas de evidencia pueden versionarse contra la misma versión TAR. Resolver siempre la política vigente cambiaría retroactivamente obligaciones ya creadas.

Por ello esta adenda propone agregar `work_obligation.evidence_policy_version_id` como FK nullable e inmutable. Esta modificación de `work_obligation` es necesaria y forma parte de la única migración de `HU-024`. No se materializan requisitos dentro de la obligación y no se crean entidades de evidencia aportada.

### 9.2 Captura para obligaciones nuevas

Al materializar una obligación, el servicio de `Generation`:

1. toma un único instante UTC `capturedAt` del reloj inyectable;
2. conserva la `task_definition_version_id` seleccionada por la regla de activación;
3. busca la versión de política del mismo `task_definition_id` y `task_definition_version_id` cuyo intervalo publicado contiene `capturedAt`;
4. persiste su ID en `work_obligation.evidence_policy_version_id`; y
5. inserta la auditoría de creación con ese ID en la misma transacción.

Si en `capturedAt` no existe política publicada aplicable, la FK queda `NULL`: la obligación captura expresamente ausencia de política configurada y no se vuelve a enlazar después. Una vez publicada la primera cobertura completa, toda obligación nueva de una TAR activa debe capturar una versión no nula; la prueba de integración cubre esta regla del materializador.

La política se resuelve por intervalo `[effective_from, effective_to)`, no sólo por el estado textual actual. Así, una publicación con vigencia futura no se aplica antes de su instante efectivo.

### 9.3 Obligaciones anteriores a `HU-024`

Las obligaciones ya existentes permanecen con `evidence_policy_version_id = NULL`. No se ejecuta backfill, no se infiere una política histórica inexistente y no se reescribe su auditoría. Conforme a `DEC-021`, representan una tarea sin política de evidencia configurada al momento de creación; por sí sola esa ausencia no crea un faltante.

Esta transición no concluye obligaciones ni implementa el gate de `HU-026`. Cuando se contrate `HU-026`, deberá respetar `NULL` como “sin requisitos configurados para esta obligación”, salvo que una adenda posterior autorice expresamente otra transición. No puede consultar la política vigente como sustituto del snapshot.

### 9.4 Inmutabilidad

No existe operación funcional para cambiar la FK capturada. Una guarda PostgreSQL rechaza cualquier `UPDATE` que intente cambiar `work_obligation.evidence_policy_version_id`, incluido `NULL → valor`, valor → otro valor o valor → `NULL`. Publicar una política posterior sólo inserta/sustituye versiones de política; nunca actualiza obligaciones.

## 10. Contrato API

### 10.1 Ruta y cabeceras

```text
PUT /api/v1/task-definitions/{taskCode}/evidence-policy
```

Cabeceras:

- cookie de sesión vigente y token CSRF conforme al contrato transversal;
- `Idempotency-Key`: obligatorio, UUID;
- `If-Match`: ausente al crear la primera política de la TAR; obligatorio con el ETag de la política `VIGENTE` al crear una sucesora;
- `Content-Type: application/json`.

Un `If-Match` presente cuando no existe política vigente, ausente cuando sí existe, mal formado o desactualizado no crea borrador. El ETag representa `evidence_policy_version.row_version` y se devuelve entre comillas.

Para hacer obtenible el ETag sin agregar otra ruta, el `GET /api/v1/task-definitions/{taskCode}` ya existente agrega el campo compatible `currentEvidencePolicy` con el mismo DTO de política y su `rowVersion`; el ETag de la política se forma con ese valor. No se agrega un endpoint GET de evidencia ni se expone evidencia aportada. Esta proyección es la única ampliación de lectura y requiere la misma autorización ya aprobada para consultar la definición TAR.

### 10.2 DTO exacto de solicitud

El objeto contiene exactamente dos propiedades:

```json
{
  "releaseId": "01900000-0000-7000-8000-000000000001",
  "requirements": [
    {
      "code": "FOTO_DIFERENCIA_DANO",
      "kind": "FOTOGRAFIA",
      "condition": { "code": "DIFERENCIA_O_DANO" }
    }
  ]
}
```

Cada elemento contiene exactamente `code`, `kind` y `condition`. `condition` es `null` para `SIEMPRE` o el objeto exacto de una propiedad mostrado arriba. No se aceptan `status`, `versionNo`, `isRequired`, `description`, `schema`, expresiones, campos adicionales ni valores `null` fuera de `condition`.

La solicitud debe contener el conjunto completo y exacto de la TAR de la sección 7. El orden recibido no cambia la identidad: el servidor valida y persiste el orden canónico. El hash idempotente usa `taskCode`, `releaseId`, el catálogo normalizado en orden canónico y el `If-Match` esperado.

### 10.3 DTO exacto de respuesta

Primera creación y repetición idéntica devuelven `201 Created`, la misma identidad y el mismo cuerpo semántico:

```json
{
  "data": {
    "policyVersionId": "01900000-0000-7000-8000-000000000010",
    "taskCode": "TAR-0092",
    "taskDefinitionVersionId": "01900000-0000-7000-8000-000000000020",
    "configurationReleaseId": "01900000-0000-7000-8000-000000000001",
    "versionNo": 1,
    "status": "BORRADOR",
    "effectiveFrom": null,
    "effectiveTo": null,
    "reason": null,
    "basedOnPolicyVersionId": null,
    "supersedesPolicyVersionId": null,
    "requirements": [
      {
        "code": "DOCUMENTO_RECEPCION",
        "kind": "DOCUMENTO_REFERENCIADO",
        "condition": null,
        "isRequired": true,
        "ordinal": 1
      },
      {
        "code": "F_ENT_001",
        "kind": "FORMULARIO_REFERENCIADO",
        "condition": null,
        "isRequired": true,
        "ordinal": 2
      },
      {
        "code": "FOTO_DIFERENCIA_DANO",
        "kind": "FOTOGRAFIA",
        "condition": { "code": "DIFERENCIA_O_DANO" },
        "isRequired": true,
        "ordinal": 3
      }
    ],
    "rowVersion": 1
  },
  "meta": {
    "correlationId": "01900000-0000-7000-8000-000000000099"
  }
}
```

La respuesta incluye `ETag: "1"`. Los instantes publicados usan RFC 3339 UTC. `basedOnPolicyVersionId` registra la versión vigente observada al crear el borrador; `supersedesPolicyVersionId` permanece nulo en borrador y se establece al publicar.

### 10.4 Repetición, idempotencia y concurrencia

- misma `Idempotency-Key`, mismo alcance normalizado y mismo cuerpo: recupera la misma versión y no duplica requisitos;
- misma clave con contenido, TAR, release o `If-Match` diferente: `409 IDEMPOTENCY_CONFLICT`;
- otra clave que intenta crear una segunda política para la misma TAR y release: `409 CONFIGURACION_SOLAPADA`;
- dos solicitudes sucesoras con el mismo ETag: como máximo una crea borrador; la otra recibe `412 VERSION_CONFLICT` o recupera el mismo resultado si comparte clave y contenido;
- la unicidad PostgreSQL y los bloqueos son la autoridad final; una consulta previa en memoria no basta.

### 10.5 Respuestas de error

Todos los errores usan `application/problem+json`, incluyen `status`, `code`, `title`, `instance` y `correlationId`, y no incluyen stack, SQL, rutas físicas, secretos, PII innecesaria ni el payload completo.

| HTTP | `code` | Uso exacto |
|---:|---|---|
| 400 | `SOLICITUD_INVALIDA` | JSON mal formado, propiedades desconocidas, UUID inválido o forma no exacta del DTO. |
| 400 | `IDEMPOTENCY_KEY_INVALIDA` | Cabecera ausente o no UUID. |
| 400 | `IF_MATCH_REQUERIDO` / `IF_MATCH_INVALIDO` | Falta el ETag exigido o su forma no es válida. |
| 401 | `AUTENTICACION_REQUERIDA` | No existe sesión vigente o MFA está incompleto. |
| 403 | `ACCESO_DENEGADO` | Falta permiso, rol canónico `DIRECCION`, empleo/cuenta vigentes o alcance `LOR-001`. |
| 404 | `DEFINICION_NO_MVP` | `taskCode` inexistente, mal canónico o fuera de las ocho TAR; todos convergen sin revelar otra definición. |
| 409 | `CONFIGURACION_BORRADOR_REQUERIDA` | La release no existe en el alcance autorizado o no permanece `BORRADOR`. |
| 409 | `CONFIGURACION_SOLAPADA` | Ya existe otra política de esa TAR en la misma release o la publicación dejaría vigencias incompatibles. |
| 409 | `IDEMPOTENCY_CONFLICT` | La clave ya fue usada con otro contenido normalizado. |
| 412 | `VERSION_CONFLICT` | ETag desactualizado o carrera de sucesión. |
| 422 | `POLITICA_EVIDENCIA_INVALIDA` | Política vacía/incompleta, requisito duplicado/desconocido, clase/condición incorrectas o combinación ajena a la TAR. |
| 422 | `VERSION_TAR_REQUERIDA` | La política no apunta al borrador de la misma release ni a la versión TAR aplicable permitida. |
| 500 | `ERROR_INTERNO` | Fallo no controlado, con referencia sanitizada y sin efecto parcial. |

No se usan `413`, `415` ni `503` en `HU-024` porque no existe transferencia ni escaneo de archivos.

## 11. Autorización y alcance

La autorización se evalúa en servidor antes de consultar o escribir la política solicitada. Requiere simultáneamente:

1. sesión individual vigente con MFA completo;
2. cuenta y empleo vigentes;
3. exactamente una asignación de rol canónico `DIRECCION` vigente;
4. permiso efectivo `PER-EVIDENCIA-CONFIG`;
5. sucursal exacta `LOR-001`; y
6. `taskCode` perteneciente al catálogo cerrado.

Otro rol, puesto textual parecido, permiso aislado sin rol, otra sucursal, `TODAS` usado como sucursal o TAR fuera del catálogo se deniega. La UI no concede autoridad y no existe fallback a puesto, turno, jerarquía textual ni operador de plataforma.

## 12. Auditoría y atomicidad

### 12.1 Eventos allowlist

| Acción | Resultado | Momento |
|---|---|---|
| `EVIDENCE_POLICY_DRAFT_CREATED` | `SUCCESS` | Se crea una nueva versión borrador completa. |
| `EVIDENCE_POLICY_DRAFT_RECOVERED` | `SUCCESS` | Se recupera una solicitud idempotente idéntica sin crear otra versión. |
| `EVIDENCE_POLICY_PUBLISHED` | `SUCCESS` | La publicación de la release vuelve vigente la versión y sustituye la anterior. |
| `EVIDENCE_POLICY_ACCESS_DENIED` | `DENIED` | Actor autenticado sin autorización suficiente. |
| `EVIDENCE_POLICY_REJECTED` | `REJECTED` | Validación, idempotencia, ETag, release o concurrencia impide la operación. |

La falta de sesión `401` se trata por la auditoría transversal de autenticación y no inventa un actor funcional. Un rechazo autenticado se audita con el código allowlist después de revertir cualquier intento fallido y sin escritura de política.

`before_data` y `after_data` usan esquema v1 y contienen sólo IDs técnicos necesarios, `taskCode`, versión, estado, vigencia, códigos de requisitos, clases, condiciones, predecesora y `rowVersion`. No contienen cuerpo completo, descripciones, datos de obligación, nombres de persona, cookies, credenciales, TOTP, códigos de recuperación, cadenas de conexión, binarios, URL ni metadatos de archivo.

### 12.2 Límites transaccionales

El `PUT` bloquea la release, la versión TAR objetivo y la política vigente; luego inserta `evidence_policy_version`, todas sus filas `evidence_requirement_version`, el registro de idempotencia y el evento de auditoría en una sola transacción. Si cualquier inserción o la auditoría falla, no queda política parcial.

La publicación usa la transacción existente de `configuration_release`: sustitución de la política anterior, publicación de la nueva, vigencia de la release y auditorías se confirman juntas. Si falla cualquier parte, la política anterior sigue aplicable y no aparece una segunda vigente.

La materialización de obligación inserta la obligación, su FK de política capturada y la auditoría de creación en la misma transacción. Un rechazo se registra en una transacción de auditoría sin mutación funcional; nunca se confirma un cambio de política como parte de esa transacción de rechazo.

## 13. Persistencia aprobable

### 13.1 Propiedad modular

`Configuration` es propietario de `evidence_requirement_catalog`, `evidence_policy_version` y `evidence_requirement_version`, porque `HU-024` configura requisitos. El módulo `Evidence` de historias posteriores consumirá el contrato, pero no escribirá estas tablas. `Generation` conserva la propiedad de `work_obligation` y es el único que escribe la FK capturada durante la materialización. `Configuration` nunca actualiza obligaciones.

### 13.2 `evidence_requirement_catalog`

| Columna | Tipo | Regla |
|---|---|---|
| `task_definition_id` | `uuid` | FK `RESTRICT` a una de las ocho identidades `task_definition`. |
| `requirement_code` | `varchar(64)` | Código exacto de la sección 7. |
| `kind` | `varchar(32)` | Una de las seis clases de la sección 5. |
| `condition_code` | `varchar(32)` | `SIEMPRE` o `DIFERENCIA_O_DANO`. |
| `ordinal` | `smallint` | Mayor a cero y exacto dentro de la TAR. |

PK `(task_definition_id, requirement_code)`, unicidad `(task_definition_id, ordinal)` y unicidad compuesta `(task_definition_id, requirement_code, kind, condition_code)`. La migración inserta exactamente las 27 filas de la sección 7. Una guarda DB impide `UPDATE` y `DELETE`; no existe `DbSet` o servicio funcional de mantenimiento.

### 13.3 `evidence_policy_version`

| Columna | Tipo | Regla |
|---|---|---|
| `id` | `uuid` | PK, UUID v7 generado por aplicación. |
| `task_definition_id` | `uuid` | FK `RESTRICT` a `task_definition`. |
| `task_definition_version_id` | `uuid` | FK compuesta `RESTRICT` a la versión exacta de la misma TAR. |
| `release_id` | `uuid` | FK `RESTRICT` a `configuration_release`. |
| `based_on_id` | `uuid null` | FK `RESTRICT` a la política vigente observada al crear el borrador. |
| `version_no` | `integer` | Mayor a cero y creciente por TAR. |
| `status` | `varchar(16)` | Sólo `BORRADOR`, `VIGENTE`, `SUSTITUIDA`. |
| `effective_from` / `effective_to` | `timestamptz null` | Nulos en borrador; intervalo `[from,to)` al publicar. |
| `reason` | `varchar(500) null` | Se fija por la publicación existente; motivo normalizado obligatorio. |
| `supersedes_id` | `uuid null` | FK `RESTRICT` a la predecesora inmediata al publicar. |
| `row_version` | `bigint` | Inicia en 1 y avanza sólo en transición válida. |

Restricciones e índices:

- único `(task_definition_id, version_no)`;
- único `(release_id, task_definition_id)`;
- clave candidata `(id, task_definition_id)` para FKs compuestas;
- índice parcial único por `task_definition_id` donde `status='VIGENTE'`;
- índice único de `supersedes_id` no nulo;
- exclusión temporal por `task_definition_id` para intervalos publicados no solapados;
- checks coherentes de estado, vigencia, motivo, predecesora y `row_version > 0`; y
- `ON DELETE RESTRICT` en todas las relaciones históricas.

### 13.4 `evidence_requirement_version`

| Columna | Tipo | Regla |
|---|---|---|
| `id` | `uuid` | PK, UUID v7. |
| `policy_version_id` | `uuid` | Parte de FK compuesta a la versión de política. |
| `task_definition_id` | `uuid` | Debe coincidir con la TAR de la política y del catálogo. |
| `requirement_code` | `varchar(64)` | Código estable del catálogo. |
| `kind` | `varchar(32)` | Snapshot exacto de la clase catalogada. |
| `condition_code` | `varchar(32)` | Snapshot exacto de la condición catalogada. |
| `ordinal` | `smallint` | Orden canónico catalogado. |
| `is_required` | `boolean` | Check obligatorio `is_required = true`. |

Unicidad `(policy_version_id, requirement_code)` y `(policy_version_id, ordinal)`. FK compuesta `(policy_version_id, task_definition_id)` a `evidence_policy_version`; FK compuesta `(task_definition_id, requirement_code, kind, condition_code)` al catálogo; todas `RESTRICT`. Las filas son inmutables y su ciclo de vida se hereda de la cabecera; la columna `status` conceptual reservada por F06 se precisa aquí como estado de `evidence_policy_version`, sin duplicación por requisito.

La completitud exacta de cada política se valida contra el catálogo dentro de la transacción antes de insertar y se vuelve a validar al publicar la release. PostgreSQL impide códigos desconocidos, combinación TAR/clase/condición incorrecta y duplicados; no se introduce un trigger con lógica de conteo ni un motor general de reglas.

### 13.5 Cambio de `work_obligation`

Se agrega únicamente:

```text
evidence_policy_version_id uuid null
```

La FK usa `ON DELETE RESTRICT` hacia `evidence_policy_version(id)`. Se crea índice no único para la FK y una guarda PostgreSQL simple de inmutabilidad en `UPDATE`. No se agrega `created_at`, no se copian requisitos, no se cambia `execution_status` y no se hace backfill.

## 14. Migración

La implementación autorizable genera una sola migración forward-only, propuesta como `AddEvidencePolicies`, que contiene exclusivamente:

1. las tres tablas de la sección 13;
2. las 27 filas sintéticas del catálogo cerrado;
3. la columna nullable y FK de snapshot en `work_obligation`;
4. índices, checks, exclusión temporal y guardas simples aprobadas; y
5. actualización del snapshot EF e inventarios de migraciones/tablas.

El archivo de migración y Designer usan LF y no contienen BOM. `Down()` lanza la excepción de reversión bloqueada. No se editan migraciones históricas ni los tres Designer con metadatos ajenos. No hay cascadas destructivas ni segunda migración correctiva.

Se verifica explícitamente que la migración no contenga `file_object`, `evidence_item`, `evidence_version`, `evidence_review_snapshot`, objetos S3, cuarentena, escaneo, conclusión o validación.

## 15. Pruebas requeridas

### 15.1 Dominio y contrato

- las ocho TAR aceptan y devuelven exactamente los 27 requisitos de la sección 7;
- cada código conserva clase, condición, obligatoriedad y orden canónicos;
- `TAR-0092/FOTO_DIFERENCIA_DANO` es el único requisito `DIFERENCIA_O_DANO`;
- `TAR-0018/FOTOGRAFIA_FINAL` siempre usa `SIEMPRE`;
- no existen `CUALQUIERA`, `AL_MENOS_N`, expresiones ni esquemas libres;
- vacío, omisión, adición, duplicado, código, clase, condición o TAR incorrectos se rechazan, mientras el orden de entrada se normaliza;
- `FORM-ADM-02` y `F-ENT-001` permanecen referencias lógicas; y
- no existe código, servicio o endpoint de archivo o evidencia aportada.

### 15.2 API, autorización y no-efecto

- primera creación, repetición idéntica, conflicto idempotente y ETag vigente/desactualizado;
- envoltura `{ data, meta }`, `correlationId`, ETag y `application/problem+json`;
- `401` sin sesión;
- `403` sin permiso, con rol distinto de `DIRECCION`, empleo/cuenta no vigente o alcance distinto de `LOR-001`;
- `404` uniforme para TAR inexistente o fuera del catálogo;
- `409` para release no borrador, solapamiento e idempotencia conflictiva;
- `412` sin efectos para concurrencia optimista;
- `422` para política inválida o versión TAR incompatible;
- los errores y logs no contienen secretos, PII innecesaria ni payload completo; y
- CSRF y denegación por defecto siguen los controles transversales existentes.

### 15.3 PostgreSQL real

- PK, FKs `RESTRICT`, checks, catálogo cerrado, unicidades, índice parcial y exclusión temporal;
- dos escritores concurrentes no crean dos borradores equivalentes ni dos versiones vigentes;
- publicación sustituye una sola versión y conserva la anterior;
- versión, requisitos, idempotencia y auditoría del `PUT` se confirman o revierten juntos;
- release, sustitución y auditorías se confirman o revierten juntas;
- rechazo o falla de auditoría no deja cabecera ni requisitos parciales;
- obligación nueva captura la política aplicable por intervalo y conserva su FK tras otra publicación;
- publicación futura no se aplica antes de `effective_from`;
- obligación creada sin política conserva `NULL` después de una publicación;
- obligaciones históricas permanecen `NULL` sin backfill;
- un `UPDATE` directo de la FK capturada se rechaza; y
- no se crean tablas o efectos de `HU-025`, `HU-026` o `TECH-EVID-001`.

SQLite y mocks no acreditan ninguna de estas propiedades. Las pruebas PostgreSQL se escriben durante la implementación y se ejecutan una sola vez externamente por el desarrollador al llegar al gate indicado.

### 15.4 Arquitectura

- `Configuration` posee catálogo y política;
- `Generation` captura sólo el contrato publicado y no escribe tablas de `Configuration`;
- `Evidence`, `Web` y Worker no contienen reglas de catálogo;
- dominio no depende de EF, ASP.NET Core, S3 ni proveedor;
- sólo se agrega el `PUT` aprobado y la proyección compatible `currentEvidencePolicy` en el GET ya existente; y
- no aparecen entidades, servicios, paquetes, endpoints o infraestructura fuera de alcance.

No hay pruebas de navegador porque no hay UI.

## 16. Gates de salida

Después de la aprobación e implementación, se ejecutan una sola vez y en el orden autorizado:

1. `dotnet restore SGOL.slnx --locked-mode` fuera del aislamiento;
2. `dotnet build SGOL.slnx --no-restore --configuration Release`;
3. suite unitaria completa;
4. suite de arquitectura completa;
5. pruebas enfocadas de `HU-024` sin Docker;
6. una ejecución externa de la suite PostgreSQL afectada, solicitada al desarrollador, y espera de su resultado;
7. Playwright marcado `no aplica` por ausencia de UI;
8. `dotnet format SGOL.slnx --verify-no-changes --no-restore`;
9. `./scripts/ci/Assert-NoVulnerablePackages.ps1`;
10. modelo EF sin cambios pendientes;
11. `./scripts/ci/verify-fuentes-protection.ps1`;
12. `./scripts/ci/verify-fuentes-mirror.ps1`, después y no en paralelo con el anterior; y
13. `git diff --check`.

Un gate omitido, compuesto o pendiente se informa como no verificado. No se ejecutan Docker ni Testcontainers dentro de la sesión y no se diagnostica Docker.

## 17. Trazabilidad y cierre condicional

La implementación aprobada actualizará en el mismo cambio:

- esta adenda, conservando la aprobación íntegra recibida;
- `docs/traceability/README.md`;
- `docs/traceability/IMPLEMENTATION_STATUS.md` como propuesta de `HU-024`;
- la documentación API mínima directamente afectada; y
- el inventario de migraciones/tablas.

La fila `HU-024` conserva el orden 23 del backlog. Esta adenda no inserta una tarea previa y no adelanta `TECH-EVID-001`.

En la rama del PR, `HU-024` permanece propuesta y no habilita `HU-025`. El mismo registro adquiere estado efectivo `Terminada` en `master`, sin commit administrativo posterior, sólo cuando el commit que contiene implementación y trazabilidad tenga pipeline requerido verde, aprobación humana, merge, ascendencia verificada en `origin/master`, suite PostgreSQL satisfactoria, `Fuentes/` protegida y cero defectos bloqueantes conocidos.

Commit, publicación de rama, apertura de PR y merge requieren autorizaciones explícitas e independientes.

## 18. Decisión íntegra solicitada

Aprobar esta adenda significa aprobar conjuntamente:

1. los 27 requisitos, seis clases y dos condiciones cerradas;
2. política completa obligatoria por TAR, sin modos agregadores;
3. integración con `configuration_release` y estados `BORRADOR/VIGENTE/SUSTITUIDA`;
4. una sola política vigente y vigencias no solapadas;
5. `PUT` con `Idempotency-Key` y `If-Match`, y la proyección `currentEvidencePolicy` en el GET existente para obtener el ETag;
6. autorización exclusiva de `DIRECCION` + `PER-EVIDENCIA-CONFIG` + `LOR-001`;
7. auditoría y límites transaccionales de la sección 12;
8. las tres tablas mínimas de política/catálogo, la única migración y sus restricciones;
9. la FK nullable e inmutable `work_obligation.evidence_policy_version_id`;
10. cero backfill y tratamiento de obligaciones históricas como “sin política configurada al crearse”;
11. ausencia de UI en este corte; y
12. exclusión total de `HU-025`, `HU-026`, archivos e infraestructura `TECH-EVID-001`.

Si cualquiera de estos puntos no se aprueba, la adenda debe corregirse antes de implementar. No se interpreta una aprobación parcial, una propuesta previa, memoria del chat ni un nombre tentativo como aprobación íntegra.
