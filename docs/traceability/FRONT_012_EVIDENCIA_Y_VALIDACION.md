# FRONT-012 — políticas de evidencia y validación por TAR

## Alcance y base

`FRONT-012` consume `HU-024`/`CAP-029`/`CA-024`/`CP-024-P/N` y `HU-027`/`CAP-032`/`CA-027`/`CP-027-P/N`, con `F05-JP-001..008`, Adendas 44, 45 y 46 y F06. La rama `codex/front-012` nació limpia de `origin/master` `8e34e9ab30038ea3bc8ef9c8176f9b4018fe4b7f` en un worktree nuevo. El checkout principal `bed96907d26170502d09f4530495c3fd5894bdcc` y sus tres cambios preexistentes no se tocaron.

`FRONT-011` está integrada: PR #77 merged; cabeza `fe3570d4a34bb7e74d2e25fd4eb92071d550d12f`, check `TECH-BASE-003 / PR gates` `SUCCESS` en run `36257731939` para esa cabeza y merge `8e34e9ab30038ea3bc8ef9c8176f9b4018fe4b7f`. La cabeza y el merge fueron verificados como ancestros de `origin/master` al iniciar esta tarea. Sus pruebas locales permanecen como evidencia histórica en `FRONT_011_POLITICAS_TAR.md`.

El responsable aprobó durante FRONT-012 la extensión mínima de `docs/design` para UI-C08/C09, que cierra la brecha consumidora BR-D09, y dos GET autorizados sobre las rutas ya usadas por los PUT. BR-D06/UI-C05 se cerró en FRONT-010 y no se modifica. El detalle TAR de FRONT-010 no entregaba historia completa de evidencia, borradores de validación ni ETag individual de lectura. Los nuevos GET `/api/v1/task-definitions/{taskCode}/evidence-policy` y `/api/v1/task-definitions/{taskCode}/validation-policy` devuelven `taskCode`, `current`, `history` y ETag fuerte de la versión vigente. La lectura se autoriza otra vez en servidor para Dirección vigente de `LOR-001`; un código no MVP recibe 404 convergente. Los PUT conservan CSRF, `Idempotency-Key`, `If-Match`, idempotencia transaccional, auditoría e historia; `meta.replayed` informa recuperación sin añadir ese dato al recurso.

## Contrato consumidor

UI-C08/C09 son secciones del detalle TAR existente en `/configuracion?taskCode=...`. La query admite sólo las ocho TAR canónicas y el shell no añade rutas API como enlaces. La proyección de sesión contiene `PER-EVIDENCIA-CONFIG` y `PER-VALIDACION-CONFIG` sólo para Dirección; presentar u ocultar controles no sustituye la autorización del servidor. Cada política tiene lectura, historia, vacío, error, confirmación, conflicto y recarga independientes. Un 412 bloquea sólo el editor afectado hasta un nuevo GET iniciado por la persona; no hay retry ni sobrescritura automática.

El editor de evidencia envía requisitos del catálogo `EvidencePolicyCatalog` en orden canónico, con tipo y condición exactos. Hay 27 requisitos entre las ocho TAR; sólo `FOTO_DIFERENCIA_DANO` de `TAR-0092` usa `DIFERENCIA_O_DANO`. Los códigos, el tipo y la condición son de sólo lectura. El editor de validación usa `ValidationPolicyCatalog`: ejecutor/validador exactos, relación `SUPERIOR_INMEDIATO`, obligatoriedad y `CUMPLIDA`, `INCOMPLETA`, `NO_CUMPLIDA`. Puesto textual, par, rol inferior y resultado ajeno no se transforman en autoridad. La selección disponible al usuario es una release `BORRADOR`; la publicación permanece en UI-C04 y la versión de obligaciones ya creadas no cambia.

## Mapa de pruebas

| Criterio | Evidencia |
|---|---|
| Ocho TAR; requisitos ordenados, tipos, condición y faltantes/duplicados/extraños | `EvidencePolicyTests`, `EvidencePolicyPersistenceTests`, `Front012BrowserTests` |
| Matriz ejecutor/validador, superior inmediato, resultados cerrados, puesto/par/rol inferior | `ValidationPolicyTests`, `EvidencePolicyPersistenceTests`, `Front012BrowserTests` |
| Lecturas GET, versión/historia/borrador, ETag, 401/403 y no filtración | pruebas GET de `EvidencePolicyTests` y `ValidationPolicyTests`; persistencia PostgreSQL; `Front012BrowserTests` |
| Autorización, alcance, concurrencia, idempotencia, auditoría, snapshot inmutable y no efecto | `EvidencePolicyPersistenceTests`, `ValidationPolicyPersistenceTests`; inventario `Hu034IdempotencyArchitectureTests` |
| Teclado, foco, confirmación, recarga 412, escritorio/móvil, ancho de `html` y `body`, PNG sintéticos sanitizados | `Front012BrowserTests`; categoría serializada completa `FRONT_BROWSER` |

## Comandos locales

```powershell
./scripts/ci/preflight.ps1
rtk proxy git fetch origin refs/heads/master:refs/remotes/origin/master
rtk proxy dotnet restore --locked-mode
rtk proxy dotnet build --no-restore --configuration Release
rtk proxy dotnet test tests/Sgol.UnitTests/Sgol.UnitTests.csproj --no-build --configuration Release --filter "FullyQualifiedName~EvidencePolicyTests|FullyQualifiedName~ValidationPolicyTests"
rtk proxy dotnet test tests/Sgol.IntegrationTests/Sgol.IntegrationTests.csproj --no-build --configuration Release --filter "FullyQualifiedName~EvidencePolicyPersistenceTests"
rtk proxy dotnet test tests/Sgol.ArchitectureTests/Sgol.ArchitectureTests.csproj --no-build --configuration Release --filter "FullyQualifiedName~EvidencePolicyArchitectureTests|FullyQualifiedName~ValidationPolicyArchitectureTests|FullyQualifiedName~Hu034IdempotencyArchitectureTests"
rtk proxy dotnet test tests/Sgol.FrontendBrowserTests/Sgol.FrontendBrowserTests.csproj --no-build --configuration Release --filter "Category=FRONT_BROWSER"
rtk proxy dotnet format whitespace SGOL.slnx --verify-no-changes --no-restore --include <archivos C# modificados>
rtk proxy git diff --check
```

Los dos PUT ya estaban en el inventario de 19 consumidores de `Idempotency-Key`; los GET no consumen esa cabecera, por lo que no cambia el conteo. Las capturas de 1440 px y 390 px en Chromium/WebKit usan datos sintéticos, enmascaran todo el encabezado de sesión y se guardan fuera del checkout en `C:\Users\josej\.codex\worktrees\front-012\front-012-evidence\`. Cubren catálogo, versión e historia, condición `DIFERENCIA_O_DANO`, matriz, borrador, confirmaciones, replay, conflicto 412, vacío, error asociado de release y perfil denegado. Cada captura comprueba el ancho de `html` y `body` y las dimensiones PNG; no se guardan cookies, CSRF, contraseñas ni TOTP.

## Resultado local y publicación

Una sola restauración locked y build Release terminaron sin errores ni advertencias. Pruebas unitarias de ambas políticas: `30/30`. Arquitectura afectada, incluido inventario de idempotencia: `8/8`. Persistencia PostgreSQL enfocada: `9/9` tras repetir un caso que inicialmente no arrancó por `DockerApiException InternalServerError` al inspeccionar el contenedor. La repetición enfocada del caso pasó `1/1`, sin cambiar código, espera ni fixture. Los dos recorridos nuevos de navegador pasaron por separado `1/1` cada uno con Kestrel HTTPS y PostgreSQL real. El primero reveló que, después de crear una release, el POST devuelve al catálogo sin selección TAR; la prueba se corrigió para volver a abrir TAR-0092 antes de buscar el editor, sin cambiar tiempos ni aplicación.

La categoría completa `FRONT_BROWSER` se ejecutó dos veces. Cada pase terminó `16/17` por un fallo intermitente de cleanup en un caso histórico distinto: primero `Front010BrowserTests.DirectionManagesEightDefinitionsAndOtherProfilesCannotMutateFromDeepLink`, después `Front011BrowserTests.PublishedPoliciesShowCurrentVersionAndHistoryWithoutEditingPastObligations`. Ambos casos pasaron aislados `1/1` después de su respectivo pase fallido, sin cambiar código, esperas, certificado, confianza ni fixture. Los dos casos nuevos de FRONT-012 pasaron enfocados y dentro de los pases completos. La fixture informa sólo «Browser fixture cleanup failed» y suprime la fase y excepción original; no hay evidencia suficiente para atribuir el fallo a un componente específico. La categoría completa queda como `Validación diferida`, no como aprobada. El formato dirigido y `git diff --check` pasaron.

El primer run remoto `36494438980` falló en la prueba de arquitectura histórica `Hu025UsesExactlyTheApprovedSurfaceAndThreePersistentAggregates`: su búsqueda por nombre confundía `_EvidenceValidation.cshtml` con una pantalla de carga de evidencias. Se reprodujo el fallo localmente, se exceptuó sólo ese archivo aprobado de UI-C08/C09 y pasaron la prueba enfocada `1/1` y la arquitectura completa `58/58`. El fallo de conservación de evidencia CV-04 fue consecuencia del corte anterior del job; no se reintentó el run fallido.

El código y la trazabilidad alcanzaron `Implementada localmente` con las pruebas enfocadas compatibles aprobadas y se publicaron mediante PR `#78` tras la primera autorización. La integración exige check verde para la cabeza final y segunda autorización específica. `FRONT-013` y `TECH-FRONT-005` no se iniciaron.
