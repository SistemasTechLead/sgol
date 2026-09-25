# FRONT-008 — configuración y planificación local

## Alcance y decisiones

Base: `origin/master` `63a8f7421296c02bf740cf4c462f219fcd3eec5c`, rama `codex/front-008`. PR `#73`, cabeza `e7a7661bb679b383434fc47f13390fdc773d1012`, run `36160122953` `SUCCESS` para esa cabeza y merge de base verificados en vivo. La evidencia local de FRONT-007 permanece histórica.

La extensión consumidora de UI-C01/C02/C03 en `docs/design` y la propuesta mínima de lectura de borrador e idempotencia de PUT fueron aprobadas en este chat. `/configuracion` consulta exclusivamente `LOR-001`; `/planificacion` consulta la semana ISO única y el calendario publicado vigente por rango en `America/Mexico_City`. Dirección puede seleccionar una release `BORRADOR` existente, leer sus días y versión después de recargar, agregar o corregir un día con motivo, confirmación, CSRF e `Idempotency-Key` por intención. La corrección envía `If-Match`; un 412 bloquea el formulario hasta recarga explícita. Administración, Subcoordinación y Piso de ventas reciben sólo las funciones proyectadas y el servidor autoriza de nuevo cada lectura o escritura.

La extensión del servidor añade `GET /api/v1/calendar/drafts/{releaseId}` para una release borrador autorizada y `meta.count` a las colecciones consumidas por el cliente compartido. PUT exige clave UUID, conserva el resultado para replay en la misma transacción que la versión y auditoría, rechaza reutilización con contenido distinto y recupera el resultado en carrera de la misma intención. No se añadió PATCH de sucursal: BR-API01 no ofrece contrato de mutación. La pantalla tampoco crea ni publica releases; FRONT-009 conserva su creación. Por tanto, la primera release borrador no puede obtenerse todavía desde esta interfaz y la tarea completa sigue **parcial**, sin estado `Implementada localmente`.

## Mapa de criterios y pruebas

| Criterio | Comprobación local |
|---|---|
| `HU-005`, `CA/CP-005`, UI-C01 | `CalendarTests`, `Front008BrowserTests`: LOR-001, zona, código inválido, consulta sin edición |
| `HU-009`, `CA/CP-009`, UI-C02 | `WeekPeriodTests`, `WeekPeriodPersistenceTests`, `Front008BrowserTests`: período ISO único, vigente/transcurrido, autorización |
| `HU-010`, `CA/CP-010`, UI-C03 consulta | `CalendarTests`, `CalendarPersistenceTests`, `Front008BrowserTests`: rango, vacío, tipos de día, fechas inválidas, autorización |
| UI-C03 borrador y corrección | `CalendarPersistenceTests`, `Front008BrowserTests`: lectura, versión, confirmación, CSRF, 412 sin sobrescritura, replay/no efecto, carrera concurrente, auditoría |
| Navegación, accesibilidad y reflow | `InterfaceDesignRulesTests`, `FRONT_BROWSER`: permisos proyectados, enlaces visibles, teclado/foco, escritorio Chromium y móvil WebKit, anchura de html/body y dimensiones PNG |

## Validación ejecutada

`scripts/ci/preflight.ps1` fue la única comprobación inicial del entorno y pasó. Hubo un único `dotnet restore --locked-mode` en el worktree nuevo. `rtk` v0.48.0 estuvo disponible. Datos y sesiones de prueba son sintéticos; Kestrel HTTPS y PostgreSQL real se usan en las pruebas de integración y navegador.

| Comando | Resultado |
|---|---|
| `rtk proxy dotnet build --no-restore --configuration Release` | Pasó, 0 advertencias y 0 errores |
| `rtk proxy dotnet test tests/Sgol.UnitTests --no-build --configuration Release --filter "FullyQualifiedName~CalendarTests|FullyQualifiedName~WeekPeriodTests"` | 30/30 |
| `rtk proxy dotnet test tests/Sgol.UnitTests --no-build --configuration Release --filter FullyQualifiedName~BranchApiEndpointTests` | 4/4, incluido código de sucursal inválido |
| `rtk proxy dotnet test tests/Sgol.IntegrationTests --no-build --configuration Release --filter "FullyQualifiedName~CalendarPersistenceTests|FullyQualifiedName~WeekPeriodPersistenceTests"` | 12/12, PostgreSQL real |
| `rtk proxy dotnet test tests/Sgol.ArchitectureTests --no-build --configuration Release --filter FullyQualifiedName~InterfaceDesignRulesTests` | 9/9 |
| `rtk proxy dotnet test tests/Sgol.FrontendBrowserTests --no-build --configuration Release --filter Category=FRONT_BROWSER` | 10/10, colección serializada; pasada completa tras ajuste de expectativa de navegación móvil |
| `rtk proxy dotnet test tests/Sgol.FrontendBrowserTests --no-build --configuration Release --filter FullyQualifiedName~Front008BrowserTests` | 1/1 tras ajuste de `IF_MATCH_INVALIDO` |
| `dotnet format whitespace --verify-no-changes --no-restore --include <archivos C# afectados>` | Pasó después de normalizar espacios en tres archivos |
| `git diff --check` | Pasó; sólo avisos de conversión LF/CRLF del checkout |

La primera pasada completa de navegador detectó expectativas históricas de menú que no incluían las dos rutas aprobadas. Tras corregirlas, 9/10 pasaron; el único fallo restante era la medición de alto del menú móvil de la prueba smoke, que sólo se aplicaba a Dirección. La pasada completa final fue 10/10. El cambio posterior del tratamiento de `IF_MATCH_INVALIDO` pasó de nuevo la prueba enfocada de FRONT-008.

Capturas PNG sintéticas sanitizadas: `C:\Users\josej\Dev\front-008-evidence\`, fuera del checkout. La máscara cubre el encabezado completo y la identidad de sesión. Hay vistas de sucursal, semana vigente/transcurrida, calendario, rango vacío, borrador, confirmación, conflicto, error de validación, navegación abierta/denegación y foco para escritorio y móvil. Se comprueban ancho de viewport/html/body en cada estado y dimensiones PNG 1440/390 px. La captura de navegación móvil abierta se añadió y pasó en una repetición enfocada de FRONT-008 posterior a la pasada completa. No se guardan cookies, CSRF, contraseñas ni TOTP en el repositorio.

No se ejecutaron la suite integral ni el pipeline remoto. No hay commit, push, PR ni merge de FRONT-008. El diff sólo contiene FRONT-008, los ajustes de expectativas de navegación de pruebas anteriores y la reconciliación documental del estado de FRONT-007. Las tres modificaciones preexistentes del checkout principal siguen separadas e intactas.
