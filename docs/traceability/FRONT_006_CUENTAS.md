# FRONT-006 — UI-I04/I05 cuentas individuales

## Base, contrato y decisión

- Base limpia: `origin/master` `6bb9450314bf2c9cf5119d507ef39800b50ef22b` en `codex/front-006`. FRONT-005 se integró por PR #71, cabeza `b8e537ff9e7692ac1ead8d0f1028c5fc7e8f6213`, run `36059652548` con `TECH-BASE-003 / PR gates` `SUCCESS` y merge `6bb9450314bf2c9cf5119d507ef39800b50ef22b`; ambos ancestros de `origin/master`.
- Fila 69 de Adenda 45, HU-006/CAP-006, CA-006/CP-006-P/N, F06 API y seguridad, Adendas 41 y 46. El responsable aprobó la extensión mínima de `docs/design`, la generación por servidor, la visualización única como excepción al criterio previo «sin secreto en DOM» y la entrega presencial por Dirección. El ajuste de contrato está en Adenda 47.
- Ruta web: sección «Cuentas» en `/personas-y-accesos`. No hay detalle de cuenta, ruta de mutación navegable, rol ni reset MFA. La sección se proyecta sólo si la sesión request-scoped incluye `PER-USUARIO-ADMIN`; el API reautoriza cada operación por permiso y recurso.

## Contrato consumidor y seguridad

- `GET /api/v1/users` conserva `data` y `meta.correlationId` y añade `meta.count` igual a la longitud de `data`. No cambia el cliente común ni crea DTO para el listado.
- Crear y reactivar sin `TemporaryPassword` genera en servidor un secreto aleatorio de 32 bytes codificado para activación. La respuesta nueva incluye `account` y `temporaryPassword` y lleva `Cache-Control: no-store`; el resultado persistido para idempotencia y la auditoría siguen usando únicamente `AccountSummary`. Replay y GET nunca devuelven el secreto. Los consumidores previos que envían `TemporaryPassword` conservan la forma previa de respuesta.
- Razor remite cada mutación por el cliente común con antiforgery, cookie permitida e `Idempotency-Key` por intención. El API real de cuentas no exige ETag/`If-Match`. Una respuesta de conflicto no dispara retry ni cambia estado en cliente. La UI no envía motivo, secreto o token en URL.
- La lista relaciona cuenta y persona por `PersonId`; el alta elige una persona existente y el servidor vuelve a validar vigencia y `LOR-001`. Cuenta duplicada o compartida, persona inexistente/inactiva/fuera de alcance, estado sin cambio, otros roles y sesión invalidada conservan error/no-efecto seguros.
- La contraseña sólo se presenta en la respuesta nueva y no se conserva entre requests. Dirección la entrega presencialmente fuera de SGOL. Capturas enmascaran bloque de activación e identidad completa del encabezado; ningún valor secreto entra en reportes ni logs.

## Mapa de pruebas

| Criterio | Prueba |
|---|---|
| Sobre de colección `meta.count`, contrato legado y respuesta nueva | `AccountAdministrationTests` |
| Generación única, replay sin secreto, hash, auditoría e idempotencia sin valor persistido | `GeneratedActivationSecretIsReturnedOnceAndNeverPersisted` |
| Persona activa/inactiva/inexistente, duplicado o cuenta compartida, concurrencia, auditoría, rollback y no-efecto | `AccountAdministrationPersistenceTests` |
| Dirección, tres roles denegados por deep link, alta, duplicado, baja/reactivación, foco, escritorio, móvil y capturas | `Front006BrowserTests` |
| Tokens y reglas de interfaz | `InterfaceDesignRulesTests` |
| Regresión de Personas y accesos y harness serializado | `Category=FRONT_BROWSER` |

## Validación local y límites

Se ejecutaron una restauración `dotnet restore --locked-mode`, build Release sin errores ni advertencias, `AccountAdministrationTests` `8/8`, `InterfaceDesignRulesTests` `9/9`, `AccountAdministrationPersistenceTests` con PostgreSQL real `10/10`, `Front006BrowserTests` `1/1` y `Category=FRONT_BROWSER` completo `8/8` con Kestrel HTTPS y PostgreSQL real. La revisión visual detectó desborde móvil por el texto largo del aviso de activación y por la opción seleccionada en el formulario después de errores de duplicado/persona inactiva; se corrigieron con salto de línea en el aviso y truncado visual del selector nativo, y se añadieron aserciones de anchura para esos estados. Una aserción histórica de FRONT-005 sobre `:focus-visible` falló en una pasada intermedia: `SelectDayAsync` esperaba un valor que ya estaba presente antes de enviar el formulario. Se corrigió para esperar la navegación nueva y usar teclado; la prueba enfocada `1/1` y la categoría completa posterior `8/8` aprobaron. Una limpieza del fixture FRONT-004 falló en una pasada intermedia, pero aprobó `1/1` aislada y `8/8` en la pasada final; no se modificaron tiempos, confianza ni limpieza sin causa reproducida. Una lista de cuentas literalmente vacía no es alcanzable con sesión real: la cuenta de Dirección que autentica la prueba es ya un registro de `GET /users`. El contrato y el componente de estado vacío permanecen implementados, y la captura correspondiente es una previsualización sintética del componente, no una respuesta funcional autenticada. No se usa endpoint de prueba ni se altera la regla del backend para fabricar el caso.

Comandos de cierre (con `--no-build --configuration Release` en las pruebas): `dotnet build SGOL.slnx --no-restore --configuration Release -v:q`; `dotnet test tests/Sgol.UnitTests/Sgol.UnitTests.csproj --filter FullyQualifiedName~AccountAdministrationTests`; `dotnet test tests/Sgol.ArchitectureTests/Sgol.ArchitectureTests.csproj --filter FullyQualifiedName~InterfaceDesignRulesTests`; `dotnet test tests/Sgol.IntegrationTests/Sgol.IntegrationTests.csproj --filter FullyQualifiedName~AccountAdministrationPersistenceTests`; `dotnet test tests/Sgol.FrontendBrowserTests/Sgol.FrontendBrowserTests.csproj --filter FullyQualifiedName~Front006BrowserTests`; `dotnet test tests/Sgol.FrontendBrowserTests/Sgol.FrontendBrowserTests.csproj --filter Category=FRONT_BROWSER`; `dotnet format whitespace SGOL.slnx --include <archivos C# modificados> --verify-no-changes --no-restore`; `git diff --check`.

No se ejecutan suite integral ni pipeline remoto durante la implementación local. Capturas sintéticas enmascaradas fuera del checkout, en `front-006-evidence/`. Sin commit, push, PR, merge ni despliegue.
