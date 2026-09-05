# F07 Adenda 14 — Contrato de demo automatizada y cierre de CV-02 para TECH-E2E-CV-02

## 1. Estado, decisión solicitada y alcance

**APROBADA íntegramente por el responsable el 2026-09-05.**

Esta adenda resuelve exclusivamente los vacíos contractuales de `TECH-E2E-CV-02 — Demo automatizada y cierre del corte CV-02`. Su aprobación íntegra autoriza iniciar la implementación en `codex/tech-e2e-cv-02`, pero no autoriza commit, publicación de rama, pull request ni merge.

La decisión agrupada solicitada es aprobar o rechazar íntegramente las secciones 2 a 18. Una observación que cambie cualquiera de ellas debe incorporarse a esta misma adenda antes de editar código, proyectos, paquetes, configuración, pipeline o trazabilidad adicional.

El alcance se limita a demostrar con datos sintéticos las capacidades ya aceptadas de `CV-02 — Trabajo planificado`: configuración versionada aplicable a `TAR-0005`, generación manual y recurrente, obligación única, elegibilidad, asignación, corrección, plan semanal, publicación incremental, idempotencia, recuperación, autorización negativa e historia. No implementa `HU-022`, `HU-023`, ninguna historia de `CV-03`, hechos de `TAR-0026`, UI o endpoints productivos, autenticación productiva, scheduler nuevo, publicación automática, conclusión, evidencia, validación, notificaciones, despliegue ni cambios de esquema.

## 2. Inserción formal y precedencia efectiva

Se inserta formalmente la tarea:

| ID | Resultado verificable | Dependencias | Ejecutar antes de | No incluye |
|---|---|---|---|---|
| `TECH-E2E-CV-02` | Demo automatizada y reproducible del corte `CV-02`, con PostgreSQL real, servicios y Worker reales, accesibilidad crítica, evidencia sanitizada y repetición sin duplicados | `HU-012` a `HU-021`, porción aplicable de `HU-034`, `TECH-JOBS-001` y dependencias aceptadas | Cierre de `CV-02` y comienzo de `HU-023` | Producto nuevo, `CV-03`, migraciones, datos reales o despliegue |

La base aceptada inmediata es `HU-013`: PR `#36`, commit implementado `c7501abd7534edd2a84115a8dec803872819cb97`, pipeline `TECH-BASE-003 / PR gates` en `SUCCESS`, run `33975676912`, aprobación humana recibida y merge `0b21a1173c8e31c82cc65cf5cc51b4d27464a36e`; ambos commits fueron verificados como ancestros de `origin/master` al preparar esta propuesta.

Aunque `HU-023` sea la siguiente fila numerada de F07, no puede iniciarse: `TECH-E2E-<CV>` es una dependencia de infraestructura obligatoria antes de cerrar cada corte. `TECH-E2E-CV-02` no habilita `HU-023` hasta cumplir todas las condiciones de la sección 18.

## 3. Naturaleza y ubicación del entregable

El entregable será una combinación mínima dentro de un único proyecto no productivo:

```text
tests/Sgol.Cv02Demo/Sgol.Cv02Demo.csproj
```

El proyecto será un host ASP.NET Core Razor Pages ejecutable y, a la vez, un proyecto de pruebas xUnit. Contendrá el orquestador de escenarios, las páginas técnicas, la capa lectora y las pruebas Playwright. Se incluirá en `SGOL.slnx` bajo `/tests/` para que restore, build, formato y reglas de arquitectura lo inspeccionen.

El proyecto podrá referenciar los contratos de módulos, `Sgol.Web` para la composición de persistencia aceptada y `Sgol.Worker` para ejecutar su entry point real. Ningún proyecto bajo `src/` referenciará el host de demo. La dirección de dependencias se comprobará mediante pruebas de arquitectura.

No se modifica `src/Sgol.Web` porque las rutas, acciones, actores, lectura reconciliada y ciclo de vida pertenecen sólo al ejecutable desechable. No se agrega UI, endpoint, permiso, cookie, autenticación ni comportamiento al producto. La demo no se publica ni se empaqueta como artefacto productivo.

## 4. Comandos, modos y ciclo de vida

El comando canónico automatizado será:

```powershell
rtk powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\demo\run-cv02.ps1 -Mode Automated
```

El modo de exhibición humana será:

```powershell
rtk powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\demo\run-cv02.ps1 -Mode Interactive
```

El script será un wrapper delgado, sin reglas de negocio. Sólo aceptará `Automated` o `Interactive`; no aceptará URL, puerto, cadena de conexión, base, credenciales, imagen, sucursal, TAR, semilla, actor, UUID ni fecha arbitrarios.

En ambos modos el ejecutable:

1. valida que no exista configuración PostgreSQL aportada externamente;
2. crea PostgreSQL efímero;
3. aplica todas las migraciones desde una base vacía;
4. prepara la semilla sintética determinista mediante el orquestador aprobado;
5. inicia Kestrel exclusivamente en `http://127.0.0.1:0` y obtiene el puerto asignado por el sistema;
6. ejecuta los escenarios o deja disponibles las tres páginas para la exhibición;
7. genera el reporte sanitizado;
8. detiene navegador, Kestrel, scopes, conexiones y contenedor, incluso ante cancelación o error.

`Automated` ejecuta Playwright headless, devuelve `0` sólo si todos los escenarios pasan y siempre limpia. `Interactive` muestra la URL loopback y conserva el host hasta `Ctrl+C`; al cancelar aplica la misma limpieza y no deja base ni proceso en segundo plano. No existe modo predeterminado, servicio continuo ni opción `--keep-data`.

## 5. Superficie HTTP cerrada

Las únicas páginas funcionales de la demo serán:

```text
GET|POST /cv02/calendario
GET|POST /cv02/recurrencias
GET|POST /cv02/plan-semanal
```

Los `POST` usarán antiforgery y sólo aceptarán identificadores de escenario definidos como constantes del servidor. Cada escenario fija internamente actor, instante, claves e identidades; no admite IDs, nivel, sucursal, TAR, fecha, clave idempotente o payload arbitrarios desde el navegador. Una ruta, método, escenario o campo adicional devuelve rechazo sanitizado y no invoca servicios de escritura.

No se expone `/api`, Swagger, endpoint productivo, selector libre de actor ni consola SQL. La raíz y cualquier ruta fuera del allowlist responden `404`. Los recursos estáticos locales requeridos por las páginas no amplían esta superficie funcional ni realizan llamadas externas.

## 6. PostgreSQL desechable y guardas de seguridad

La demo usa Testcontainers con la misma imagen aceptada por las pruebas de integración:

```text
postgres:18.6-alpine3.23
```

Cada ejecución crea un contenedor nuevo sin volumen, con puerto aleatorio publicado sólo en loopback, base y credenciales aleatorias generadas por Testcontainers y nombre de base con prefijo `sgol_cv02_`. La cadena se obtiene únicamente del objeto `PostgreSqlContainer` recién creado y se pasa en memoria a los hosts internos.

El ejecutable falla antes de abrir una conexión si encuentra `ConnectionStrings__Sgol`, `ConnectionStrings:Sgol`, `DATABASE_URL`, un argumento de conexión o cualquier proveedor externo equivalente. También valida que host/puerto correspondan al contenedor de la ejecución, que la base conserve el prefijo y que no exista volumen. No acepta una cadena arbitraria ni reutiliza una base existente.

Las migraciones se aplican desde cero mediante el ensamblado productivo vigente. Al finalizar se descartan todos los contextos y conexiones y se elimina el contenedor. Una segunda ejecución comienza con otra base vacía. No se copian, importan, anonimizan ni consultan datos reales.

## 7. Semilla, actores, reloj e identidades

La semilla se identifica como `CV02-SEED-V1` y es completamente sintética. Sólo crea `LOR-001` y las precondiciones mínimas para los escenarios:

- `DIR-CV02`, actor Dirección;
- `ADMIN-CV02`, superior de Subcoordinación y Piso;
- `SUB-CV02`, subordinado de Administración y superior de Piso;
- `PISO-A-CV02` y `PISO-B-CV02`, dos candidatos para carga y desempate;
- `OUTSIDE-CV02`, usuario de la misma sucursal pero fuera de la jerarquía autorizada para el recurso demostrado.

Todos tienen persona, empleo, cuenta y rol sintéticos vigentes según el escenario. No se usan nombres humanos, correos reales, teléfonos, contraseñas, TOTP, cookies ni códigos de recuperación. La demo no autentica contra el producto: cada acción cerrada selecciona un actor sintético fijo y entrega su identidad al contrato real, cuya autorización de permiso, rol, recurso, jerarquía, estado y sucursal permanece activa.

El calendario incluye días laborables e inhábiles en la semana ISO que cruza 2026/2027. El reloj de demo sólo puede avanzar por una secuencia allowlist que cubre el límite de semana ISO y las ventanas locales `12:00` y `17:00` de `America/Mexico_City`; los cortes equivalentes se conservan como UTC con sufijo `Z`. No se lee la zona del navegador para decisiones funcionales.

`IClock` y `IUuidGenerator` se sustituyen en la composición de prueba por implementaciones deterministas. La secuencia de UUID es estable por escenario y agota con error si aparece una creación no prevista. Fechas, claves idempotentes, referencias de origen y motivos son constantes sintéticas versionadas en el código de la demo.

La preparación de precondiciones técnicas puede insertar fixtures por utilidades de prueba cuando no constituye el comportamiento bajo demostración. Las operaciones que son resultado de los escenarios —configuración/publicación, solicitud, obligación, evaluación, asignación, corrección, plan, publicación, recurrencia y auditoría— deben pasar por los contratos y servicios reales; no pueden sembrarse como resultado ya calculado.

## 8. Servicios reales y ejecución real del Worker

La demo reutiliza las interfaces y servicios aceptados de configuración, calendario, definición TAR, activación, elegibilidad, generación, obligación, asignación, corrección, semana, plan, publicación, auditoría y persistencia. Usa las restricciones, índices, transacciones, idempotencia y concurrencia de PostgreSQL sin reemplazos en memoria.

La recurrencia no llamará directamente a `RecurringGenerationJob` ni a `ScheduledJobRunner`. Ejecutará el entry point real:

```csharp
WorkerApplication.RunAsync(
    ["run-job", "--job", "GENERATE_DUE_RECURRENCES", "--scheduled-for", scheduledFor],
    configureServices: demoOverrides,
    configuration: disposableConfiguration)
```

Así se ejercitan el parser, registro del job, prueba de conexión, `ScheduledJobRunner`, advisory lock, `scheduled_job_run`, scopes, manejador `RecurringGenerationJob`, cadena funcional y códigos de salida de `Sgol.Worker run-job`. `demoOverrides` sólo reemplaza `IClock`, `IUuidGenerator` y el inyector de falla de prueba aprobado; no sustituye servicios funcionales ni persistencia.

Las escrituras humanas de la demo usan sus servicios aceptados con actores sintéticos y cabeceras lógicas equivalentes a `Idempotency-Key` e `If-Match`. No se falsifica una sesión ni se concede autoridad adicional. La auditoría real comparte las transacciones funcionales conforme a cada contrato.

## 9. Capa lectora estrictamente de sólo lectura

El proyecto tendrá una capa `Cv02DemoReader` destinada sólo a reconciliar y presentar el estado. Podrá leer las tablas aceptadas necesarias para los 13 escenarios, siempre mediante consultas `AsNoTracking`, una transacción PostgreSQL `READ ONLY` y proyecciones a DTO de demo sanitizados.

La capa no implementará comandos, no llamará servicios de escritura y no tendrá método que exponga `DbContext`, entidad rastreada, SQL libre o `SaveChanges`. Un interceptor de prueba rechazará cualquier `SaveChanges` iniciado desde su scope. Las pruebas tomarán conteos/huellas antes y después de cada lectura y confirmarán cero cambios funcionales, de auditoría, idempotencia y `scheduled_job_run`.

La lectura no se convierte en API productiva ni define semántica nueva. Cuando un dato no exista en contratos aceptados, la página mostrará “no disponible en este corte” y el escenario fallará si ese dato era parte de su criterio; nunca lo inferirá ni lo fabricará.

## 10. Matriz mínima de escenarios

Cada fila es obligatoria. El nombre indicado será estable en el reporte y en la prueba automatizada.

| # | Escenario / prueba automatizada | HU, CA y CP principales | Resultado verificable |
|---:|---|---|---|
| 1 | `S01_PublishesVersionedTar0005Configuration` | `HU-008`, `HU-009`, `HU-011`, `HU-012`, `HU-017`; `CA-012`, `CP-012-P`, `CA-017`, `CP-017-P` | Publica definición, calendario, activación recurrente y política de elegibilidad versionadas para `TAR-0005`; queda una `VIGENTE` y la predecesora se conserva cuando aplica. |
| 2 | `S02_ManualReplayRecoversSameRequestAndObligation` | `HU-014`, `CA-014`, `CP-014-P/N`; `HU-015`, `CA-015`, `CP-015-P/N`; `HU-034`, `CA-034`, `CP-034-P` | Una solicitud manual aceptada y su repetición idéntica devuelven los mismos IDs de solicitud y obligación; contenido distinto con la misma identidad entra en conflicto sin segundo hecho. |
| 3 | `S03_WorkingDayCreatesNoonAndSeventeenWindows` | `HU-013`, `CA-013`, `CP-013-P` | `Sgol.Worker run-job` crea exactamente las ocurrencias locales 12:00 y 17:00, con solicitudes/obligaciones únicas y auditoría `SYSTEM`. |
| 4 | `S04_NonWorkingDayIsOmittedWithoutShift` | `HU-013`, `CA-013`, `CP-013-N` | El inhábil queda `OMITIDA`, sin adelanto ni traslado y con cero solicitud, obligación, evaluación, asignación, plan o publicación para esa fecha. |
| 5 | `S05_EligibilityAndAutomaticAssignmentAreExplainableAndDeterministic` | `HU-016`, `CA-016`, `CP-016-P/N`; `HU-018`, `CA-018`, `CP-018-P` | Dos candidatos controlados ejercitan gates, carga, espera y código; gana el esperado y quedan snapshot y explicación reales. |
| 6 | `S06_NoEligibleCandidateCreatesNoAssignment` | `HU-016`, `CA-016`, `CP-016-N`; `HU-018`, `CA-018` | Registra `SIN_CANDIDATO_ELEGIBLE`, conserva campos reservados y no crea responsable ni asignación ficticia. |
| 7 | `S07_AuthorizedCorrectionPreservesAssignmentHistoryAndReason` | `HU-019`, `CA-019`, `CP-019-P` | Un superior autorizado corrige a candidato elegible con motivo; la anterior queda `SUSTITUIDA`, la nueva `VIGENTE`, ambas auditadas y enlazadas. |
| 8 | `S08_OneWeeklyPlanPublishesIncrementally` | `HU-020`, `CA-020`, `CP-020-P/N`; `HU-021`, `CA-021`, `CP-021-P` | Dos niveles convergen en un plan; publicación V1 respeta alcance y una obligación tardía produce V2 acumulada. |
| 9 | `S09_FullReplayCreatesNoDuplicateFacts` | `HU-013` a `HU-021`, porción aplicable de `HU-034`; `CP-013-N`, `CP-014-N`, `CP-015-N`, `CP-018-N`, `CP-020-N` | Repetir la cadena completa recupera las mismas identidades y conserva conteos, salvo nuevas auditorías de intentos expresamente permitidas por los contratos. |
| 10 | `S10_LateObligationCreatesPlanV2AndPreservesV1` | `HU-015`, `HU-020`, `HU-021`; `CA-015`, `CA-020`, `CA-021`, `CP-021-P` | La obligación tardía no sustituye otra obligación: se incorpora en V2 del mismo plan y V1 permanece histórica e inmutable. |
| 11 | `S11_OutOfHierarchyMutationIsRejectedWithoutPartialEffects` | `HU-019`, `CA-019`, `CP-019-N`; `HU-021`, `CA-021`, `CP-021-N` | `OUTSIDE-CV02` intenta corregir y publicar fuera de jerarquía; se rechaza sin cambios parciales y con auditoría del intento cuando corresponda. |
| 12 | `S12_PartialRecurringBatchResumesMissingWork` | `HU-013`, `CA-013`, `CP-013-N`; `HU-034`, `CA-034`; `TEC-JOB-001` | Una falla inyectada después de un commit aprobado deja el run fallido; la reejecución recupera lo confirmado, completa faltantes y no duplica. |
| 13 | `S13_DoesNotAutoPublishConcludeCreateEvidenceOrValidate` | límites de `HU-013`, `HU-020`, `HU-021`; exclusión de `HU-022` a `HU-028` | Tras generación y replay existen plan/obligaciones en estados aprobados, pero no hay publicación automática, conclusión, evidencia, validación ni hechos de `TAR-0026`. |

La matriz complementa, no sustituye, las pruebas existentes de cada HU. Toda fila negativa verifica código/resultado, ausencia de cambio de negocio, ausencia de recurso parcial, conservación histórica y auditoría permitida. Si al implementar una fila un contrato aceptado no permite producir u observar el resultado sin ampliar `src/`, esquema o semántica, se detiene esa fila y se solicita revisión explícita de esta adenda; no se agrega comportamiento de producto para hacer pasar la demo.

## 11. Falla parcial, repetición y concurrencia

El inyector de falla sólo existe en el ensamblado de demo y se conecta a una frontera ya inyectable o decorable de la orquestación de prueba. No altera código productivo ni simula una respuesta de dominio. La falla se produce después de confirmar una frontera real de la cadena recurrente y antes de la siguiente; se comprueba el estado PostgreSQL intermedio, el `scheduled_job_run` fallido y la recuperación posterior.

La repetición completa parte tanto de la misma base de una ejecución como de una segunda base limpia. La primera demuestra idempotencia; la segunda demuestra semilla y resultado reproducibles. Las pruebas de concurrencia ejecutan operaciones realmente simultáneas contra PostgreSQL, no sólo llamadas secuenciales, y comprueban unicidades, locks, ETag, una asignación vigente y un plan.

No se manipulan filas para fabricar la recuperación salvo el mecanismo de falla. No se marca manualmente un run como exitoso ni se inserta una identidad duplicada para luego ocultarla.

## 12. Interfaz, diseño y accesibilidad

La aprobación de esta adenda aprueba una interfaz visual exclusivamente dentro del host de demo. Antes de escribir Razor, CSS o marcado se leerán `docs/design/tokens.md`, `docs/design/componentes.md`, `docs/design/estados-y-mensajes.md`, `docs/design/estados-de-dominio.md` y `docs/design/accesibilidad.md`.

La demo copiará o referenciará de forma aislada los tokens aprobados sin modificar `src/Sgol.Web` y sin literales visuales fuera de la hoja que declare variables. Si falta un componente, estado o mensaje necesario, se detendrá la implementación; no se inventará.

Las tres páginas incluyen estado normal, foco, deshabilitado, error, cargando y vacío; navegación completa por teclado; foco visible; títulos, regiones, tablas y botones semánticos; etiquetas y errores asociados; texto más icono para estados; anuncios adecuados para cambios dinámicos; y contraste WCAG 2.2 AA. Se probarán viewport de escritorio y teléfono. Fechas y horas mostrarán explícitamente `America/Mexico_City` y UTC cuando ayude a reconciliar, sin depender de la zona del navegador. JSON, UUID, SQL o payload crudo no será el mensaje principal; los detalles técnicos sanitizados estarán en una región secundaria expandible.

La página de calendario presenta versiones, laborables/inhábiles y ventanas; recurrencias presenta resultados y reconciliación de identidades; plan semanal presenta obligación, asignación, corrección, versiones y alcance de publicación. Ninguna página permite editar datos fuera de sus escenarios cerrados.

## 13. Playwright, navegadores y compatibilidad

Se agregará una sola dependencia nueva centralizada y fijada:

```text
Microsoft.Playwright 1.62.0
```

La dependencia se usará como biblioteca desde el mismo proyecto; no se agrega runner adicional, Node/npm, axe, Selenium ni herramienta global. La versión `1.62.0` publicada por Microsoft tiene licencia MIT en el paquete .NET. Se incorporará a `Directory.Packages.props`, al `PackageReference` del proyecto y a su lockfile; restore bloqueado y `Assert-NoVulnerablePackages.ps1` revisarán dependencias directas y transitivas antes del cierre.

Los binarios fijados por esa versión serán Chromium `151.0.7922.34` y WebKit `26.5`. La automatización correrá headless en ambos: Chromium para escritorio y teléfono emulado, y WebKit con viewport/táctil de teléfono para compatibilidad equivalente al motor de Safari disponible. No se usa un navegador del sistema como autoridad reproducible.

Los binarios se instalarán una sola vez fuera de esta sesión mediante el `playwright.ps1` generado por el build, con `install chromium webkit`, en una ruta de artefactos local al workspace definida sólo para ese proceso. El wrapper de demo no descarga ni actualiza navegadores: verifica las revisiones exactas y falla con una instrucción sanitizada si faltan. No se modifica `HOME`, perfil, registro ni configuración persistente.

Playwright comprobará automáticamente rutas cerradas, antiforgery, semántica y nombres accesibles, asociación de etiquetas/errores, orden de tabulación, activación sólo con teclado, foco visible mediante estilo calculado, anuncios dinámicos, estados normal/cargando/vacío/error, ausencia de desbordamiento crítico y las vistas teléfono/escritorio. El contraste WCAG 2.2 AA se verificará contra los tokens aprobados y mediante revisión manual registrada; también se registrará una revisión manual en las versiones estables disponibles de Chrome, Edge y Safari móvil cuando el entorno de cierre las ofrezca. La versión efectiva de cada navegador manual quedará en la evidencia y un navegador no disponible se reportará como no verificado, nunca como aprobado.

Capturas, trazas y video se recolectan en temporal durante cada escenario y se conservan sólo si falla. En éxito se eliminan. No contienen cookies, conexiones ni payloads completos.

## 14. Evidencia reproducible y retención

Cada ejecución genera siempre:

```text
.artifacts/cv02/latest/TECH-E2E-CV-02-report.json
.artifacts/cv02/latest/TECH-E2E-CV-02-report.md
```

Ambos reportes contienen exclusivamente:

- `TECH-E2E-CV-02` y versión de esquema del reporte;
- HU/CA/CP/TEC cubiertos;
- commit exacto;
- sistema operativo, .NET, PostgreSQL, Playwright y navegador;
- fecha UTC de inicio/fin;
- identificador `CV02-SEED-V1` y huella SHA-256 de constantes sintéticas, no sus payloads completos;
- cada escenario, duración y `PASSED`/`FAILED`;
- rutas relativas de artefactos de falla;
- identificador de defecto relacionado cuando exista;
- resultado de limpieza de host, navegador y PostgreSQL.

Se excluyen contraseñas, TOTP, códigos de recuperación, cookies, tokens, cadenas de conexión, credenciales PostgreSQL, SQL, stack traces sin sanitizar, PII, nombres humanos, datos reales, payloads completos, contenido de auditoría completo, evidencia y validaciones. Los errores usan códigos allowlist y mensajes sanitizados.

`.artifacts/cv02/` permanece fuera de Git. Localmente se conserva sólo `latest`; el inicio siguiente elimina sus artefactos de éxito y rota un fallo anterior a una única carpeta `previous-failure`. En CI, si posteriormente se autoriza ejecutar la demo completa allí, el reporte y los artefactos de fallo tendrán retención exacta de 14 días. La evidencia de aprobación humana no se inventa ni se genera desde el harness.

## 15. Pruebas posteriores a la aprobación

El proyecto incluirá pruebas sin Docker para:

- parser de modos y rechazo de argumentos/configuración arbitrarios;
- binding exclusivo a loopback y puerto efímero;
- allowlist de rutas, métodos, escenarios y actores;
- guardas de conexión desechable y ausencia de cadenas en mensajes;
- determinismo de semilla, reloj, UUID y reporte;
- lector `AsNoTracking`, transacción `READ ONLY`, interceptor de escritura y huella sin cambios;
- sanitización de errores y artefactos;
- cierre ordenado y limpieza simulable de host/navegador/contenedor;
- dirección de dependencias, inclusión en `SGOL.slnx` y ausencia de referencias desde `src/`;
- ausencia de ruta HTTP productiva, cambio de migración, snapshot o modelo EF;
- reglas visuales, rutas y estructura accesible comprobables sin navegador.

La suite externa única con PostgreSQL/Testcontainers, Playwright y demo completa cubrirá:

- arranque desde base vacía y aplicación íntegra de migraciones;
- semilla y segunda ejecución limpia reproducibles;
- los 13 escenarios de la sección 10;
- idempotencia y concurrencia PostgreSQL real;
- Worker real, recurrencia laborable/inhábil y recuperación de caída parcial;
- autorización negativa sin efectos y auditoría correspondiente;
- historia de configuración, asignación y plan;
- ausencia de publicación automática, conclusión, evidencia y validación;
- Chromium/WebKit headless, teclado, foco, estados y viewport teléfono/escritorio;
- reporte sanitizado y eliminación de secretos/datos reales;
- cierre de Kestrel, navegadores, conexiones y PostgreSQL incluso tras falla.

No se ejecutará Docker, Testcontainers ni instalador de navegador dentro de la sesión de implementación. Al llegar al gate se solicitará al desarrollador una sola ejecución externa del comando canónico y se esperará su resultado completo. Persistencia, restricciones, transacciones, unicidad, concurrencia, recurrencias y recuperación no se considerarán verificadas por pruebas sin PostgreSQL real.

## 16. Participación en CI y gates finales

El PR ejecutará mediante `SGOL.slnx` restore, build, unitarias, arquitectura, formato, análisis de dependencias y los controles del harness que no necesitan Docker o navegador. La demo completa no se añadirá silenciosamente al pipeline actual: para este corte su gate es la ejecución externa única registrada. Incorporarla después al pipeline requerirá autorización separada de configuración de CI.

Después de implementar, los gates se ejecutarán una sola vez y en este orden:

1. `rtk dotnet restore SGOL.slnx --locked-mode`, fuera del aislamiento;
2. `rtk dotnet build SGOL.slnx --no-restore --configuration Release`;
3. suite unitaria y de arquitectura completa;
4. pruebas del proyecto `Sgol.Cv02Demo` filtradas para no requerir Docker ni navegador;
5. solicitud, espera y registro de una única ejecución externa de PostgreSQL/Testcontainers, Playwright y demo completa;
6. `rtk dotnet format SGOL.slnx --verify-no-changes --no-restore`;
7. `rtk powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\ci\Assert-NoVulnerablePackages.ps1`;
8. `rtk dotnet tool run dotnet-ef migrations has-pending-model-changes --project src/Sgol.Web --startup-project src/Sgol.Web --no-build --configuration Release`;
9. `rtk powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\ci\verify-fuentes-protection.ps1`;
10. después de terminar el anterior, `rtk powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\ci\verify-fuentes-mirror.ps1`;
11. `rtk git diff --check`.

Cada gate se reporta por separado. Un gate omitido, compuesto, pendiente o ejecutado sólo parcialmente queda `NO VERIFICADO` con causa. Los verificadores de `Fuentes/` nunca corren en paralelo.

## 17. Archivos previstos, cero cambios productivos y trazabilidad

Después de aprobar esta adenda, el cambio mínimo previsto queda limitado a:

- `tests/Sgol.Cv02Demo/**`;
- `scripts/demo/run-cv02.ps1` y documentación de instalación externa de navegadores;
- `SGOL.slnx`;
- `Directory.Packages.props` y lockfile del proyecto nuevo;
- pruebas de arquitectura directamente necesarias;
- `.gitignore` sólo para `.artifacts/cv02/` si aún no está cubierto;
- `docs/operations/cv02-demo.md`;
- `docs/traceability/README.md`;
- `docs/traceability/IMPLEMENTATION_STATUS.md` como propuesta en rama.

La expectativa contractual es cero migraciones, cero cambios en tablas, entidades, snapshot/modelo EF, endpoints, autorización, configuración o UI productivos, y cero cambios bajo `src/`. Si la implementación pareciera requerir cualquiera de ellos, se detiene y se solicita una revisión explícita de esta adenda.

`docs/design/logo.svg`, `docs/design/mapa-pantallas.md`, los tres `Designer.cs` indicados como cambios de metadatos y cualquier otro cambio ajeno permanecen intactos y fuera de staging. `Fuentes/` no se busca, abre, modifica ni usa como salida.

La trazabilidad añadirá una fila por escenario con su prueba y reporte, registrará la base aceptada de `HU-013` indicada en la sección 2 y marcará `TECH-E2E-CV-02` sólo como propuesta en rama. No se reescriben F00–F07 congelados ni se inicia `HU-023`.

## 18. Eficacia, cierre y autorizaciones independientes

La aprobación íntegra de esta adenda es la única decisión que permite comenzar el proyecto, los paquetes, los scripts, la interfaz, las pruebas y la trazabilidad de implementación. No declara `TECH-E2E-CV-02` ni `CV-02` terminados y no autoriza commit, publicación, pull request o merge.

`TECH-E2E-CV-02` sólo queda `Terminada` cuando el commit exacto que contiene implementación y trazabilidad tiene pipeline requerido verde, aprobación humana, merge en `master`, ascendencia verificada en `origin/master`, ejecución completa satisfactoria de la demo, todos los gates registrados y cero defectos bloqueantes.

`CV-02` sólo queda cerrado cuando, además, todas sus historias permanecen aceptadas y el gate de corte de F07 se satisface. Hasta entonces `HU-023` y `CV-03` continúan bloqueados.

## 19. Revisión aprobada por incompatibilidad E2E HU-018/HU-021

La ejecución externa de la demostración del 5 de septiembre de 2026 aprobó `S01` a `S07` y se detuvo en `S08` al intentar publicar una obligación asignada por el servicio real de `HU-018`. El hallazgo no corresponde a datos simulados ni a una carencia del harness:

- la sección 7 de `F07_ADENDA_08_CONTRATO_DE_ASIGNACION_HU_018.md` fija una forma exacta para `assignment_version.explanation` que contiene `eligibilityEvaluationId`, pero no `eligibilityPolicyVersionId`; además declara que la política permanece referenciada por esa evaluación;
- la sección 6 de `F07_ADENDA_11_CONTRATO_DE_PUBLICACION_DE_PLAN_HU_021.md` afirma que `eligibilityPolicyVersionId` fue aprobado en `HU-018/HU-019` y la implementación de publicación exige ese campo directamente;
- las pruebas aisladas de `HU-021` construyen manualmente una explicación con `eligibilityPolicyVersionId`, en vez de consumir una asignación automática real, por lo que no detectan la incompatibilidad;
- una asignación automática aceptada queda por ello rechazada como `CONTENIDO_PLAN_INCOMPATIBLE`, aunque su `eligibilityEvaluationId` conduce de forma inmutable a la política exacta persistida.

Se propone aprobar exclusivamente la siguiente corrección de interoperabilidad:

1. Se conserva sin cambios la forma exacta y la historia de la explicación automática de `HU-018`.
2. Para una asignación `AUTOMATICA`, `HU-021` obtiene `eligibilityEvaluationId` de la explicación, carga la evaluación persistida y exige que pertenezca a la misma obligación y que su `eligibility_policy_version_id` coincida con la política exacta de `work_obligation.task_definition_version_id`.
3. Para una asignación `CORRECCION`, se conserva la referencia directa `eligibilityPolicyVersionId` aprobada por `HU-019`; cuando también exista `eligibilityEvaluationId`, ambas referencias deben ser coherentes con la misma evaluación, obligación y política.
4. Una referencia ausente, mal formada, inexistente o incoherente continúa produciendo `409 CONTENIDO_PLAN_INCOMPATIBLE`, con rollback y sin efectos parciales.
5. La comprobación ocurre dentro de la transacción y bloqueos ya aprobados de publicación. No cambia el universo visible, la jerarquía, el alcance acumulado, la idempotencia, el ETag, la auditoría ni la forma de respuesta.
6. La corrección se limita a `EfPlanPublicationService`, sus pruebas de integración PostgreSQL y el escenario E2E existente. Debe añadirse una prueba que encadene evaluación real, asignación automática real y publicación real, además de negativas de evaluación/política incoherentes sin efectos.
7. Se mantienen cero migraciones, cero cambios de esquema o entidades, cero endpoints, cero cambios de autorización y cero UI productiva.

Esta revisión fue aprobada íntegramente el 5 de septiembre de 2026. La sección prevalece sólo sobre la afirmación incompatible de la sección 6 de la Adenda 11 y autoriza el cambio productivo mínimo descrito. No autoriza ninguna otra modificación bajo `src/`, ni commit, publicación, PR o merge. `TECH-E2E-CV-02`, el cierre de `CV-02` y el inicio de `HU-023` continúan sujetos a todos sus gates y condiciones restantes.
