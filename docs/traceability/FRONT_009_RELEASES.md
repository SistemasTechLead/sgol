# FRONT-009 — UI-C04 releases de configuración

## Base y decisión contractual

- Base remota verificada el 2026-09-25: `origin/master` `63a8f7421296c02bf740cf4c462f219fcd3eec5c`, que contiene el merge de `FRONT-007`. Worktree nuevo `C:\Users\josej\Dev\SGOL-front-009`, rama `codex/front-009`, inicialmente limpio. El checkout principal conservó intactos sus cambios en `AGENTS.md`, `scripts/demo/run-cv03.ps1` y `tests/Sgol.ArchitectureTests/Cv03DemoArchitectureTests.cs`.
- El PR draft `#74` de `FRONT-008` estaba abierto, cabeza `6f9a1ef7870e3160e497af14b1702995b6960fcc`, sin merge. Sus commits no son ancestros de esta rama. La Adenda 49 registra la excepción aprobada para integrar FRONT-009 antes de FRONT-008; no aprueba merge ni adelanta el recorrido combinado.
- Fila 77 de Adenda 45; `HU-008`/`CAP-009`, `CA-008`/`CP-008-P/N`, `RN-006/008/027/028`, F06 API y Adenda 46. El responsable aprobó expresamente la extensión mínima UI-C04 de `docs/design/componentes.md` y `estados-y-mensajes.md` y la resolución consumidora BR-M13 el 2026-09-25. BR-D04 ya tenía la primitiva de confirmación motivada.

## Contrato consumidor

- `/configuracion` es la ruta `NAV-CONFIGURATION` aprobada. UI-C04 usa exclusivamente `GET/POST /api/v1/configuration/releases` y `POST /api/v1/configuration/releases/{releaseId:guid}/publish`; no introduce subruta, editor TAR, calendario, edición de sucursal ni regla cliente de solapamiento. La lista misma presenta el historial: versión, estado, vigencia, publicación, actor y motivo de la respuesta real. El borrador carece de número y vigencia hasta publicar.
- El `GET` del API incorpora `meta.count` para cumplir la forma de colección exigida por `ISgolApiClient`. La sesión request-scoped proyecta `PER-CONFIG-ADMIN` sólo para Dirección como señal de presentación; `IConfigurationReleaseService` vuelve a autorizar en PostgreSQL. La página usa `no-store` y no muestra datos ante 403.
- Cada mutación POST valida antiforgery Razor y reenvía el par CSRF por `RazorAntiforgeryBridge`, con `Idempotency-Key` por intención. La publicación toma el `RowVersion` real del listado y lo codifica con `VersionEtag.Format` en `If-Match`. La fecha y hora se capturan en `America/Mexico_City` y se envían como instante UTC, sin usar la zona del navegador. La API valida fecha, estado, cobertura, vigencias y motivo; historia y auditoría se conservan en su transacción existente.
- `412 VERSION_CONFLICT` y `400 IF_MATCH_INVALIDO/IF_MATCH_REQUERIDO` bloquean el control de ese borrador hasta GET explícito «Recargar releases»; nunca se adopta el nuevo ETag ni se reenvía automáticamente. Solapamiento 422, validación, denegación, errores desconocidos y replay tienen mensajes seguros cerrados, asociados al formulario o al resumen; el motivo no entra en URL ni reporte.
- El diálogo nativo tiene foco inicial en Cancelar, Escape y retorno al disparador; errores de publicación reabren el mismo diálogo y enfocan el resumen. El estado vacío ofrece creación autorizada, y la tabla mantiene semántica en móvil con desplazamiento dentro de su contenedor. Sólo variables de diseño existentes gobiernan la presentación.

## Mapa de pruebas

| Criterio | Prueba |
|---|---|
| `meta.count` del listado y proyección `PER-CONFIG-ADMIN` sólo a Dirección; idempotencia, `If-Match`, 412 y respuesta segura | `ConfigurationReleaseTests` |
| Borrador, primera publicación, versión sucesora, historia, auditoría, replay, autorización, estado, solapamiento y no efecto | `ConfigurationReleasePersistenceTests` con PostgreSQL real |
| Reglas de interfaz, tokens, navegación y separación de sesión | `InterfaceDesignRulesTests` |
| Escritorio Chromium y móvil WebKit: vacío, creación, publicación, V1/V2, confirmación, Escape/retorno de foco, validación, solapamiento, 412, recarga, 403 en otros roles, etiquetas, landmarks y ancho `html/body`/PNG | `Front009BrowserTests` con Kestrel HTTPS y PostgreSQL desechable |
| Regresión de pantallas y navegación existentes | `Category=FRONT_BROWSER`, colección serializada |

## Comandos y límites

`scripts/ci/preflight.ps1`; `dotnet restore --locked-mode`; `dotnet build --no-restore --configuration Release`; `dotnet test tests/Sgol.UnitTests/Sgol.UnitTests.csproj --no-build --configuration Release --filter FullyQualifiedName~ConfigurationReleaseTests`; `dotnet test tests/Sgol.ArchitectureTests/Sgol.ArchitectureTests.csproj --no-build --configuration Release --filter FullyQualifiedName~InterfaceDesignRulesTests`; `dotnet test tests/Sgol.IntegrationTests/Sgol.IntegrationTests.csproj --no-build --configuration Release --filter FullyQualifiedName~ConfigurationReleasePersistenceTests`; `dotnet test tests/Sgol.FrontendBrowserTests/Sgol.FrontendBrowserTests.csproj --no-build --configuration Release --filter FullyQualifiedName~Front009BrowserTests`; `dotnet test tests/Sgol.FrontendBrowserTests/Sgol.FrontendBrowserTests.csproj --no-build --configuration Release --filter Category=FRONT_BROWSER`; formato dirigido; `git diff --check`.

Resultado local: restore locked `26/26`; build Release `26/26`, cero errores y advertencias; unitarias de release y proyección `7/7`; arquitectura de interfaz `9/9`; PostgreSQL de release `4/4`; `FRONT_BROWSER` completo `10/10` con Kestrel HTTPS, PostgreSQL real, Chromium escritorio y WebKit móvil. La primera pasada completa obtuvo `6/10`: cuatro pruebas antiguas asumían que Dirección tenía exactamente un enlace de navegación. Se acotaron los selectores al enlace de personas y se verificó explícitamente el nuevo enlace aprobado de configuración; la segunda pasada completa aprobó `10/10` sin cambiar tiempos ni fixtures. Tras el ajuste final de redacción idempotente, la prueba enfocada FRONT-009 volvió a pasar `1/1`. El formato dirigido y `git diff --check` pasaron.

Las capturas sintéticas sanitizadas se guardan fuera del checkout en `C:\Users\josej\Dev\front-009-evidence`; se enmascara la identidad de sesión. No contienen cookies, CSRF, contraseñas, TOTP ni tokens. Ninguna evidencia local equivale a pipeline remoto, publicación, integración o autorización de merge. El recorrido combinado de primera release y BR-API01 quedan para el PR `#74` de FRONT-008 tras adaptar esa rama; `FRONT-010` y `TECH-FRONT-005` permanecen fuera de alcance.

No quedó validación local de FRONT-009 diferida. La suite integral de la solución y el pipeline remoto no se ejecutaron por el alcance local autorizado; no son resultados aprobados de este cambio.

Publicación: PR draft `#75` desde `codex/front-009`. La integración no está autorizada ni ejecutada; el pipeline debe verificarse para la cabeza exacta antes de solicitar una decisión de merge separada.

## Reconciliación posterior en FRONT-008

La descripción anterior corresponde al estado local y de PR draft anterior al cierre. PR `#75` quedó **Integrada**: cabeza `59c5371e8a4d22c65bfa19f4cd95790e45b58e04`, check `TECH-BASE-003 / PR gates` `SUCCESS` run `36174853716` para esa cabeza y merge `9ce0aa82c2494b8826265772d08504bdbd19f5c1`; cabeza y merge son ancestros del `origin/master` verificado el 2026-09-25. FRONT-008 retomó desde ese merge el recorrido combinado de primera release sin adelantar FRONT-010.
