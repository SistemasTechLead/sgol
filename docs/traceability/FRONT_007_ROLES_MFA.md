# FRONT-007 — UI-I06/I07 rol y reset MFA

## Base y contrato

- Base limpia `origin/master` `64b77802afbee2780032b4c020663be7db199474` en `codex/front-007`. FRONT-006 fue integrada por PR #72, cabeza `aa5f6daaf9fe4e365a19ae555ed21a4b3b5528d3`, gate `TECH-BASE-003 / PR gates` `SUCCESS` en run `36072586018` para esa cabeza y merge `64b77802afbee2780032b4c020663be7db199474`. Ambos commits se verificaron ancestros de `origin/master`.
- Fila 70 de Adenda 45, `HU-007`/`CAP-007`, `CA-007`/`CP-007-P/N`, F06 API y seguridad, Adendas 41, 46 y 48. La aprobación de Adenda 47 para FRONT-006 no se reutilizó para reset MFA.
- Se conserva `/personas-y-accesos` como única ruta Razor de cuentas y roles. La Adenda 46 no habilita detalle navegable; no se añadió subruta, vínculo API arbitrario ni opción de menú futura.

## Contrato consumidor

- `GET /api/v1/users/{id}/role-assignments` reutiliza `RoleAssignmentDetails`, con historia completa y ETag sólo del rol `ACTIVO`. `IRoleAssignmentService.GetAsync` revalida `PER-ROL-ADMIN`, cuenta activa y empleo vigente en `LOR-001`; inexistente, inactiva y fuera de alcance comparten 404 seguro. La respuesta lleva `no-store` y no expone secretos.
- La sección «Roles y recuperación MFA» se muestra sólo con `PER-ROL-ADMIN` proyectado en la sesión request-scoped; el servidor decide la autoridad real. Por cuenta activa muestra rol ausente/vigente e historia `ACTIVO`/`SUSTITUIDO`. Asignar, cambiar y revocar usan CSRF e `Idempotency-Key` por intención; cambio y revocación usan `If-Match` del GET. Un 412 mantiene el conflicto visible, bloquea controles de ese recurso y exige «Recargar roles» sin reintento automático.
- Reset MFA exige motivo, cuenta activa en alcance, Dirección y MFA del actor autenticado en los últimos cinco minutos. El endpoint rechaza una sesión sin esa prueba antes de llamar al servicio y `EfAccountAdministrationService` vuelve a validar el instante recibido con su reloj, incluso para invocaciones internas. Se revocan TOTP, códigos y desafíos; se rota `SecurityStamp`, se exige cambio de contraseña y nuevo enrolamiento, y se audita en la misma transacción.
- Cuando `temporaryPassword` se omite, el servidor genera la contraseña temporal. La respuesta nueva la muestra una sola vez, con `Cache-Control: no-store`, para entrega presencial por Dirección. El replay devuelve `AccountSummary` persistido sin secreto; `meta.replayed` permite distinguirlo. La forma histórica con contraseña suministrada sigue admitida. La validación JSON de la superficie acepta exactamente `{reason}` o `{reason, temporaryPassword}`.
- Motivos, ETag, idempotencia, CSRF y contraseña no viajan en URL. Las capturas enmascaran todo el encabezado de sesión y el bloque sensible. Las pruebas usan datos sintéticos y escriben capturas fuera del checkout en `C:\Users\josej\Dev\front-007-evidence`.

## Mapa de pruebas

| Criterio | Prueba |
|---|---|
| Lectura con ETag activo y vacío sin ETag; forma de mutación y no efecto de `If-Match` inválido | `RoleAdministrationTests` |
| MFA reciente aceptado y vencido sin invocar servicio; redacción de comando | `AccountAdministrationTests` |
| Lectura autorizada, cuenta ausente/inactiva/fuera de alcance, historia, rol único, replay, concurrencia, auditoría e invalidación | `RoleAdministrationPersistenceTests` |
| Reset con generación única, replay sin secreto, `SecurityStamp`, hash, auditoría, cuenta inactiva/fuera de alcance y no efecto | `AccountAdministrationPersistenceTests` |
| Dirección en escritorio/móvil: rol ausente, asignación, cambio, historia, revocación, confirmaciones, 412, reset, foco y cuatro roles | `Front007BrowserTests` |
| Regresión de toda «Personas y accesos» y shell en Kestrel HTTPS/PostgreSQL real, harness serializado | `Category=FRONT_BROWSER` |
| Tokens, componentes y separación de sesión/rol | `InterfaceDesignRulesTests` |

## Validación local y límites

Se ejecutó una sola restauración `dotnet restore --locked-mode` en el worktree nuevo. Build Release `26/26` sin errores ni advertencias; unitarias enfocadas `23/23`; arquitectura de interfaz `9/9`; PostgreSQL de roles y cuentas `22/22`; `Front007BrowserTests` `1/1`; categoría completa `FRONT_BROWSER` `9/9` en Kestrel HTTPS y PostgreSQL real. El formato dirigido y `git diff --check` pasaron. La primera pasada de navegador obtuvo `7/9`: FRONT-006 tenía un selector de vacío ambiguo después de introducir el vacío de historia y FRONT-003 agotó una espera de navegación. FRONT-003 pasó aislada sin cambiar tiempos; el selector de FRONT-006 se acotó y una reproducción enfocada localizó que WebKit móvil movía el foco al primer formulario al enviar Tab desde el teclado global de un `select` nativo. La prueba envía Tab al control enfocado y FRONT-006 pasó `1/1`; después pasaron dos ejecuciones completas consecutivas `9/9`. La primera pasada de PostgreSQL obtuvo `20/21` por un filtro de búsqueda de scope demasiado específico en la prueba nueva; se corrigió y pasó `22/22` en la ejecución enfocada final.

Comandos: `dotnet restore --locked-mode`; `dotnet build --no-restore --configuration Release`; `dotnet test tests/Sgol.UnitTests/Sgol.UnitTests.csproj --no-build --configuration Release --filter 'FullyQualifiedName~RoleAdministrationTests|FullyQualifiedName~AccountAdministrationTests'`; `dotnet test tests/Sgol.ArchitectureTests/Sgol.ArchitectureTests.csproj --no-build --configuration Release --filter 'FullyQualifiedName~InterfaceDesignRulesTests'`; `dotnet test tests/Sgol.IntegrationTests/Sgol.IntegrationTests.csproj --no-build --configuration Release --filter 'FullyQualifiedName~RoleAdministrationPersistenceTests|FullyQualifiedName~AccountAdministrationPersistenceTests'`; `dotnet test tests/Sgol.FrontendBrowserTests/Sgol.FrontendBrowserTests.csproj --no-build --configuration Release --filter 'Category=FRONT_BROWSER'`; formato dirigido; `git diff --check`.

La validación local se completó sin suite integral ni despliegue. Se creó el commit y se publicó la rama mediante PR #73; el pipeline para la cabeza final queda pendiente de verificación y el merge requiere una autorización separada. No se ejecutó FRONT-008 ni TECH-FRONT-005.
