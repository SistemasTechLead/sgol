# SGOL — Adenda 24 a F07: contrato de política de validación versionada para `HU-027`

## 1. Control del documento

| Campo | Valor |
|---|---|
| Tipo | Adenda normativa a `F07_BACKLOG_DE_IMPLEMENTACION.md` |
| Estado | `APROBADA ÍNTEGRAMENTE` |
| Fecha | 2026-09-10 |
| Historia | `HU-027 — Dirección versiona validación por TAR` |
| Corte y épica | `CV-04` / `EP-08` |
| Efecto | Precisa el contrato ejecutable de `HU-027` sin alterar su orden ni habilitar `HU-028` |
| Precedencia | `TECH-E2E-CV-03` y `CV-03` reconocidos como cerrados conforme a la evidencia primaria aportada: PR `#46`, commit `157bc43886b58a2e1a0c036dbda437114cf9f283`, pipeline `SUCCESS` run `34514719087`, aprobación humana, merge `437f1d875491097d208ca7fc3ad11d4094b26b81` y ascendencia verificada en `origin/master` |
| Conservación | Mantiene sin cambios los documentos F00–F07 aprobados y `Fuentes/` |
| Aprobación | Aprobada íntegramente por el responsable el 2026-09-10 antes de iniciar la implementación |

La aprobación autoriza la implementación local y la migración descritas. No autoriza commit, publicación de rama, pull request ni merge, que permanecen como decisiones independientes.

## 2. Hechos aprobados que no se reabren

1. Sólo una sesión vigente de rol canónico `DIRECCION`, permiso `PER-VALIDACION-CONFIG` y alcance `LOR-001` configura la política.
2. El único endpoint nuevo es `PUT /api/v1/task-definitions/{taskCode}/validation-policy`.
3. Sólo se admiten `TAR-0005`, `TAR-0007`, `TAR-0008`, `TAR-0011`, `TAR-0018`, `TAR-0026`, `TAR-0092` y `TAR-0093`.
4. Todas las TAR requieren validación separada mediante `SUPERIOR_INMEDIATO`.
5. Los únicos resultados permitidos son `CUMPLIDA`, `INCOMPLETA` y `NO_CUMPLIDA`.
6. La autoridad se expresa por roles canónicos, nunca por puesto textual, persona, par, rol inferior, turno o semejanza de nombre.
7. Una versión futura sustituye a la anterior sin modificar obligaciones ya creadas.
8. `validation_policy_version` pertenece a `Configuration` y referencia una versión exacta de definición de tarea.
9. Historia, denegación por defecto, auditoría transaccional, ETag, idempotencia e inmutabilidad histórica son obligatorios.
10. `HU-027` no crea `validation_requirement`, no emite ni sustituye `validation_decision_version` y no decide los casos funcionales reservados a `HU-028`.

La autoridad ordinaria cerrada es:

| `taskCode` | `executorRole` | `validatorRelation` | `validatorRole` |
|---|---|---|---|
| `TAR-0005` | `SUBCOORDINACION` | `SUPERIOR_INMEDIATO` | `ADMINISTRACION` |
| `TAR-0007` | `PISO_VENTAS` | `SUPERIOR_INMEDIATO` | `SUBCOORDINACION` |
| `TAR-0008` | `SUBCOORDINACION` | `SUPERIOR_INMEDIATO` | `ADMINISTRACION` |
| `TAR-0011` | `SUBCOORDINACION` | `SUPERIOR_INMEDIATO` | `ADMINISTRACION` |
| `TAR-0018` | `PISO_VENTAS` | `SUPERIOR_INMEDIATO` | `SUBCOORDINACION` |
| `TAR-0026` | `ADMINISTRACION` | `SUPERIOR_INMEDIATO` | `DIRECCION` |
| `TAR-0092` | `SUBCOORDINACION` | `SUPERIOR_INMEDIATO` | `ADMINISTRACION` |
| `TAR-0093` | `SUBCOORDINACION` | `SUPERIOR_INMEDIATO` | `ADMINISTRACION` |

## 3. Carencias contractuales comprobadas

### 3.1 Hechos documentales

F05 y F06 fijan el objetivo, el endpoint, el permiso, el versionado, la relación ordinaria, los tres resultados, la tabla conceptual y las garantías transversales de HTTP, concurrencia y auditoría. No fijan de manera exacta:

- el cuerpo y la respuesta del `PUT`;
- cómo se obtiene y cuándo se exige `If-Match`;
- el efecto de repetir una configuración semánticamente equivalente;
- cómo entra la política en `configuration_release` y cuándo se vuelve aplicable;
- cómo se vincula sin ambigüedad con `task_definition_version` y con obligaciones nuevas;
- cómo se consulta el histórico;
- los códigos de error específicos;
- las restricciones, índices y guardas de PostgreSQL; ni
- los nombres y contenidos permitidos de los eventos de auditoría.

### 3.2 Inferencias insuficientes para implementar

Las implementaciones existentes de elegibilidad, activación y evidencia demuestran un patrón técnico reutilizable, pero no autorizan por sí solas a trasladar a validación su DTO, snapshot, catálogo de errores o eventos. La fila conceptual de F06 tampoco decide si una obligación conserva sólo su `task_definition_version_id` o una referencia directa a la política exacta.

### 3.3 Propuesta

Las secciones 4 a 13 cierran exclusivamente esas carencias. No cambian la matriz funcional ni anticipan el acto de validar.

## 4. Alcance cerrado

### 4.1 Incluido

- contrato de dominio y aplicación de la política de validación;
- catálogo cerrado de ocho combinaciones de autoridad;
- el único `PUT` aprobado;
- proyecciones de política vigente e histórica en el `GET /api/v1/task-definitions/{taskCode}` existente;
- integración con `configuration_release`;
- persistencia de `validation_policy_version` y snapshot nullable en obligaciones nuevas;
- autorización, CSRF transversal, ETag, idempotencia, concurrencia, auditoría y pruebas; y
- trazabilidad mínima de `HU-027`.

### 4.2 Excluido

- `HU-028`, `HU-029`, `HU-031` y cualquier historia posterior;
- `validation_requirement` y `validation_decision_version`;
- emisión, sustitución, escalamiento, autovalidación, fundamento o evaluación de una decisión;
- bandeja de validaciones, supervisión, indicadores, avisos o mensajería;
- UI, navegador, Worker, scheduler, outbox, broker, Redis, microservicio o integración externa; y
- cambios en `Fuentes/`.

No se añade UI: ninguna fuente aprobada define una ruta o composición visual para esta historia.

## 5. DTO exacto del `PUT`

### 5.1 Cabeceras

- `Idempotency-Key` es obligatoria y contiene un UUID.
- `If-Match` se omite al crear la primera política de una TAR.
- Si existe una política `VIGENTE`, `If-Match` es obligatorio y contiene exactamente su ETag `"<rowVersion>"`.
- Un `If-Match` válido enviado cuando aún no existe política vigente devuelve `412 VERSION_CONFLICT`.
- La respuesta exitosa devuelve el ETag de la versión borrador creada o recuperada.

### 5.2 Cuerpo

El cuerpo contiene exactamente estas seis propiedades:

```json
{
  "releaseId": "01900000-0000-7000-8000-000000000001",
  "isRequired": true,
  "executorRole": "SUBCOORDINACION",
  "validatorRelation": "SUPERIOR_INMEDIATO",
  "validatorRole": "ADMINISTRACION",
  "allowedResults": ["CUMPLIDA", "INCOMPLETA", "NO_CUMPLIDA"]
}
```

La combinación debe coincidir exactamente con la fila de la TAR en la sección 2. `isRequired` sólo admite `true`. `allowedResults` se interpreta como conjunto, pero el servidor lo normaliza y persiste en el orden canónico mostrado. Se rechazan omisiones, duplicados, resultados adicionales, propiedades desconocidas, valores nulos, rol o relación no canónicos y cualquier texto de puesto o persona.

### 5.3 Respuesta

La creación inicial y el replay idempotente devuelven `201 Created`, la misma identidad y el mismo cuerpo semántico:

```json
{
  "data": {
    "policyVersionId": "01900000-0000-7000-8000-000000000010",
    "taskCode": "TAR-0005",
    "taskDefinitionVersionId": "01900000-0000-7000-8000-000000000020",
    "configurationReleaseId": "01900000-0000-7000-8000-000000000001",
    "versionNo": 1,
    "isRequired": true,
    "executorRole": "SUBCOORDINACION",
    "validatorRelation": "SUPERIOR_INMEDIATO",
    "validatorRole": "ADMINISTRACION",
    "allowedResults": ["CUMPLIDA", "INCOMPLETA", "NO_CUMPLIDA"],
    "status": "BORRADOR",
    "effectiveFrom": null,
    "effectiveTo": null,
    "reason": null,
    "basedOnPolicyVersionId": null,
    "supersedesPolicyVersionId": null,
    "rowVersion": 1
  },
  "meta": {
    "correlationId": "01900000-0000-7000-8000-000000000099"
  }
}
```

La cabecera es `ETag: "1"`. Los instantes publicados usan RFC 3339 UTC.

## 6. Idempotencia, equivalencia y concurrencia

El hash normalizado usa actor, operación, `taskCode`, `releaseId`, los cuatro valores escalares de política, el conjunto canónico de resultados y el `If-Match` esperado.

- misma `Idempotency-Key` y mismo contenido normalizado, incluso si cambia el orden de `allowedResults`: recupera la misma versión, devuelve `201` y no duplica auditoría de creación;
- misma clave con otro contenido, TAR, release o `If-Match`: `409 IDEMPOTENCY_CONFLICT`;
- otra clave para una configuración equivalente o distinta de la misma TAR y release: `409 CONFIGURACION_SOLAPADA`;
- dos sucesoras con el mismo ETag: como máximo una crea borrador; la otra recupera sólo si comparte clave y contenido, o devuelve `412 VERSION_CONFLICT`; y
- la restricción PostgreSQL y el bloqueo transaccional son la autoridad final.

No existe idempotencia natural entre claves distintas y no se actualiza un borrador existente: cada `PUT` exitoso crea o recupera una versión inmutable.

## 7. Versión TAR, publicación y vigencia

1. La política pertenece a la identidad estable `task_definition` de la TAR y apunta a una única `task_definition_version` exacta.
2. Si la release contiene una versión TAR `BORRADOR` del mismo código, la política debe apuntar a ella; si no, apunta únicamente a la versión TAR `VIGENTE` o `INACTIVA_NUEVAS` aplicable.
3. No puede apuntar a una versión histórica distinta, inexistente o de otra TAR.
4. El `PUT` crea una versión `BORRADOR`; una release admite como máximo una política por TAR.
5. La publicación existente de `configuration_release` publica las políticas incluidas. La nueva pasa a `VIGENTE`, recibe `effective_from`, `reason` y `supersedes_id`; la anterior pasa a `SUSTITUIDA` con `effective_to` igual al inicio de la sucesora.
6. La primera publicación que incorpora políticas de validación debe dejar exactamente ocho políticas aplicables y completas. Una publicación posterior arrastra sin reescritura las políticas no modificadas.
7. Sólo existe una política `VIGENTE` por TAR y sus intervalos publicados no se solapan.
8. `versionNo` comienza en 1 y crece por identidad `task_definition`; el cliente no lo suministra.

La publicación conserva el endpoint, DTO, motivo, idempotencia y semántica temporal ya aprobados para `configuration_release`; esta adenda sólo agrega la cobertura atómica de políticas de validación.

## 8. Snapshot de obligación e historia

`work_obligation` agrega `validation_policy_version_id uuid null`, FK `RESTRICT` a la política exacta. Al crear una obligación nueva:

- se resuelve la política publicada aplicable al instante de materialización y compatible con su `task_definition_version_id`;
- la obligación y la FK se insertan en la misma transacción;
- la FK queda inmutable; y
- una publicación posterior no la cambia.

No existe backfill. Las obligaciones creadas antes de la primera política aplicable conservan `NULL`. `HU-027` no crea requisitos o decisiones para ellas. `HU-028` deberá resolver expresamente, antes de implementarse, si y cómo materializa un requisito para una obligación histórica o ya concluida sin reescribir esta FK, `execution_result` ni el snapshot de conclusión.

El `GET /api/v1/task-definitions/{taskCode}` existente agrega:

- `currentValidationPolicy`: versión `VIGENTE` completa o `null`; y
- `validationPolicyHistory`: versiones publicadas `VIGENTE` y `SUSTITUIDA`, ordenadas por `versionNo` descendente.

Cada elemento usa exactamente el objeto `data` de la sección 5.3, sin envoltura interna. Los borradores se conocen por la respuesta del `PUT` y por la release administrativa existente; no se mezclan en el histórico publicado. No se crea otro endpoint de consulta.

## 9. Errores exactos

Todos usan `application/problem+json` e incluyen `status`, `code`, `title`, `instance` y `correlationId`.

| HTTP | `code` | Uso |
|---:|---|---|
| 400 | `SOLICITUD_INVALIDA` | JSON mal formado, forma distinta, propiedad desconocida o UUID inválido. |
| 400 | `IDEMPOTENCY_KEY_INVALIDA` | Cabecera ausente o no UUID. |
| 400 | `IF_MATCH_REQUERIDO` / `IF_MATCH_INVALIDO` | Falta el ETag exigido o su forma no es válida. |
| 401 | `AUTENTICACION_REQUERIDA` | No existe sesión vigente o MFA está incompleto. |
| 403 | `ACCESO_DENEGADO` | Falta rol, permiso, cuenta/empleo vigente o alcance exacto. |
| 404 | `DEFINICION_NO_MVP` | Código inexistente, mal canónico o fuera de las ocho TAR. |
| 409 | `CONFIGURACION_BORRADOR_REQUERIDA` | La release no existe en el alcance o no permanece `BORRADOR`. |
| 409 | `CONFIGURACION_SOLAPADA` | Ya existe una política de esa TAR en la release o la publicación solaparía vigencias. |
| 409 | `IDEMPOTENCY_CONFLICT` | La clave ya se usó con otro contenido normalizado. |
| 412 | `VERSION_CONFLICT` | ETag desactualizado, inesperado en la primera versión o carrera de sucesión. |
| 422 | `POLITICA_VALIDACION_INVALIDA` | Combinación incompleta o distinta del catálogo cerrado. |
| 422 | `VERSION_TAR_REQUERIDA` | La versión TAR no es la de la release ni la aplicable permitida. |
| 500 | `ERROR_INTERNO` | Fallo no controlado, sin efecto parcial ni detalle sensible. |

Los rechazos autenticados demuestran no-efecto. Un recurso fuera de catálogo converge en `404`; no se revela si existe otra definición.

## 10. Autorización

Antes de consultar o escribir se exigen simultáneamente sesión individual y MFA completos, cuenta y empleo vigentes, una sola asignación vigente de rol `DIRECCION`, permiso `PER-VALIDACION-CONFIG`, alcance `LOR-001` y TAR del catálogo cerrado. Permiso aislado, puesto textual, otro rol, otra sucursal, `TODAS` como sucursal o jerarquía inferida se deniegan. La UI nunca concede autoridad.

## 11. Auditoría y atomicidad

| Acción | Resultado | Momento |
|---|---|---|
| `VALIDATION_POLICY_DRAFT_CREATED` | `SUCCESS` | Se crea el borrador completo. |
| `VALIDATION_POLICY_DRAFT_RECOVERED` | `SUCCESS` | Se recupera el replay idéntico sin segunda política. |
| `VALIDATION_POLICY_PUBLISHED` | `SUCCESS` | La release vuelve vigente la nueva versión y sustituye la anterior. |
| `VALIDATION_POLICY_ACCESS_DENIED` | `DENIED` | Actor autenticado carece de autoridad. |
| `VALIDATION_POLICY_REJECTED` | `REJECTED` | Validación, release, ETag, idempotencia o concurrencia rechaza la operación. |

La ausencia de sesión usa la auditoría transversal de autenticación. `before_data` y `after_data` esquema v1 incluyen sólo IDs técnicos, `taskCode`, los roles y relación canónicos, resultados permitidos, estado, vigencia, predecesora y `rowVersion`; excluyen nombres de persona, puestos textuales, contenido de obligaciones, evidencia, secretos y payload HTTP completo.

El `PUT` bloquea release, versión TAR objetivo y política vigente; política, idempotencia y auditoría se confirman en una sola transacción. La publicación confirma release, transición de políticas y auditorías conjuntamente. Si falla cualquier escritura o auditoría, no queda política parcial, segunda vigente ni modificación de obligación. Un rechazo se audita sin mutación funcional.

## 12. Persistencia y migración

`validation_policy_version` contiene:

| Columna | Regla |
|---|---|
| `id uuid` | PK, UUID v7 de aplicación. |
| `task_definition_id uuid` | FK `RESTRICT` a una de las ocho identidades. |
| `task_definition_version_id uuid` | FK compuesta `RESTRICT` a la versión exacta de la misma TAR. |
| `release_id uuid` | FK `RESTRICT` a `configuration_release`. |
| `based_on_id uuid null` | FK `RESTRICT` a la vigente observada al crear el borrador. |
| `version_no integer` | Mayor a cero y creciente por TAR. |
| `is_required boolean` | Check `is_required = true`. |
| `executor_role varchar(32)` | Rol canónico exacto de la sección 2. |
| `validator_relation varchar(32)` | Check `SUPERIOR_INMEDIATO`. |
| `validator_role varchar(32)` | Superior inmediato exacto de la sección 2. |
| `allowed_results jsonb` | Array canónico exacto con los tres resultados, sin duplicados. |
| `status varchar(16)` | `BORRADOR`, `VIGENTE` o `SUSTITUIDA`. |
| `effective_from`, `effective_to` | `timestamptz null`, coherentes con el estado. |
| `reason varchar(500) null` | Nulo en borrador; motivo normalizado de publicación. |
| `supersedes_id uuid null` | FK `RESTRICT` a la predecesora inmediata. |
| `row_version bigint` | Mayor a cero; inicia en 1. |

Restricciones mínimas:

- únicos `(task_definition_id, version_no)` y `(release_id, task_definition_id)`;
- clave candidata `(id, task_definition_id)` y FKs compuestas que impiden cruzar TAR;
- índice parcial único por TAR donde `status='VIGENTE'`;
- `supersedes_id` no nulo único y de la misma TAR;
- exclusión temporal por TAR para intervalos publicados no solapados;
- check allowlist que fija las ocho combinaciones completas de roles;
- check estructural y de contenido exacto de `allowed_results`;
- checks coherentes de estado, vigencia, motivo y predecesora;
- `ON DELETE RESTRICT` en todas las relaciones; y
- guarda PostgreSQL que impide cambiar los campos semánticos y bloquea toda mutación de una versión `SUSTITUIDA`; sólo permite las transiciones de ciclo de vida aprobadas.

`work_obligation.validation_policy_version_id` tiene índice, FK `RESTRICT` y guarda de inmutabilidad. La compatibilidad entre obligación, versión TAR y política se vuelve a comprobar dentro de la transacción de materialización.

Se genera una sola migración forward-only, propuesta como `AddValidationPolicies`. Incluye tabla, columna snapshot, índices, checks, exclusión y guardas; no hace backfill ni crea tablas de `HU-028`. El archivo y Designer usan LF y no contienen BOM; `Down()` lanza la excepción de reversión bloqueada; no se edita una migración histórica.

## 13. Pruebas, gates y cierre

Las pruebas deben demostrar:

- las ocho combinaciones exactas y los tres resultados canónicos;
- sólo Dirección autorizada; `401`, `403` y `404` convergente;
- rechazo sin efecto de puesto textual, persona, par, inferior, relación distinta, resultado desconocido, duplicado o conjunto incompleto;
- primera versión, sucesión, replay equivalente, conflicto de clave, solapamiento y carrera ETag;
- publicación futura sin efecto anticipado, historia consultable e inmutable y una vigente por TAR;
- obligación nueva captura la versión exacta; obligaciones anteriores y ya creadas no cambian ni reciben backfill;
- auditoría y escritura atómicas, incluido fallo de auditoría; y
- límites arquitectónicos y ausencia total de `validation_requirement`, decisión, UI o capacidad posterior.

Se escriben pruebas PostgreSQL reales, pero no se ejecutan dentro de la sesión. Tras la implementación aprobada se ejecutarán primero filtros enfocados y, una sola vez al final, restore locked fuera del aislamiento, build Release, suite completa, format, arquitectura/contrato/seguridad/migración, modelo EF sin cambios pendientes, vulnerabilidades NuGet, protección y espejo de `Fuentes/` y `git diff --check`. Después se solicitará una única corrida PostgreSQL externa consolidada.

La implementación actualizará `docs/traceability/IMPLEMENTATION_STATUS.md` en el mismo cambio como `Propuesta implementada`, nunca como `Terminada`. `HU-027` sólo será `Terminada` tras commit exacto, pipeline requerido exitoso, aprobación humana, merge, ascendencia en `origin/master`, PostgreSQL externo satisfactorio, protección de `Fuentes/` y cero defectos bloqueantes. Nada de ello habilita por sí solo una acción Git posterior.

## 14. Preguntas y decisión íntegra solicitada

Se solicita aprobar o rechazar conjuntamente estas decisiones:

1. DTO exacto de seis propiedades y respuesta de la sección 5.
2. Primera versión sin `If-Match`, sucesoras con ETag obligatorio y replay normalizado de la sección 6.
3. Integración con `configuration_release` y cobertura de ocho políticas de la sección 7.
4. FK snapshot nullable e inmutable en obligaciones nuevas, sin backfill, de la sección 8.
5. Histórico publicado dentro del `GET /api/v1/task-definitions/{taskCode}` existente, sin endpoint adicional.
6. Catálogo de errores de la sección 9.
7. Eventos allowlist y límites transaccionales de la sección 11.
8. Tabla, restricciones, migración única y guardas de la sección 12.
9. Ausencia de UI y exclusión total de `HU-028` y posteriores.

La pregunta de aprobación es: **¿se aprueba íntegramente la Adenda 24, sin cambios ni aprobación parcial, para autorizar después la implementación local de `HU-027`?**

Si cualquier punto requiere cambio, la adenda debe corregirse y volver a aprobarse íntegramente antes de producir código.
