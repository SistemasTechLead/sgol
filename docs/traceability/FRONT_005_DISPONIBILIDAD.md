# FRONT-005 — UI-I03 disponibilidad diaria

## Alcance y base

- Base: `origin/master` `b291d3ab5dafe651455a5230d2aa7780b875c359`, PR `#70` de `FRONT-004` integrado; cabeza `aa23c419df907f032b999eeeef7442b99d291dda` con run `36049553112` `SUCCESS` para ese SHA. Cabeza y merge verificados como ancestros de `origin/master` el 2026-09-24.
- Contrato: fila 68 de Adenda 45 (`FRONT-005`), `HU-003`, `CA-003`, `CP-003-P/N`, `RN-004`, `RN-005`, Adenda 46 y la extensión mínima de UI-I03 en `docs/design/componentes.md` y `estados-y-mensajes.md`, aprobada expresamente por el responsable en este chat.
- Ruta web: detalle aprobado `/personas-y-accesos/personas/{personId:guid}`. No se crea ruta de UI-I03. El shell conserva sólo Personas y accesos como sección implementada y visible.
- API: GET `/api/v1/people/{personId}/availability?fromDate=...&toDate=...` y PUT `/api/v1/people/{personId}/availability/{date}`. El GET agrega `meta.count` al sobre de colección ya requerido por el cliente común. El PUT transmite únicamente `isAvailable` booleano, CSRF e `Idempotency-Key`; al corregir, `If-Match` proviene de `rowVersion`. Ningún porcentaje ni intervalo horario se añade.

## Presentación y seguridad

- El rango inclusivo y el día seleccionado son fechas ISO locales de `America/Mexico_City`; el rango inicial es la semana ISO lunes–domingo. El servidor conserva el límite de 100 días. La ausencia de fila se presenta «Sin registro» y nunca se interpreta como `false`; `false` se presenta «No disponible».
- La proyección de sesión de Dirección incluye el permiso existente `PER-DISPONIBILIDAD-ADMIN`. Los otros tres roles no reciben esa proyección. El servidor sigue validando permiso vigente, persona activa, sucursal y recurso en cada GET/PUT. Un deep link de otro rol devuelve 404 seguro sin datos ni controles.
- La página ofrece rango, selección de día, valor binario, estado vacío, éxito, error y bloqueo de corrección en `412 VERSION_CONFLICT` y `400 IF_MATCH_INVALIDO`. Sólo «Recargar disponibilidad» vuelve a consultar; no hay reintento automático. Persona inactiva conserva lectura segura sin acción de escritura.
- Los controles de fecha y radios usan etiqueta, foco visible y orden de teclado; el error de rango se asocia a las fechas, y el formulario anuncia carga. El diseño refluye en móvil. No se guardan identidad de sesión, cookies, CSRF ni otros secretos en capturas.

## Mapa de pruebas

| Criterio | Prueba |
|---|---|
| `CA-003`, `CP-003-P/N`, `RN-004/005`: booleano, fecha local, ausencia distinta de `false`, rango y sobre de colección | `AvailabilityAdministrationTests`; `AvailabilityAdministrationPersistenceTests`; `Front005BrowserTests` |
| Permiso de sesión y denegación de Administración, Subcoordinación y Piso de ventas | `SessionProjectsAvailabilityPermissionOnlyForDirection`; `OtherRolesCannotReadOrWriteAvailabilityAndLeaveNoValue`; `Front005BrowserTests` |
| Creación, corrección, `If-Match`, 412, historia, idempotencia y no-efecto | `CorrectionPreservesHistoryRequiresCurrentETagAndLeavesExactlyOneCurrentVersion`; `ConcurrentCorrectionsLeaveOneCurrentVersionUnderPostgreSqlConstraint`; `IdempotentReplayPreservesOneVersionAndRejectsDifferentIntent`; `Front005BrowserTests` |
| Persona inactiva/inexistente, auditoría y rollback | `AuthorizationAndTargetStateAreDeniedWithoutFunctionalEffect`; `AuditAndFunctionalFailuresRollbackAndSuccessfulWriteChangesNoAuthorityOrCredentials` |
| Rango vacío y con registros, fecha/rango inválidos, navegación, foco, responsive y deep link seguro | `Front005BrowserTests`; `RangeIsInclusiveBoundedAndReturnsOnlyCurrentValues` |

## Validación local

En el worktree nuevo se ejecutó una restauración efectiva `dotnet restore --locked-mode` y `dotnet build --no-restore --configuration Release` (0 advertencias, 0 errores). Pruebas enfocadas ejecutadas: `AvailabilityAdministrationTests` 17/17, `InterfaceDesignRulesTests` 9/9 y `AvailabilityAdministrationPersistenceTests` 11/11 con PostgreSQL real. La categoría completa `Category=FRONT_BROWSER` pasó 7/7, serialmente, con Kestrel HTTPS, PostgreSQL, Chromium escritorio y WebKit móvil. `dotnet format whitespace SGOL.slnx --include <archivos C# modificados> --verify-no-changes --no-restore` y `git diff --check` pasaron. Las capturas sintéticas sanitizadas están fuera del checkout en `front-005-evidence/`, al lado del worktree.

No se ejecutó suite integral ni pipeline remoto. Sin commit, push, PR, merge o despliegue de `FRONT-005`. `FRONT-006`, `TECH-FRONT-005` y UI-I04..I07 permanecen fuera de alcance.
