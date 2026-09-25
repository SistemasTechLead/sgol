# FRONT-008 — configuración y planificación local

## Alcance y decisiones

Base original: `origin/master` `63a8f7421296c02bf740cf4c462f219fcd3eec5c`, rama `codex/front-008`, PR draft `#74`. Se rebasó sobre `origin/master` `9ce0aa82c2494b8826265772d08504bdbd19f5c1`, merge de FRONT-009 mediante PR `#75`; su cabeza `59c5371e8a4d22c65bfa19f4cd95790e45b58e04` y merge son ancestros de `origin/master`, con `TECH-BASE-003 / PR gates` `SUCCESS` run `36174853716` para la cabeza. La evidencia local de FRONT-007/009 permanece histórica.

La extensión consumidora de UI-C01/C02/C03 en `docs/design` y la propuesta mínima de lectura de borrador e idempotencia de PUT fueron aprobadas en este chat. `/configuracion` consulta exclusivamente `LOR-001`; `/planificacion` consulta la semana ISO única y el calendario publicado vigente por rango en `America/Mexico_City`. Dirección puede seleccionar una release `BORRADOR` existente, leer sus días y versión después de recargar, agregar o corregir un día con motivo, confirmación, CSRF e `Idempotency-Key` por intención. La corrección envía `If-Match`; un 412 bloquea el formulario hasta recarga explícita. Administración, Subcoordinación y Piso de ventas reciben sólo las funciones proyectadas y el servidor autoriza de nuevo cada lectura o escritura.

La extensión del servidor añade `GET /api/v1/calendar/drafts/{releaseId}` para una release borrador autorizada y `meta.count` a las colecciones consumidas por el cliente compartido. PUT exige clave UUID, conserva el resultado para replay en la misma transacción que la versión y auditoría, rechaza reutilización con contenido distinto y recupera el resultado en carrera de la misma intención. No se añadió PATCH de sucursal: la Adenda 49 excluye expresamente BR-API01 y el contrato sólo autoriza consulta. FRONT-009 conserva la creación/publicación en UI-C04. Ambas unidades conviven en `/configuracion`: la ficha LOR-001 se muestra a las sesiones autorizadas; la historia y mutaciones de releases sólo a Dirección. Desde UI-C03 sin borrador se navega a UI-C04 para crearlo; cada borrador real enlaza de vuelta a UI-C03 para editar sus días. El calendario vigente no cambia hasta publicar.

## Mapa de criterios y pruebas

| Criterio | Comprobación local |
|---|---|
| `HU-005`, `CA/CP-005`, UI-C01 | `CalendarTests`, `Front008BrowserTests`: LOR-001, zona, código inválido, consulta sin edición |
| `HU-009`, `CA/CP-009`, UI-C02 | `WeekPeriodTests`, `WeekPeriodPersistenceTests`, `Front008BrowserTests`: período ISO único, vigente/transcurrido, autorización |
| `HU-010`, `CA/CP-010`, UI-C03 consulta | `CalendarTests`, `CalendarPersistenceTests`, `Front008BrowserTests`: rango, vacío, tipos de día, fechas inválidas, autorización |
| UI-C03 borrador y corrección | `CalendarPersistenceTests`, `Front008BrowserTests`: lectura, versión, confirmación, CSRF, 412 sin sobrescritura, replay/no efecto, carrera concurrente, auditoría |
| Primera release desde interfaz | `Front008BrowserTests` y `Front009BrowserTests`: vacío de planificación → crear en UI-C04 → editar días en UI-C03 → recargar; la publicación y su historia siguen en UI-C04 |
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

La publicación inicial del avance parcial fue el commit `6f9a1ef7870e3160e497af14b1702995b6960fcc` en el PR draft `#74`; no es la cabeza final tras el rebase. FRONT-008 aún no tiene merge. Las tres modificaciones preexistentes del checkout principal siguen separadas e intactas. La validación posterior al rebase y el SHA exacto que se publique en el PR se registran abajo antes de solicitar autorización de merge.

## Revalidación después de FRONT-009

Esta sección separa el resultado anterior al rebase de la validación actual. La primera prueba de navegador del recorrido combinado pasó en Chromium escritorio y WebKit móvil: sin releases → crear primer borrador en `/configuracion` → editar días en `/planificacion` → recargar y conservar el día borrador. La captura móvil llevó a mostrar «Editar días del borrador» también junto a la confirmación de creación, porque la acción de la tabla quedaba al extremo derecho de su desplazamiento interno.

| Comando | Resultado posterior al rebase |
|---|---|
| `rtk proxy dotnet build --no-restore --configuration Release` | Pasó, 0 advertencias y 0 errores; no hizo falta otro restore |
| `rtk proxy dotnet test tests/Sgol.UnitTests --no-build --configuration Release --filter "FullyQualifiedName~CalendarTests|FullyQualifiedName~WeekPeriodTests|FullyQualifiedName~BranchApiEndpointTests|FullyQualifiedName~ConfigurationReleaseTests"` | 41/41 |
| `rtk proxy dotnet test tests/Sgol.ArchitectureTests --no-build --configuration Release --filter FullyQualifiedName~InterfaceDesignRulesTests` | 9/9 |
| `rtk proxy dotnet test tests/Sgol.ArchitectureTests --no-build --configuration Release` | 58/58 tras corregir el inventario de consumidores idempotentes |
| `rtk proxy dotnet test tests/Sgol.IntegrationTests --no-build --configuration Release --filter "FullyQualifiedName~CalendarPersistenceTests|FullyQualifiedName~WeekPeriodPersistenceTests|FullyQualifiedName~ConfigurationReleasePersistenceTests"` | 16/16, PostgreSQL real |
| `rtk proxy dotnet test tests/Sgol.FrontendBrowserTests --no-build --configuration Release --filter Category=FRONT_BROWSER` | 12/12, colección serializada, Kestrel HTTPS, PostgreSQL real, Chromium escritorio y WebKit móvil |
| `dotnet format whitespace --verify-no-changes --no-restore --include <archivos C# afectados>` | Pasó para el código C# afectado |
| `git diff --check` | Pasó; avisos LF/CRLF del checkout sin error de espacios |

Las capturas móviles regeneradas tienen ancho PNG real de 390 px en todos los estados; las de escritorio tienen 1440 px. Cada captura del navegador comprueba antes el ancho de viewport, `html` y `body`. La máscara cubre el encabezado completo. No se ejecutó la suite integral de la solución. El PR `#74` está abierto y listo para revisión. La comprobación requerida de su cabeza final se acredita externamente antes de solicitar el merge, ya que este cambio documental genera un SHA nuevo.

El primer run remoto de la rama rebasada, `36179819820` para `5123004641c67bc2892a62e81f88511815439d3f`, falló en `Hu034IdempotencyArchitectureTests.HttpConsumersUseTheSingleCanonicalHeaderParser`: la prueba esperaba 18 archivos consumidores y `CalendarApiEndpoints` elevó el total a 19 usando el parser canónico. Se reprodujo localmente el `18` frente a `19`, se actualizó ese inventario y se añadió `CALENDAR_DAY_PUT` a la lista de operaciones versionadas. La arquitectura completa pasó después `58/58`. La subida de evidencia CV-04 del run fallido no encontró archivos porque el gate se había detenido antes; no se trató como una segunda regresión funcional ni se reintentó el mismo SHA.

El run `36181179417` terminó `SUCCESS` para `7e574cc711c3dab81ca2b8afb4dd30987dc30e5b`, después de la corrección enfocada. Esa validación corresponde a una cabeza histórica; el commit documental posterior requiere verificar nuevamente el check para su SHA exacto.
