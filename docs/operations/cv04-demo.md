# Demo automatizada y cierre de `CV-04`

## Propósito

`TECH-E2E-CV-04` ejecuta una aceptación finita de `HU-027`, `HU-028` y `HU-031` conforme a la Adenda 42. Usa PostgreSQL efímero real, Kestrel HTTPS, autenticación hospedada con cookie, CSRF, cambio de contraseña y MFA TOTP, y las APIs y servicios productivos aceptados.

No crea UI. Navegador y accesibilidad se reportan como `NO_APLICA`. Tampoco prueba ni habilita `CV-05`, frontend, OAuth/OIDC, SSO, Redis, servicios cloud o datos reales.

## Precondiciones

- Windows o Linux AMD64 con Docker Linux disponible.
- .NET SDK `10.0.400` y artefactos Release restaurados y compilados.
- Git disponible y ejecución desde un worktree del repositorio.
- Ninguna variable externa de conexión, Kestrel o bootstrap incluida en la lista de rechazo del harness.

El harness crea sus contraseñas, secreto TOTP, certificado, base y cuentas en memoria o almacenamiento efímero. Esos valores no se imprimen ni se escriben en reportes.

## Comando único

```powershell
./scripts/demo/run-cv04.ps1 -Mode Automated
```

El comando termina con `0` sólo después de completar dos ciclos, comparar su huella funcional, escribir el reporte y verificar la limpieza. Cualquier fallo de escenario, reporte o cleanup termina con exit distinto de cero. El launcher captura inmediatamente el exit de cada proceso nativo.

## Matriz cerrada

La ejecución contiene exactamente `S01` a `S24`:

- `S01-S02`: ocho políticas TAR canónicas y rechazo de puesto textual, par y rol inferior.
- `S03-S10`: tres resultados, permanencia `CONCLUIDA`, sustitución motivada, rechazos sin efecto, autovalidación ordinaria rechazada y excepción de Dirección.
- `S11-S16`: alcance jerárquico estricto, filtros reductores, anti-IDOR y pendientes exactas.
- `S17-S19`: auditoría transaccional, dos carreras de escritura y un único resultado vigente.
- `S20-S22`: cookie ausente, CSRF ausente, MFA incompleto e invalidación de sesión sin efectos.
- `S23-S24`: segunda ejecución reproducible y cleanup verificable.

## Diagnóstico y evidencia

Las fases públicas son únicamente:

`PREFLIGHT`, `POSTGRESQL`, `HTTPS`, `CSRF`, `LOGIN`, `PASSWORD_CHANGE`, `MFA_ENROLL`, `MFA_VERIFY`, `SEED`, `POLICY`, `VALIDATION`, `REPLACEMENT`, `SUPERVISION`, `AUTHORIZATION`, `AUDIT`, `REPORT` y `CLEANUP`.

La evidencia pública se limita a fase, escenario, exit entero, código cerrado, conteos cerrados, duración y estado. Los archivos se generan en:

- `.artifacts/cv04/latest/TECH-E2E-CV-04-report.json`
- `.artifacts/cv04/latest/TECH-E2E-CV-04-report.md`
- `.artifacts/cv04/previous-failure/` sólo para conservar el fallo sanitizado inmediatamente anterior.

No contienen contraseñas, secretos TOTP, recovery codes, cookies, antiforgery tokens, hashes privados, stamps, cadenas de conexión, URLs privadas, SQL, payloads, respuestas sensibles, excepciones ni mensajes originales.

## Limpieza

Cada ciclo posee su PostgreSQL, proceso Web, certificado y clientes. La limpieza se ejecuta aun después de un fallo, termina el proceso Web, dispone el contenedor y elimina el certificado efímero. Un fallo de cleanup es bloqueante y nunca reemplaza el código del fallo primario.

## CI

El workflow requerido ejecuta `TECH-E2E-CV-04 / integral hosted demo` sobre `pull_request.head.sha` después del build y de las pruebas, y conserva únicamente `.artifacts/cv04/latest/**`. El SHA del reporte debe coincidir con el SHA cabeza del PR.
