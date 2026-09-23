# TECH-FRONT-004 — harness de navegador común

## Alcance y fixture autorizado

`tests/Sgol.FrontendBrowserTests` ejecuta SGOL real en Kestrel HTTPS contra un PostgreSQL desechable de Testcontainers. La prueba crea cuatro personas, empleos, cuentas y roles sintéticos (`DIRECCION`, `ADMINISTRACION`, `SUBCOORDINACION`, `PISO_VENTAS`). Completa primer acceso por los endpoints hospedados de autenticación, fuera de Playwright, y entrega a cada contexto de navegador únicamente la cookie de sesión plena. El cliente Razor y el shell son los integrados por TECH-FRONT-002/003. No se crea ruta, endpoint ni credencial productiva.

El certificado HTTPS efímero se agrega temporalmente al almacén de raíces del usuario para que la llamada interna Razor→API confíe en Kestrel. El fixture comprueba que esa huella no existía previamente y retira exclusivamente ese certificado al terminar. También cierra los contextos, navegadores, clientes y proceso web, elimina el PFX temporal y desecha el contenedor. Si la limpieza falla, el test falla. Requiere permiso de escritura en `CurrentUser/Root` para el certificado de prueba.

## Comandos reproducibles

Desde la raíz del worktree, con Windows AMD64, Docker Desktop y el motor Linux AMD64 disponibles:

```powershell
dotnet restore tests/Sgol.FrontendBrowserTests/Sgol.FrontendBrowserTests.csproj --locked-mode
dotnet build tests/Sgol.FrontendBrowserTests/Sgol.FrontendBrowserTests.csproj --no-restore --configuration Release
pwsh tests/Sgol.FrontendBrowserTests/bin/Release/net10.0/playwright.ps1 install chromium webkit
dotnet test tests/Sgol.FrontendBrowserTests/Sgol.FrontendBrowserTests.csproj --no-build --configuration Release --filter FullyQualifiedName~FourRoleShellAndUnauthorizedState
dotnet test tests/Sgol.ArchitectureTests/Sgol.ArchitectureTests.csproj --no-build --configuration Release --filter FullyQualifiedName~InterfaceDesignRulesTests
git diff --check
```

El comando `playwright.ps1` es el que genera `Microsoft.Playwright` 1.62.0; se comprobó al ejecutar el proyecto. El paquete ya estaba fijado centralmente y presente en TECH-E2E-CV-02, por lo que TECH-FRONT-004 no incorpora una dependencia ni versión nueva. El nuevo proyecto posee su propio `packages.lock.json`. Chromium de escritorio usa 1440×900; WebKit móvil, 390×844. La ejecución local verificó Chromium 151.0.7922.34 y WebKit 26.5.

En GitHub Actions, después del build Release y antes de `dotnet test`, el workflow ejecuta `./tests/Sgol.FrontendBrowserTests/bin/Release/net10.0/playwright.ps1 install --with-deps chromium webkit`. El primer pipeline del PR `#66` (run `35934604798`, cabeza `51e64a81b449bb4aed10442ec96855f0208d6375`) falló porque el runner Linux carecía de los binarios de Playwright; la preservación de evidencia CV-04 falló de forma derivada al quedar omitida su demo. Esta corrección instala los navegadores y sus dependencias del sistema antes del gate. Su resultado remoto se registra sólo después de ejecutarse para el nuevo SHA.

## Mapa de pruebas y límites

| Criterio | Comprobación | Resultado local |
|---|---|---|
| Cuatro roles y aislamiento | Autenticación real una vez por cuenta; contexto Playwright aislado por rol y navegador; cookie plena y `/api/v1/auth/session` 200 | 8 combinaciones pasan |
| 401 | Contexto anónimo e ingreso de cookie inválida consultan `/api/v1/auth/session`, exigen 401; el anónimo recibe `application/problem+json`, el shell queda sin identidad y descarta la cookie inválida | 2 viewports pasan |
| Shell vacío | Página Razor existente `/branches/LOR-001` muestra «Aún no hay secciones disponibles» sin enlace `NAV-*`; título, `h1`, `main`, banner y navegación nombrada | 8 combinaciones pasan |
| Estilos y reflow | El layout calculado es grid; no hay desplazamiento horizontal de página | 8 combinaciones pasan |
| Teclado y foco | En escritorio Tab enfoca el enlace de salto visible y Enter llega a `main`; en WebKit móvil el enlace se enfoca explícitamente y Enter llega a `main` | 8 combinaciones pasan |
| Accesibilidad crítica equivalente | Idioma, título, landmarks, nombres de controles, `alt`, IDs únicos y referencias ARIA válidas en el shell renderizado. Las pruebas de arquitectura existentes cubren contraste de tokens y contrato visual | 8 combinaciones pasan; no equivale a auditoría WCAG completa |
| Evidencia y limpieza | Capturas PNG con nombre de sesión enmascarado; reporte JSON con campos permitidos; sin trazas, videos, consola ni cuerpos HTTP; cookies y recursos del fixture limpiados | `report.json` y 8 capturas locales; cleanup pasa |

La evidencia queda en `.artifacts/tech-front-004/`, ignorada por Git. El reporte guarda sólo tarea, resultado, navegador, viewport, rol y nombre de captura. No guarda URL, request, cookie, CSRF, contraseña, TOTP, recovery code, conexión ni contenido de respuesta. En errores, xUnit informa una categoría o estado HTTP, sin cuerpos sensibles. Las capturas sólo muestran la página sintética y enmascaran el nombre de sesión; no se habilitan trazas, videos ni logs del servidor.

Antes de `FRONT-001` no existe login visual ni un GET `NAV-*` protegido. Por ello este smoke no afirma probar redirección visual del 401, MFA visual, menús funcionales ni autorización/IDOR/no-efecto del servidor. Esas reglas conservan sus pruebas API/PostgreSQL; el navegador no las sustituye. La suite funcional completa corresponde a las historias consumidoras y a `TECH-FRONT-005`.
