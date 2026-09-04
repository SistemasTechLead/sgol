# F07 Adenda 11 — Contrato de publicación incremental de plan para HU-021

## 1. Estado, decisión solicitada y alcance

**APROBADA íntegramente por el responsable el 2026-09-04.**

Esta adenda resuelve exclusivamente los vacíos y contradicciones contractuales de `HU-021 — Superior publica su alcance incrementalmente`. Su aprobación íntegra autoriza iniciar la implementación en `codex/hu-021`, pero no autoriza commit, publicación de rama, pull request ni merge.

No modifica los contratos cerrados de `HU-007`, `HU-010`, `HU-015`, `HU-019`, `HU-020`, `HU-034 mínimo` o `TECH-AUD-001`. No autoriza `GET /plans/{planId}/versions`, recurrencias, generación, asignación, corrección, conclusión, evidencia, validación, indicadores, UI ni endpoints adicionales.

La decisión agrupada solicitada es aprobar o rechazar íntegramente las secciones 2 a 15. Una observación que cambie cualquiera de ellas debe incorporarse a esta misma adenda antes de editar código de producción o generar la migración.

## 2. Estados del plan y de las publicaciones

`work_plan` conserva exclusivamente los estados aprobados por F05 y F06:

- la primera publicación efectiva de cualquier alcance cambia `BORRADOR` a `PUBLICADO`;
- toda publicación efectiva posterior conserva `PUBLICADO`;
- `work_plan` nunca vuelve a `BORRADOR`; y
- HU-021 no agrega `EN_REVISION`, `CERRADO` ni un estado por nivel al plan.

`plan_version` usa exclusivamente:

- `VIGENTE`: última publicación confirmada para el par `(plan_id, scope_role)`; y
- `SUSTITUIDA`: publicación anterior del mismo par, conservada como historia.

Existe como máximo una versión `VIGENTE` por plan y alcance. Una publicación incremental efectiva cambia la anterior del mismo alcance a `SUSTITUIDA` y crea su sucesora `VIGENTE`. Una publicación de otro alcance no sustituye esa versión.

Los estados `EN_REVISION → PUBLICADO → CERRADO` y el estado de partida `PUBLICADA` de CAP-025 son terminología legacy de la fuente de consolidación. En el MVP:

- `EN_REVISION` se concreta mediante `work_plan.status = BORRADOR` sin persistir otro literal;
- `CERRADO` pertenece al cierre/reapertura formal de período, expresamente fuera del MVP; y
- que una obligación fue publicada se representa por su fila histórica en `plan_version_obligation`, no mediante un estado nuevo en `work_obligation`.

## 3. Permiso, actor y alcance jerárquico

El endpoint exige `PER-PLAN-PUBLICAR`. En el MVP ese permiso se deriva exclusivamente de una cuenta activa de `LOR-001` con empleo activo y vigente y exactamente un rol canónico activo y vigente:

| Rol canónico del actor | Posee permiso | Alcance publicado por la operación |
|---|---:|---|
| `DIRECCION` | Sí | `DIRECCION`, `ADMINISTRACION`, `SUBCOORDINACION` y `PISO_VENTAS` |
| `ADMINISTRACION` | Sí | `ADMINISTRACION`, `SUBCOORDINACION` y `PISO_VENTAS` |
| `SUBCOORDINACION` | Sí | `SUBCOORDINACION` y `PISO_VENTAS` |
| `PISO_VENTAS` | No | Ninguno |

Una operación publica conjuntamente el nivel propio del actor y todos sus niveles inferiores. El cliente no elige un nivel. `scope_role` identifica el nivel superior incluido en ese alcance acumulado y es exactamente el rol canónico vigente del actor al confirmar: `DIRECCION`, `ADMINISTRACION` o `SUBCOORDINACION`.

Dirección puede publicar todos los niveles en una operación; Administración nunca incluye Dirección; Subcoordinación nunca incluye Administración o Dirección; Piso nunca publica. El nivel funcional de una obligación procede de la `eligibility_policy_version.required_role` exacta vinculada a su asignación, no del puesto, turno, texto laboral, nombre de área, pertenencia al plan ni una propiedad enviada por el cliente.

Cuenta inactiva, empleo inactivo/no vigente, actor fuera de `LOR-001`, ausencia de rol vigente, más de un rol vigente, rol desconocido o `PISO_VENTAS` reciben `403 ACCESO_DENEGADO`. Un `work_plan` inexistente o que no pertenezca a `LOR-001` recibe `404 PLAN_NO_ENCONTRADO`, sin revelar otra sucursal.

Si el rol del actor cambia entre publicaciones, cada petición usa exclusivamente el rol vigente revalidado dentro de su transacción. La publicación histórica conserva su `scope_role` y `published_by`; el cambio de rol no reescribe ni sustituye publicaciones de otro alcance. El actor sólo puede sustituir posteriormente la versión vigente correspondiente a su nuevo alcance.

## 4. Ruta, cuerpo, encabezados y comando

La única entrada HTTP es:

`POST /api/v1/plans/{planId}/publications`

Requiere sesión autenticada, protección CSRF cuando corresponda al cliente de navegador, un único `Idempotency-Key` UUID y un único `If-Match` fuerte.

La petición admite únicamente:

- ausencia total de cuerpo; o
- objeto JSON vacío `{}` con `Content-Type: application/json`.

Ambas formas son equivalentes. `null`, arreglo, escalar, JSON mal formado o cualquier propiedad —incluidas `scopeRole` u `obligationIds`— devuelve `400 SOLICITUD_PUBLICACION_INVALIDA`. No existe selección explícita de obligaciones ni de alcance.

`planId` debe ser un UUID no vacío. El endpoint construye `PublishWorkPlanCommand` con exactamente:

| Campo | Tipo | Origen |
|---|---|---|
| `ActorUserId` | `uuid` | `ClaimTypes.NameIdentifier` de la sesión |
| `IdempotencyKey` | `uuid` | `Idempotency-Key` |
| `CorrelationId` | `uuid` | correlación resuelta por middleware |
| `PlanId` | `uuid` | ruta |
| `ExpectedRowVersion` | entero positivo | ETag fuerte interpretado desde `If-Match` |

El comando no contiene sucursal, período, rol, obligación, asignación, estado o instante. Sucursal, período, rol y contenido se resuelven y bloquean en persistencia. `publishedAt` se captura una sola vez con `IClock.UtcNow` dentro del intento que confirma.

`If-Match` ausente devuelve `400 IF_MATCH_REQUERIDO`. Un valor débil, sin comillas, múltiple, comodín, no entero o no positivo devuelve `400 IF_MATCH_INVALIDO`. El formato válido es exactamente el producido por `VersionEtag.Format`: `"<rowVersion>"`.

## 5. Respuesta exitosa exacta

Una publicación efectiva devuelve `201 Created`, sin `Location`, y `ETag` con el `work_plan.row_version` posterior. El cuerpo contiene exactamente:

```json
{
  "data": {
    "result": "PUBLICADA_INICIAL",
    "planId": "019...",
    "planStatus": "PUBLICADO",
    "publicationId": "019...",
    "versionNo": 1,
    "versionStatus": "VIGENTE",
    "scopeRole": "SUBCOORDINACION",
    "publishedBy": "019...",
    "publishedAt": "2026-09-04T18:00:00Z",
    "obligations": [
      {
        "obligationId": "019...",
        "assignmentVersionId": "019..."
      }
    ],
    "addedObligations": [
      {
        "obligationId": "019...",
        "assignmentVersionId": "019..."
      }
    ],
    "rowVersion": 2
  },
  "meta": {
    "correlationId": "019..."
  }
}
```

`result` es `PUBLICADA_INICIAL` cuando no existía versión anterior para ese `scope_role`, aunque otro alcance ya hubiera publicado el plan. Es `PUBLICADA_INCREMENTAL` cuando sustituye una versión del mismo alcance. `obligations` es el snapshot acumulado completo; `addedObligations` contiene sólo las filas nuevas respecto de la versión sustituida. Ambas listas se ordenan por `obligationId` UUID canónico y no contienen nombres, puestos, textos laborales ni payloads.

Un reintento idempotente exitoso devuelve `200 OK`, `result = RECUPERADA`, los mismos `publicationId`, `versionNo`, `publishedBy`, `publishedAt`, snapshot, adiciones, datos funcionales y ETag de la respuesta original. `meta.correlationId` corresponde a la petición actual.

No existe una respuesta exitosa sin una publicación efectiva. Los alcances vacíos o sin novedades usan los resultados funcionales de la sección 11 y no incrementan versión ni ETag.

## 6. Selección de la publicación inicial

La primera versión de un alcance contiene exactamente todas las obligaciones del mismo `branch_id` y `period_id` del plan que, al instante único `publishedAt`, cumplen simultáneamente:

1. su `execution_status` es `PENDIENTE`;
2. existe una única política de elegibilidad exacta para su `task_definition_version_id` y su `required_role` está incluido en el alcance acumulado del actor;
3. existe exactamente una `assignment_version` `VIGENTE` para la obligación;
4. la asignación referencia una política de elegibilidad coherente con la obligación y con el mismo `required_role`;
5. la persona asignada tiene cuenta, empleo y exactamente un rol canónico activos y vigentes en `LOR-001`; y
6. ese rol vigente coincide exactamente con `required_role`.

La referencia de política aplicable se toma del `eligibilityPolicyVersionId` estructurado en `assignment_version.explanation`, aprobado por HU-018/HU-019, y se contrasta con la política ligada a `work_obligation.task_definition_version_id`. La asignación se captura mediante su ID inmutable en `plan_version_obligation`.

Una obligación `CONCLUIDA` que nunca fue publicada no entra en una publicación nueva. Una obligación fuera del alcance se ignora sin revelar su identidad. Una obligación `PENDIENTE` dentro del alcance sin asignación vigente provoca `422 OBLIGACION_SIN_ASIGNACION`; no se omite silenciosamente ni se inventa una política. Una política ausente, múltiple o incompatible, o una asignación incoherente, provoca `409 CONTENIDO_PLAN_INCOMPATIBLE`. Una asignación cuyo responsable dejó de ser publicable provoca `422 ASIGNACION_NO_PUBLICABLE`.

La publicación es atómica: una sola obligación no publicable dentro del alcance impide crear la versión completa. Si no existe ninguna obligación `PENDIENTE` aplicable al alcance, devuelve `422 SIN_OBLIGACIONES_PUBLICABLES`. No se crea una versión vacía y el plan permanece `BORRADOR` si ningún otro alcance lo había publicado.

## 7. Publicación incremental y obligaciones tardías

Una obligación es tardía para un alcance cuando pertenece al mismo plan, cumple por primera vez las reglas de la sección 6 y su `obligation_id` no aparece en el snapshot de la versión vigente de ese `scope_role`.

La sucesora es un snapshot completo acumulado:

- copia literalmente cada par `(obligation_id, assignment_version_id)` de la versión sustituida; y
- agrega cada obligación tardía con la `assignment_version` vigente bloqueada en ese instante.

Una obligación ya publicada puede aparecer en versiones históricas sucesivas del mismo alcance porque cada versión es un snapshot, pero aparece una sola vez dentro de cada versión. La versión anterior queda `SUSTITUIDA` y no se modifica su contenido.

Una obligación previamente publicada se conserva en la sucesora aunque después esté `CONCLUIDA`. Su asignación congelada tampoco se reemplaza por una corrección posterior. Corregir una asignación no crea ni exige por sí solo otra publicación en HU-021; una publicación incremental futura arrastra el par histórico anterior y sólo congela asignaciones para obligaciones nuevas. Esta separación evita que HU-021 ejecute implícitamente HU-019 o altere historia ya publicada.

Una obligación tardía ya `CONCLUIDA` no entra. Si existe versión vigente pero no hay al menos una obligación tardía publicable, devuelve `422 SIN_NOVEDADES_PUBLICABLES`; no crea versión, no sustituye historia y no incrementa `work_plan.row_version`.

La numeración es global por plan, no independiente por alcance. Publicaciones de alcances acumulados distintos pueden contener la misma obligación: cada una representa una decisión de publicación diferente. Esto no duplica `work_plan`, `work_obligation` ni `assignment_version`. La restricción de contenido se aplica por versión, no globalmente a toda la historia.

## 8. Identidad y restricciones físicas

`plan_version.id` es la identidad UUID v7 inmutable de una publicación. La clave funcional de secuencia es `(plan_id, version_no)` y `version_no` empieza en `1`, creciendo de uno en uno globalmente para el plan bajo bloqueo de `work_plan`.

`plan_version` contiene:

| Columna | Regla |
|---|---|
| `id` | PK UUID, sin generación de base |
| `plan_id` | FK `work_plan(id)` `RESTRICT` |
| `version_no` | entero positivo; único por plan |
| `status` | `VIGENTE/SUSTITUIDA` |
| `scope_role` | `DIRECCION/ADMINISTRACION/SUBCOORDINACION` |
| `published_by` | FK `app_user(id)` `RESTRICT` |
| `published_at` | `timestamptz` UTC |
| `supersedes_id` | FK autorreferente `RESTRICT`, nula sólo en la primera versión del alcance |
| `plan_row_version` | `bigint >= 2`; ETag posterior capturado para replay exacto |

Las restricciones PostgreSQL incluyen:

- única `(plan_id, version_no)`;
- única `(plan_id, plan_row_version)`;
- índice único parcial `(plan_id, scope_role) WHERE status = 'VIGENTE'`;
- `supersedes_id` único cuando no es nulo;
- FK compuesta que obliga a que `supersedes_id` pertenezca al mismo `plan_id` y `scope_role`; y
- check que impide autosustitución.

`plan_version_obligation` contiene sólo `plan_version_id`, `obligation_id` y `assignment_version_id`. Su PK es `(plan_version_id, obligation_id)`. Las tres FKs usan `RESTRICT`.

Para impedir físicamente emparejar una obligación con la asignación de otra, la migración agrega la clave alternativa única `(assignment_version.id, assignment_version.obligation_id)` y `plan_version_obligation(assignment_version_id, obligation_id)` la referencia mediante FK compuesta. No se agrega columna ni se modifica una fila existente de `assignment_version`.

La misma obligación y la misma asignación pueden reaparecer en snapshots históricos o alcances superpuestos. No existe una unicidad global que destruya esa historia. La pertenencia de obligación y plan a la misma sucursal/período se revalida bajo bloqueos dentro de la transacción, conforme a F06 para restricciones que cruzan agregados.

## 9. Idempotencia

`Idempotency-Key` debe contener un único UUID no vacío aceptable por `Guid.TryParse`; ausente, múltiple o inválido devuelve `400 IDEMPOTENCY_KEY_INVALIDA`.

El alcance persistido es exactamente `planning:plan-publication:{actorUserId:D}:{planId:D}`. Comprende actor, operación y recurso. El hash SHA-256 usa UTF-8 y este orden canónico:

1. literal `WORK_PLAN_PUBLICATION`;
2. `planId` UUID canónico `D`;
3. `ExpectedRowVersion` decimal invariante; y
4. literal `{}` como cuerpo normalizado.

Ausencia de cuerpo y `{}` producen el mismo hash. `correlationId`, hora, rol resuelto, estado actual, obligaciones y asignaciones no forman parte del hash.

Los casos exactos son:

- misma clave, alcance y hash con publicación confirmada: recupera la misma identidad y respuesta original;
- misma clave y hash con rechazo terminal: recupera el mismo rechazo;
- misma clave con otro plan, ETag o cuerpo: `409 IDEMPOTENCY_CONFLICT`, sin sobrescribir el registro original;
- claves distintas con el mismo ETag concurrente: sólo una puede publicar; la otra obtiene `412 VERSION_CONFLICT`;
- clave distinta con ETag actual pero sin novedades: `422 SIN_NOVEDADES_PUBLICABLES`.

Una recuperación idempotente se decide después de revalidar la seguridad actual y antes de volver a aplicar el ETag funcional. Por ello acepta el `If-Match` original, ya desactualizado por la primera confirmación, y devuelve el ETag original conservado en `plan_version.plan_row_version`. Si la cuenta o el rol dejaron de ser válidos, la seguridad actual prevalece y no se revela el resultado anterior.

Los rechazos funcionales alcanzados con sesión identificable, ruta, clave, ETag y cuerpo sintácticamente válidos se conservan como `idempotency_record.status = COMPLETED`, incluidos acceso denegado, plan oculto/inexistente, conflicto de versión, contenido incompatible, obligación/asignación no publicable, falta de contenido y concurrencia agotada. Reintentar el mismo hash recupera el rechazo; corregir la causa exige una clave nueva.

Un éxito usa `resource_type = PLAN_VERSION` y `resource_id = publicationId`. Un rechazo posterior a resolver el plan usa `resource_type = WORK_PLAN` y `resource_id = planId`; antes de resolverlo usa `resource_type = BRANCH` y `resource_id = BranchScope.LorettaId`. `response_code` conserva el HTTP original, `created_at = publishedAt` y `expires_at = DateTimeOffset.MaxValue` durante el MVP.

Falta de sesión, actor UUID inválido, `planId` inválido, clave/ETag inválidos o JSON inválido no crea idempotencia funcional. Sólo produce telemetría HTTP segura. Un conflicto de reutilización se audita en una transacción separada y nunca reemplaza el registro original.

## 10. ETag, transacción y concurrencia

Para una clave nueva, después de bloquear `work_plan`, `ExpectedRowVersion` debe coincidir con `work_plan.row_version`; de lo contrario devuelve `412 VERSION_CONFLICT` sin efecto sobre plan o publicaciones, salvo su idempotencia y auditoría de rechazo confirmadas juntas.

Cada publicación efectiva incrementa `work_plan.row_version` exactamente en uno. Una recuperación idempotente, un rechazo y una operación sin novedades no lo incrementan. La primera publicación de un plan recién asegurado cambia `row_version` de `1` a `2`.

Cada intento usa una sola transacción PostgreSQL `READ COMMITTED` mediante `AuditTransaction`. El orden obligatorio es:

1. consultar y bloquear, si existe, `idempotency_record` de `(scope, key)`;
2. bloquear `app_user`, empleo vigente y roles vigentes del actor, en ese orden, y revalidar autoridad;
3. bloquear la fila canónica `branch` de `LOR-001`;
4. bloquear `work_plan` por `planId`, ocultar otra sucursal y validar ETag/estado para una clave nueva;
5. bloquear la versión `VIGENTE` del mismo `(plan_id, scope_role)`, si existe;
6. bloquear obligaciones del plan en orden de UUID;
7. bloquear políticas, asignaciones y estado vigente de responsables en orden estable de UUID;
8. construir el snapshot y volver a validar todas las relaciones;
9. sustituir la versión anterior cuando corresponda e insertar la nueva versión y su contenido;
10. cambiar/conservar `work_plan.status`, incrementar una sola vez su `row_version` e insertar idempotencia y auditoría; y
11. guardar y confirmar.

El bloqueo de `work_plan` serializa la numeración global y publicaciones de alcances iguales o distintos. Dos alcances distintos con el mismo ETag no publican ambos: uno confirma y el otro recibe `412`. Con ETags sucesivos, ambos pueden confirmar y cada alcance conserva como máximo una versión vigente.

Una misma clave concurrente converge en un único `publicationId` por la PK de idempotencia y recuperación posterior. `23505` de la PK idempotente se reintenta para leer el resultado confirmado. `23505` de la versión global, vigencia parcial, sucesión o contenido se reintenta para producir recuperación, `412` o conflicto contractual; otra `23505` devuelve `409 PLAN_PUBLICATION_CONFLICT`.

Los SQLSTATE `40P01` y `40001` permiten como máximo dos reintentos adicionales, tres intentos totales, con el mismo alcance, clave y hash. Agotarlos devuelve `409 PLAN_PUBLICATION_CONCURRENCY_CONFLICT`, sin publicación parcial. Después de rollback se limpia el seguimiento de EF y se repite el orden completo.

PostgreSQL —restricciones, bloqueos e idempotencia persistida— es la autoridad final. Ninguna preconsulta en memoria decide unicidad, siguiente versión, vigencia o recuperación.

## 11. Resultados de error exactos

| Condición | HTTP | Código |
|---|---:|---|
| Sesión ausente | 401 | `AUTENTICACION_REQUERIDA` |
| Actor sin autoridad actual o Piso | 403 | `ACCESO_DENEGADO` |
| `planId`, ruta o cuerpo inválido | 400 | `SOLICITUD_PUBLICACION_INVALIDA` |
| `Idempotency-Key` ausente/múltiple/inválido | 400 | `IDEMPOTENCY_KEY_INVALIDA` |
| `If-Match` ausente | 400 | `IF_MATCH_REQUERIDO` |
| `If-Match` inválido | 400 | `IF_MATCH_INVALIDO` |
| Plan inexistente o de otra sucursal | 404 | `PLAN_NO_ENCONTRADO` |
| Misma clave con otro hash | 409 | `IDEMPOTENCY_CONFLICT` |
| Plan en estado persistido incompatible | 409 | `ESTADO_PLAN_INCOMPATIBLE` |
| Política/contenido relacional incoherente | 409 | `CONTENIDO_PLAN_INCOMPATIBLE` |
| ETag desactualizado | 412 | `VERSION_CONFLICT` |
| Obligación aplicable sin asignación vigente | 422 | `OBLIGACION_SIN_ASIGNACION` |
| Responsable/asignación dejó de ser publicable | 422 | `ASIGNACION_NO_PUBLICABLE` |
| Primera publicación sin obligaciones aplicables | 422 | `SIN_OBLIGACIONES_PUBLICABLES` |
| Incremento sin obligaciones nuevas aplicables | 422 | `SIN_NOVEDADES_PUBLICABLES` |
| Reintentos de concurrencia agotados | 409 | `PLAN_PUBLICATION_CONCURRENCY_CONFLICT` |
| Otra violación de integridad de publicación | 409 | `PLAN_PUBLICATION_CONFLICT` |

Los errores usan `application/problem+json`, incluyen `code` y el `correlationId` actual, y no revelan identidades fuera del alcance.

## 12. Auditoría

Todos los eventos funcionales usan:

- `actor_type = USER`;
- `actor_user_id = ActorUserId`;
- `branch_id = BranchScope.LorettaId` cuando se resolvió el alcance;
- `correlation_id = CorrelationId` actual;
- `request_id = Idempotency-Key` UUID canónico `D`;
- `resource_type = WORK_PLAN`; y
- `resource_id = planId` sólo después de resolverlo sin filtración.

Los literales exactos son:

| Resultado | `action` | `outcome` |
|---|---|---|
| Publicación inicial efectiva | `WORK_PLAN_PUBLISHED` | `PUBLICADA_INICIAL` |
| Publicación incremental efectiva | `WORK_PLAN_PUBLISHED` | `PUBLICADA_INCREMENTAL` |
| Reintento exitoso recuperado | `WORK_PLAN_PUBLICATION_RECOVERED` | `RECUPERADA` |
| Rechazo, incluida falta de novedades | `WORK_PLAN_PUBLICATION_REJECTED` | código literal del error |

Para una publicación efectiva, `beforeData` contiene exactamente `schemaVersion`, `planId`, `planStatus`, `rowVersion`, `scopeRole`, `publicationId`, `versionNo` y `versionStatus` anteriores; los cuatro últimos valores de publicación son nulos en la primera versión del alcance. `afterData` contiene exactamente `schemaVersion`, `planId`, `planStatus`, `rowVersion`, `publicationId`, `versionNo`, `versionStatus`, `scopeRole`, `publishedBy`, `publishedAt`, `obligationCount` y `addedObligations`, donde cada adición contiene sólo `obligationId` y `assignmentVersionId`.

Para recuperación, `beforeData = null`; `afterData` contiene exactamente `schemaVersion`, `planId`, `publicationId`, `versionNo`, `scopeRole`, `planRowVersion` y `result = RECUPERADA`.

Para rechazo, `beforeData = null`; `afterData` contiene exactamente `schemaVersion`, `planId` cuando sea visible, `scopeRole` cuando haya sido autorizado, `expectedRowVersion` cuando sea sintácticamente válido y `errorCode`. No se incluyen obligaciones causantes en errores de acceso o recurso oculto.

No se auditan nombres, puestos, turnos, payloads de obligación, explicaciones de elegibilidad/asignación, cookies, secretos, cadenas de conexión ni contenido del hash idempotente. Los IDs mínimos de obligación/asignación sólo aparecen en `addedObligations` de una publicación autorizada y confirmada.

Plan, versión, snapshot, idempotencia y auditoría se confirman o revierten juntos. Una falla de idempotencia o auditoría revierte el cambio de estado/ETag y toda fila nueva. Cada recuperación agrega una auditoría nueva sin crear versión. El conflicto idempotente conserva sólo su auditoría separada y no altera el resultado original.

## 13. Migración única

HU-021 genera como máximo una migración definitiva `AddPlanPublications`. Crea únicamente:

- `plan_version`;
- `plan_version_obligation`;
- sus PK, checks, índices y FKs aprobados en la sección 8; y
- la clave alternativa compuesta sobre `assignment_version` imprescindible para la FK de integridad obligación–asignación.

No agrega columnas a `work_plan`: usa `status` y `row_version` existentes. No agrega ni modifica columnas o filas de `work_obligation`, `assignment_version`, rol, empleo, período o idempotencia. No crea tablas de ejecución, evidencia, validación, cierre, cola, recurrencia o UI.

Todas las FKs usan `ON DELETE RESTRICT`. La migración usa LF, no contiene BOM y su `Down()` lanza la excepción de reversión bloqueada del repositorio. Se actualizan el snapshot EF y los inventarios de migraciones/tablas. No se genera una segunda migración correctiva.

## 14. Pruebas y gates posteriores a la aprobación

La implementación cubrirá las pruebas solicitadas en la instrucción de HU-021 y, de forma expresa:

- matriz completa Dirección/Administración/Subcoordinación/Piso y autoridad sólo por rol vigente;
- cuerpo vacío, encabezados, contrato HTTP, ETag y códigos de la sección 11;
- V1 acumulada, V2 con adiciones, V1 histórica y misma identidad de plan;
- obligación tardía, concluida no publicada, concluida ya congelada, sin asignación, asignación incoherente y fuera de alcance;
- cambio de rol del actor y del responsable entre publicaciones;
- snapshot que conserva la asignación publicada aunque exista una corrección posterior;
- reintento con identidad/ETag originales, conflicto de hash y rechazo terminal recuperado;
- incremento único de `row_version` y ausencia de incremento en replay/rechazo;
- concurrencia con misma clave, claves distintas y alcances distintos;
- rollback por falla de auditoría o idempotencia;
- PK, checks, índices parciales, FKs `RESTRICT` y FK compuesta obligación–asignación;
- ausencia de modificaciones en obligaciones/asignaciones y de tablas fuera de alcance; y
- modelo EF sin cambios pendientes.

Las pruebas de persistencia, restricciones y concurrencia usan PostgreSQL real. Se escriben, pero su suite Testcontainers completa se solicita al desarrollador una sola vez en el gate externo. Los gates finales se ejecutan una sola vez y en el orden indicado por la instrucción de HU-021.

## 15. No efectos, eficacia y autorizaciones independientes

La escritura pertenece al módulo `Planning`. Las lecturas de identidad, organización, obligación, política y asignación se realizan mediante contratos existentes o consultas internas acotadas, sin escribir tablas de otros módulos.

HU-021 no crea otro `work_plan`, no cambia período, obligación, estado de ejecución, asignación, elegibilidad, generación ni contenido de una publicación histórica. No genera, asigna, corrige, concluye, carga evidencia, valida, ejecuta recurrencias ni expone UI/GET adicional.

La aprobación íntegra de esta adenda es la única decisión que permite comenzar código de producción y la migración de HU-021. No declara la historia terminada y no autoriza commit, publicación, PR o merge. Implementación y trazabilidad deben viajar en el mismo cambio. HU-021 sólo queda `Terminada` después de pipeline requerido verde sobre el commit exacto, aprobación humana, merge y ascendencia verificada en `origin/master`.
