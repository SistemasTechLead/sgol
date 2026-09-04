# F07 Adenda 12 — Contrato de Worker, outbox y jobs para TECH-JOBS-001

## 1. Estado, decisión solicitada y alcance

**APROBADA íntegramente por el responsable el 2026-09-04.**

Esta adenda resuelve exclusivamente los vacíos contractuales de `TECH-JOBS-001 — Host Worker, bloqueo PostgreSQL, outbox y telemetría`. Su aprobación íntegra autoriza iniciar la implementación en `codex/tech-jobs-001`, pero no autoriza commit, publicación de rama, pull request ni merge.

No implementa `HU-013`, recurrencias funcionales, solicitudes de generación, obligaciones, asignaciones, publicaciones, conclusión, evidencia, validación, S3, escaneo, réplica, respaldo, UI, HTTP ni proveedor externo de observabilidad. No modifica productores existentes para emitir eventos.

La decisión agrupada solicitada es aprobar o rechazar íntegramente las secciones 2 a 15. Una observación que cambie cualquiera de ellas debe incorporarse a esta misma adenda antes de editar código de producción, pruebas de implementación o generar la migración.

## 2. Forma del host y comandos exactos

`Sgol.Worker` es un host genérico no enrutable con dos modos explícitos:

1. `Sgol.Worker outbox`: proceso continuo que atiende exclusivamente `outbox_event`.
2. `Sgol.Worker run-job --job <jobName> --scheduled-for <instant>`: comando de una sola ejecución programada que usa `scheduled_job_run`.
3. `Sgol.Worker --help`: muestra únicamente los comandos y argumentos anteriores y termina sin abrir conexión.

No existe comando predeterminado. Argumentos ausentes, duplicados, desconocidos o con valor inválido fallan antes de ejecutar trabajo. `instant` usa exclusivamente RFC 3339 en UTC con sufijo `Z`; se rechaza un offset distinto y nunca se deriva una ventana funcional de `America/Mexico_City`.

El modo `outbox` prueba conectividad PostgreSQL antes de entrar al ciclo. Selecciona trabajo continuamente, espera cinco segundos mediante un retraso cancelable cuando no existe trabajo elegible y no registra `scheduled_job_run`. El modo `run-job` registra la ejecución lógica solicitada y termina al concluirla; no atiende outbox.

TECH-JOBS-001 no registra ningún job funcional en producción. El nombre `TECH_TEST_JOB` sólo puede registrarse desde proyectos de prueba y nunca forma parte de la composición productiva. Una historia posterior debe aprobar y registrar explícitamente su nombre y manejador; que la tabla admita el nombre no lo vuelve ejecutable. Un `jobName` no registrado falla antes de adquirir bloqueo o insertar una fila.

Los códigos de salida son:

| Código | Significado |
|---:|---|
| `0` | ayuda, terminación normal, cancelación controlada dentro de la gracia, ejecución ya completada o lock ocupado por otra instancia |
| `1` | falla del procesamiento o del job después de iniciar trabajo, incluidos reintentos de infraestructura agotados |
| `64` | comando, argumento o `jobName` inválido/no registrado |
| `69` | configuración PostgreSQL ausente/inválida o base inaccesible en la comprobación inicial |
| `130` | cancelación que excede el período de gracia |

La conexión se obtiene de la misma clave de configuración PostgreSQL usada por `Sgol.Web`; no se registra su valor. Configuración ausente, vacía o inválida nunca inicia un ciclo degradado en memoria.

## 3. Apagado y cancelación

`SIGTERM`, `SIGINT` o el token del host detienen nuevas selecciones de outbox y nuevas ejecuciones. El proceso concede como máximo 30 segundos al elemento actual. Toda espera y todo backoff observan cancelación.

Si el elemento termina dentro de la gracia, se confirma o revierte según su resultado, se libera el bloqueo PostgreSQL, se cierra la conexión y el proceso devuelve `0`. Si excede 30 segundos, se cancela el comando, se revierte la transacción abierta, se libera el bloqueo al cerrar la conexión y devuelve `130`. No se usa `Environment.FailFast`, no se abandona una transacción confirmada a medias y no se inicia otro elemento durante el apagado.

## 4. Coordinación PostgreSQL para jobs programados

Cada invocación `run-job` usa un advisory lock PostgreSQL de **sesión**, no un lock en memoria ni de transacción. La clave de dos enteros se construye así:

- primer entero fijo: `0x53474F4C` (`SGOL`);
- segundo entero: los primeros 32 bits con signo, en orden big-endian, de SHA-256 sobre UTF-8 de `jobName + "\n" + scheduledFor`, donde `scheduledFor` está en formato canónico `O` UTC.

Se abre una conexión dedicada, se llama una sola vez a `pg_try_advisory_lock(int,int)` y se conserva esa sesión durante toda la ejecución. No se espera: lock ocupado produce telemetría `SKIPPED_LOCKED`, no crea ni modifica `scheduled_job_run` y devuelve `0`. Una colisión de hash sólo puede omitir conservadoramente una ejecución concurrente; nunca permite dos propietarios.

Una vez adquirido el advisory lock, el comando inserta o bloquea la fila `(job_name, scheduled_for)` antes de ejecutar el manejador. El orden es siempre advisory lock y después fila de ejecución; nunca se adquieren dos locks de job en una invocación. El lock se libera explícitamente al terminar y, ante caída, PostgreSQL lo libera al cerrar la sesión.

`lock_timeout` es cinco segundos para el bloqueo de la fila de ejecución y el timeout de los comandos de infraestructura es 15 segundos. El advisory lock y la reclamación outbox son intentos no bloqueantes. Agotar `lock_timeout`, perder la conexión después del arranque o exceder el timeout de comando sigue el tratamiento acotado de infraestructura y, si no se recupera, termina el proceso con `1`; nunca cambia a coordinación en memoria.

`23505` sobre la unicidad `(job_name, scheduled_for)` converge cargando la fila existente bajo bloqueo. Otra `23505` es una falla de integridad terminal y segura. `40P01` o `40001` permiten dos reintentos adicionales —tres intentos totales— con demoras cancelables de 100 ms y 500 ms. Agotarlos marca la ejecución `FAILED` cuando exista una fila reclamable, emite alerta y devuelve `1`.

## 5. Contrato de `scheduled_job_run`

La tabla contiene exactamente:

| Columna | Tipo y nulabilidad | Regla |
|---|---|---|
| `id` | `uuid NOT NULL` | PK; UUID v7 generado por la aplicación |
| `job_name` | `varchar(64) NOT NULL` | `^[A-Z][A-Z0-9_]{0,63}$` |
| `scheduled_for` | `timestamptz NOT NULL` | instante lógico UTC recibido por comando |
| `started_at` | `timestamptz NOT NULL` | último inicio o reinicio, capturado una vez |
| `ended_at` | `timestamptz NULL` | fin del último intento; nulo sólo en `RUNNING` |
| `status` | `varchar(16) NOT NULL` | sólo `RUNNING`, `SUCCEEDED` o `FAILED` |
| `checkpoint` | `jsonb NULL` | objeto acotado, propiedad exclusiva del manejador registrado |
| `error` | `varchar(512) NULL` | código sanitizado; nunca excepción o SQL sin depurar |

Existe unicidad exacta `(job_name, scheduled_for)`, check de objeto JSON para `checkpoint`, check de coherencia temporal y checks de estado: `RUNNING` exige `ended_at IS NULL AND error IS NULL`; `SUCCEEDED` exige `ended_at IS NOT NULL AND error IS NULL`; `FAILED` exige `ended_at IS NOT NULL AND error IS NOT NULL`.

La tabla admite nombres futuros que satisfagan la sintaxis, pero el host sólo ejecuta nombres registrados en su composición. TECH-JOBS-001 no interpreta `checkpoint`; limita el JSON serializado a 64 KiB, profundidad 16 y ausencia de secretos, PII o payload funcional completo.

La primera reclamación inserta `RUNNING`. Una fila `SUCCEEDED` representa la misma ejecución lógica ya completada: no se ejecuta otra vez y devuelve `0`. Una fila `FAILED` puede reabrirse en una invocación explícita posterior con la misma clave: conserva `id` y `checkpoint`, reemplaza `started_at`, limpia `ended_at/error` y vuelve a `RUNNING`. Una fila `RUNNING` encontrada después de adquirir el advisory lock se considera abandonada por caída y se recupera del mismo modo. No se crea otra fila para un intento del mismo hecho lógico.

Éxito actualiza la misma fila a `SUCCEEDED`; falla la actualiza a `FAILED`. El historial de intentos operativos se conserva en telemetría estructurada, no en `audit_event`. Las filas no se borran ni purgan durante el MVP. `scheduled_job_run` es bitácora técnica mutable de la ejecución lógica, no auditoría funcional append-only.

`scheduled_for`, `started_at` y `ended_at` son instantes UTC. Una historia funcional posterior podrá calcular un `scheduled_for` desde `America/Mexico_City`, pero TECH-JOBS-001 no calcula calendarios, ventanas ni vencimientos.

## 6. Contrato de `outbox_event`

La tabla contiene exactamente:

| Columna | Tipo y nulabilidad | Regla |
|---|---|---|
| `id` | `uuid NOT NULL` | PK; UUID v7 generado por la aplicación e identidad idempotente de entrega |
| `event_type` | `varchar(128) NOT NULL` | `^[A-Z][A-Z0-9_.]{0,127}$` |
| `aggregate_id` | `uuid NULL` | identidad del agregado productor cuando exista |
| `payload` | `jsonb NOT NULL` | sobre versionado descrito abajo |
| `created_at` | `timestamptz NOT NULL` | instante único de creación UTC |
| `available_at` | `timestamptz NOT NULL` | no anterior a `created_at`; próxima elegibilidad |
| `processed_at` | `timestamptz NULL` | confirmación exitosa UTC |
| `attempt_count` | `integer NOT NULL DEFAULT 0` | de `0` a `5` |
| `last_error` | `varchar(512) NULL` | código sanitizado del último fallo |

`payload` es un objeto JSON de máximo 64 KiB y profundidad máxima 16 con exactamente esta envoltura técnica:

```json
{
  "schemaVersion": 1,
  "correlationId": "UUID",
  "data": {}
}
```

`schemaVersion` es el entero `1`; `correlationId` es UUID canónico y `data` es un objeto cuyo esquema pertenece al `event_type` registrado. El nombre debe estar registrado junto con un manejador y un validador de payload para poder insertarse mediante el contrato interno o procesarse. TECH-JOBS-001 no registra tipos productivos; las pruebas inyectan `TECH.TEST_EVENT.V1` sin incorporarlo a la composición de producción.

El payload y `last_error` excluyen contraseña, hash, TOTP, códigos de recuperación, cookies, claves, cadenas de conexión, URL firmada, binario, PII, contenido de evidencia, payload funcional completo innecesario, SQL, stack trace y texto de excepción sin sanitizar. `last_error` contiene sólo un código allowlist estable producido por el manejador o `UNEXPECTED_HANDLER_FAILURE`.

Una fila con `event_type` no registrado o payload que no satisface su esquema nunca se ejecuta. Consume intentos como fallo seguro con `UNREGISTERED_EVENT_TYPE` o `INVALID_EVENT_PAYLOAD`, respectivamente, hasta quedar agotada y alertada; no se deserializa dinámicamente a tipos arbitrarios ni se omite como éxito.

Un evento exitoso exige `processed_at IS NOT NULL`, `last_error IS NULL` y entre uno y cinco intentos. Un evento pendiente nuevo exige `processed_at IS NULL`, `attempt_count = 0` y `last_error IS NULL`. Un evento en reintento exige `processed_at IS NULL`, de uno a cuatro intentos y `last_error IS NOT NULL`. Un evento agotado exige `processed_at IS NULL`, cinco intentos y `last_error IS NOT NULL`; permanece retenido pero la consulta lo excluye. No se agrega columna de estado, lease, propietario o alerta.

No existe borrado ni purga de outbox durante el MVP. Una política posterior de retención requiere aprobación expresa.

## 7. Escritura transaccional de outbox

TECH-JOBS-001 expone un contrato interno mínimo `IOutboxWriter` que valida el tipo y el sobre y agrega la entidad al mismo `DbContext` de la operación productora. No abre transacción, no llama `SaveChanges` y no confirma por sí mismo. El productor futuro debe invocarlo dentro de su `AuditTransaction`, de modo que hecho, auditoría y outbox confirmen o reviertan juntos.

El escritor captura `created_at` una sola vez con `IClock.UtcNow`; `available_at` es ese mismo instante salvo que el llamador proporcione un instante posterior aprobado por su contrato. Genera `id` con `IUuidGenerator`. El `correlationId` procede del contexto de la operación y se persiste sólo en la envoltura técnica.

TECH-JOBS-001 no modifica `HU-014`, `HU-015`, `HU-020`, `HU-021` ni otro productor existente y no inserta eventos funcionales retroactivos.

## 8. Reclamación, transacción y orden de outbox

Cada instancia selecciona un evento a la vez mediante SQL parametrizado equivalente a:

```sql
SELECT ...
FROM outbox_event
WHERE processed_at IS NULL
  AND attempt_count < 5
  AND available_at <= @now
ORDER BY available_at, created_at, id
FOR UPDATE SKIP LOCKED
LIMIT 1;
```

PostgreSQL y el lock de fila son la autoridad final. No existe preconsulta ni lock en memoria. La selección es no bloqueante: una fila ocupada se salta. El lote lógico de cada ciclo contiene como máximo 25 eventos, pero cada evento usa su propia transacción `READ COMMITTED`, su propio lock y su propio resultado; por ello una falla no revierte ni retiene los otros 24.

El orden estable es `available_at ASC`, `created_at ASC`, `id ASC`. Con varias instancias, el inicio global puede intercalarse por `SKIP LOCKED`, pero ninguna fila tiene dos propietarios simultáneos y cada instancia conserva ese orden entre filas elegibles no bloqueadas.

Dentro de la transacción se crea un savepoint, se incrementa `attempt_count` inmediatamente antes de invocar el manejador y se ejecuta el efecto. En éxito, efecto, incremento y `processed_at` se confirman juntos, con `last_error = NULL`. El instante de intento se captura una vez y gobierna selección, `processed_at` o el cálculo de reintento.

Si el manejador falla, se revierte al savepoint para eliminar todo efecto y cambio rastreado del intento, se limpia el `ChangeTracker`, se recarga bajo el mismo lock y se registra el fallo sanitizado en la misma transacción todavía propietaria de la fila. Así no existe una ventana entre rollback y programación del reintento.

## 9. Reintentos, agotamiento y SQLSTATE

Outbox permite cinco intentos totales. Tras un fallo se confirma el nuevo `attempt_count`, `last_error` y:

| Intento fallido | Nuevo `available_at` |
|---:|---|
| `1` | instante del intento + 1 minuto |
| `2` | instante del intento + 5 minutos |
| `3` | instante del intento + 15 minutos |
| `4` | instante del intento + 60 minutos |
| `5` | instante del intento; queda agotado y excluido por `attempt_count < 5` |

No hay jitter, para que el contrato sea determinista y comprobable. Al quinto fallo se emiten el log y contador de agotamiento; no se llama a proveedor externo ni se crea otra tabla o estado.

`40P01` y `40001` de la infraestructura de reclamación permiten dos reintentos adicionales —tres intentos totales— con 100 ms y 500 ms. Cada retry revierte la transacción, limpia el `ChangeTracker`, vuelve a capturar el instante y repite desde la selección. Estos retries no incrementan `attempt_count` si el manejador no comenzó. Agotarlos emite alerta operativa y deja el evento elegible sin cambio parcial.

Una `23505` producida por la restricción idempotente del efecto se interpreta únicamente mediante el contrato del manejador y recupera su resultado existente; otra `23505` es fallo terminal del intento, se sanitiza y consume un intento de outbox. No se oculta una violación desconocida como éxito.

## 10. Entrega al menos una vez e idempotencia del consumidor

`outbox_event.id` es la clave idempotente obligatoria del consumidor. Todo manejador debe producir su efecto lógico mediante una restricción única PostgreSQL basada en ese ID o una clave funcional igual de fuerte. Cuando el efecto está en la misma base, efecto y `processed_at` comparten la transacción y el mismo `DbContext`/conexión.

Si un proceso cae después de un efecto externo pero antes de `processed_at`, PostgreSQL libera el lock y el evento vuelve a ser elegible. El manejador repite la misma clave; el destino debe devolver el efecto existente. TECH-JOBS-001 no promete exactamente una entrega y no incorpora efectos externos.

Una caída en parte de un lote conserva los eventos ya confirmados; el evento con transacción no confirmada y los faltantes se recuperan individualmente. Un fallo antes de commit no deja incremento, marca de procesado ni efecto PostgreSQL parcial. Después de rollback o retry siempre se limpia el `ChangeTracker` y se reconstruye el scope del elemento; no se reutilizan entidades rastreadas del intento fallido.

## 11. Telemetría estable y segura

Se usa `ILogger`, `ActivitySource` y `Meter` de .NET, sin SDK ni proveedor externo. Los nombres de fuente son `Sgol.Jobs` y el atributo `service` vale `Sgol.Worker`.

Eventos de log estables:

| EventId | Nombre | Nivel | Resultado principal |
|---:|---|---|---|
| `2100` | `WorkerStarted` | Information | modo iniciado |
| `2101` | `WorkerStopped` | Information | modo terminado |
| `2110` | `OutboxClaimed` | Debug | evento reclamado |
| `2111` | `OutboxProcessed` | Information | `SUCCEEDED` |
| `2112` | `OutboxRetryScheduled` | Warning | `RETRY_SCHEDULED` |
| `2113` | `OutboxAttemptsExhausted` | Error | `EXHAUSTED` y señal de alerta |
| `2120` | `ScheduledJobStarted` | Information | `RUNNING` |
| `2121` | `ScheduledJobLockBusy` | Information | `SKIPPED_LOCKED` |
| `2122` | `ScheduledJobCompleted` | Information | `SUCCEEDED` |
| `2123` | `ScheduledJobFailed` | Error | `FAILED` y señal de alerta |
| `2124` | `PostgresConcurrencyRetry` | Warning | `RETRY` o `EXHAUSTED` |
| `2130` | `WorkerShutdownRequested` | Information | cancelación solicitada |
| `2131` | `WorkerShutdownGraceExceeded` | Error | `CANCELLED_TIMEOUT` y señal de alerta |

Propiedades allowlist: `correlationId`, `jobName`, `eventType`, `attempt`, `durationMs`, `result`, `errorCode`, `scheduledFor` y los IDs técnicos mínimos `eventId`/`jobRunId`. No se registra `payload`, `checkpoint`, `last_error`/`error` crudos, excepción completa, SQL ni valores de configuración. La excepción sólo puede enviarse a `ILogger` después de pasar por redacción y sin incluir su mensaje como propiedad estructurada.

Métricas estables:

- contadores `sgol.worker.outbox.attempts`, `sgol.worker.outbox.processed`, `sgol.worker.outbox.failures`, `sgol.worker.outbox.exhausted` y `sgol.worker.jobs.runs`;
- histogramas en milisegundos `sgol.worker.outbox.duration`, `sgol.worker.jobs.duration` y `sgol.worker.shutdown.duration`.

Las etiquetas se limitan a `jobName`, `eventType`, `attempt` y `result`; nunca incluyen IDs, correlación, error libre ni datos funcionales. `OutboxAttemptsExhausted`, `ScheduledJobFailed`, concurrencia agotada y gracia excedida son las señales que una plataforma futura puede convertir en alerta.

El `correlationId` del outbox se lee del sobre y gobierna el `Activity` y los logs del intento. `run-job` crea un UUID v7 de correlación por invocación. La auditoría funcional permanece en `audit_event`, dentro de la transacción de negocio y append-only; esta telemetría es operativa, puede fallar sin alterar el hecho y no sustituye auditoría.

## 12. Migración única y restricciones

TECH-JOBS-001 genera como máximo una migración definitiva `AddJobInfrastructure`. Crea únicamente `outbox_event`, `scheduled_job_run` y sus PK, checks e índices aprobados aquí. No crea tabla de lock, lease, intento, alerta, handler, efecto ni recurrencia y no modifica tablas existentes.

Índices:

- `outbox_event(processed_at, available_at, created_at, id)` para selección estable;
- `outbox_event(event_type, created_at)` para operación;
- único `scheduled_job_run(job_name, scheduled_for)`; y
- `scheduled_job_run(status, scheduled_for)` para operación y recuperación.

No existen FKs en estas dos tablas dentro de TECH-JOBS-001: `aggregate_id` identifica un agregado de cualquier módulo y no puede referenciar una única tabla. Si una historia futura agrega una FK, usa `ON DELETE RESTRICT` y requiere contrato expreso.

La migración y su Designer usan LF, no contienen BOM y `Down()` lanza la excepción de reversión bloqueada usada por el repositorio. Se actualizan snapshot EF e inventarios de migraciones/tablas y el modelo queda sin cambios pendientes. No se genera una segunda migración correctiva.

## 13. Pruebas posteriores a la aprobación

La implementación cubrirá las pruebas solicitadas para TECH-JOBS-001 y, de forma expresa:

- composición y arranque de `Sgol.Worker` sin servidor HTTP;
- comandos, códigos de salida, configuración ausente, cancelación y gracia de 30 segundos;
- reloj y UUID inyectables, y captura única de cada instante;
- unicidad y recuperación de `(job_name, scheduled_for)`;
- dos procesos con advisory lock, lock ocupado y liberación por caída;
- reclamación `FOR UPDATE SKIP LOCKED`, orden estable y lote parcial;
- éxito único, cinco intentos, backoff exacto, agotamiento y señal de alerta;
- rollback/savepoint, limpieza de tracker y recuperación tras caída;
- idempotencia del efecto con entrega repetida;
- tratamiento de `23505`, `40P01` y `40001`, incluido agotamiento;
- sanitización de errores, logs y métricas sin payload ni secretos;
- checks, índices, nulabilidad, tamaños y unicidades PostgreSQL;
- ausencia de eventos/tipos/jobs productivos y de hechos de recurrencia, generación, obligación, asignación o publicación;
- dirección de dependencias, inventarios y modelo EF sin cambios pendientes.

Persistencia, restricciones, locks, recuperación y concurrencia se prueban con PostgreSQL real. Las pruebas se escriben pero la suite Testcontainers completa se solicita al desarrollador una sola vez en el gate externo; no se ejecuta Docker dentro de la sesión.

## 14. Gates y trazabilidad posteriores a la aprobación

Después de implementar se ejecutan una sola vez y en el orden de la instrucción de TECH-JOBS-001: restore bloqueado fuera del aislamiento; build Release; suite unitaria y arquitectura completa; solicitud y espera de la suite PostgreSQL/Testcontainers externa; formato; vulnerabilidades; modelo EF sin cambios pendientes; protección y espejo de `Fuentes/` de forma secuencial; y `git diff --check`.

En el mismo cambio se actualizan `docs/traceability/README.md` y `docs/traceability/IMPLEMENTATION_STATUS.md`. Se registra `HU-021` como base aceptada con PR `#34`, commit `6e22847081a10043fce0fe9c22ca25157e4e18bc`, pipeline `SUCCESS`, run `33917438014` y merge `dc1ba3d92c998297d0bb9b4ad638551be222f781`; TECH-JOBS-001 permanece como propuesta en rama.

## 15. No efectos, eficacia y autorizaciones independientes

`Sgol.Worker` sólo compone infraestructura y manejadores registrados; no contiene reglas de negocio. Los contratos de outbox y jobs viven en una capa interna reutilizada por host y módulos sin hacer que dominio dependa de ASP.NET Core, EF Core, PostgreSQL o telemetría concreta.

TECH-JOBS-001 no crea solicitudes, obligaciones, asignaciones, planes, publicaciones, recurrencias, evidencias, validaciones, archivos, avisos ni auditoría funcional ficticia. No expone HTTP, no agrega Docker Compose, no implementa despliegue OCI y no incorpora broker, Redis, caché, microservicios o Kubernetes.

La aprobación íntegra de esta adenda es la única decisión que permite comenzar código de producción, pruebas de implementación y migración. No declara la tarea terminada y no autoriza commit, publicación, PR o merge. Implementación y trazabilidad deben viajar en el mismo cambio. TECH-JOBS-001 sólo queda `Terminada` después de pipeline requerido verde sobre el commit exacto, aprobación humana, merge y ascendencia verificada en `origin/master`.
