# FRONT-011 — políticas de activación y elegibilidad por TAR

## Alcance y base

`FRONT-011` consume `HU-012`/`CAP-014`/`CA-012`/`CP-012-P/N` y `HU-017`/`CAP-021`/`CA-017`/`CP-017-P/N`. Parte del merge verificado de FRONT-010, PR #76, `2443e121c46238433adffd63bc905f8bfccfee2e`. La rama aislada es `codex/front-011`.

El checkout principal permaneció en `master`/HEAD `bed96907d26170502d09f4530495c3fd5894bdcc`, 47 commits detrás de `origin/master`, con cambios preexistentes en `AGENTS.md`, `scripts/demo/run-cv03.ps1` y `tests/Sgol.ArchitectureTests/Cv03DemoArchitectureTests.cs` preservados sin inspeccionar sus diffs. El worktree nuevo `C:\Users\josej\.codex\worktrees\front-011\SGOL` nació limpio de `origin/master` `2443e121c46238433adffd63bc905f8bfccfee2e`; no se reutilizó otro checkout de FRONT.

La extensión de `docs/design` y de lectura API se aprobó expresamente para esta tarea. UI-C06/C07 permanecen dentro del detalle TAR de `/configuracion`, sin ruta web nueva. Las dos lecturas `GET /api/v1/task-definitions/{taskCode}/activation-policy` y `GET /api/v1/task-definitions/{taskCode}/eligibility-policy` devuelven `taskCode`, `current`, `history` y un ETag fuerte cuando existe versión vigente. Cada lectura reautoriza en servidor a Dirección vigente de `LOR-001` y no expone datos a otros perfiles. Los PUT existentes conservan CSRF, `Idempotency-Key`, `If-Match`, historia, auditoría y reglas transaccionales. El PUT informa `meta.replayed` cuando recupera una intención previa; el recurso y la historia no incorporan ese indicador de transporte.

El contrato consumidor aprobado se registra en `F07_ADENDA_50_CONTRATO_CONSUMIDOR_FRONT_011.md`.

## Esquema consumidor cerrado

| TAR | Activación | Origen | Schedule | Rol de elegibilidad |
|---|---|---|---|---|
| TAR-0005 | `RECURRENTE` | `WORKING_DAY_WINDOW_V1` | `WORKING_DAY_WINDOWS`, días laborables, 12:00 y 17:00, `America/Mexico_City` | `SUBCOORDINACION` |
| TAR-0007 | `MANUAL` | `MANUAL_REFERENCE_V1` | `null` | `PISO_VENTAS` |
| TAR-0008 | `MANUAL` | `MANUAL_REFERENCE_V1` | `null` | `SUBCOORDINACION` |
| TAR-0011 | `MANUAL` | `MANUAL_REFERENCE_V1` | `null` | `SUBCOORDINACION` |
| TAR-0018 | `MANUAL` | `MANUAL_REFERENCE_V1` | `null` | `PISO_VENTAS` |
| TAR-0026 | `RECURRENTE` | `SERVICE_DUE_DATE_REFERENCE_V1` | `BUSINESS_DAYS_BEFORE_DUE_DATE`, 3 días hábiles antes, ajuste previo, hora local `HH:mm`, `America/Mexico_City` | `ADMINISTRACION` |
| TAR-0092 | `MANUAL` | `MANUAL_REFERENCE_V1` | `null` | `SUBCOORDINACION` |
| TAR-0093 | `MANUAL` | `MANUAL_REFERENCE_V1` | `null` | `SUBCOORDINACION` |

En las ocho TAR `requiresAvailability=true` y `requiredShift=null`. El editor no acepta modo, campo JSON, texto de puesto ni turno. El servidor valida otra vez la totalidad de estos datos; la versión TAR seleccionada debe corresponder exactamente a la release borrador o a su versión vigente aplicable. Una política guardada permanece en `BORRADOR` hasta publicación de la release por UI-C04. Las obligaciones ya creadas son inmutables.

## Mapa de pruebas

| Criterio | Prueba |
|---|---|
| CA-012 / CP-012-P/N: ocho mecanismos, manual, recurrente, schedule cerrado, rechazo de evento/exterior | `ActivationPolicyTests`; `ActivationPolicyPersistenceTests`; `Front011BrowserTests` |
| CA-017 / CP-017-P/N: rol exacto, disponibilidad, turno nulo y código no MVP | `EligibilityPolicyTests`; `EligibilityPolicyPersistenceTests`; `Front011BrowserTests` |
| Lectura, versión e historia, 401/403 y no filtración | `ActivationPolicyTests.GetEndpoint_RequiresSessionAndReturnsHistoryEnvelope`; `EligibilityPolicyTests.GetEndpoint_RequiresSessionAndReturnsHistoryEnvelope`; pruebas PostgreSQL de ambos servicios; `Front011BrowserTests` |
| Conflicto `412`, idempotencia, autorización, auditoría, no efecto y obligaciones anteriores | `ActivationPolicyPersistenceTests`; `EligibilityPolicyPersistenceTests`; `Front011BrowserTests` |
| Teclado, foco, escritorio, móvil, ancho de `html`/`body` y PNG sanitizado | `Front011BrowserTests` y categoría completa `FRONT_BROWSER` |

## Comandos locales

```powershell
rtk proxy pwsh -NoProfile -File scripts/ci/preflight.ps1
rtk proxy dotnet restore --locked-mode
rtk proxy dotnet build --no-restore --configuration Release
rtk proxy dotnet test tests/Sgol.UnitTests/Sgol.UnitTests.csproj --no-build --configuration Release --filter "FullyQualifiedName~ActivationPolicyTests|FullyQualifiedName~EligibilityPolicyTests"
rtk proxy dotnet test tests/Sgol.IntegrationTests/Sgol.IntegrationTests.csproj --no-build --configuration Release --filter "FullyQualifiedName~ActivationPolicyPersistenceTests|FullyQualifiedName~EligibilityPolicyPersistenceTests"
rtk proxy dotnet test tests/Sgol.ArchitectureTests/Sgol.ArchitectureTests.csproj --no-build --configuration Release --filter "FullyQualifiedName~Hu034IdempotencyArchitectureTests|FullyQualifiedName~VersioningConsumerTests"
rtk proxy dotnet test tests/Sgol.FrontendBrowserTests/Sgol.FrontendBrowserTests.csproj --no-build --configuration Release --filter "Category=FRONT_BROWSER"
rtk proxy dotnet format whitespace SGOL.slnx --verify-no-changes --no-restore --include <archivos C# modificados>
rtk git diff --check
```

Las capturas de escritorio y móvil usan datos sintéticos y máscara de todo el encabezado de sesión. Se guardan fuera del checkout en `C:\Users\josej\.codex\worktrees\front-011\front-011-evidence\` y no forman parte del cambio. `Hu034IdempotencyArchitectureTests` permanece en 19 consumidores: la lectura GET nueva no consume `Idempotency-Key`, y los PUT ya estaban inventariados.

## Resultado local y límite de publicación

`Publicada` para revisión mediante PR `#77`, después de la primera autorización expresa. El primer commit publicado fue `4b90cb2722d3a4a026eca8d5eb8f9f5ecb7afa`; este ajuste de trazabilidad crea una cabeza posterior que requiere su propio check verde. Se ejecutó una sola vez `dotnet restore --locked-mode` en el worktree nuevo. La compilación Release terminó sin errores ni advertencias; las pruebas enfocadas unitarias pasaron `23/23`, las de arquitectura `4/4` (incluido el inventario de 19 consumidores), las de servidor con PostgreSQL real `9/9` y la categoría completa `FRONT_BROWSER` `15/15` con Kestrel HTTPS, Chromium y WebKit. La prueba de navegador enfocada de FRONT-011 también pasó `1/1` tras corregir la espera del harness.

El primer pase completo de navegador con un helper que esperaba `DOMContentLoaded` después de recibir el POST terminó `14/15`: el POST de creación respondió `200`, pero esa espera genérica agotó el tiempo. La reproducción enfocada falló en la misma línea; se corrigió registrando el evento de carga antes del clic y se repitieron la prueba enfocada y la categoría completa con resultado verde. No se cambió el timeout, el certificado ni la fixture. Todas las capturas móviles son PNG de 390 px de ancho; cada captura comprobó que `html` y `body` no superan el viewport. Las capturas cubren catálogo/vacío, detalle e historia, borrador/vigente, editor y confirmación manual/recurrente, elegibilidad, replay, validación, 412 y perfiles denegados.

El formato dirigido (`dotnet format whitespace --verify-no-changes --no-restore --include` de los C# afectados) y `git diff --check` pasaron sin cambios ni errores. `origin/master` permanecía en `2443e121c46238433adffd63bc905f8bfccfee2e` al publicar; la rama contiene sólo FRONT-011 y la reconciliación documental de FRONT-010. El check remoto del SHA final y el merge tienen gates separados: el check se comprueba tras este ajuste, y el merge sólo procede con segunda autorización específica. `FRONT-012`, `FRONT-013` y `TECH-FRONT-005` siguen sin iniciarse.
