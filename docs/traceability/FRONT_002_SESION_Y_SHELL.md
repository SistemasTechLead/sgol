# FRONT-002 — sesión, shell y cierre de sesión

## Contrato y decisión de diseño

`FRONT-002` implementa únicamente `UI-A06/A07` de la Adenda 45. La Adenda 41 define `GET /api/v1/auth/session` y `POST /api/v1/auth/logout`; la Adenda 46 define la entrada pública, sesión request-scoped, CSRF, cookies allowlisted y autoridad del servidor. El responsable aprobó expresamente en este chat el cierre mínimo de `BR-D03/BR-D15` y el uso de `/mi-trabajo` como anfitrión protegido vacío. Las decisiones se incorporaron primero en `docs/design/componentes.md`, `estados-y-mensajes.md` y `navegacion.md`.

`/` redirige a `/acceso` sin sesión plena y a `/mi-trabajo` con ella. `/mi-trabajo` exige sesión plena y presenta sólo el encabezado de sesión, el título «Mi trabajo» y «Aún no hay secciones disponibles». `NAV-MY-WORK` es una ruta anfitriona, no un hijo funcional visible del menú. No se han creado bandeja, avisos, obligaciones, evidencia, conclusión, consultas funcionales, filtros, tablas ni acciones de recurso; `UI-E01..UI-E08` permanecen no implementadas para `FRONT-016/017/018`. La página pública de sucursal conserva su ruta y no es anfitriona del shell autenticado.

El encabezado usa `DisplayName`, la traducción cerrada de `RoleCode`, el menor de `IdleExpiresAt` y `AbsoluteExpiresAt` y la hora de `America/Mexico_City`. No hay umbral inventado de expiración próxima ni temporizador de cliente. Un rol desconocido y una respuesta inválida fallan cerrados. La navegación parte de páginas realmente implementadas y visibles; actualmente no muestra enlaces funcionales. La visibilidad no autoriza recursos.

Logout se envía sólo por POST con el par antiforgery validado en Razor y API. `204` limpia snapshot, CSRF y cookies permitidas y muestra «Sesión cerrada.» en `/acceso`. Sesión vencida o invalidada muestra el mensaje aprobado de reinicio. Un rechazo CSRF no reintenta ni afirma cierre; conserva la sesión y muestra el mensaje común. Un fallo remoto no confirmado borra localmente las cookies permitidas y presenta el mensaje exacto de contención, sin afirmar auditoría remota. El aviso transitorio de redirección es un valor protegido de vigencia breve con tres códigos cerrados, sin identidad ni secreto.

## Mapa de prueba y resultado local

| Superficie | Comprobación | Resultado |
|---|---|---|
| Presentación pura y puente | Cuatro traducciones de rol, rol desconocido, expiración efectiva y día local, aviso protegido, entrada, sesión plena, CSRF ausente/API, `204`, fallo remoto/excepción, cookies permitidas y no-efecto | Pruebas enfocadas de `Front002Tests`, `TechFront003Tests` y `HostedAuthenticationTests`: `47/47` |
| Servidor hospedado | Login, sesión, expiración de ticket, invalidación, logout, autorización y auditoría/no-efecto existentes con Kestrel HTTPS y PostgreSQL | `HostedAuthenticationKestrelSmokeTests`: `1/1` |
| Arquitectura | Tokens, layout, separación de autoridad, ruta anfitriona sin enlace funcional y componentes base | `InterfaceDesignRulesTests`: `9/9` |
| Navegador real | Cuatro roles en Chromium escritorio y WebKit móvil; identidad, rol, expiración, título, vacío sin enlaces, accesibilidad crítica, teclado/foco, `/` y deep link, CSRF/no-efecto, logout e invalidación del principal | `Category=FRONT_BROWSER`: `4/4`; cleanup `PASSED` |

Comandos locales finales:

```powershell
dotnet restore --locked-mode
dotnet build --no-restore --configuration Release
dotnet test tests/Sgol.UnitTests/Sgol.UnitTests.csproj --no-restore --configuration Release --filter 'FullyQualifiedName~Front002Tests|FullyQualifiedName~TechFront003Tests|FullyQualifiedName~HostedAuthenticationTests'
dotnet test tests/Sgol.ArchitectureTests/Sgol.ArchitectureTests.csproj --no-build --configuration Release --filter FullyQualifiedName~InterfaceDesignRulesTests
dotnet test tests/Sgol.IntegrationTests/Sgol.IntegrationTests.csproj --no-build --configuration Release --filter FullyQualifiedName~HostedAuthenticationKestrelSmokeTests
dotnet test tests/Sgol.FrontendBrowserTests/Sgol.FrontendBrowserTests.csproj --no-restore --configuration Release --filter Category=FRONT_BROWSER
dotnet format SGOL.slnx --verify-no-changes --no-restore --include <archivos C# modificados>
git diff --check
```

Restore bloqueado correcto; build Release de `26/26` proyectos con cero errores y advertencias; formato dirigido y `git diff --check` correctos. La primera pasada de `FRONT_BROWSER` no inició fixtures porque Docker Desktop estaba apagado (`npipe://./pipe/docker_engine` inaccesible). Tras iniciarlo, se reprodujo y corrigió una aserción de foco que dependía del estado de navegación previo, y una carrera de la prueba con la redirección del POST. La inspección de capturas detectó una fila fantasma móvil; se corrigió la cuadrícula y la categoría final volvió a pasar `4/4`. Ninguna corrección aumentó tiempos, reintentos o autoridad funcional.

Las ocho capturas de los cuatro roles y el reporte sanitizado se conservan fuera del checkout en el directorio local `C:\Users\josej\.codex\visualizations\2026\09\24\01a0d40d-37a4-78c0-bf74-98ab56f2b832\front-002-evidence`. La identidad sintética está enmascarada. El reporte contiene sólo rol, navegador, viewport, resultado y nombres de captura; no serializa cookies, CSRF, credenciales, TOTP, códigos, cuerpos de respuesta ni trazas. Los datos sintéticos y PostgreSQL desechable se limpiaron.

## Límites

No se ejecutó la suite integral ni pipeline remoto. No hay publicación, PR, merge ni despliegue. `TECH-FRONT-005`, `FRONT-003` y `FRONT-016..018` no se iniciaron. Las pruebas de servidor conservan la evidencia de autorización, auditoría y no-efecto; Playwright no la sustituye.
