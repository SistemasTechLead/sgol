# F07 Adenda 13 — Contrato de generación recurrente para HU-013

## 1. Estado, decisión solicitada y alcance

**APROBADA íntegramente por el responsable el 2026-09-04.**

Esta adenda resuelve exclusivamente los vacíos contractuales de `HU-013 — Job genera recurrencias vencidas sin duplicar`. Su aprobación íntegra autoriza iniciar la implementación en `codex/hu-013`, pero no autoriza commit, publicación de rama, pull request ni merge.

La decisión agrupada solicitada es aprobar o rechazar íntegramente las secciones 2 a 16. Una observación que cambie cualquiera de ellas debe incorporarse a esta misma adenda antes de editar código de producción, pruebas de implementación o generar la migración.

El alcance queda limitado a `LOR-001`, `TAR-0005`, el job programado, la cadena automática aprobada y sus pruebas. No implementa fuentes de servicio para `TAR-0026`, otras recurrencias, UI, HTTP, scheduler en memoria, publicación automática, conclusión, evidencia, validación, avisos, integración externa ni una historia posterior.

## 2. Regla recurrente incluida y exclusión expresa de TAR-0026

`HU-013` ejecuta únicamente `TAR-0005`, mediante su esquema aprobado `WORKING_DAY_WINDOW_V1` y sus ventanas locales exactas `12:00` y `17:00`.

`TAR-0026` conserva su política recurrente aprobada `SERVICE_DUE_DATE_REFERENCE_V1`, pero queda fuera de ejecución en esta historia. El repositorio no contiene una fuente aprobada y persistida de `servicio + vencimiento real + referencia del recibo`; por ello el job no enumera, sintetiza ni infiere hechos para esa TAR. La mera existencia de su regla recurrente no constituye una ocurrencia y no produce `RECHAZADA`, solicitud, obligación, asignación ni plan. Se emite una señal operativa agregada `ORIGIN_SOURCE_NOT_IMPLEMENTED` con `taskCode=TAR-0026`, sin dato de servicio y sin auditoría funcional, porque no existió intento sobre un hecho originador.

Una ampliación futura que incorpore hechos reales de servicio requiere su propia aprobación contractual y no forma parte de `HU-013`.

## 3. Job, comando y relación con la programación externa

El único job funcional registrado en producción es:

```text
GENERATE_DUE_RECURRENCES
```

El comando de imagen publicada es:

```text
dotnet Sgol.Worker.dll run-job --job GENERATE_DUE_RECURRENCES --scheduled-for <RFC3339-UTC-Z>
```

Ejemplo válido:

```text
dotnet Sgol.Worker.dll run-job --job GENERATE_DUE_RECURRENCES --scheduled-for 2026-09-04T18:15:00Z
```

`scheduled_for` es el corte lógico UTC inclusivo de la evaluación, no el instante real de arranque ni una hora local. Puede contener segundos o fracción porque el contrato base acepta RFC 3339 UTC; no se redondea. La plataforma externa debe invocar el comando cada 15 minutos. La configuración del scheduler y del despliegue no pertenece a `HU-013`.

El job captura `IClock.UtcNow` una sola vez al comenzar su manejador como `observedNow`. Si `scheduled_for > observedNow`, falla sin evaluar ocurrencias con `RECURRENCE_SCHEDULED_FOR_FUTURE`. La repetición exacta de un `scheduled_for` ya `SUCCEEDED` conserva el comportamiento de `TECH-JOBS-001`: no ejecuta el manejador y devuelve código `0`.

Una ejecución vacía, sólo omitida, sólo recuperada o con rechazos funcionales controlados termina `SUCCEEDED` y devuelve `0`. Una falla técnica, un corte futuro o un horizonte excedido termina `FAILED` y devuelve `1`. Nombre o comando inválido conserva `64`; configuración/base inválida conserva `69`; cancelación conserva `0` o `130` según la gracia aprobada.

No se agrega host, endpoint, servicio continuo, scheduler en memoria ni proveedor externo.

## 4. Ocurrencia vencida, atraso e intervalo evaluado

Para `TAR-0005`, una ocurrencia es la tupla exacta:

```text
ruleVersionId + LOR-001 + localDate + localWindow + ISO week period
```

Su `occurrenceInstant` se obtiene combinando `localDate` con `12:00` o `17:00` en `America/Mexico_City` y convirtiendo ese valor a UTC mediante una zona inyectable. Una ocurrencia está vencida cuando:

```text
fromExclusive < occurrenceInstant <= scheduled_for
```

La comparación es inclusiva en el corte superior: exactamente a la ventana ya está vencida; cualquier segundo o milisegundo posterior también. Antes de la ventana no se genera.

`fromExclusive` se obtiene así:

1. si existe una ejecución previa `SUCCEEDED` del mismo job con `scheduled_for` menor, se usa el mayor de esos instantes;
2. si no existe, se usa el inicio UTC del día local que contiene el `scheduled_for`, como límite exclusivo, de modo que el primer despliegue no crea trabajo histórico anterior a ese día;
3. si la distancia entre la ejecución previa y el nuevo corte excede siete días exactos, no se procesa un lote parcial: el job falla con `RECURRENCE_RECOVERY_HORIZON_EXCEEDED` y alerta. El operador puede ejecutar cortes intermedios de hasta siete días para avanzar sin omisiones silenciosas.

El job procesa atrasos de días anteriores dentro de ese intervalo y en orden `occurrenceInstant`, `ruleVersionId`. El máximo de recuperación por invocación es siete días; no existe recuperación histórica ilimitada.

Una recurrencia laborable que se procesa por primera vez con más de 30 minutos de diferencia entre `observedNow` y `occurrenceInstant` emite `RECURRENCE_DELAYED`. No se alerta como retrasada una ocurrencia inhábil ni una identidad ya confirmada anteriormente.

## 5. Versiones de regla, definición y calendario

Cada ventana histórica se gobierna por la versión de regla cuyo intervalo de eficacia contiene `occurrenceInstant`:

```text
effective_from <= occurrenceInstant
AND (effective_to IS NULL OR occurrenceInstant < effective_to)
```

La misma condición se exige para la versión exacta de `task_definition_version` y para la versión de `calendar_day_version` de `LOR-001` y `localDate`. El límite inferior es inclusivo y el superior exclusivo.

Una regla publicada después de la ventana no actúa retroactivamente. Una regla sustituida o desactivada después de la ventana sí gobierna esa ventana histórica si estaba eficaz entonces. Una regla sustituida o desactivada antes o exactamente en la ventana no se selecciona. Cambiar después el calendario no reinterpreta una ocurrencia ya confirmada; una recuperación usa la identidad y los hechos existentes.

Sólo se seleccionan `TAR-0005`, modo `RECURRENTE`, esquema `WORKING_DAY_WINDOW_V1`, zona `America/Mexico_City` y el schedule canónico exacto de 12:00/17:00. Una versión que incumpla ese contrato se clasifica `RECHAZADA` con `RECURRENCE_RULE_INVALID`, sin crear hecho funcional.

Si no existe una versión eficaz de calendario para la fecha, la ocurrencia se clasifica `RECHAZADA` con `RECURRENCE_CALENDAR_NOT_CONFIGURED`. Si existe y `is_working_day=false`, se clasifica `OMITIDA`; no se busca otro día, no se adelanta y no se traslada.

## 6. Semana ISO y período

La fecha local determina año y semana ISO lunes–domingo. El job consulta `week_period` en PostgreSQL por `LOR-001 + isoYear + isoWeek`.

Si el período no existe, el paso interno de período lo crea de forma idempotente con las mismas fechas deterministas de `WeekContract`, unicidad PostgreSQL y auditoría de actor sistema. Una carrera `23505` recupera el mismo período. Un período existente incompatible produce `RECURRENCE_PERIOD_CONFLICT` y una falla técnica segura; no se crea la ocurrencia.

El cambio de día o semana se determina siempre a partir de `America/Mexico_City`, nunca de la zona del servidor.

## 7. Identidad funcional, origen, clave y hash

Para `TAR-0005`:

- `origin_type` es exactamente `WORKING_DAY_WINDOW_V1`;
- `origin_reference` es ASCII canónico `LOR-001|yyyy-MM-dd|HH:mm`;
- `yyyy-MM-dd` es la fecha local invariante y `HH:mm` es exactamente `12:00` o `17:00`;
- regla, sucursal y período permanecen en las columnas propias de `generation_request` y no se duplican dentro del origen.

La cadena semántica canónica es UTF-8, sin BOM, con LF y estos campos en este orden:

```text
SGOL_RECURRENCE_OCCURRENCE_V1
<ruleVersionId en N minúscula>
<branchId en N minúscula>
<periodId en N minúscula>
WORKING_DAY_WINDOW_V1
LOR-001|yyyy-MM-dd|HH:mm
```

El `request_hash` es SHA-256 de esa cadena, en hexadecimal minúscula de 64 caracteres. La `Idempotency-Key` es un UUID v8 determinista: primeros 16 bytes del mismo SHA-256 en orden de red, bits de versión fijados a `8` y variante RFC 4122 fijada a `10`; su conversión a `Guid` debe probar explícitamente el orden de bytes de .NET.

Las identidades de evaluación, asignación y plan usan el mismo algoritmo con prefijos de propósito distintos: `SGOL_RECURRENCE_ELIGIBILITY_V1`, `SGOL_RECURRENCE_ASSIGNMENT_V1` y `SGOL_RECURRENCE_PLAN_V1`. Así se reanuda cada frontera sin confundir operaciones.

La restricción única PostgreSQL de la clave idempotente y la restricción funcional de `generation_request` son la autoridad final. Una preconsulta sólo puede evitar trabajo innecesario. `23505` sobre una restricción conocida carga y compara el hecho existente; datos iguales producen recuperación y datos distintos conflicto. Cualquier `23505` desconocida es falla técnica.

Dos jobs con distinto `scheduled_for` pueden alcanzar la misma ventana; ambos usan la misma identidad y las restricciones PostgreSQL permiten un solo hecho. El advisory lock de `TECH-JOBS-001` continúa protegiendo la misma invocación lógica, pero no sustituye la unicidad funcional.

## 8. Actor sistema, autorización y visibilidad

El actor exacto es lógico, no una cuenta:

```text
actor_type = SYSTEM
actor_user_id = NULL
```

No se reutiliza la cuenta bootstrap de Dirección, no se crea usuario técnico y no se concede permiso o rol humano. Para solicitudes recurrentes, `generation_request.requested_by = NULL` significa exclusivamente actor `SYSTEM`.

Las validaciones humanas de sesión, rol jerárquico y `PER-OBLIGACION-CREAR` no aplican al manejador interno. Tampoco aplican `PER-PLAN-VER` ni otra autorización humana a los pasos internos de período y plan. Sí permanecen: `LOR-001`, código TAR permitido, intervalo eficaz de definición/regla/calendario/política, modo recurrente, schedule y origen exactos, período ISO, obligación `PENDIENTE`, elegibilidad vigente, disponibilidad, rol exacto, unicidad y estados de plan.

El `correlationId` UUID v7 creado por `run-job` se propaga sin cambio a todos los pasos, auditorías, actividades y logs de la invocación. Los endpoints humanos existentes no adquieren permisos nuevos; una solicitud con `requested_by = NULL` no se vuelve visible por la jerarquía de un creador humano.

## 9. Resultados funcionales y persistencia

Los cuatro valores de `HU-013` son resultados de evaluación de la ocurrencia, no nuevos estados de `generation_request` ni de `scheduled_job_run`:

| Resultado | Regla | Persistencia |
|---|---|---|
| `GENERADA` | La cadena termina y al menos uno de sus hechos requeridos se creó en esta invocación. | Hechos creados por sus tablas; auditoría de ocurrencia y telemetría. |
| `RECUPERADA` | Todos los hechos requeridos aplicables ya existían con la misma identidad y contenido. | No crea duplicados; auditoría de recuperación y telemetría. |
| `OMITIDA` | El calendario eficaz marca la fecha inhábil. | Sólo auditoría de la evaluación y telemetría; cero solicitud, obligación, evaluación, asignación o publicación. |
| `RECHAZADA` | Existe una ventana identificable, pero regla o calendario incumple el contrato funcional. | Sólo auditoría del intento y telemetría sanitizada; cero hecho funcional. |

Una ejecución que recupera algunos pasos y crea otros se clasifica `GENERADA`, porque completó un faltante. `RECUPERADA` exige que no haya creado ningún hecho nuevo.

La auditoría de orquestación usa `resource_type=RECURRENCE_OCCURRENCE`, `actor_type=SYSTEM`, `actor_user_id=NULL` y las acciones `RECURRENCE_GENERATED`, `RECURRENCE_RECOVERED`, `RECURRENCE_OMITTED` o `RECURRENCE_REJECTED`. Para generada/recuperada, `resource_id` es el ID de obligación; para omitida/rechazada es nulo. `after_data` versión 1 contiene sólo `taskCode`, `ruleVersionId`, `branchCode`, `localDate`, `window`, `periodId`, `originType`, `originReference`, resultado y código allowlist cuando corresponda.

Una regla inválida repetida no crea filas de error, solicitudes rechazadas ni otros recursos funcionales. Cada evaluación puede dejar un nuevo `audit_event` porque representa un intento real e inmutable; esto no es un duplicado funcional. Las omisiones repetidas conservan cero obligaciones y cero asignaciones.

Los hechos aceptados conservan la retención e inmutabilidad de sus historias existentes. `scheduled_job_run` sigue siendo bitácora técnica mutable y no sustituye la auditoría funcional.

## 10. Cadena automática de cada ocurrencia

Una ocurrencia laborable válida ejecuta, en este orden:

1. crea o recupera el `week_period`;
2. crea o recupera `generation_request` recurrente;
3. crea o recupera `work_obligation`;
4. crea o recupera la evaluación de elegibilidad con `eligibilityDate=localDate` y `eligibilityDateSource=SCHEDULED_OCCURRENCE`;
5. crea o recupera la asignación automática si existe candidato elegible;
6. crea o recupera el `work_plan` único `LOR-001 + week_period` en su estado existente.

Todos los pasos forman parte de la ejecución de `HU-013`, pero cada uno es una operación idempotente confirmada por separado. Los servicios humanos públicos no se invocan fingiendo un usuario; se extraen o incorporan operaciones internas explícitas que reutilizan las mismas reglas, restricciones y entidades sin abrir transacciones anidadas.

Si no existe candidato elegible, se conserva la evaluación `SIN_CANDIDATO_ELEGIBLE`, no se crea asignación y la ocurrencia puede terminar `GENERADA` o `RECUPERADA` según los demás hechos. El plan semanal se asegura igualmente; la obligación queda pendiente y sin responsable hasta una reevaluación autorizada posterior. No se inventa candidato.

Una obligación ya existente nunca se sustituye. Un plan `BORRADOR` se recupera sin publicar. Un plan `PUBLICADO` tampoco se republica ni recibe una versión automática; la obligación recurrente tardía queda disponible para una publicación incremental humana posterior. No se crea `plan_version` ni `plan_version_obligation` en `HU-013`.

No se concluyen obligaciones y no se crean evidencias, validaciones ni avisos.

## 11. Transacciones, caída parcial y recuperación

La unidad funcional de confirmación es un paso de una ocurrencia; el lote no es una sola transacción de negocio. Cada paso usa una transacción `READ COMMITTED`, bloqueos y `AuditTransaction` propios, confirma hecho y auditoría juntos, limpia el `ChangeTracker` al terminar y descarta su scope.

El `ScheduledJobRunner` conserva su transacción técnica para `scheduled_job_run`. El manejador crea scopes/`DbContext` independientes para las operaciones funcionales; no abre una transacción anidada sobre el `DbContext` entregado por el runner. La fila técnica puede quedar `RUNNING`/abandonada o revertirse ante caída sin deshacer ocurrencias ya confirmadas.

Una caída después de parte del lote conserva las ocurrencias y pasos confirmados. La reejecución calcula de nuevo el intervalo desde la última ejecución `SUCCEEDED`, recupera identidades existentes y completa faltantes. En particular:

- caída después de solicitud y antes de obligación: recupera solicitud y crea obligación;
- después de obligación y antes de elegibilidad: recupera ambas y crea evaluación;
- después de evaluación y antes de asignación: recupera evaluación y completa asignación si hay candidato;
- después de asignación y antes de plan: recupera asignación y asegura el plan;
- antes de cualquier commit: no deja estado parcial.

Un fallo funcional de una ocurrencia (`RECHAZADA`) se audita y permite continuar el lote. Una falla técnica aborta el lote, hace que el runner marque `FAILED` y devuelve `1`; la invocación posterior reanuda. Los reintentos funcionales por identidad son independientes de los tres intentos técnicos de `TECH-JOBS-001` para `40P01`/`40001`.

Después de rollback, `23505`, `40P01`, `40001` o retry, el scope fallido se descarta o el `ChangeTracker` se limpia antes de releer. Nunca se reutiliza una entidad rastreada del intento revertido.

## 12. Checkpoint de scheduled_job_run

El checkpoint pertenece al manejador y usa exactamente `RECURRENCE_CHECKPOINT_V1`:

```json
{
  "schemaVersion": 1,
  "kind": "RECURRENCE_CHECKPOINT_V1",
  "fromExclusive": "2026-09-04T05:00:00.0000000Z",
  "throughInclusive": "2026-09-04T18:15:00.0000000Z",
  "generated": 1,
  "recovered": 0,
  "omitted": 1,
  "rejected": 0
}
```

Los instantes usan formato `O` UTC. Los contadores son enteros no negativos. El checkpoint se actualiza una sola vez, después de procesar todo el intervalo y antes de que el runner confirme `SUCCEEDED`; no es cursor de commit parcial ni autoridad de idempotencia.

Un lote vacío escribe los cuatro contadores en cero. Una fila `RUNNING` abandonada o `FAILED` conserva el tratamiento de `TECH-JOBS-001`; al reabrirse no confía en un checkpoint no confirmado y vuelve a calcular desde la última fila `SUCCEEDED` anterior.

## 13. Tratamiento de SQLSTATE y concurrencia

`23505` sólo se convierte en recuperación cuando la restricción es una de las unicidades conocidas de período, solicitud, obligación, evaluación, asignación o plan y el contenido persistido coincide. Un conflicto semántico o una restricción desconocida no se oculta como éxito.

`40P01` y `40001` conservan tres intentos técnicos totales, con 100 ms y 500 ms, misma identidad funcional y demora cancelable. Al agotarse, la ocurrencia produce falla técnica, el job queda `FAILED`, se emite `RECURRENCE_POSTGRES_CONCURRENCY_EXHAUSTED` y el proceso devuelve `1`.

El advisory lock serializa una misma pareja `(jobName, scheduled_for)`. La unicidad PostgreSQL protege además dos workers con cortes distintos que contienen la misma ventana. No se usa una preconsulta ni un lock de proceso como autoridad final.

## 14. Telemetría y exclusiones

Se reutilizan `ILogger`, `ActivitySource` y `Meter` de `Sgol.Jobs`. Se agregan eventos estructurados para inicio/resultado de ocurrencia, atraso y rechazo funcional; no se agrega proveedor externo.

Las propiedades de log permitidas para la ocurrencia son `correlationId`, `jobName`, `taskCode`, `window`, `attempt`, `durationMs`, `result`, `errorCode`, `scheduledFor` y los IDs técnicos mínimos ya permitidos. Las métricas son:

- contador `sgol.worker.recurrence.occurrences`;
- histograma `sgol.worker.recurrence.duration`;
- contador `sgol.worker.recurrence.delayed`;
- contador `sgol.worker.recurrence.origin_source_unavailable`.

Las etiquetas se limitan a `jobName`, `taskCode`, `window`, `attempt` y `result`. No incluyen fecha, UUID, correlación, persona, servicio, referencia ni error libre. `GENERADA`, `RECUPERADA`, `OMITIDA` y `RECHAZADA` se distinguen en `result`; atraso mayor de 30 minutos y agotamiento técnico son señales separadas.

Se excluyen contraseña, hash, TOTP, códigos de recuperación, cookies, claves, cadenas de conexión, URL firmada, payload completo, PII, datos de evidencia, SQL, stack trace y mensajes de excepción sin sanitizar. La auditoría funcional permanece transaccional y append-only; la telemetría operativa puede fallar sin cambiar el resultado.

## 15. Migración única necesaria

El esquema actual no puede representar sin falsedad al actor sistema porque `generation_request.requested_by` es `NOT NULL` y FK a `app_user`. Se aprueba una única migración definitiva `AllowSystemRecurringGenerationRequests` que:

1. cambia `generation_request.requested_by` a nullable;
2. conserva la FK `ON DELETE RESTRICT` cuando existe usuario;
3. agrega el check `CK_generation_request_actor`:

```sql
(requested_by IS NOT NULL AND origin_type = 'MANUAL_REFERENCE_V1')
OR
(requested_by IS NULL AND origin_type = 'WORKING_DAY_WINDOW_V1')
```

No agrega tabla, identidad técnica, columna de actor, estado ni índice. `SERVICE_DUE_DATE_REFERENCE_V1` no queda habilitado para actor sistema en esta historia. Los registros manuales existentes siguen exigiendo usuario.

La migración y su Designer usan LF y no contienen BOM; `Down()` lanza la excepción de reversión bloqueada; no hay borrado en cascada. Se actualizan snapshot e inventarios de migraciones/tablas y el modelo EF queda sin cambios pendientes. No se genera una segunda migración correctiva.

## 16. Pruebas, gates, trazabilidad y no efectos

Después de la aprobación, la implementación debe cubrir las pruebas solicitadas en el prompt y, expresamente:

- composición productiva con sólo `GENERATE_DUE_RECURRENCES`, Worker sin HTTP y rechazo de nombre no registrado;
- UTC/local, cambio de día y semana, límites inclusivos 12:00/17:00 y captura única de reloj;
- selección histórica por intervalos eficaces de regla, definición y calendario;
- día inhábil, repetición de omisión y ausencia de adelanto/traslado;
- primer arranque del día, atraso dentro de siete días, horizonte excedido, corte futuro y señal >30 minutos;
- identidad canónica, UUID v8, hash, restricciones y recuperación exacta;
- actor `SYSTEM` sin usuario, rol o permiso humano y denegación de visibilidad humana no concedida;
- cadena solicitud, obligación, elegibilidad, asignación y plan, incluidos candidato ausente y plan publicado sin nueva versión;
- caída y reanudación desde cada frontera, rollback, tracker limpio y lote parcial;
- `23505`, `40P01`, `40001`, advisory lock y dos workers sobre PostgreSQL real;
- cuatro resultados, checkpoint V1, auditoría y telemetría sanitizadas;
- cero TAR manuales, cero hechos inventados para `TAR-0026`, cero publicación, conclusión, evidencia o validación;
- dirección de dependencias, inventarios y modelo EF sin cambios pendientes.

Persistencia, restricciones, locks, recuperación y concurrencia se prueban con PostgreSQL real. Las pruebas se escriben en la implementación, pero la suite Testcontainers completa se solicita al desarrollador una sola vez en el gate externo; no se ejecuta Docker dentro de la sesión.

Los gates finales se ejecutan una sola vez y en el orden solicitado: restore locked fuera del aislamiento; build Release; suite unitaria y arquitectura; suite PostgreSQL/Testcontainers externa y espera de resultado; format; vulnerabilidades; modelo EF; protección y espejo de `Fuentes/` secuenciales; `git diff --check`.

La implementación actualizará `docs/traceability/README.md` y `docs/traceability/IMPLEMENTATION_STATUS.md` en el mismo cambio. Registrará `TECH-JOBS-001` como base aceptada con PR `#35`, commit `1517be53434f4d4af2aefd136f462d6c08eeb9df`, pipeline `SUCCESS`, run `33923994226`, aprobación humana, merge `f8217da95d59718c7f3bd7b21c09f4bb56122b6c` y ascendencia verificada. `HU-013` permanecerá como propuesta en rama hasta cumplir pipeline, aprobación, merge y ascendencia propios.

La aprobación de esta adenda no autoriza commit, publicación, PR ni merge. No se modifica `Fuentes/`, `docs/design/logo.svg`, `docs/design/mapa-pantallas.md` ni los snapshots de migración ajenos indicados en el prompt.
