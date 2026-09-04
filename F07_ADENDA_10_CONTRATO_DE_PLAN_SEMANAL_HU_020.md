# F07 Adenda 10 — Contrato de plan semanal único para HU-020

## 1. Estado, decisión solicitada y alcance

**APROBADA íntegramente por el responsable el 2026-09-04.**

Esta adenda resuelve exclusivamente los vacíos y la contradicción contractual de `HU-020 — SGOL crea/recupera plan semanal único`. Su aprobación íntegra autoriza iniciar la implementación en `codex/hu-020`, pero no autoriza commit, publicación, pull request ni merge.

No modifica los contratos cerrados de `HU-007`, `HU-010`, `HU-015`, `HU-019`, `HU-034 mínimo` o `TECH-AUD-001`. No autoriza `GET /plans/{isoYear}/{isoWeek}`, publicación, versiones de plan, recurrencias, generación, asignación, corrección, conclusión, evidencia, validación, indicadores, UI ni endpoints adicionales.

La decisión agrupada solicitada es aprobar o rechazar íntegramente las secciones 2 a 14. Una observación que cambie cualquiera de ellas debe incorporarse a esta misma adenda antes de editar código de producción o generar la migración.

## 2. Permiso, actor y operaciones autorizadas

La contradicción entre F05 y F06 se resuelve a favor del permiso funcional específico de la historia:

- `POST /api/v1/plans/{isoYear}/{isoWeek}/ensure` exige `PER-PLAN-VER`, como establece la fila `HU-020 / CAP-024` de F05;
- `PER-PLAN-PUBLICAR` queda reservado a `POST /plans/{planId}/publications` de HU-021;
- esta separación también concreta CAP-024, que exige que el permiso de preparación sea distinto del permiso de publicación.

En el MVP poseen `PER-PLAN-VER` las cuentas activas de `LOR-001` con un único rol canónico vigente `DIRECCION`, `ADMINISTRACION`, `SUBCOORDINACION` o `PISO_VENTAS`. Esta regla conserva el contrato ya implementado por HU-010. El endpoint devuelve sólo metadatos del plan, no la lista de obligaciones ni información de otros niveles; la lectura del contenido con alcance autorizado queda en `GET /plans/{isoYear}/{isoWeek}`, fuera de HU-020.

La operación actúa como comando de un usuario autenticado, nunca como actor sistema. El contrato interno explícito es `IWorkPlanService.EnsureAsync(EnsureWorkPlanCommand, CancellationToken)`. No existe un segundo comando interno para el sistema y HU-020 no autoriza que jobs o módulos externos omitan las mismas guardas.

En el único instante `ensuredAt` capturado conforme a la sección 7, el actor debe tener simultáneamente:

- `app_user.status = ACTIVA`;
- empleo `ACTIVA` y vigente en `LOR-001`;
- exactamente una `role_assignment_version` `ACTIVO` y vigente en `LOR-001`; y
- uno de los cuatro roles canónicos anteriores.

Actor sin permiso, sin rol vigente, con más de un rol vigente, con cuenta o empleo inactivos, o fuera de `LOR-001` recibe `403 ACCESO_DENEGADO`. Puesto, turno, nombre laboral, texto parecido, asignación de una obligación o visibilidad de UI nunca conceden permiso.

`GET /api/v1/plans/{isoYear}/{isoWeek}` no forma parte de HU-020. Que F06 lo enumere no autoriza implementarlo en esta historia.

## 3. Ruta, cuerpo, sucursal y comando

La única entrada HTTP es:

`POST /api/v1/plans/{isoYear}/{isoWeek}/ensure`

Requiere sesión autenticada, protección CSRF cuando corresponda al cliente de navegador e `Idempotency-Key`. No usa `If-Match`.

La petición admite cualquiera de estas dos representaciones equivalentes:

- ausencia total de cuerpo; o
- objeto JSON vacío `{}` con `Content-Type: application/json`.

No se admite ninguna propiedad. Un JSON mal formado, `null`, arreglo, escalar u objeto con propiedades devuelve `400 SOLICITUD_PLAN_INVALIDA`. La ausencia de cuerpo y `{}` se normalizan a la misma entrada canónica.

`branchId` no proviene del cliente. El endpoint fija `BranchScope.LorettaId` y el servicio comprueba la fila canónica `LOR-001`. HU-020 no admite otra sucursal ni crea una sucursal. La ruta contiene únicamente el año y la semana porque el MVP tiene un solo alcance de sucursal.

El endpoint construye `EnsureWorkPlanCommand` con exactamente:

| Campo | Tipo | Origen |
|---|---|---|
| `ActorUserId` | `uuid` | `ClaimTypes.NameIdentifier` de la sesión. |
| `IdempotencyKey` | `uuid` | Encabezado `Idempotency-Key`. |
| `CorrelationId` | `uuid` | Correlación resuelta por el middleware. |
| `BranchId` | `uuid` | Constante `BranchScope.LorettaId`. |
| `IsoYear` | entero | Segmento `isoYear` de la ruta. |
| `IsoWeek` | entero | Segmento `isoWeek` de la ruta. |

No se agregan área, rol, nivel, obligación, asignación, estado o fecha al comando.

## 4. Semana y período materializado

`isoYear` admite `1..9999`. `isoWeek` admite `1..ISOWeek.GetWeeksInYear(isoYear)`. Los segmentos deben ser enteros decimales sin signo ni espacios y la combinación debe ser calculable por `WeekContract.Calculate`.

HU-020 requiere que exista previamente el `week_period` de HU-010 para `(BranchScope.LorettaId, isoYear, isoWeek)`. Una semana calculable pero todavía no materializada no crea el período de manera implícita. El cliente autorizado debe obtener o materializar antes la semana mediante la operación aprobada de HU-010.

Los resultados exactos son:

| Condición | HTTP y código |
|---|---|
| Segmento no entero o fuera del formato decimal | `400 SOLICITUD_PLAN_INVALIDA` |
| Año/semana fuera del rango ISO | `422 SEMANA_ISO_INVALIDA` |
| `week_period` inexistente en `LOR-001` | `404 PERIODO_NO_ENCONTRADO` |
| Período cuya sucursal, año, semana o fechas lunes-domingo no coinciden | `409 PERIODO_INCOMPATIBLE` |

Un período de otra sucursal nunca se usa ni se crea. Como la sucursal no es entrada del cliente, la coincidencia de año/semana en otra sucursal no cambia el resultado `404 PERIODO_NO_ENCONTRADO` de `LOR-001`.

La clave funcional exacta del plan es `(work_plan.branch_id, work_plan.period_id)`. `period_id` referencia el período materializado y `branch_id` debe coincidir con el del período.

## 5. Identidad, estado y respuesta HTTP

Una identidad nueva de `work_plan` se crea con:

- `id`: UUID v7 generado una sola vez para el intento que confirma;
- `branch_id = BranchScope.LorettaId`;
- `period_id`: ID del `week_period` bloqueado;
- `status = BORRADOR`; y
- `row_version = 1`.

`work_plan` no recibe `created_at`: F06 no lo define y HU-020 no inventa ese campo. El instante funcional `ensuredAt` se captura una sola vez mediante `IClock.UtcNow` y se registra en `idempotency_record.created_at` y `audit_event.occurred_at` de la petición que confirma.

Una creación devuelve `201 Created` y `data.result = CREADA`. Una recuperación por la misma clave o por unicidad funcional devuelve `200 OK` y `data.result = RECUPERADA`. No se emite `Location`, porque HU-020 no implementa un endpoint de lectura del recurso.

El cuerpo exitoso contiene exactamente:

```json
{
  "data": {
    "result": "CREADA",
    "planId": "019...",
    "branchId": "019...",
    "branchCode": "LOR-001",
    "periodId": "019...",
    "isoYear": 2026,
    "isoWeek": 36,
    "status": "BORRADOR",
    "rowVersion": 1
  },
  "meta": {
    "correlationId": "019..."
  }
}
```

La recuperación devuelve la identidad única y el estado/ETag vigentes al momento de la lectura bloqueada. En el alcance de HU-020 ambos permanecen `BORRADOR` y `"1"`. Una futura publicación de HU-021 podrá cambiar estado y versión; no creará otra identidad.

Si ya existe un plan —incluido un futuro plan `PUBLICADO`— `ensure` nunca lo devuelve a `BORRADOR`, no incrementa su versión y no modifica ningún campo.

## 6. Contenido exacto del BORRADOR

HU-020 no persiste membresía de obligaciones. El contenido lógico vigente de un plan `BORRADOR` se deriva como el conjunto de todas las filas `work_obligation` que cumplen simultáneamente:

```text
work_obligation.branch_id = work_plan.branch_id
AND work_obligation.period_id = work_plan.period_id
```

No se agrega filtro por rol, nivel, área, TAR, `execution_status`, existencia de asignación o identidad del responsable. Por tanto:

- entran obligaciones `PENDIENTE` y `CONCLUIDA`;
- una obligación sin asignación también pertenece al contenido lógico;
- obligaciones de políticas o responsables de dos niveles distintos pertenecen al mismo plan cuando comparten sucursal y período;
- no existe una identidad de plan por rol, nivel o área; y
- una semana sin filas coincidentes conserva válidamente un plan vacío.

No se referencia ninguna `assignment_version` en HU-020. La asignación que deba congelar una publicación se decidirá y referenciará en `plan_version_obligation` bajo HU-021.

El contenido del BORRADOR es dinámico: una obligación confirmada después del primer `ensure`, con la misma sucursal y período, aparece en la derivación posterior del mismo plan sin modificar `work_plan`. No se captura snapshot, no se incrementa `row_version` y no se escribe auditoría de plan por esa aparición derivada.

El endpoint `ensure` no devuelve la lista ni el conteo de obligaciones. La prueba de CA-020 se realiza sobre la regla de derivación del servicio/persistencia y demuestra que obligaciones sintéticas de dos niveles resuelven el mismo `planId`. La exposición HTTP del contenido y su filtrado jerárquico pertenecen al `GET` fuera de alcance.

No se crea una tabla de partidas en borrador. Tampoco se crean `plan_version`, `plan_version_obligation`, una publicación V1 ni un snapshot implícito. El término “agrupar” de HU-020 queda satisfecho por la pertenencia derivada al único agregado identificado por `(branch_id, period_id)`.

## 7. Idempotencia

`Idempotency-Key` es obligatorio y debe contener un único UUID canónico aceptable por `Guid.TryParse`; ausente, múltiple o inválido devuelve `400 IDEMPOTENCY_KEY_INVALIDA`.

El alcance persistido es exactamente `planning:work-plan:ensure:{actorUserId:D}:{branchId:D}`. Comprende actor, operación y sucursal. El hash SHA-256 usa serialización canónica UTF-8, en este orden:

1. literal de operación `WORK_PLAN_ENSURE`;
2. `branchId` UUID canónico `D`;
3. `isoYear` decimal invariante; y
4. `isoWeek` decimal invariante.

La ausencia de cuerpo y `{}` producen el mismo hash. `correlationId`, encabezados de transporte, estado derivado del período, hora y obligaciones no forman parte del hash.

Los casos son:

- mismo alcance, clave y hash: recupera el resultado terminal, nunca crea otro plan ni otro registro idempotente;
- mismo alcance y clave con otro año o semana: `409 IDEMPOTENCY_CONFLICT`;
- clave distinta para la misma semana: la unicidad funcional recupera el mismo `planId` y confirma un registro idempotente propio con respuesta `200`;
- claves distintas concurrentes: ambas convergen en el mismo `planId` por bloqueo y por `UX_work_plan_branch_period`.

Los rechazos de negocio alcanzados con actor existente, clave válida y ruta entera se conservan como resultados idempotentes. Esto incluye cuenta/empleo/rol no autorizados, semana ISO inválida, período inexistente o incompatible y concurrencia agotada. Reintentar la misma clave y hash recupera el mismo rechazo; corregir autoridad, materializar el período o cambiar semana exige una clave nueva.

Para un rechazo sin plan, `idempotency_record.resource_type = BRANCH` y `resource_id = BranchScope.LorettaId`. Para creación o recuperación, usa `resource_type = WORK_PLAN` y el `planId` único. `status = COMPLETED`; `response_code` conserva el HTTP original; `created_at = ensuredAt`; `expires_at = DateTimeOffset.MaxValue` durante el MVP.

Una petición sin sesión, con `ActorUserId` inexistente, clave inválida, segmentos no enteros o JSON inválido no crea idempotencia funcional. Sólo produce telemetría HTTP segura, porque todavía no existe una identidad de petición funcional completa que pueda referenciarse sin violar integridad.

Un conflicto por reutilización de clave nunca sobrescribe el registro original. Se audita en una transacción separada y devuelve `409 IDEMPOTENCY_CONFLICT`.

## 8. ETag y versión

Toda respuesta exitosa entrega `ETag: "<rowVersion>"` usando `VersionEtag.Format`. La identidad nueva empieza con `row_version = 1`, por lo que su ETag es exactamente `"1"`.

`If-Match` no participa en `ensure`: la operación no actualiza un plan existente. Si el cliente envía el encabezado, se ignora como encabezado no aplicable y no forma parte del hash.

Una recuperación devuelve el `row_version` y ETag vigentes de la misma identidad. HU-020 nunca incrementa `row_version`. La primera publicación de HU-021 y cada mutación posterior del plan que ese contrato apruebe deberán usar `If-Match` e incrementar `work_plan.row_version`; HU-020 no implementa esa transición.

No se agrega otra columna de versión ni se usa la versión de período, obligación o asignación como ETag del plan.

## 9. Transacción y orden de bloqueos

Cada resultado funcional se procesa en una sola transacción PostgreSQL `READ COMMITTED`. `AuditTransaction` se reutiliza para abrir, confirmar o revertir cada intento; las preconsultas sólo pueden optimizar y nunca deciden autorización, período, idempotencia o unicidad.

El orden obligatorio dentro de cada intento es:

1. bloquear la fila canónica `branch` de `LOR-001` mediante `SELECT ... FOR UPDATE`;
2. consultar y bloquear, si existe, `idempotency_record` para `(scope, key)` mediante `SELECT ... FOR UPDATE`;
3. resolver y bloquear `app_user`, empleo vigente y rol vigente del actor, en ese orden; capturar `ensuredAt` una sola vez y revalidar cuenta, sucursal, vigencia y `PER-PLAN-VER`;
4. comparar `request_hash`; si existe un resultado terminal, recuperarlo después de la revalidación de seguridad y sin reevaluar las precondiciones funcionales que originaron ese resultado;
5. para una clave nueva, validar la semana ISO y bloquear el `week_period` exacto mediante `SELECT ... FOR UPDATE`; comprobar sucursal, año, semana y fechas lunes-domingo;
6. consultar y bloquear, si existe, `work_plan` para `(branch_id, period_id)` mediante `SELECT ... FOR UPDATE`;
7. crear el `work_plan` sólo si no existe; si existe, conservarlo sin cambios;
8. insertar, cuando la clave es nueva, `idempotency_record` con el resultado de creación, recuperación o rechazo;
9. insertar `audit_event`; y
10. guardar y confirmar.

El bloqueo de la sucursal serializa el alcance único del piloto y hace determinista el orden incluso cuando dos claves distintas aseguran semanas diferentes. El bloqueo del período conserva compatibilidad con operaciones que refresquen su estado derivado. El índice único `UX_work_plan_branch_period` continúa siendo la autoridad final aun si una ruta futura no toma esos bloqueos.

Cuenta, empleo, rol y sucursal se vuelven a leer dentro de cada reintento y también antes de entregar una recuperación. El período se vuelve a leer en cada intento de una clave nueva; un resultado terminal previo conserva su respuesta aunque el período se materialice o cambie después. La seguridad actual prevalece: una cuenta ahora inactiva o sin permiso recibe `403 ACCESO_DENEGADO` y no obtiene datos aun cuando la clave antes hubiera confirmado un éxito. Después de rollback se limpia el seguimiento de EF y se repite el orden completo. Nunca se conserva un plan, registro idempotente o auditoría parcial.

## 10. Concurrencia y SQLSTATE

Dos solicitudes concurrentes sobre la misma semana se comportan así:

- mismo alcance, clave y hash: la segunda recupera el único resultado confirmado;
- mismo alcance y clave con hash distinto: la segunda devuelve `409 IDEMPOTENCY_CONFLICT`;
- claves distintas: una crea y la otra recupera el mismo plan; ambas terminan con un registro idempotente propio.

Los SQLSTATE `40P01` y `40001` permiten como máximo dos reintentos adicionales, tres intentos totales, con la misma clave y hash. Agotarlos devuelve `409 WORK_PLAN_CONCURRENCY_CONFLICT`, sin efectos parciales, y confirma conjuntamente su rechazo idempotente y auditoría cuando sea posible iniciar un intento final seguro.

Una violación `23505` de `UX_work_plan_branch_period` revierte el intento y vuelve a ejecutar la operación dentro del mismo máximo para recuperar la identidad confirmada. Una violación `23505` de la PK de `idempotency_record` vuelve a leer el registro confirmado y produce recuperación o `IDEMPOTENCY_CONFLICT`. Otra `23505` devuelve `409 WORK_PLAN_CONFLICT`; no se interpreta como éxito.

La restricción única y la transacción PostgreSQL, no una comprobación previa en memoria, son la autoridad final para una sola identidad.

## 11. Auditoría

Los eventos usan:

- `actor_type = USER`;
- `actor_user_id = ActorUserId`;
- `branch_id = BranchScope.LorettaId`;
- `correlation_id = CorrelationId`;
- `request_id = Idempotency-Key` en formato UUID canónico `D`.

El identificador HTTP de la petición es el mismo `correlationId` resuelto por el middleware. HU-020 no persiste una tercera identidad de transporte ni modifica el esquema de auditoría.

Los literales exactos son:

| Resultado | `action` | `resource_type` | `outcome` |
|---|---|---|---|
| Creación | `WORK_PLAN_ENSURED` | `WORK_PLAN` | `CREADA` |
| Recuperación, por la misma clave o por unicidad funcional | `WORK_PLAN_RECOVERED` | `WORK_PLAN` | `RECUPERADA` |
| Rechazo | `WORK_PLAN_ENSURE_REJECTED` | `WORK_PLAN` | Código literal del error |

En creación, `beforeData = null`; `afterData` contiene exactamente `schemaVersion`, `planId`, `branchId`, `periodId`, `isoYear`, `isoWeek`, `status` y `rowVersion`.

En recuperación, `beforeData = null`; `afterData` contiene exactamente `schemaVersion`, `planId`, `branchId`, `periodId`, `isoYear`, `isoWeek`, `status`, `rowVersion` y `result = RECUPERADA`.

En autorización denegada, semana inválida, período inexistente/incompatible, conflicto idempotente o concurrencia, `beforeData = null`; `afterData` contiene exactamente `schemaVersion`, `branchId`, `isoYear`, `isoWeek` y `errorCode`. `resource_id` es el `planId` si ya se resolvió; en otro caso es nulo. `reason = null` en todos los eventos de HU-020.

No se auditan nombres, puestos, texto laboral, lista o conteo de obligaciones, payloads, asignaciones, cookies, IP sin hash, secretos ni contenido de idempotencia distinto de la referencia `request_id`.

Creación, idempotencia y auditoría comparten la misma transacción PostgreSQL. Una falla de auditoría o idempotencia revierte el plan. Una recuperación con clave nueva confirma su idempotencia y auditoría juntas; una recuperación de la misma clave agrega sólo la nueva auditoría. Un rechazo con clave libre confirma su resultado idempotente y auditoría sin crear o modificar el plan. Un conflicto conserva sólo su auditoría y nunca reemplaza el registro original.

## 12. Migración e integridad

HU-020 crea como máximo una migración definitiva `AddWorkPlans` con una sola tabla nueva, `work_plan`, y estas columnas:

| Columna | Tipo PostgreSQL | Regla |
|---|---|---|
| `id` | `uuid` | PK, sin generación de base. |
| `branch_id` | `uuid` | No nulo; FK a `branch(id)` con `ON DELETE RESTRICT`. |
| `period_id` | `uuid` | No nulo; FK a `week_period(id)` con `ON DELETE RESTRICT`. |
| `status` | texto | No nulo; check `BORRADOR/PUBLICADO`. |
| `row_version` | `bigint` | No nulo; check `>= 1`. |

El índice único definitivo se llama `UX_work_plan_branch_period` y cubre `(branch_id, period_id)`. La aplicación comprueba que la sucursal del período coincide; no se debilita ninguna restricción existente.

La migración usa LF, no contiene BOM y su `Down()` lanza la excepción de reversión bloqueada usada por el repositorio. Se actualizan inventarios de migraciones/tablas y el snapshot EF. No se genera una segunda migración correctiva.

No se crean `plan_version`, `plan_version_obligation`, partidas BORRADOR, tablas de publicación, ejecución, evidencia o validación. Si durante la implementación aparece una necesidad real de otra estructura, se detiene el trabajo y se obtiene una nueva decisión expresa.

## 13. No efectos y límites modulares

La escritura pertenece al módulo `Planning`. Otros módulos sólo aportan lecturas seguras de período, obligación y organización; HU-020 no escribe sus tablas.

HU-020 no modifica ningún campo de `week_period`, `work_obligation`, `assignment_version`, evaluación, candidato, persona, cuenta, empleo, rol, disponibilidad, calendario, definición, política, solicitud de generación o auditoría histórica.

No genera obligaciones, no asigna ni corrige responsables, no concluye, no publica, no crea snapshots, no calcula elegibilidad o carga, no crea evidencia o validaciones y no ejecuta recurrencias. El estado `work_plan.status` es independiente de `work_obligation.execution_status`.

## 14. Pruebas, gates y eficacia

La implementación posterior debe cubrir todas las pruebas positivas, negativas, idempotentes, de autorización, contenido derivado, ETag, auditoría, rollback, no efecto, restricción, concurrencia y contrato HTTP indicadas en la instrucción de HU-020.

En particular, CA-020 se demuestra creando obligaciones sintéticas del mismo período cuyas políticas corresponden al menos a dos niveles, comprobando que la derivación devuelve un único `planId`, y verificando que ninguna fila o clave de plan contiene rol, área o nivel. Una prueba separada conserva el plan vacío.

Las pruebas de persistencia, índices, bloqueos y concurrencia usan PostgreSQL real y se ejecutan externamente conforme al entorno conocido. La implementación y trazabilidad deben viajar en el mismo cambio y pasar los gates finales una sola vez en el orden autorizado.

La aprobación de esta adenda no declara HU-020 terminada. Commit, publicación, PR y merge requieren autorizaciones explícitas e independientes. HU-020 sólo queda `Terminada` después del pipeline requerido verde sobre el commit exacto, aprobación humana, merge y ascendencia verificada en `origin/master`.
