# F07 Adenda 09 — Contrato de corrección de asignación para HU-019

## 1. Estado, decisión solicitada y alcance

**APROBADA íntegramente por el responsable el 2026-09-04.**

Esta adenda resuelve exclusivamente los vacíos contractuales de `HU-019 — Superior corrige asignación inferior con motivo`. Su aprobación autoriza iniciar la implementación en `codex/hu-019`, pero no autoriza migración no prevista, commit, publicación, pull request ni merge.

No modifica los contratos cerrados de `HU-004`, `HU-007`, `HU-016` o `HU-018`; no autoriza una asignación automática nueva, el recálculo automático de elegibilidad, contadores de carga, planificación, conclusión, evidencia, validación, recurrencias, UI ni endpoints adicionales.

La decisión agrupada solicitada es aprobar o rechazar íntegramente las secciones 2 a 14. Una observación que cambie una de ellas debe incorporarse a esta misma adenda antes de editar código de producción.

## 2. Endpoint, cuerpo y comando interno

La única entrada HTTP es:

`POST /api/v1/obligations/{id}/assignment-corrections`

Requiere sesión autenticada, protección CSRF cuando corresponda al cliente de navegador, `Idempotency-Key` e `If-Match`. El identificador `id` de la ruta es `obligationId` y no se repite en el cuerpo.

El cuerpo JSON contiene exactamente:

| Propiedad | Tipo | Regla |
|---|---|---|
| `newResponsiblePersonId` | `uuid` | No nulo ni vacío; identidad estable de `person`, nunca nombre, puesto o texto laboral. |
| `eligibilityEvaluationId` | `uuid` | No nulo ni vacío; evaluación confirmada de HU-016 que se pretende usar. |
| `reason` | `string` | Motivo obligatorio normalizado conforme a esta sección. |

No se admiten propiedades adicionales. `reason` se normaliza a Unicode Form C, se reemplaza cada secuencia de uno o más caracteres de espacio en blanco por un espacio ASCII y se eliminan espacios al inicio y al final. El valor normalizado debe tener entre 10 y 500 unidades UTF-16 de .NET, inclusivas. El valor normalizado es el que se valida, persiste y audita. Nulo, vacío, sólo espacios o fuera de ese intervalo devuelve `400 MOTIVO_INVALIDO` sin efectos funcionales.

El endpoint construye un único `CorrectAssignmentCommand` con exactamente:

| Campo | Tipo | Origen |
|---|---|---|
| `ActorUserId` | `uuid` | `ClaimTypes.NameIdentifier` de la sesión. |
| `IdempotencyKey` | `uuid` | Encabezado `Idempotency-Key`. |
| `CorrelationId` | `uuid` | Correlación resuelta por el middleware. |
| `ObligationId` | `uuid` | Segmento `id` de la ruta. |
| `NewResponsiblePersonId` | `uuid` | Cuerpo normalizado. |
| `EligibilityEvaluationId` | `uuid` | Cuerpo. |
| `Reason` | `string` | Motivo normalizado. |
| `ExpectedRowVersion` | `long` positivo | ETag interpretado desde `If-Match`. |

`IAssignmentCorrectionService.CorrectAsync` es el contrato interno explícito. No crea una segunda operación funcional ni permite omitir guardas del endpoint.

## 3. Identidad idempotente y respuestas

`Idempotency-Key` es obligatorio y debe ser un UUID. Ausente o inválido devuelve `400 IDEMPOTENCY_KEY_INVALIDA`.

El alcance persistido de la clave es exactamente `assignment:correction:{actorUserId:D}:{obligationId:D}`. Por tanto, la identidad comprende usuario, operación y obligación. El hash SHA-256 usa serialización canónica, en este orden:

1. `obligationId` UUID canónico `D`;
2. `newResponsiblePersonId` UUID canónico `D`;
3. `eligibilityEvaluationId` UUID canónico `D`;
4. `reason` normalizado; y
5. `expectedRowVersion` decimal invariante.

`correlationId`, encabezados de transporte y hora no forman parte del hash.

Una creación devuelve `201 Created`, `result = CREADA`, el ID de la sucesora y `Location: /api/v1/obligations/{obligationId}`. Un reintento con el mismo alcance, clave y hash devuelve `200 OK`, `result = RECUPERADA` y exactamente los mismos IDs, datos funcionales y ETag de la creación. La recuperación se decide dentro de una transacción después de bloquear la obligación y antes de volver a aplicar el ETag; por ello acepta el `If-Match` original, que ya quedó desactualizado por la primera confirmación. El encabezado sigue siendo obligatorio y debe ser sintácticamente válido.

Creación y recuperación devuelven `ETag` y este cuerpo:

```json
{
  "data": {
    "result": "CREADA",
    "assignmentId": "019...",
    "obligationId": "019...",
    "previousAssignmentId": "019...",
    "newResponsiblePersonId": "019...",
    "status": "VIGENTE",
    "assignmentType": "CORRECCION",
    "reason": "Motivo normalizado",
    "assignedBy": "019...",
    "assignedAt": "2026-09-04T18:00:00Z",
    "supersedesId": "019...",
    "rowVersion": 2
  },
  "meta": {
    "correlationId": "019..."
  }
}
```

En recuperación sólo cambia `data.result` a `RECUPERADA` y `meta.correlationId` refleja la petición actual. Reutilizar la misma clave y alcance con un hash diferente devuelve `409 IDEMPOTENCY_CONFLICT`; no sobrescribe el registro original ni modifica asignaciones.

Todos los rechazos de negocio que alcanzan el servicio con identidad válida se conservan como resultados idempotentes. Reintentar la misma clave y hash recupera el mismo rechazo; corregir datos, ETag o autoridad exige una clave nueva.

## 4. ETag y recurso mutable protegido

El ETag anterior a la corrección es el de `work_obligation.row_version`, con el formato exacto existente `"<entero-positivo>"`. Lo entrega la representación autorizada de la obligación; HU-019 no agrega otro endpoint de lectura.

`If-Match` ausente devuelve `400 IF_MATCH_REQUERIDO`; un valor sin comillas, débil, múltiple, comodín o que no sea un entero positivo devuelve `400 IF_MATCH_INVALIDO`. Después de bloquear `work_obligation`, una versión diferente de `ExpectedRowVersion` devuelve `412 VERSION_CONFLICT`, sin efecto parcial.

Una corrección confirmada incrementa exactamente en uno `work_obligation.row_version`. No cambia ningún otro campo de la obligación. La sucesora no recibe `row_version`: `assignment_version` continúa sin ese campo y no se modifica el esquema.

Una corrección concurrente con otra clave y el ETag anterior espera el bloqueo y después devuelve `412 VERSION_CONFLICT`. Sólo una petición con el mismo alcance, clave y hash de una corrección ya confirmada obtiene recuperación idempotente.

## 5. Permiso, sucursal y autoridad jerárquica

El permiso estable es `PER-ASIGNACION-CORREGIR`. En el MVP lo poseen únicamente cuentas activas de `LOR-001` con rol canónico vigente `DIRECCION`, `ADMINISTRACION` o `SUBCOORDINACION`; `PISO_VENTAS` no lo posee. El servicio aplica denegación por defecto y no confía sólo en la visibilidad del endpoint.

El nivel funcional de la obligación es `eligibility_policy_version.required_role` de la política exacta referenciada por la evaluación de elegibilidad aceptada para la corrección. La evaluación debe pertenecer a la misma obligación y su política debe corresponder a `work_obligation.task_definition_version_id`. No se deriva el nivel del responsable vigente, del puesto, del nombre de la TAR ni de texto laboral.

Después de adquirir los bloqueos organizacionales se captura un único `correctedAt` mediante `IClock.UtcNow`. En ese instante el actor debe tener simultáneamente:

- cuenta `ACTIVA` vinculada a una persona;
- empleo `ACTIVA` en `LOR-001`, con `valid_from <= correctedAt` y `valid_to` nulo o posterior;
- una sola `role_assignment_version` `ACTIVO` en `LOR-001`, con `valid_from <= correctedAt` y `valid_to` nulo o posterior; y
- uno de los tres roles que conceden `PER-ASIGNACION-CORREGIR`.

La comparación es estricta: `Rank(actorRole) > Rank(requiredRole)`, con `DIRECCION > ADMINISTRACION > SUBCOORDINACION > PISO_VENTAS`. No se reutiliza `CanAccessLevel` sin una guarda estricta adicional. Dirección puede corregir cualquier obligación cuyo rol requerido sea inferior a Dirección; ninguna persona puede corregir una obligación de su mismo nivel o superior.

Actor sin permiso, sin rol vigente, con cuenta o empleo inactivos, o fuera de `LOR-001` recibe `403 ACCESO_DENEGADO`. Un par o inferior también recibe `403 ACCESO_DENEGADO`, sin revelar detalles de la obligación.

Se permite corregir una obligación actualmente asignada a la propia persona del actor si, debido a un cambio histórico de rol, el actor cumple en `correctedAt` la superioridad estricta respecto del nivel funcional de la obligación. El nuevo responsable sólo es aceptable con el rol exacto requerido; por ello el actor superior no puede elegirse a sí mismo como nuevo responsable. Ningún puesto, nombre laboral, asignación previa o texto parecido concede permiso ni nivel.

## 6. Evaluación y revalidación de elegibilidad

La petición consume por ID la última evaluación de HU-016 ya confirmada y visible al ejecutar la comprobación para la obligación, ordenada por `evaluated_at` descendente y después por `id` descendente. Puede ser la evaluación usada por HU-018 o una reevaluación posterior ejecutada previamente como operación separada; HU-019 nunca ejecuta HU-016 de forma implícita. Una evaluación concurrente que todavía no confirmó no invalida retrospectivamente la corrección; podrá usarse únicamente en otra petición posterior.

Después de bloquear la obligación se comprueba que:

1. la evaluación existe y pertenece exactamente a `obligationId`;
2. es la última evaluación confirmada de la obligación;
3. su política corresponde a la versión TAR exacta de la obligación;
4. su resultado y cardinalidad de candidatos son compatibles; y
5. `newResponsiblePersonId` aparece en esa evaluación con `is_eligible = true`.

Además, para cumplir la revalidación de CAP-023 sin modificar el snapshot, se vuelven a comprobar en `correctedAt` las condiciones vivas de RN-011 contra la misma política exacta y la misma `eligibility_date` de la evaluación:

- empleo activo y vigente en `LOR-001`;
- cuenta activa;
- rol canónico activo, vigente y exactamente igual a `required_role`;
- versión vigente de disponibilidad positiva para `eligibility_date`; y
- turno del empleo coincidente sólo cuando `required_shift` no es nulo.

Los IDs de las versiones vivas usadas quedan referenciados en la explicación, pero no se actualizan. Si empleo, cuenta, rol, disponibilidad o turno cambió después de `evaluated_at` y ya no cumple, la corrección devuelve `422 RESPONSABLE_INELEGIBLE`. Una política publicada después no sustituye la política exacta de la obligación; se valida la política referenciada por la evaluación.

Una persona ahora elegible que no aparece como elegible en el snapshot solicitado no se admite. Requiere que HU-016 confirme previamente una reevaluación y que la corrección use el ID de esa nueva evaluación.

Los resultados exactos son:

| Condición | HTTP y código |
|---|---|
| Evaluación inexistente | `404 EVALUACION_ELEGIBILIDAD_NO_ENCONTRADA` |
| Evaluación de otra obligación o política/TAR incompatible | `422 EVALUACION_ELEGIBILIDAD_INCOMPATIBLE` |
| Evaluación que no es la última confirmada | `409 EVALUACION_ELEGIBILIDAD_DESACTUALIZADA` |
| Resultado o cardinalidad internamente incompatible | `409 EVALUACION_ELEGIBILIDAD_INCOMPATIBLE` |
| Persona ausente, excluida o que falla la revalidación viva | `422 RESPONSABLE_INELEGIBLE` |

Estos rechazos no cambian la evaluación, candidatos, persona, cuenta, empleo, rol, disponibilidad, política ni asignación.

## 7. Cadena de sustitución y efecto funcional

HU-019 puede corregir la asignación `VIGENTE` tanto si es `AUTOMATICA` como si es `CORRECCION`. Esta regla concreta el caso de “otro superior posterior puede corregir” de HU-019: DEC-036 inicia la cadena desde una asignación automática y cada corrección posterior vuelve a evaluar permiso, autoridad y elegibilidad.

Elegir a la persona que ya es responsable vigente devuelve `422 ASIGNACION_SIN_CAMBIO`; sólo el reintento de una corrección ya confirmada se recupera por idempotencia. Sí se permite volver a elegir a una persona que apareció antes en la cadena si no es la vigente y cumple nuevamente toda la elegibilidad.

La asignación anterior pasa de `VIGENTE` a `SUSTITUIDA`. La sucesora usa `VIGENTE/CORRECCION`, conserva el mismo `obligation_id` y su `supersedes_id` apunta exclusivamente a la versión inmediatamente anterior. `assigned_by`, `reason` y `supersedes_id` son no nulos. La cadena y el índice único de `supersedes_id` conservan todas las versiones y evitan bifurcaciones.

`assigned_at = correctedAt` es el único instante de corte de HU-019. DEC-018 no exige intervalos de ejecución porque RN-016 no exige inicio explícito ni medición de duración en el MVP; la cadena de `assignment_version` basta mientras la obligación permanezca `PENDIENTE`. No se agrega historial de ejecución.

Una obligación inexistente o fuera de `LOR-001` devuelve `404 OBLIGACION_NO_ENCONTRADA`; una obligación no `PENDIENTE` devuelve `422 OBLIGACION_NO_CORREGIBLE`; la ausencia de asignación vigente devuelve `409 ASIGNACION_VIGENTE_NO_ENCONTRADA`.

## 8. Esquema exacto de `assignment_version.explanation`

La sucesora conserva un objeto JSON `camelCase` no nulo con exactamente esta forma:

```json
{
  "schemaVersion": 1,
  "operation": "ASSIGNMENT_CORRECTION",
  "obligationId": "019...",
  "supersededAssignmentId": "019...",
  "previousResponsiblePersonId": "019...",
  "newResponsiblePersonId": "019...",
  "actorUserId": "019...",
  "correctedAt": "2026-09-04T18:00:00Z",
  "reasonField": "assignment_version.reason",
  "eligibilityEvaluationId": "019...",
  "eligibilityPolicyVersionId": "019...",
  "actorEmploymentVersionId": "019...",
  "actorRoleAssignmentVersionId": "019...",
  "candidateEmploymentVersionId": "019...",
  "candidateRoleAssignmentVersionId": "019...",
  "candidateAvailabilityVersionId": "019...",
  "actorRoleCode": "ADMINISTRACION",
  "targetRoleCode": "SUBCOORDINACION",
  "hierarchyResult": "AUTHORIZED_STRICTLY_SUPERIOR"
}
```

No se duplica el texto del motivo en la explicación: `reasonField` referencia la columna canónica `assignment_version.reason`. No se copian nombres, puesto, textos laborales, turno, razones de exclusión, carga, snapshot completo, contenido de política, direcciones de red ni secretos. Todos los UUID son referencias; las fuentes permanecen históricas o inmutables conforme a sus contratos.

## 9. Transacción, bloqueos y orden de escritura

La operación usa una sola transacción PostgreSQL `READ COMMITTED`. Las preconsultas sólo optimizan y nunca deciden autoridad, ETag, idempotencia, elegibilidad o unicidad.

El orden obligatorio es:

1. bloquear `work_obligation` mediante `SELECT ... FOR UPDATE`;
2. leer con bloqueo, si existe, el registro del alcance y `Idempotency-Key`; un hash idéntico terminal se recupera antes de comparar ETag;
3. comprobar `LOR-001`, estado `PENDIENTE` y `work_obligation.row_version`;
4. cargar y bloquear la única `assignment_version` `VIGENTE` mediante `SELECT ... FOR UPDATE`;
5. cargar la evaluación, candidato y política exactos y comprobar que la evaluación es la última ya confirmada y visible en ese punto; HU-016 no bloquea actualmente la obligación y una evaluación todavía no confirmada no participa;
6. resolver los IDs organizacionales y bloquear las filas `person` involucradas en orden UUID, después las filas `app_user` en orden UUID y finalmente las versiones vigentes de empleo, rol y disponibilidad en orden de tabla e ID;
7. capturar `correctedAt` una sola vez y volver a comprobar permiso, cuenta, empleo, sucursal, rol, jerarquía y elegibilidad con las filas bloqueadas;
8. validar nuevamente que la asignación bloqueada continúa `VIGENTE` y que no se eligió al responsable actual;
9. actualizar la anterior a `SUSTITUIDA` y ejecutar `SaveChanges` dentro de la transacción antes de insertar la sucesora;
10. incrementar en uno `work_obligation.row_version`;
11. insertar la sucesora, el registro idempotente y el evento de auditoría; y
12. ejecutar el guardado final y confirmar.

No se bloquean ni actualizan `eligibility_candidate`; la evaluación y sus candidatos permanecen inmutables. La política exacta se lee como versión histórica y no se modifica. Los bloqueos de `person`/`app_user` y versiones vigentes impiden confirmar usando organización que cambie durante la revalidación.

El índice parcial `UX_assignment_version_current_obligation` sigue siendo la autoridad final de una sola asignación `VIGENTE`. El índice `UX_assignment_version_supersedes` evita que dos sucesoras apunten a la misma versión.

## 10. Concurrencia y SQLSTATE

Dos correcciones concurrentes sobre la misma obligación se serializan por el bloqueo de `work_obligation`:

- mismo alcance, clave y hash: la segunda recupera la sucesora confirmada;
- clave distinta con el mismo ETag anterior: la segunda devuelve `412 VERSION_CONFLICT`;
- misma clave con contenido diferente: devuelve `409 IDEMPOTENCY_CONFLICT`.

Los SQLSTATE `40P01` y `40001` permiten como máximo dos reintentos adicionales, tres intentos totales, con la misma clave y hash. Agotarlos devuelve `409 ASSIGNMENT_CORRECTION_CONCURRENCY_CONFLICT`, sin efectos parciales.

Una violación `23505` de la PK de idempotencia se resuelve leyendo el registro confirmado y devolviendo recuperación o `IDEMPOTENCY_CONFLICT`. Una violación `23505` de `UX_assignment_version_current_obligation` o `UX_assignment_version_supersedes` vuelve a ejecutar la operación dentro del mismo máximo de intentos para producir recuperación idempotente o `412 VERSION_CONFLICT`. Otra violación `23505` devuelve `409 ASSIGNMENT_CORRECTION_CONFLICT`; no se interpreta como éxito.

Después de cada rollback/reintento se limpia el seguimiento de EF y se vuelven a comprobar ETag, asignación vigente, autoridad, estado y elegibilidad bajo los bloqueos. Nunca se confirma una sucesora sobre una versión ya `SUSTITUIDA`.

## 11. Auditoría e idempotencia de rechazos

Los eventos usan `actor_type = USER`, `actor_user_id = ActorUserId`, `correlation_id = CorrelationId` y `request_id = Idempotency-Key` en formato UUID canónico. El middleware hace que el identificador HTTP de la petición sea el mismo `correlationId`; no se persiste una tercera identidad de transporte ni se requiere un cambio de esquema.

| Resultado | `action` | `resource_type` | `outcome` |
|---|---|---|---|
| Creación | `ASSIGNMENT_CORRECTED` | `ASSIGNMENT_VERSION` | `CREADA` |
| Recuperación | `ASSIGNMENT_CORRECTION_RECOVERED` | `ASSIGNMENT_VERSION` | `RECUPERADA` |
| Rechazo | `ASSIGNMENT_CORRECTION_REJECTED` | `WORK_OBLIGATION` | Código literal del error |

En creación, `beforeData` contiene exactamente `schemaVersion`, `assignmentId`, `obligationId`, `personId`, `status` y `assignmentType` de la versión sustituida. `afterData` contiene exactamente `schemaVersion`, `assignmentId`, `obligationId`, `personId`, `status`, `assignmentType`, `supersedesId`, `eligibilityEvaluationId` y `rowVersion`. El motivo normalizado se conserva una sola vez en `audit_event.reason` además de la columna funcional requerida `assignment_version.reason`.

En recuperación, `beforeData = null`; `afterData` contiene exactamente `schemaVersion`, `obligationId`, `assignmentId`, `result = RECUPERADA` y `rowVersion`; `reason = null`.

En autorización denegada, candidato inelegible, versión desactualizada, evaluación incompatible o conflicto, `beforeData = null`; `afterData` contiene exactamente `schemaVersion`, `obligationId` y `errorCode`; `reason = null`. No se registran el motivo no confirmado, nombres, puesto, texto laboral, snapshot, razones detalladas de inelegibilidad, contenido de política, cookies, IP sin hash ni secretos.

Creación, sustitución anterior, incremento de versión, idempotencia y auditoría comparten la misma transacción PostgreSQL. Una falla de auditoría o idempotencia revierte todo. Un rechazo con clave todavía libre confirma conjuntamente su resultado idempotente y auditoría sin cambiar asignaciones. Un conflicto por reutilización de clave conserva sólo su auditoría y nunca sobrescribe el registro original. Una recuperación conserva una auditoría nueva y no crea otro registro idempotente.

Solicitudes no autenticadas o rechazadas por formato antes de obtener `ActorUserId`, `Idempotency-Key`, cuerpo y ETag válidos no crean idempotencia funcional; se conservan únicamente en telemetría HTTP segura.

## 12. Códigos HTTP completos

Todos los errores usan `application/problem+json` y devuelven `correlationId`.

| Condición | HTTP | `code` |
|---|---:|---|
| Sin sesión | 401 | `AUTENTICACION_REQUERIDA` |
| Actor inválido, sin permiso, inactivo, fuera de sucursal, par o inferior | 403 | `ACCESO_DENEGADO` |
| Clave ausente o no UUID | 400 | `IDEMPOTENCY_KEY_INVALIDA` |
| `If-Match` ausente | 400 | `IF_MATCH_REQUERIDO` |
| `If-Match` inválido | 400 | `IF_MATCH_INVALIDO` |
| JSON mal formado, propiedades adicionales o UUID de cuerpo ausente/inválido | 400 | `SOLICITUD_CORRECCION_INVALIDA` |
| Motivo nulo, vacío, sólo espacios o fuera de longitud | 400 | `MOTIVO_INVALIDO` |
| Obligación inexistente o fuera de alcance | 404 | `OBLIGACION_NO_ENCONTRADA` |
| Evaluación inexistente | 404 | `EVALUACION_ELEGIBILIDAD_NO_ENCONTRADA` |
| ETag desactualizado | 412 | `VERSION_CONFLICT` |
| Clave reutilizada con otro contenido | 409 | `IDEMPOTENCY_CONFLICT` |
| Sin asignación vigente | 409 | `ASIGNACION_VIGENTE_NO_ENCONTRADA` |
| Evaluación desactualizada o incompatible internamente | 409 | `EVALUACION_ELEGIBILIDAD_DESACTUALIZADA` o `EVALUACION_ELEGIBILIDAD_INCOMPATIBLE` |
| Concurrencia agotada o integridad conflictiva | 409 | `ASSIGNMENT_CORRECTION_CONCURRENCY_CONFLICT` o `ASSIGNMENT_CORRECTION_CONFLICT` |
| Obligación concluida | 422 | `OBLIGACION_NO_CORREGIBLE` |
| Evaluación de otra obligación/política | 422 | `EVALUACION_ELEGIBILIDAD_INCOMPATIBLE` |
| Responsable igual al vigente | 422 | `ASIGNACION_SIN_CAMBIO` |
| Responsable ausente, excluido o ya no elegible | 422 | `RESPONSABLE_INELEGIBLE` |

Un error deliberadamente oculto no revela si falló existencia, sucursal o jerarquía.

## 13. Integridad, no efectos y migración

La obligación conserva su ID, solicitud, versión TAR, período, origen, payload, vencimiento y estado `PENDIENTE`; sólo avanza `row_version`. La carga continúa derivándose de la única asignación `VIGENTE`, por lo que refleja al nuevo responsable sin persistir ni modificar contadores.

HU-019 no modifica evaluación, candidatos, persona, cuenta, empleo, rol, disponibilidad, política ni snapshots. No crea asignaciones `AUTOMATICA`, solicitudes de generación, obligaciones, planes, versiones o asociaciones de plan, evidencias, validaciones, recurrencias ni hechos de ejecución.

Se reutilizan `AssignmentVersion`, `AuditTransaction`, `IClock`, `VersionEtag`, la infraestructura de idempotencia y las restricciones de `AddAssignmentVersions`. No se prevé migración. Una necesidad real de esquema obliga a detener la implementación y obtener otra decisión expresa; no autoriza agregar `row_version` a `assignment_version` ni relajar restricciones existentes.

## 14. Pruebas, gates y eficacia

La implementación posterior debe cubrir todas las pruebas positivas, negativas, idempotentes, de ETag, jerarquía, revalidación, cadena, explicación, auditoría, rollback, no efecto y contrato HTTP indicadas en la instrucción de HU-019. Las pruebas de persistencia, índices, bloqueos y concurrencia usan PostgreSQL real y se ejecutan externamente conforme al entorno conocido.

La aprobación de esta adenda no declara HU-019 terminada. La implementación, trazabilidad y gates deben viajar en el mismo cambio; commit, publicación, PR y merge requieren autorizaciones independientes. HU-019 sólo queda `Terminada` después del pipeline requerido verde sobre el commit exacto, aprobación humana, merge y ascendencia verificada en `origin/master`.
