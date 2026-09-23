# TECH-FRONT-003 — bloqueo contractual de 401 hospedado

## HECHOS

- Base: `origin/master` `d6c1181503741479338f72f1a58b67755e6e8b37`; Adenda 46 integrada por PR `#64`. Rama `codex/tech-front-003` avanzó mediante fast-forward, sin tocar el checkout principal.
- F06 exige `application/problem+json` para errores. El cliente común integrado de TECH-FRONT-002 valida ese tipo antes de leer Problem Details.
- El endpoint `GET /api/v1/auth/session` responde 200 para las cuatro sesiones plenas en la prueba hospedada; después de rotar sello/rol/empleo/estado en PostgreSQL, responde 401.
- En esa respuesta 401, Kestrel HTTPS emitió `Content-Type: application/json`. La prueba enfocada falla al exigir `application/problem+json`.

## HIPOTESIS

`HostedCookieEvents.WriteProblemAsync` fija `Response.ContentType` y después usa `WriteAsJsonAsync` sin indicar el tipo de contenido; esta escritura entrega `application/json`.

## PRUEBA_DE_LA_HIPOTESIS

`HostedAuthenticationKestrelSmokeTests.RealKestrelHttpsCompletesHostedAuthenticationContract` usa PostgreSQL real, cuatro roles y Kestrel HTTPS. La aserción en la invalidación observó `Actual: application/json`, `Expected: application/problem+json`. El cliente `SgolApiClient` falló cerrado con `ApiProtocolException` en la misma respuesta.

La prueba exacta se repitió antes de corregir el servidor y reprodujo el mismo fallo. Tras indicar `contentType: "application/problem+json"` explícitamente en `WriteAsJsonAsync`, la prueba Kestrel HTTPS/PostgreSQL pasó (`1/1`), incluido el 401 con cookie invalidada explícita. Las pruebas enfocadas de las otras rutas también pasaron: `AUTENTICACION_REQUERIDA`, `ACCESO_DENEGADO`, `RATE_LIMITED`, `CSRF_INVALID` y `DATOS_AUTENTICACION_INVALIDOS` conservaron Problem Details, `code` y `correlationId`.

## CAUSA_DEMOSTRADA

`WriteAsJsonAsync` sin parámetro de tipo de contenido sustituyó el `Response.ContentType` asignado previamente por `application/json`. La misma secuencia estaba presente en el rechazo 429 del limitador y en los dos rechazos del middleware CSRF. Las cuatro llamadas afectadas ahora pasan explícitamente `options: null, contentType: "application/problem+json"`. No se alteraron los códigos HTTP o funcionales, cuerpos, cookies, autorización, auditoría, `Retry-After`, `no-store` ni reglas de dominio. El cliente común sigue rechazando `application/json` en respuestas Problem Details.

## EFECTO_OBSERVADO

El efecto histórico fue que la infraestructura Razor no podía traducir `SESSION_INVALID` mediante el cliente común sin relajar el contrato de errores. Ese bloqueo quedó resuelto tras la regresión Kestrel HTTPS/PostgreSQL verde. El código de TECH-FRONT-003 permanece sin commit, sujeto a sus restantes validaciones enfocadas.

## Corrección mínima autorizada

El responsable autorizó corregir mecánicamente las cuatro llamadas afectadas y continuar TECH-FRONT-003, manteniendo la prohibición de commit, push, PR y merge. La regresión de contenido pasó antes de reanudar las validaciones más amplias.
