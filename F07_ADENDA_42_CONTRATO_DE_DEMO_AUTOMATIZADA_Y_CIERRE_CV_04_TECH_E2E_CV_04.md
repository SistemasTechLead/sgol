# SGOL — Adenda 42 a F07: contrato de demo automatizada y cierre de `CV-04` para `TECH-E2E-CV-04`

## 1. Control de la propuesta

| Campo | Valor |
|---|---|
| Tipo | Propuesta de adenda normativa a `F07_BACKLOG_DE_IMPLEMENTACION.md` |
| Estado | `APROBADA ÍNTEGRAMENTE` |
| Fecha | 2026-09-19 |
| Tarea propuesta | `TECH-E2E-CV-04 — Demo automatizada y cierre del corte CV-04` |
| Corte | `CV-04 — Validación y supervisión` |
| Base exacta verificada | `origin/master` en `10309937f596e5f6702181183b0cbaaa1b52f4c4` |
| Historias demostradas | `HU-027`, `HU-028` y `HU-031` |
| Criterios | `CA-027`, `CA-028`, `CA-031` y sus CP positivos y negativos |
| Aprobación | Aprobada íntegramente por el responsable el 2026-09-19 antes de modificar código |

Esta adenda formaliza exclusivamente el gate ausente de `CV-04`. No cambia los contratos funcionales aceptados de `HU-027`, `HU-028` o `HU-031`; no modifica F00–F07 congelados ni `Fuentes/`; y no autoriza `CV-05`, frontend, despliegue, merge ni datos reales.

La creación y revisión de este archivo no constituyen aprobación. Sólo una aprobación humana íntegra y explícita autoriza implementar el proyecto, script, pruebas, documentación, trazabilidad y participación en CI descritos aquí. Hasta recibirla no se modifica código.

## 2. Precedencia comprobada y eficacia

La comprobación previa confirmó:

1. `origin/master` resuelve exactamente a `10309937f596e5f6702181183b0cbaaa1b52f4c4`.
2. `TECH-AUTH-001` está integrada: commit implementado `8cb881f3192a534e9a101d169fc38fcb0f749fc2`, PR `#56` merged, pipeline `SUCCESS` run `35469361269` sobre ese commit y merge `10309937f596e5f6702181183b0cbaaa1b52f4c4`.
3. `TECH-E2E-CV-03` está integrada: commit `157bc43886b58a2e1a0c036dbda437114cf9f283`, PR `#46`, pipeline `SUCCESS` run `34514719087` y merge `437f1d875491097d208ca7fc3ad11d4094b26b81`.
4. `HU-027` está integrada: commit `72d4658136e8d2dfdfc30b4faac93658769b8db6`, PR `#47`, pipeline `SUCCESS` run `34525053653` y merge `c1fc6595de21095186a54d15617a4e07c4142be2`.
5. `HU-028` está integrada: commit `6fdc187c6430624f134ddfecf1592f7ccb167df8`, PR `#48`, pipeline `SUCCESS` run `34541989487` y merge `c87e6c94c5cd00eb3829d27619e16a2e4e6a3012`.
6. `HU-031` está integrada: commit `9b75e1c2bf1d1719f960fa1ed310a1ef40eb3275`, PR `#49`, pipeline `SUCCESS` run `34550189196` y merge `e47b6de6daa433ec0a62027c4e523931219086e5`.
7. Los cinco commits implementados y sus merges indicados son ancestros de la base exacta.
8. El lenguaje histórico de propuesta conservado en algunas secciones de `docs/traceability/IMPLEMENTATION_STATUS.md` no invalida los PR, checks, merges ni ascendencia efectivos.
9. No existe `F07_ADENDA_42_*`, rama remota `codex/tech-e2e-cv-04` ni PR previo con esa cabeza.
10. El checkout principal conserva cambios ajenos en `AGENTS.md`, `scripts/demo/run-cv03.ps1` y `tests/Sgol.ArchitectureTests/Cv03DemoArchitectureTests.cs`; la propuesta se prepara en un worktree limpio creado desde la base exacta y no incorpora esos archivos.

La aprobación íntegra inserta formalmente:

| ID | Resultado verificable | Dependencias | Ejecutar antes de | No incluye |
|---|---|---|---|---|
| `TECH-E2E-CV-04` | Demo automatizada, finita, reproducible y sanitizada de política, decisión versionada y supervisión jerárquica mediante PostgreSQL real, Kestrel HTTPS y autenticación hospedada real | `TECH-E2E-CV-03`, `HU-027`, `HU-028`, `HU-031` y `TECH-AUTH-001` integradas | Cierre formal de `CV-04` | `CV-05`, `HU-029`, `HU-032`, `HU-033`, `HU-034`, `HU-035`, frontend o comportamiento productivo nuevo |

La aprobación no cierra la tarea. `TECH-E2E-CV-04` sólo será `Terminada` y `CV-04` sólo será `Cerrado` después de implementación, gate integral satisfactorio, pipeline requerido verde sobre el SHA exacto, aprobación humana, merge commit, actualización no destructiva de `origin/master`, ascendencia verificada y protección de `Fuentes/`.

## 3. Fuentes y límite interpretativo

La propuesta se limita a `CV-04`, `HU-027`, `HU-028`, `HU-031`, `CA-027`, `CA-028`, `CA-031`, `RN-003`, `RN-006`, `RN-008`, `RN-020` a `RN-028`, las Adendas 24, 25, 26 y 41, `ADR-004`, `ADR-012`, `NFR-001`, `NFR-002`, `NFR-004`, `NFR-010`, y las implementaciones integradas que la demo consume.

Las Adendas 14 y 23 se usan sólo como precedentes estructurales para aislar un harness, cerrar comandos, usar datos sintéticos, sanear evidencia, limpiar recursos y condicionar el cierre. No autorizan copiar su matriz, UI, semilla, rutas, actores ni autenticación. En especial, el esquema de autenticación sintética de `TECH-E2E-CV-03` queda prohibido.

Ante una discrepancia entre este documento y un contrato funcional integrado, prevalece el contrato funcional. La implementación se detiene y la contradicción se presenta antes de tocar `src/`, persistencia o semántica.

## 4. Naturaleza y aislamiento del entregable

Se crea un proyecto no productivo e independiente:

```text
tests/Sgol.Cv04Demo/Sgol.Cv04Demo.csproj
```

`Sgol.Cv04Demo` será un ejecutable automatizado y proyecto xUnit. Se incluirá en `SGOL.slnx` bajo `/tests/` y podrá referenciar únicamente `src/Sgol.Web/Sgol.Web.csproj` y los contratos transitivos ya aceptados. No referenciará `Sgol.Cv02Demo`, `Sgol.Cv03Demo`, `Sgol.Worker` ni un proyecto de frontend. Ningún proyecto bajo `src/` podrá referenciarlo.

El proyecto reutiliza las versiones centralizadas existentes de `Microsoft.NET.Test.Sdk`, `Testcontainers`, `Testcontainers.PostgreSql`, `xunit` y `xunit.runner.visualstudio`. No se agrega paquete ni se modifica `Directory.Packages.props`.

El harness no se publica, despliega ni empaqueta como producto. No agrega endpoint, permiso, regla, tabla, columna, migración, índice, vista, cache, outbox, Worker, scheduler, broker, servicio o canal.

## 5. Comando, modo y precondiciones

El único comando canónico será:

```powershell
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\demo\run-cv04.ps1 -Mode Automated
```

El wrapper sólo acepta `-Mode Automated`. No acepta URL, puerto, conexión, credencial, certificado, imagen, actor, sucursal, TAR, fecha, UUID, semilla, timeout, reintento, archivo, payload ni destino arbitrario. No existe modo interactivo, `--keep-data`, reanudación ni reutilización de infraestructura.

Antes de iniciar PostgreSQL, el proceso ejecuta `PREFLIGHT` y comprueba de forma cerrada:

- .NET 10 y arquitectura AMD64 compatibles;
- Docker disponible para un contenedor desechable;
- ausencia de configuración externa de PostgreSQL, autenticación o Kestrel que pueda redirigir la corrida;
- existencia del ejecutable Release esperado y de la migración vigente `20260918001719_AddHostedAuthentication` como última migración integrada;
- catálogo exacto de escenarios, fases, rutas, métodos, roles y TAR;
- directorio de artefactos fuera de Git; y
- que ninguna referencia del proyecto apunte a CV-02, CV-03, frontend o código de prueba de identidad sintética.

Una precondición fallida termina antes de infraestructura costosa con exit no cero y código cerrado. No se diagnostica mediante reintentos o ampliación de límites.

## 6. Infraestructura real

La demo usa exclusivamente:

| Servicio | Versión o forma fijada | Uso |
|---|---|---|
| PostgreSQL | `postgres:18.6-alpine3.23` | Migraciones, identidad, autorización, políticas, obligaciones, evidencia, decisiones, auditoría, locks, concurrencia, idempotencia y consultas read-only |
| `Sgol.Web` | Kestrel del commit exacto | API productiva real sobre HTTPS loopback |
| TLS | Certificado autofirmado efímero | HTTPS real, validado por huella SHA-256 exacta por el cliente del harness |

PostgreSQL se crea desde base vacía, en contenedor nuevo, puerto aleatorio publicado sólo en loopback, red privada nueva, credenciales aleatorias en memoria y sin volumen persistente. Se aplican todas las migraciones integradas. El nombre de base usa el prefijo `sgol_cv04_` y debe coincidir exactamente con la identidad creada por la corrida.

Kestrel escucha sólo en `https://127.0.0.1:{puerto-aleatorio}`. El certificado y su contraseña se generan en runtime, viven en temporal privado, no se registran y se eliminan en `finally`. El cliente confía únicamente en la huella del certificado de esa corrida; no deshabilita globalmente la validación TLS.

No se usa SQLite, `TestServer`, `WebApplicationFactory` como transporte de aceptación, HTTP plano, base externa, S3, SeaweedFS, ClamAV, Redis, broker ni cloud. S3 y antimalware no son infraestructura necesaria para demostrar CV-04; la evidencia funcional requerida se aporta como evidencia estructurada sintética mediante las APIs y servicios reales ya aceptados de CV-03.

## 7. Autenticación hospedada real

Ningún `ClaimsPrincipal`, claim, cookie, desafío o marca MFA se fabrica manualmente. Cada actor obtiene autoridad sólo por el flujo hospedado de `TECH-AUTH-001` sobre HTTPS y PostgreSQL:

1. obtener `GET /api/v1/auth/csrf` y conservar la cookie antiforgery real;
2. ejecutar `POST /api/v1/auth/login` con cuenta sintética persistida y contraseña temporal generada en runtime;
3. ejecutar `POST /api/v1/auth/password/change` cuando `nextStep=CHANGE_PASSWORD`;
4. ejecutar `POST /api/v1/auth/mfa/enroll`, conservar el secreto únicamente en memoria y calcular el TOTP conforme a RFC 6238;
5. ejecutar `POST /api/v1/auth/mfa/confirm` o `POST /api/v1/auth/mfa/verify` según el estado;
6. descartar los recovery codes después de comprobar únicamente su conteo cerrado cuando el contrato los emita;
7. volver a obtener CSRF después del cambio de principal; y
8. comprobar `GET /api/v1/auth/session` antes de usar endpoints de negocio.

Se crean cuentas sintéticas reales separadas para `DIRECCION`, `ADMINISTRACION`, `SUBCOORDINACION` y `PISO_VENTAS`, con persona, empleo, rol canónico vigente, permisos contractuales y `LOR-001`. Dirección se provisiona mediante el bootstrap real; las otras cuentas se crean mediante las APIs administrativas reales y completan individualmente primer acceso y MFA.

Las contraseñas, secreto TOTP, códigos, cookies, CSRF, sello, certificado y conexión se generan en runtime y nunca se versionan ni aparecen en reportes. Las sesiones se mantienen en clientes separados; no se comparte cookie entre actores.

Las negativas demuestran que cookie ausente o alterada, CSRF ausente o inválido, preautenticación sin MFA y sesión invalidada bloquean sin efecto. La invalidación usa una operación hospedada real que rota el `SecurityStamp`; no se edita una cookie ni se fabrica un ticket. Para no consumir el rate limit compartido de loopback, el estado de bloqueo o invalidación se verifica en PostgreSQL o en fixtures aislados, no mediante solicitudes adicionales. No se aumentan límites, reintentos ni timeouts.

## 8. Semilla sintética y creación de precondiciones

La semilla estable `CV04-SEED-V1` crea sólo datos sintéticos en `LOR-001`:

- `DIR-CV04`, rol `DIRECCION`;
- `ADMIN-A-CV04` y `ADMIN-B-CV04`, roles `ADMINISTRACION` para comprobar autoridad ordinaria y par;
- `SUB-A-CV04` y `SUB-B-CV04`, roles `SUBCOORDINACION` para comprobar inferior, par y supervisión;
- `PISO-A-CV04` y `PISO-B-CV04`, roles `PISO_VENTAS` para ejecución, alcance e IDOR; y
- un actor sintético sin alcance efectivo sobre el recurso, usado sólo donde el contrato permite comprobar convergencia sin revelar su existencia.

Fechas UTC, semana ISO, UUID, claves idempotentes, ETag, textos permitidos, referencias y motivos son constantes versionadas. Contraseñas, TOTP, recovery codes, cookies, CSRF, conexión y certificado son deliberadamente efímeros y no forman parte de la huella reproducible.

El fixture puede crear datos base que no son el resultado demostrado. No puede insertar directamente:

- una `validation_policy_version` que un escenario afirme configurar o publicar;
- un `validation_requirement` que el escenario afirme materializar;
- una `validation_decision_version`, sustitución o estado vigente;
- una auditoría de éxito o rechazo que el escenario afirme producir; ni
- un resultado o transición de ejecución que el escenario afirme observar.

Las ocho políticas se configuran y publican mediante las APIs y servicios reales de `HU-027`. Las obligaciones, asignaciones, evidencia estructurada vigente y conclusiones necesarias se crean mediante capacidades reales aceptadas de CV-03. Emisión, sustitución, supervisión, detalle, evidencia e historial atraviesan las rutas productivas de `HU-028` y `HU-031`.

El fixture directo queda limitado a identidades, catálogos y condiciones basales imposibles de crear por una API aprobada, siempre antes del escenario y sin atribuir su inserción como resultado funcional.

## 9. Matriz cerrada de escenarios

Cada nombre es estable en pruebas y reportes. Toda fila negativa comprueba código o estado cerrado, ausencia de cambio funcional, ausencia de recurso parcial, conservación de historia e inexistencia de auditoría de éxito falsa.

| ID | Escenario estable | Cobertura | Resultado verificable |
|---|---|---|---|
| `S01` | `EightTarPoliciesResolveCanonicalImmediateSuperior` | `HU-027`, `CA-027`, `RN-020` | Las ocho TAR publican y congelan exactamente la relación `SUPERIOR_INMEDIATO`, executor y validator canónicos aprobados. |
| `S02` | `TextualPositionPeerAndLowerRoleGrantNoAuthority` | `HU-027`, `CA-027`, `CP-027-N`, `RN-003` | Puesto textual, Administración par, Subcoordinación par y rol inferior no configuran ni ejercen autoridad; no hay efecto ni éxito auditado. |
| `S03` | `OrdinarySuperiorIssuesFulfilledDecision` | `HU-028`, `CA-028`, `RN-020` a `RN-022` | Superior inmediato emite `CUMPLIDA` sobre obligación concluida con evidencia vigente; queda una decisión vigente. |
| `S04` | `OrdinarySuperiorIssuesIncompleteDecision` | `HU-028`, `CA-028`, `RN-022` | `INCOMPLETA` se persiste como resultado contractual único y la ejecución permanece `CONCLUIDA`. |
| `S05` | `OrdinarySuperiorIssuesNotFulfilledDecision` | `HU-028`, `CA-028`, `CPE-002`, `RN-022`, `RN-024` | `NO_CUMPLIDA` no reabre, cancela ni crea otra obligación; la ejecución permanece `CONCLUIDA`. |
| `S06` | `MotivatedReplacementPreservesBothVersions` | `HU-028`, `CP-028-P`, `RN-023`, `RN-027` | Sustitución autorizada y motivada conserva la anterior `SUSTITUIDA`, crea una sola `VIGENTE` y enlaza ambos snapshots. |
| `S07` | `SecondDirectDecisionHasNoEffect` | `HU-028`, `CP-028-N` | Otra emisión directa con clave distinta devuelve `DECISION_VALIDACION_YA_EXISTE`; no crea requisito, decisión, snapshot, idempotencia ni auditoría de éxito. |
| `S08` | `UnknownResultHasNoEffect` | `HU-028`, `CP-028-N` | Resultado fuera de `CUMPLIDA`, `INCOMPLETA`, `NO_CUMPLIDA` devuelve `RESULTADO_VALIDACION_INVALIDO` sin efecto. |
| `S09` | `NonDirectionSelfValidationHasNoEffect` | `HU-028`, `CP-028-N`, `RN-021` | Autovalidación de Administración, Subcoordinación o Piso devuelve `AUTOVALIDACION_NO_PERMITIDA` y no altera estado ni auditoría de éxito. |
| `S10` | `DirectionSelfValidationIsExceptionalAndAudited` | `HU-028`, `RN-021`, `NFR-004` | Dirección valida su obligación por `AUTOVALIDACION_DIRECCION`, con evento específico y sin ampliar la excepción a otro rol. |
| `S11` | `AdministrationSeesOnlySubcoordinationAndFloor` | `HU-031`, `CA-031`, `CP-031-P/N`, `RN-025` | Administración observa Subcoordinación y Piso, evidencia y validaciones permitidas; no Dirección, Administración par, propia ni otra sucursal. |
| `S12` | `SubcoordinationSeesOnlyFloor` | `HU-031`, `CA-031`, `RN-025` | Subcoordinación observa únicamente Piso; no Administración, Dirección, pares, propia ni otra sucursal. |
| `S13` | `FloorObtainsNoSupervision` | `HU-031`, `CP-031-N` | Piso recibe `403 ACCESO_DENEGADO` y no obtiene colección ni detalle de otros actores. |
| `S14` | `FiltersNeverExpandHierarchicalScope` | `HU-031`, `RN-025`, `NFR-002` | Filtros por persona, nivel y semana se combinan con `AND`; un valor válido fuera de alcance produce página vacía y nunca amplía el universo. |
| `S15` | `DetailEvidenceDecisionAndHistoryConvergeAntiIdor` | `HU-028`, `HU-031`, `RN-027` | Un actor fuera de alcance no distingue obligación, evidencia, decisión o historial existente de uno inexistente; los recursos no aparecen. |
| `S16` | `PendingValidationsAreExactAndReadOnly` | `HU-031`, `CA-031`, `RN-020`, `RN-025` | Pendiente materializada y derivada aparecen exactamente; ejecución pendiente, política nula, decisión vigente, requisito resuelto y fuera de alcance no aparecen; el GET no escribe. |
| `S17` | `AuditIsTransactionalAndRejectionsHaveNoFalseSuccess` | `HU-027`, `HU-028`, `RN-028`, `NFR-004` | Éxitos y cambios críticos confirman auditoría en su transacción; fallos revierten; rechazos no producen evento `SUCCESS` falso ni mutación parcial. |
| `S18` | `ConcurrentInitialDecisionsHaveOneWinner` | `HU-028`, `RN-022`, `RN-027` | Dos decisiones concurrentes con el mismo ETag dejan exactamente una vigente; la perdedora queda sin efecto con conflicto contractual. |
| `S19` | `ConcurrentReplacementsHaveOneWinner` | `HU-028`, `RN-023`, `RN-027` | Dos sustituciones concurrentes con el mismo ETag dejan una sola sucesora vigente y una cadena histórica coherente. |
| `S20` | `MissingOrTamperedCookieBlocksWithoutEffect` | `TECH-AUTH-001`, `ADR-012` | Cookie ausente o alterada devuelve rechazo de autenticación antes del servicio; no hay mutación funcional ni auditoría de éxito. |
| `S21` | `CsrfFailureBlocksEveryMutationWithoutEffect` | `TECH-AUTH-001`, `ADR-012` | CSRF ausente o inválido bloquea política, decisión y sustitución sin cambiar datos funcionales, idempotencia ni auditoría de éxito. |
| `S22` | `IncompleteMfaAndInvalidatedSessionBlockWithoutEffect` | `TECH-AUTH-001`, `ADR-004` | Preautenticación sin MFA y cookie invalidada por rotación real de sello no acceden a negocio ni producen efecto. |
| `S23` | `SecondRunReproducesSameFunctionalFingerprint` | `TECH-E2E-CV-04` | Una segunda corrida completa usa infraestructura e identidades nuevas y reproduce escenarios, estados y conteos cerrados, excluidos secretos efímeros. |
| `S24` | `CleanupLeavesNoOwnedResource` | `TECH-E2E-CV-04` | Tras éxito y tras una falla controlada de infraestructura no quedan host, proceso, conexión, certificado, temporal, contenedor, red, volumen ni base propiedad de la corrida. |

La matriz es finita. No se agrega, elimina, fusiona o relaja un escenario durante la implementación sin revisar y aprobar esta adenda. Si un escenario requiere cambiar `src/`, esquema o semántica, se detiene y se presenta la brecha contractual.

## 10. Fases y diagnóstico sanitizado

Toda ejecución usa únicamente estas fases cerradas:

```text
PREFLIGHT
POSTGRESQL
HTTPS
CSRF
LOGIN
PASSWORD_CHANGE
MFA_ENROLL
MFA_VERIFY
SEED
POLICY
VALIDATION
REPLACEMENT
SUPERVISION
AUTHORIZATION
AUDIT
REPORT
CLEANUP
```

Cada evidencia pública contiene exclusivamente:

```text
phase: código cerrado
scenario: S01..S24 o NONE
exit: entero
code: código cerrado allowlist CV04_*
counts: mapa cerrado de enteros no negativos
durationMs: entero no negativo
state: PASSED|FAILED|NOT_APPLICABLE
```

No contiene mensajes originales, excepciones, stack traces, SQL, payloads, cuerpos HTTP, respuestas sensibles, rutas físicas, nombres de proceso libres, contraseñas, secretos TOTP, recovery codes, cookies, tokens CSRF, hashes privados, sellos, conexión, certificado, URL privada, UUID de personas o recursos, ni contenido de evidencia.

Los códigos mínimos incluyen `CV04_PRECONDITION_FAILED`, `CV04_POSTGRESQL_START_FAILED`, `CV04_HTTPS_START_FAILED`, `CV04_HTTP_CONTRACT_FAILED`, `CV04_DATABASE_CONTRACT_FAILED`, `CV04_SCENARIO_FAILED`, `CV04_REPORT_FAILED`, `CV04_CLEANUP_FAILED`, `CV04_UNEXPECTED_FAILURE` y `CV04_EXECUTION_PASSED`. Un valor no allowlist se reduce a `CV04_UNEXPECTED_FAILURE`.

Se captura inmediatamente el exit de cada proceso nativo. No se consulta `$LASTEXITCODE` después de ejecutar otra operación. Los resultados de procesos, escenario, reporte y cleanup se conservan por separado.

## 11. Limpieza y semántica de exit

La limpieza ocurre en `finally` y en orden inverso:

1. cancelar solicitudes y cerrar clientes HTTP;
2. detener Kestrel y capturar su exit;
3. cerrar contextos, conexiones y lectores PostgreSQL;
4. eliminar certificado, contraseña temporal y archivos privados;
5. detener y eliminar contenedor PostgreSQL;
6. eliminar red y cualquier volumen accidental propiedad de la corrida; y
7. comprobar por identidad exacta que no queda recurso propiedad de la corrida.

Un fallo de escenario conserva su código primario aunque también falle cleanup; el reporte registra además `CV04_CLEANUP_FAILED` como fallo secundario cerrado. Cleanup nunca reemplaza u oculta la causa primaria.

No se emite `PASS` antes de comprobar escenario, reporte y limpieza. Una corrida sin fallo primario pero con cleanup fallido nunca se declara exitosa; termina con `CV04_CLEANUP_FAILED`. Así cleanup no convierte un `PASS` ya emitido en rojo: el éxito sólo existe después de verificarlo. Todo fallo de cleanup es bloqueante.

El proceso devuelve `0` únicamente después de dos ciclos completos, reporte válido, sanitización aprobada y cleanup verificado. Cualquier otro estado devuelve exit no cero. No se usan reintentos, esperas o timeouts mayores para ocultar errores.

## 12. Reportes y retención

Cada ejecución crea exclusivamente:

```text
.artifacts/cv04/latest/TECH-E2E-CV-04-report.json
.artifacts/cv04/latest/TECH-E2E-CV-04-report.md
```

El JSON usa `schemaId: sgol.tech-e2e-cv04.report` y `schemaVersion: 1`. Ambos reportes contienen:

- tarea, corte y commit Git exacto;
- sistema operativo, arquitectura y versión .NET;
- imagen exacta de PostgreSQL;
- instante UTC de inicio y fin y duración total;
- identificador `CV04-SEED-V1` y SHA-256 de las constantes públicas de semilla, nunca de secretos;
- fases y `S01` a `S24` con estado, duración, código cerrado y conteos cerrados;
- huella funcional no reversible del resultado de ambos ciclos;
- `browser=NOT_APPLICABLE` y `accessibility=NOT_APPLICABLE`;
- estado separado de reporte y cleanup; y
- estado final `PASSED` o `FAILED`.

Al iniciar, un `latest` exitoso anterior se elimina. Un fallo anterior puede rotarse a una única carpeta `.artifacts/cv04/previous-failure/`, sustituyendo la anterior. Se conservan como máximo el resultado actual y un fallo anterior. `.artifacts/cv04/` permanece ignorado por Git.

En CI se publica sólo el JSON y Markdown sanitizados, con retención de 14 días. No se suben logs crudos, TRX del gate integral, temporales, certificados ni artefactos privados.

## 13. Interfaz, navegador y accesibilidad

No existe una UI aprobada para `CV-04`. Por ello:

- no se lee ni modifica `docs/design`;
- no se crea Razor, HTML, CSS, JavaScript, SPA, ruta de página ni recurso estático;
- no se instala ni ejecuta Playwright o navegador; y
- navegador, teclado, foco, contraste, viewport y accesibilidad se reportan `NO APLICA`, nunca `PASSED`.

Una futura UI requiere tarea, contrato y diseño aprobados separados. Esta adenda no los anticipa.

## 14. Pruebas puras previas al smoke

Antes de iniciar infraestructura se implementan y ejecutan pruebas puras que demuestran:

- parser que sólo acepta `Automated` y rechaza configuración arbitraria;
- proyecto aislado, referencias cerradas y ausencia de referencias desde `src/`;
- catálogo exacto `S01` a `S24`, fases, actores, roles, ocho TAR, rutas y métodos;
- imposibilidad de usar el esquema de autenticación sintética de CV-03 o construir manualmente un principal;
- determinismo y agotamiento de reloj, UUID e identificadores públicos de semilla;
- allowlist y sanitización adversarial de diagnóstico y reportes;
- exclusión de campos sensibles, mensajes, excepciones, payloads y rutas;
- captura inmediata y composición correcta de exits nativos;
- preservación de fallo primario y bloqueo por cleanup;
- `PASS` sólo después de reporte y limpieza;
- dos ciclos nuevos con huella funcional comparable;
- coordinadores sustituibles para orden de infraestructura y cleanup, sin simular resultados funcionales; y
- ausencia de UI, Playwright, cambios productivos, migraciones, paquetes y referencias a CV-02/CV-03.

Estas pruebas no sustituyen PostgreSQL, Kestrel, TLS, cookies, CSRF, TOTP ni concurrencia reales.

## 15. Validación enfocada e integral

Después de la aprobación y cuando exista una razón concreta para considerar listo cada nivel, se ejecuta una sola vez en este orden:

1. sintaxis de PowerShell y pruebas puras del harness, script, catálogo, reporte, exits y diagnóstico;
2. build Release sólo de `Sgol.Cv04Demo` y `Sgol.ArchitectureTests`;
3. pruebas unitarias/componentes filtradas existentes de política, decisión, sustitución, jerarquía, autorización y autenticación;
4. PostgreSQL/Testcontainers enfocado existente para migraciones, constraints, índices, locks, decisión vigente única, sustitución concurrente, auditoría y no-efecto;
5. smoke Kestrel HTTPS con autenticación hospedada y PostgreSQL real;
6. gate integral `TECH-E2E-CV-04` mediante el comando canónico, exactamente una vez localmente;
7. `dotnet build SGOL.slnx --no-restore --configuration Release`;
8. validadores de rutas, secretos, migraciones, `Fuentes/`, formato aplicable y `git diff --check`; y
9. sólo después de aprobar todo lo anterior, commit, push, PR y un pipeline remoto sobre el SHA exacto.

No se ejecutan suites completas durante diagnóstico. Un fallo compatible de build, prueba enfocada, smoke o gate bloquea la publicación. Una limitación real se registra con causa exacta y no se presenta como éxito.

## 16. Participación en CI y SHA exacto

El workflow requerido `.github/workflows/pull-request.yml` agregará un único step nombrado `TECH-E2E-CV-04 / integral hosted demo` después de restore, build y pruebas y antes de los gates operativos más costosos. El step:

1. comprueba `git rev-parse HEAD == IMPLEMENTATION_SHA`;
2. exige Linux AMD64 y Docker disponibles;
3. ejecuta el comando canónico sin parámetros extra;
4. captura inmediatamente el exit del proceso;
5. valida que el reporte declara el mismo SHA y `PASSED`;
6. falla si falta reporte, escenario, segundo ciclo o cleanup; y
7. no imprime ni sube salida privada.

El step de artefactos existente se ampliará sólo para los dos reportes sanitizados de `.artifacts/cv04/latest/`, con retención de 14 días. El gate se ejecuta una vez por run. No se agrega `workflow_dispatch`, rerun automático, matriz, reintento ni excepción por rama.

El check requerido `TECH-BASE-003 / PR gates` sólo acredita esta tarea cuando su `headSha` coincide exactamente con la cabeza del PR. Un run verde sobre otro SHA, un rerun después de cambiar la cabeza o una suma de resultados históricos no sirve.

## 17. Archivos previstos después de la aprobación

La implementación queda limitada a:

- `tests/Sgol.Cv04Demo/**`;
- `scripts/demo/run-cv04.ps1`;
- `tests/Sgol.ArchitectureTests/Cv04DemoArchitectureTests.cs`;
- `SGOL.slnx`;
- `.gitignore` sólo para `.artifacts/cv04/`;
- `.github/workflows/pull-request.yml` sólo para el step y los dos reportes de la sección 16;
- `docs/operations/cv04-demo.md`;
- `docs/traceability/README.md`;
- `docs/traceability/IMPLEMENTATION_STATUS.md`; y
- esta Adenda 42.

No se prevén cambios en `src/**`, migraciones, `Directory.Packages.props`, lockfiles de proyectos existentes, `tests/Sgol.Cv02Demo/**`, `tests/Sgol.Cv03Demo/**`, `scripts/demo/run-cv03.ps1`, `tests/Sgol.ArchitectureTests/Cv03DemoArchitectureTests.cs`, `AGENTS.md`, documentos F00–F07 congelados ni `Fuentes/**`.

Una necesidad de cambiar una ruta fuera de esta lista detiene la implementación y exige decisión explícita. Un defecto productivo reproducible tampoco autoriza corregirlo dentro de esta tarea.

## 18. Límites expresos

`TECH-E2E-CV-04` no puede:

- cambiar contrato, DTO, error, permiso, jerarquía, estado o semántica de `HU-027`, `HU-028`, `HU-031` o `TECH-AUTH-001`;
- implementar o probar como objetivo `CV-05`, `HU-029`, `HU-032`, `HU-033`, `HU-034` o `HU-035`;
- agregar frontend, Razor, SPA, UI, Playwright o diseño;
- agregar endpoint, migración, tabla, índice, paquete o dependencia productiva;
- usar autenticación sintética, header mágico, JWT, claim fabricado o bypass;
- usar SQLite, HTTP plano, base externa, Redis, broker, microservicio, OAuth/OIDC, SSO o cloud;
- ampliar rate limit, timeout, reintentos, permisos, roles o sucursales;
- usar datos o secretos reales;
- conservar infraestructura o temporales; ni
- tocar, enumerar recursivamente o usar `Fuentes/` como salida.

Las regresiones de historias fuera de alcance que ejecute el pipeline permanecen como protección, no como funcionalidad de esta tarea.

## 19. Trazabilidad, publicación y cierre

Después de implementar, el mismo cambio registrará en `docs/traceability/IMPLEMENTATION_STATUS.md` el estado `Implementada localmente`, comando, escenarios, autenticación real, infraestructura, resultados, SHA, reportes, cleanup, límites y validaciones diferidas. No declarará `Publicada`, `Integrada`, `Terminada` o `CV-04 Cerrado` antes de la evidencia correspondiente.

Antes de cada commit y push se mostrará `git status`, se enumerarán rutas exactas, se ejecutará `git diff --check`, se confirmará que los tres archivos ajenos no están incluidos, que no se incluyeron versiones antiguas de CV-02/CV-03 y que `Fuentes/` no cambió. El staging será por rutas explícitas; no se usará `git add .` ni `git add -A`.

La autorización de este prompt, después de aprobar la adenda, permite commits locales coherentes, push sólo a `codex/tech-e2e-cv-04`, PR exclusivo hacia `master` y checks requeridos. No permite force-push, modificar otros PR, merge, despliegue ni recursos cloud.

Con PR exclusivo, demo integral verde, check requerido verde sobre el SHA exacto y cero defectos bloqueantes, el proceso se detiene antes del merge y solicita autorización explícita. Sólo una autorización posterior permite merge commit sin squash. Después se actualiza `origin/master` de forma no destructiva y se demuestra que el commit implementado es ancestro antes de declarar `TECH-E2E-CV-04 = Integrada/Terminada` y `CV-04 = Cerrado/Terminado`.

No se inicia `CV-05` ni frontend al cerrar esta tarea.

## 20. Decisión íntegra solicitada

Se solicita aprobar o rechazar esta Adenda 42 como una unidad. Una aprobación parcial, condicionada, el nombre del archivo, su revisión o la autorización para editar no permiten iniciar la implementación.

La pregunta de aprobación es:

> ¿Apruebas íntegramente `F07_ADENDA_42_CONTRATO_DE_DEMO_AUTOMATIZADA_Y_CIERRE_CV_04_TECH_E2E_CV_04.md`, sin cambios, para autorizar la implementación, validación, publicación y apertura del PR de `TECH-E2E-CV-04` bajo este contrato, manteniendo el merge sujeto a una autorización posterior explícita?
