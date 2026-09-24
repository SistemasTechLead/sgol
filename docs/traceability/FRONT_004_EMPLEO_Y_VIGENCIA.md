# FRONT-004 — UI-I02 empleo y vigencia

## Contrato y alcance

La fila 67 de la Adenda 45 consume HU-001/HU-002, CA-001/002 y CP-001-P/N, CP-002-P/N. FRONT-003 está integrada mediante PR #69: cabeza `65f3b6bc2a4652f7eb92d167e88b9d079444d702`, check requerido verde en run `36041719411`, merge `9dba2ed71841c7df443ea773bd986208928d98d1`, con ascendencia verificada en `origin/master`. La validación local descrita en `FRONT_003_PERSONAS.md` es evidencia histórica de esa tarea.

El detalle GET existente `/personas-y-accesos/personas/{personId:guid}` muestra el historial antes y después de cada cambio. El formulario PATCH cambia puesto y turno y exige motivo. Los diálogos POST de baja y reactivación usan BR-D04, motivo obligatorio, foco inicial en Cancelar, Escape y retorno al disparador. La extensión consumidora de mensajes y estados en `docs/design/estados-y-mensajes.md` fue aprobada expresamente para esta tarea. No se añadieron rutas Razor, campos, DTO, endpoints ni permisos.

El PageModel utiliza la sesión request-scoped, `RazorAntiforgeryBridge` y `ISgolApiClient` existentes. Cada POST valida antiforgery, reenvía el par CSRF a la API y usa una nueva clave idempotente por intención. El ETag de GET o de la mutación exitosa viaja en un campo de formulario y se reenvía como `If-Match`; no aparece en URL. Un 412 muestra conflicto sin volver a consultar automáticamente ni ofrecer formularios hasta que la persona pulse «Recargar detalle». El motivo no vuelve a renderizarse, no está en URL ni en capturas. El backend conserva `PER-PERSONA-ADMIN`, autorización por recurso, transacción, historia y auditoría; el puesto «Director» y turno no conceden rol ni permiso.

El shell conserva sólo la sección Personas y accesos realmente disponible para Dirección. La lista y el detalle de FRONT-003 siguen siendo los destinos; las acciones de UI-I02 aparecen únicamente con un detalle autorizado y una versión vigente. Los otros roles ven un 404 uniforme en deep link y no reciben los datos ni acciones. UI-I03..I07, disponibilidad, cuentas, roles y reset MFA no forman parte del cambio.

## Mapa de pruebas

| Superficie | Cobertura |
|---|---|
| API unitaria | `PersonAdministrationTests`: contrato PATCH/POST, ETag, idempotencia, 412 y datos laborales |
| PostgreSQL real | `PersonAdministrationPersistenceTests`: edición, puesto «Director» sin elevar rol, replay, sin cambio, motivo vacío, versión obsoleta, baja/reactivación, cuatro versiones y eventos, auditoría y no-efecto; rechazo de los otros tres roles para las tres mutaciones |
| Arquitectura | `InterfaceDesignRulesTests`: tokens, componentes y separación de presentación y autoridad |
| Kestrel HTTPS + navegador | `Front004BrowserTests`: Dirección en Chromium escritorio y WebKit móvil, detalle antes/después, formulario, historial, diálogos, motivo obligatorio, 412 y recarga explícita, navegación, foco, Escape, deep link 404 y tres roles denegados; `Category=FRONT_BROWSER` completa preserva regresiones previas |

Las capturas son de datos sintéticos. El harness las generó bajo `.artifacts/front-004/` y, tras validar la categoría completa, se trasladaron fuera del checkout a `C:\Users\josej\.codex\visualizations\2026\09\24\01a0d4c6-d895-7c42-bba9-64dcba7886bc\SGOL-front-004-final-evidence\front-004`. Enmascara toda la identidad de sesión y cualquier textarea con contenido antes de guardar imágenes, y destruye PostgreSQL, sesiones y certificado temporal. No guarda cookies, CSRF, contraseñas, TOTP ni motivos en reportes.

## Validación local

```powershell
dotnet restore --locked-mode
dotnet build --no-restore --configuration Release
dotnet test tests/Sgol.UnitTests/Sgol.UnitTests.csproj --no-build --configuration Release --filter FullyQualifiedName~PersonAdministrationTests
dotnet test tests/Sgol.ArchitectureTests/Sgol.ArchitectureTests.csproj --no-build --configuration Release --filter FullyQualifiedName~InterfaceDesignRulesTests
dotnet test tests/Sgol.IntegrationTests/Sgol.IntegrationTests.csproj --no-build --configuration Release --filter FullyQualifiedName~PersonAdministrationPersistenceTests
dotnet test tests/Sgol.FrontendBrowserTests/Sgol.FrontendBrowserTests.csproj --no-build --configuration Release --filter Category=FRONT_BROWSER
dotnet format SGOL.slnx --verify-no-changes --no-restore --include src/Sgol.Web/Pages/People/Details.cshtml.cs tests/Sgol.FrontendBrowserTests/Front004BrowserTests.cs tests/Sgol.IntegrationTests/PersonAdministrationPersistenceTests.cs
git diff --check
```

Resultado local: restore y build Release correctos; unitarias `8/8`, arquitectura `9/9`, PostgreSQL `14/14` y categoría de navegador `6/6`. No se ejecutó suite integral, pipeline remoto ni despliegue durante la implementación local. La publicación y la integración permanecen pendientes de autorización separada.
