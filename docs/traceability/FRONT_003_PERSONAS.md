# FRONT-003 — UI-I01 personas

## Alcance y contratos

`FRONT-003` consume `HU-001`, `CA-001`, `CP-001-P/N`, la fila 66 de la Adenda 45 y la navegación/sesión de la Adenda 46. La extensión mínima de `docs/design/navegacion.md` y `estados-y-mensajes.md` para ruta de detalle y mensajes de UI-I01 fue aprobada expresamente por el responsable en este chat antes de editar la interfaz.

La lista y alta viven en `/personas-y-accesos`; el detalle de lectura en `/personas-y-accesos/personas/{personId:guid}`. El shell muestra `NAV-IDENTITY` sólo cuando la sesión plena informa Dirección y `PER-PERSONA-ADMIN`; el servidor vuelve a autorizar lista, alta y detalle. Otros roles reciben 403 en la sección y 404 uniforme en un deep link concreto. Ninguna UI-I02..I07 se presenta. La sesión continúa request-scoped, el formulario valida antiforgery en Razor y reenvía el par CSRF a la API, y cada intención de alta lleva `Idempotency-Key` fuera de la URL.

La API ya devuelve `PersonSummary` y `PersonDetails` con `EmploymentHistory`. Sólo se añadió `meta.count` al envelope de `GET /api/v1/people` para cumplir el contrato del cliente común; no se agregó endpoint, campo de dominio, DTO ni regla. La proyección de sesión de Dirección ahora incluye el permiso existente `PER-PERSONA-ADMIN`. El historial se muestra en lectura con estado, vigencia, puesto y turno que entrega el backend. Los instantes se presentan en `America/Mexico_City` con formato numérico independiente de la abreviatura regional de septiembre.

La inspección de capturas detectó que, en móvil, una página de detalle corta dejaba un espacio vertical entre la navegación y el contenido. La fila móvil del grid ahora tiene tres pistas explícitas y una aserción de navegador comprueba su posición.

La validación de navegador tuvo un fallo transitorio de limpieza y otro al cancelar Windows la confianza del certificado temporal del fixture. Se reprodujo de forma enfocada la prueba de acceso, se descartaron los experimentos de confianza alternativa y se conservó el harness original. La pasada final de la categoría completa terminó `5/5` con cleanup correcto; no se aumentaron tiempos ni reintentos.

## Mapa de pruebas y evidencia

| Superficie | Criterio | Evidencia local |
|---|---|---|
| API y página | Alta con idempotencia y ETag; lista vacía con `meta.count=0`; estado vacío de modelo; duplicado seguro | `PersonAdministrationTests`, `Front003Tests` |
| PostgreSQL real | Alta, duplicado sin efecto, historia, denegación de lista/alta/detalle para los otros tres roles, auditoría y rollback | `PersonAdministrationPersistenceTests` `13/13` |
| Arquitectura | Tokens, componentes y separación entre navegación y autoridad | `InterfaceDesignRulesTests` `9/9` |
| Kestrel HTTPS + PostgreSQL + navegador | Dirección en escritorio/móvil: navegación, tabla, formulario, foco/teclado, alta, detalle, duplicado, 404; tres roles denegados sin datos, deep links y limpieza | `Front003BrowserTests` y categoría completa `FRONT_BROWSER` `5/5` |

La cuenta de Dirección debe poseer una persona activa para obtener sesión plena; por eso la base real del harness no puede producir lista vacía. La respuesta API vacía y el estado del PageModel se prueban de forma enfocada. Las imágenes `*-empty-preview.png` son una vista sintética de presentación del componente vacío, no evidencia de una lista vacía hospedada.

Las capturas de escritorio y móvil usan personas sintéticas y máscara para el nombre de sesión. La evidencia final se conserva fuera del checkout en `C:\Users\josej\.codex\visualizations\2026\09\24\01a0d46b-25a0-7d82-8037-c7b3463ffb67\front-003-evidence`; no incluye cookies, tokens, contraseñas, TOTP, códigos de recuperación ni cuerpos de respuesta. El harness destruye PostgreSQL, certificado y sesiones temporales.

## Comandos de validación local

```powershell
dotnet restore --locked-mode
dotnet build --no-restore --configuration Release
dotnet test tests/Sgol.UnitTests/Sgol.UnitTests.csproj --no-build --configuration Release --filter 'FullyQualifiedName~PersonAdministrationTests|FullyQualifiedName~Front003Tests|FullyQualifiedName~HostedAuthenticationTests|FullyQualifiedName~Front002Tests'
dotnet test tests/Sgol.ArchitectureTests/Sgol.ArchitectureTests.csproj --no-build --configuration Release --filter FullyQualifiedName~InterfaceDesignRulesTests
dotnet test tests/Sgol.IntegrationTests/Sgol.IntegrationTests.csproj --no-build --configuration Release --filter FullyQualifiedName~PersonAdministrationPersistenceTests
dotnet test tests/Sgol.FrontendBrowserTests/Sgol.FrontendBrowserTests.csproj --no-build --configuration Release --filter Category=FRONT_BROWSER
dotnet format SGOL.slnx --verify-no-changes --no-restore --include <archivos C# modificados>
git diff --check
```

No se ejecutó suite integral ni pipeline remoto durante la implementación local. La integración requiere pipeline verde para la cabeza exacta y segunda autorización de merge; no se realizó merge ni despliegue. `FRONT-004`, `TECH-FRONT-005`, edición de empleo, puesto/turno, baja/reactivación, cuentas y roles quedan fuera de alcance.
