# FRONT-001 — acceso hospedado en Razor

## Contrato y decisiones

`FRONT-001` consume exclusivamente UI-A01..A05 de la Adenda 45 y los endpoints de login, contraseña, MFA y recuperación del contrato hospedado de la Adenda 41. La Adenda 46 gobierna rutas Razor, CSRF, cookies y separación entre preauth y sesión plena. BR-D01 quedó aprobada e implementada en `TECH-FRONT-001`. El responsable aprobó en este chat el cierre mínimo de BR-D02 y BR-M01/M02, incorporado en `docs/design/componentes.md` y `docs/design/estados-y-mensajes.md` antes de escribir las vistas.

Las páginas `/acceso`, `/acceso/cambiar-contrasena`, `/acceso/mfa/enrolar`, `/acceso/mfa/verificar` y `/acceso/codigos-recuperacion` comparten el cliente HTTP, la validación Razor/API de CSRF y los componentes de credencial. Un marcador protegido de diez minutos sólo conserva el `nextStep` no secreto durante la navegación interna. No concede autoridad ni permite acceso sin la cookie preauth y la validación del backend. Las páginas de paso no aceptan deep link sin marcador; la respuesta de códigos sólo se renderiza desde la mutación que los creó, con `no-store` y sin GET de relectura. La sesión plena sigue siendo exclusivamente `__Host-SGOL-Session` emitida después de MFA o regeneración; el shell no consulta sesión en las páginas preauth.

La respuesta hospedada de login elimina una preauth anterior antes de emitir la nueva. El puente de cookies permite esa pareja de borrado y reemplazo, pero rechaza dos valores vivos para el mismo nombre. Una respuesta `401 CODIGO_MFA_INVALIDO` conserva el desafío para otro intento; `401 DESAFIO_INVALIDO` lo descarta. Ninguna de estas decisiones modifica la autorización, auditoría, rate limiting o persistencia del servidor.

## Mapa UI y pruebas

| Unidad | Ruta y acción API | Estado y control enfocado |
|---|---|---|
| UI-A01 | `/acceso` → `POST auth/login` | Credenciales no enumerables, CSRF, bloqueo y rate limit en `AccessErrors_CsrfLockoutRateLimit_AndKeyboardRemainSafe`; contrato hospedado en `RealKestrelHttpsCompletesHostedAuthenticationContract` |
| UI-A02 | `/acceso/cambiar-contrasena` → `POST auth/password/change` | Primer acceso, `autocomplete`, preauth sin cookie plena en `FirstAccess_RecurrentTotp_AndRecoveryReachFullSessionOnlyAfterMfa` |
| UI-A03 | `/acceso/mfa/enrolar` → `POST auth/mfa/enroll`, `POST auth/mfa/confirm` | Clave manual sólo durante el paso y confirmación TOTP en el recorrido de primer acceso |
| UI-A04 | `/acceso/mfa/verificar` → `POST auth/mfa/verify` | Login recurrente, TOTP y recovery inválidos, replay y preauth restringida en navegador y `PostgreSqlSerializesReplayRecoveryAndExactConcurrentLockout` |
| UI-A05 | `/acceso/codigos-recuperacion` → `POST auth/recovery-codes/regenerate` | Diez códigos una sola vez, consumo de recovery y regeneración antes de cookie plena; GET posterior no los reexpone |

El smoke de navegador usa SGOL real en Kestrel HTTPS y PostgreSQL Testcontainers desechable. No escribe capturas, trazas, cuerpos, cookies, CSRF, contraseñas, TOTP, manual key ni recovery codes. La prueba de concurrencia PostgreSQL y el smoke hospedado del servidor conservan las verificaciones de autorización, auditoría y no-efecto; la UI no las sustituye. `FourRoleShellAndUnauthorizedState_AreAccessibleOnDesktopAndMobile` verifica que el cambio del layout no altera el shell existente. Las tres pruebas `FRONT_BROWSER` comparten una colección xUnit para ejecutar en serie sus instancias Kestrel, PostgreSQL y Playwright.

## Comandos locales enfocados

```powershell
dotnet restore SGOL.slnx --locked-mode
dotnet build SGOL.slnx --no-restore --configuration Release -m:1
dotnet test tests/Sgol.FrontendBrowserTests/Sgol.FrontendBrowserTests.csproj --no-build --configuration Release --filter FullyQualifiedName~AccessBrowserTests
dotnet test tests/Sgol.FrontendBrowserTests/Sgol.FrontendBrowserTests.csproj --no-build --configuration Release --filter FullyQualifiedName~FourRoleShellAndUnauthorizedState
dotnet test tests/Sgol.UnitTests/Sgol.UnitTests.csproj --no-build --configuration Release --filter FullyQualifiedName~LoginMayReplaceDeletedPreauthCookieButRejectsTwoLiveValues
dotnet test tests/Sgol.ArchitectureTests/Sgol.ArchitectureTests.csproj --no-build --configuration Release --filter FullyQualifiedName~InterfaceDesignRulesTests
dotnet test tests/Sgol.IntegrationTests/Sgol.IntegrationTests.csproj --no-build --configuration Release --filter 'FullyQualifiedName~PostgreSqlSerializesReplayRecoveryAndExactConcurrentLockout|FullyQualifiedName~RealKestrelHttpsCompletesHostedAuthenticationContract'
git diff --check
```

## Validación ejecutada

Restore locked `26/26`; build Release de la solución `26/26`, cero errores y dos advertencias de copia de DLL en proyectos de prueba. Las builds enfocadas de Web y navegador terminaron sin advertencias. Playwright nuevo `2/2`, regresión del shell `1/1`, cookies `1/1`, arquitectura de interfaz `9/9`, PostgreSQL concurrente `1/1` y Kestrel HTTPS del contrato hospedado `1/1`. El formato dirigido a los C# cambiados y `git diff --check` terminaron sin diferencias. Un primer intento del smoke detectó que la eliminación y emisión de la cookie preauth en el mismo login eran rechazadas por el cliente común; se corrigió sólo esa rotación. El gate remoto `35942441818` falló en el smoke del shell tras pasar build y pruebas de integración; la ejecución conjunta local también produjo un timeout en acceso, mientras la prueba del shell aislada pasó. Tras serializar las tres pruebas del harness, el proyecto compiló sin advertencias y el smoke conjunto pasó `3/3`. Los intentos fallidos previos no son evidencia de aceptación; el gate del nuevo SHA queda pendiente.

## Límites

No se crean SSO, JWT, «recordarme», SPA, CORS, credenciales productivas, endpoints de prueba, reglas de negocio en cliente, cloud ni despliegue. `FRONT-002` y `TECH-FRONT-005` no se inician. El destino funcional `/mi-trabajo` queda para la historia que implemente esa página; FRONT-001 confirma la sesión plena en la respuesta del paso final y mediante `GET auth/session` en la prueba. La suite integral, Playwright completo y escáneres no se ejecutaron como validaciones locales de implementación; el gate remoto sólo se activó tras la autorización de publicación y merge.
