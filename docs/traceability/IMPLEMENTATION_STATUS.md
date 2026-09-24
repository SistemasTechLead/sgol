# Estado de implementación

Este archivo permite iniciar cada tarea de forma incremental. Registra evidencia aceptada conforme a `F07_ENMIENDA_001_CIERRE_DE_TAREA_EN_UN_PR.md`; no sustituye la fila del backlog, las fuentes autorizadas de la tarea actual ni sus gates de salida.

Una sección preparada en una rama de pull request es una propuesta de base aceptada. Sólo adquiere eficacia como `Terminada` cuando el registro y el commit implementado están incorporados en `master`, el PR consta como merged, el check requerido pasó para ese commit, existe aceptación humana y `Fuentes/` permaneció protegida.

## Integración documental — planificación y backlog frontend

| Campo | Valor |
|---|---|
| Base integrada | PR `#60`, cabeza `28a1ec531e910d7ce6e1e1fd51c17a4010b3bcbe`, pipeline `35808190514` `SUCCESS`, merge `d311d967e925f4b9c1d1161bb86914d587d809af`; ascendencia verificada en `origin/master` |
| Aprobación | El responsable aprobó íntegramente los dos borradores y ordenó incorporarlos el 2026-09-22 |
| Contratos incorporados | Adenda 44, que inserta `TECH-UI-PLAN-001`; Adenda 45, que inserta `TECH-FRONT-001..005` y `FRONT-001..020`; Adenda 46, integrada por PR `#64` en `origin/master` `d6c1181503741479338f72f1a58b67755e6e8b37`, que resuelve `BR-N01..BR-N08` y fija sesión/CSRF para `TECH-FRONT-003` |
| `TECH-UI-PLAN-001` | `Terminada`: inventario de 48 unidades, brechas, trazabilidad y backlog preparados, reconciliados, aprobados e integrados mediante PR `#60` |
| Backend de entrada | `TECH-E2E-CV-04` y `TECH-E2E-CV-05` integradas/terminadas; CV-04 y CV-05 cerrados; dependencias exactas verificadas como ancestros |
| Brechas vigentes | BR-D17 quedó resuelta por `TECH-FRONT-001`; BR-API11/12 quedaron resueltas para presentación por `TECH-FRONT-002`; `BR-N01..BR-N08` quedaron resueltas documentalmente por la Adenda 46. Las demás brechas BR-API, BR-D y BR-M conservan sólo su tarea consumidora; este registro no las declara resueltas |
| Estado de implementación | `TECH-FRONT-001` `Integrada` mediante PR `#62`, cabeza `7a0d8540dbfd211fc0f5156a02d834c5832bbabf`, check requerido `SUCCESS` run `35906130681`, merge `8c76c094da653903cd72acce9373dd2b0e4a2914`. `TECH-FRONT-002` `Integrada` mediante PR `#63`, cabeza `a464bdac745d6dc38b4e72fe40f5af824287b38a`, pipeline `35912549181` `SUCCESS`, merge `c5313aff32a62118d01e4bde6f139cbfcc1c23f9`. `TECH-FRONT-003` `Integrada` mediante PR `#65`, cabeza `72ded2f86ad7baafa567b34cd3f84dad160ed699`, pipeline `35929424107` `SUCCESS`, merge `35239ebd50e120dd775c07d726df0ca899393b71`; ambos commits ancestros de `origin/master` verificado. `TECH-FRONT-004` `Implementada localmente` en `codex/tech-front-004`; el commit que contiene esta actualización y su PR son la evidencia de publicación, sin atribuir integración |
| Punto de parada | `TECH-FRONT-004` implementa sólo el harness y su smoke del shell/401. Commit, push y PR fueron autorizados separadamente; el responsable autorizó intentar el pipeline y hacer merge sólo tras un resultado verde para el SHA exacto. No se inicia `FRONT-001` ni `TECH-FRONT-005` |

## Integrada — `TECH-E2E-CV-05`

| Campo | Valor |
|---|---|
| Tarea | `TECH-E2E-CV-05` — Demo automatizada y cierre del corte `CV-05` |
| Contrato | `F07_ADENDA_43_CONTRATO_DE_DEMO_AUTOMATIZADA_Y_CIERRE_CV_05_TECH_E2E_CV_05.md`, aprobada íntegramente por el responsable antes de modificar código |
| Base | `origin/master` `00da83ca26f67e523d0d6067af737f38315e62b0`; worktree aislado `codex/tech-e2e-cv-05` |
| Dependencias | `HU-029` PR `#50`, `HU-032` PR `#51`, `HU-033` PR `#52`, `HU-034` PR `#53`, `TECH-OPS-001` PR `#54`, `TECH-AUTH-001` PR `#56`, `HU-035` efectiva PR `#57` y `TECH-E2E-CV-04` PR `#58`, integradas en la base; `#55` es histórico y no fue el merge efectivo de `HU-035` |
| Alcance | Proyecto aislado `tests/Sgol.Cv05Demo`; matriz cerrada `S01-S22`, dos ciclos, PostgreSQL, almacenes S3 compatibles, ClamAV, Kestrel HTTPS, autenticación hospedada y recuperación real. Navegador y accesibilidad `NO_APLICA` |
| Evidencia CI | Primero artifacts originales descargables durante 14 días mientras el repositorio sea público y GitHub Free conserve costo `$0`. Sólo ante fallo demostrado de cuota: nuevo SHA, resumen/log sanitizados y excepción de retención visible según sección 12 de la Adenda 43 |
| Estado | `Integrada` y `Terminada`; PR `#59` merged, cabeza `5f92fbc4c38acdc711513d447dac615bd8a2aed8`, check requerido `SUCCESS` en run `35797088036`, merge commit `32c3961ff67f79670d0824da71d4f70a06d1dc01` y ascendencia verificada en `origin/master`; `CV-05` cerrado |
| Correcciones productivas autorizadas durante CV-05 | Tras comprobar fallos hospedados, el responsable autorizó expresamente tres correcciones mínimas sin migración ni cambio contractual: el lector de auditoría reconoce IDs de requisito y decisión en la completitud de validación; `AuditDeleteAttemptMiddleware` corre después de autenticación para registrar el rechazo de borrado; Web registra `RecoveryReferenceRequestedOutboxHandler` para aceptar la solicitud de conciliación. Regresiones enfocadas: traza PostgreSQL `1/1`, middleware `3/3` y composición `1/1`; S01–S12 hospedados `1/1` y S13–S19 reales `1/1` |
| Diagnóstico y validación local | La semilla sintética corrigió la vigencia cronológica y añadió la política canónica de elegibilidad de TAR-0008 como precondición; la restauración invalida el pool PostgreSQL al recrear la base. La prueba pura del harness `5/5`, arquitectura `2/2`, contratos unitarios/componentes `71/71`, PostgreSQL enfocado `20/20` y smoke HTTPS `1/1` pasaron. Navegador y accesibilidad `NO_APLICA`. La evidencia local permanece fuera de Git en `.artifacts/cv05/latest/` |
| Primer pipeline remoto | PR `#59`, run `35795581552` sobre `d2165f9f30619afdfd0d5a1aa651060d92d2cb4d`: build, suite general, CV-04, formato, escaneo OCI, TECH-OPS y probe de réplica aprobaron; HU-035 falló antes de CV-05 y antes de subir artifacts porque el healthcheck PostgreSQL por socket aceptó el servidor temporal durante init y el primer `psql` encontró el apagado. No hay resultado de cuota para este run |
| Corrección de bootstrap autorizada | El responsable autorizó exigir `pg_isready -h 127.0.0.1` en `new-tech-ops-synthetic-environment.ps1`, sin aumentar intentos, intervalos ni timeouts. La regresión enfocada `Hu035BootstrapWaitsForFinalPostgreSqlTcpServer` falló antes del cambio y pasó después; el servidor temporal oficial sólo escucha socket y se apaga antes del servidor TCP final |
| Publicación e integración | El run final `35797088036` aprobó `TECH-BASE-003 / PR gates` sobre la cabeza exacta `5f92fbc4c38acdc711513d447dac615bd8a2aed8`; PR `#59` merged mediante `32c3961ff67f79670d0824da71d4f70a06d1dc01`; cabeza y merge son ancestros de `origin/master` |

## Integrada — `TECH-E2E-CV-04`

| Campo | Valor |
|---|---|
| Tarea | `TECH-E2E-CV-04` — Demo automatizada y cierre del corte `CV-04` |
| Estado | `Integrada` y `Terminada`; PR `#58` merged, cabeza `945c966edbc84552bab5abd91a9866e128f18ead`, check requerido `SUCCESS` en run `35772495865`, merge commit `00da83ca26f67e523d0d6067af737f38315e62b0` y ascendencia verificada en `origin/master` |
| Contrato | `F07_ADENDA_42_CONTRATO_DE_DEMO_AUTOMATIZADA_Y_CIERRE_CV_04_TECH_E2E_CV_04.md`, aprobada íntegramente por el responsable el 2026-09-19 antes de modificar código |
| Dependencias aceptadas | `HU-027` PR `#47`, `HU-028` PR `#48`, `HU-031` PR `#49`, `TECH-E2E-CV-03` PR `#46` y `TECH-AUTH-001` PR `#56` están incorporadas en la ascendencia de `origin/master` `10309937f596e5f6702181183b0cbaaa1b52f4c4` |
| Harness | Proyecto aislado `tests/Sgol.Cv04Demo`; dos ciclos finitos con PostgreSQL `postgres:18.6-alpine3.23`, Kestrel HTTPS, certificado efímero, cuentas reales de los cuatro roles, login, cambio obligatorio de contraseña, MFA TOTP, cookie y CSRF reales |
| Matriz | `S01-S24`: ocho TAR, autoridad canónica y rechazos, tres resultados, permanencia `CONCLUIDA`, sustitución e historia, autovalidación, jerarquía, filtros, anti-IDOR, pendientes, auditoría, concurrencia, seguridad de sesión, reproducibilidad y cleanup |
| Evidencia | Reportes JSON y Markdown sanitizados bajo `.artifacts/cv04/latest/`; sólo fase, escenario, exit, código cerrado, conteos, duración y estado. Navegador y accesibilidad son `NO_APLICA` |
| Superficie productiva | Sin cambios en `src/**`, endpoints, migraciones, paquetes, contratos funcionales, frontend o comportamiento de autenticación; reutiliza las capacidades productivas ya aceptadas |
| Validación local | Pruebas puras `6/6`; arquitectura enfocada `2/2` y consolidada `50/50`; unitarias/componentes de política, decisión, jerarquía y autenticación `64/64`; PostgreSQL/Testcontainers enfocado `15/15`; smoke Kestrel HTTPS hospedado `1/1`; gate final con dos ciclos, una huella, `65` evidencias `PASSED`, cero fallos y cleanup `PASSED`; build Release `24/24`, cero errores/advertencias; formato, autenticación hospedada, vulnerabilidades NuGet, `git diff --check` y ausencia de cambios Git en `Fuentes/` aprobados |
| Diagnóstico previo al gate final | Los intentos fallidos conservaron cleanup verde y localizaron: reloj de fixture anterior al empleo, release/regla de activación incorrecta o duplicada, forma recurrente y referencia canónica faltantes de `TAR-0005`, payload `ACCION_O_CONFORMIDAD` no canónico, expectativas HTTP demasiado estrechas para anti-IDOR/CSRF y conjunto incompleto de pendientes. El run remoto `35474478216` aprobó build y las suites generales, pero el segundo ciclo del gate CV-04 rechazó `S01` con `CV04_HTTP_STATUS_422`; la evidencia preservada y la reproducción con diagnóstico cerrado identificaron `CV04_POLICY_OVERLAP`: la vigencia `UtcNow + 1 s` podía preceder a la evidencia sembrada en `seedNow + 2 s` durante el ciclo caliente. Se corrigió calculando la nueva vigencia estrictamente después de la versión vigente y del reloj actual, sin espera, reintento ni aumento de límites; el gate local posterior volvió a aprobar dos ciclos, una huella y cleanup. El espejo byte-a-byte local queda diferido al runner Linux por `core.autocrlf=true`; Gitleaks no está instalado localmente y permanece requerido en CI |
| Publicación e integración | PR `#58` merged; cabeza `945c966edbc84552bab5abd91a9866e128f18ead`, check requerido `SUCCESS` en run `35772495865`, merge commit `00da83ca26f67e523d0d6067af737f38315e62b0`. El historial conserva fallos de cuota de artifacts y un diagnóstico HU-035 opcional exigido indebidamente por el primer publicador alternativo. La excepción puntual de evidencia sanitizada aprobada para `#58` no se extiende a `CV-05`; los originales y layout OCI de ese PR no quedaron garantizados como descargables por 14 días. |
| Límites | Sin `CV-05`, `HU-029`, `HU-032`, `HU-033`, `HU-034`, `HU-035` salvo regresión automática, sin UI, cloud, datos o secretos reales |

## Integrada — `TECH-AUTH-001`

| Campo | Valor |
|---|---|
| Tarea | `TECH-AUTH-001` — Autenticación hospedada y sesión del backend |
| Estado | `Integrada` y `Terminada`; PR `#56`, commit implementado `8cb881f3192a534e9a101d169fc38fcb0f749fc2`, pipeline `SUCCESS` run `35469361269` y merge commit `10309937f596e5f6702181183b0cbaaa1b52f4c4` |
| Contrato | `F07_ADENDA_41_CONTRATO_DE_AUTENTICACION_HOSPEDADA_Y_SESION.md`, aprobada íntegramente por el responsable el 2026-09-17 y renumerada con aprobación explícita el 2026-09-19; acotada a endpoints funcionales, sin interfaz |
| Superficie | API `/api/v1/auth`: token CSRF, login, cambio obligatorio de contraseña, enrolamiento y confirmación TOTP, desafío TOTP o recovery code, regeneración de recovery codes, estado de sesión y logout; además, reset administrativo de MFA en `/api/v1/users/{id}/mfa-reset` |
| Sesión y seguridad | Cookie `__Host-SGOL-Session` segura, `HttpOnly`, `SameSite=Strict`, renovación deslizante con límite absoluto de ocho horas; cookie de preautenticación protegida; CSRF obligatorio en mutaciones; `NameIdentifier` conserva el UUID canónico de `AppUser`; el rol informativo no sustituye las consultas de autorización en PostgreSQL |
| Identidad persistida | Reutiliza `AppUser`, `IdentityCredential`, `RoleAssignmentVersion`, `IPasswordHasher<AppUser>` y `SecurityStamp`; valida cuenta, persona, empleo vigente único en `LOR-001`, rol canónico vigente único, contraseña y MFA antes de construir el principal |
| Primer acceso y recuperación | Contraseña temporal con `MustChangePassword`, enrolamiento TOTP RFC 6238, desafío con protección contra replay, diez recovery codes de consumo único almacenados por hash, regeneración autenticada y reset administrativo auditado |
| Bloqueo e invalidación | Cinco intentos fallidos activan bloqueo progresivo de 15, 30 y 60 minutos; cada cookie se revalida contra cuenta, empleo, rol y security stamp; contraseña, estado de cuenta, rol, empleo o stamp obsoletos invalidan la sesión |
| Persistencia | Migración expand-only `20260918001719_AddHostedAuthentication`: campos de bloqueo/concurrencia y tablas de desafíos, credencial TOTP y recovery codes; sin backfill inseguro, sin borrado y con `Down()` bloqueado |
| Auditoría y observabilidad | Auditoría transaccional de los eventos de seguridad y contadores acotados para login, bloqueo, MFA, recovery, rechazo de sesión y rate limiting; no registra contraseñas, códigos, cookies ni secretos |
| Datos sintéticos | Runbook local para crear personas, empleos, cuentas y roles `DIRECCION`, `ADMINISTRACION`, `SUBCOORDINACION` y `PISO_VENTAS` mediante APIs reales; contraseñas y secretos sólo por variables o archivos locales ignorados |
| Validación enfocada | Restore locked `23/23`; builds Release de Unit e Integration sin errores/advertencias; unitarias y componentes afectados `12/12`; diagnóstico cerrado puro `1/1`; PostgreSQL/Testcontainers `2/2` para migración, primer acceso, replay TOTP, recovery de consumo único concurrente, bloqueo exacto concurrente e invalidaciones; inventario de migración, once constraints y el índice único parcial TOTP `1/1`; smoke Kestrel HTTPS real con PostgreSQL final `1/1` en `25.3 s`, cuatro roles, cookies reales, CSRF, login, cambio de contraseña, TOTP, recovery, sesión, endpoint protegido, logout, reset MFA, bloqueo, invalidaciones, auditoría sanitizada y cleanup |
| Diagnóstico sanitizado | El smoke sólo publica `stage`, `scenario`, `exit`, `errorCode` y `state`, todos cerrados. El primer intento Kestrel falló en `LOGIN/LOCKOUT` porque el fixture agotó correctamente el límite de diez solicitudes desde `127.0.0.1`; se corrigió verificando el estado PostgreSQL sin aumentar límites ni reintentar requests. Un intento previo del componente detectó que la sonda de continuidad de la rama antigua dependía de un handler Web ausente en `origin/master`; no se reintrodujo código HU-035 histórico ni se usó ese endpoint para aceptar autenticación. El run remoto `35468064873` falló sólo en el escaneo histórico de Gitleaks: el vector RFC exacto estaba anclado contra `match` completo; la corrección conserva las anclas y aplica la excepción únicamente al `secret` extraído. El run `35468138503` aprobó autenticación, PostgreSQL, formato, seguridad, imagen y TECH-OPS, pero la regresión HU-035 rechazó correctamente que su inventario siguiera declarando `20260914210503_AddRecoveryReconciliation` como última migración después de aplicar `20260918001719_AddHostedAuthentication`; se actualizaron únicamente las cuatro referencias ejecutables de compatibilidad derivadas de la migración, partiendo de las versiones aceptadas en `origin/master` |
| Autorización observada | Los cuatro roles acceden con sesión real a `/api/v1/branches/LOR-001`; sólo `DIRECCION` accede a `/api/v1/users` y puede ejecutar reset MFA, mientras `ADMINISTRACION`, `SUBCOORDINACION` y `PISO_VENTAS` reciben `403` por las reglas existentes |
| Límites | Sin Razor, HTML, CSS, SPA, OAuth, SSO, JWT persistente, Redis, impersonación, proveedor externo, bypass ni administración general de sesiones |
| Validación remota | Pipeline requerido `SUCCESS`, run `35469361269`, para el commit implementado exacto `8cb881f3192a534e9a101d169fc38fcb0f749fc2` |
| Siguiente paso | El frontend puede consumir los contratos HTTP reales; cualquier interfaz requiere una tarea y contrato de diseño separados |

La integración de `TECH-AUTH-001` no autoriza cambios funcionales adicionales de autenticación dentro de `TECH-E2E-CV-04`.

## Integrada — `HU-035`

| Campo | Valor |
|---|---|
| Tarea | `HU-035` — Dirección verifica recuperación con identidades e historia |
| Estado | `Integrada` y `Terminada`; PR efectivo `#57`, cabeza `191c9f8925f417642825975f8c0a75e508daf467`, pipeline requerido `SUCCESS` run `35464203236`, merge `424744bdca7bdbf32ac8b9e8bd108191c2504867` y ascendencia verificada en `origin/master` |
| Vigencia de filas históricas | Las filas diagnósticas siguientes conservan la secuencia pre-merge y sus límites en el momento de cada evidencia; cualquier frase “pendiente”, “continúa no terminada” o equivalente quedó superada por el estado final de integración de esta fila y no describe el estado vigente |
| Contrato | Adendas `F07_ADENDA_33_CONTRATO_DE_RECONCILIACION_Y_SIMULACRO_DE_RECUPERACION_HU_035.md` y `F07_ADENDA_34_CONTRATO_DE_GATE_AMD64_AUTOMATIZADO_HU_035.md`, aprobadas íntegramente por el responsable el 2026-09-14; `F07_ADENDA_35_DIAGNOSTICO_SANITIZADO_DE_REPLICA_HU_035.md`, aprobada íntegramente el 2026-09-15 exclusivamente para diagnóstico sanitizado, implementación local y gates enfocados; la historia continúa sin estado `Terminada` |
| Commit implementado | Commit que contiene esta actualización |
| Contrato diagnóstico complementario | `F07_ADENDA_36_PRUEBA_ENFOCADA_DE_STREAM_DE_REPLICA_HU_035.md`, aprobada el 2026-09-15: prueba sintética enfocada del stream de réplica y pruebas puras, sin cambio productivo ni aceptación AMD64; HU-035 continúa no terminada |
| Diagnóstico interno de réplica | `F07_ADENDA_37_DIAGNOSTICO_INTERNO_DE_ESCRITURA_DE_REPLICA_HU_035.md`, aprobada el 2026-09-17: instrumentación opt-in de PUT/GET/HEAD reales y pruebas puras de propagación al gate, sin nuevas operaciones S3; no identifica todavía la causa del HTTP 500 ni declara terminada HU-035 |
| Captura diagnóstica del servidor | Ajuste aprobado de la Adenda 37: captura acotada anterior a limpieza ante fallo real de REFERENCE; únicamente CHUNK_UPLOAD_FAILED o CREATE_ENTRY_FAILED con correlación temporal. No acredita causa raíz, aceptación integral ni cierre de HU-035 |
| Experimento de red de réplica | Adenda 38 aprobada el 2026-09-17: A/B aislado de migración de red primaria con primera réplica real, manifiesto exacto y registro por intento; implementación local y pruebas puras, ejecución manual AMD64 pendiente de autorización. No corrige el HTTP 500 ni acepta HU-035 |
| Red estable del simulacro | Adenda 39 aprobada el 2026-09-17: SeaweedFS arranca y permanece en la red privada definitiva de HU-035; TECH-OPS predeterminado conservado. Respaldado por `MIGRATION_EFFECT_SUPPORTED`, run `35281798132`, intento `1`, SHA `b75ae2712e3733540df11cd50109b9d0f81ab135`; pendiente de validación integral del gate real. No acredita cierre de HU-035 |
| Diagnóstico causal de PUT, REFERENCE, restore y reconciliación | Adenda 40 y complementos aprobados el 2026-09-18 y 2026-09-19: amplían el artifact existente con categorías cerradas del PUT y clasificaciones sanitizadas de red, dirección anunciada, nodo y capacidad; registran resultado cerrado de outbox/job/reconciliación; clasifican las excepciones no contractuales de captura exclusivamente por siete etapas y seis clases cerradas; y publican caso, exit y código cerrado de una reconciliación o verificación de restore inesperada, sin conservar mensaje ni excepción interna. No cambian S3, reintentos, timeouts ni matriz y no acreditan cierre de HU-035 |
| Causas raíz y correcciones | Los runs demostraron, sucesivamente: checkpoint UUID incompatible con JSON objeto (`35386054702`); dirección temporal anunciada por SeaweedFS (`35388790792`); identificador PostgreSQL inexistente `migration_id` en lugar de `"MigrationId"` (`35398125435`); y pérdida del stdin del contenedor `age` porque su wrapper omitía `--interactive` (`35400323775`). Las correcciones mínimas usan checkpoint cerrado, alias final estable, identificador EF exacto y stdin adjunto sólo para `age`, sin alterar reintentos, estados, operaciones S3, topología ni alcance productivo |
| Bloqueo diagnóstico vigente | El run `35454375124` demostró el falso `RECONCILIATION_IMMUTABLE_CONFLICT` por pérdida de precisión sub-microsegundo; la corrección normaliza la frontera PostgreSQL, conserva el hash y mantiene el rechazo real. El run `35456084194`, SHA `32eb4e15d99ebbbf9e0b7146a68422bd47c9b34e`, aprobó el replay positivo y demostró en `identity_missing` que el rol sintético aislado requería el `SET session_replication_role` ya usado por la matriz; se concedió sólo ese parámetro, sin `SUPERUSER`. Los runs `35457536271` y `35460899582` localizaron un restore dependiente de timing: `BackupProcessPipeline` permitía que una clausura exitosa de `age -> pg_restore` escapara como `IOException` antes de evaluar exits. La corrección espera ambos procesos y sólo acepta la clausura con ambos exits cero. El run `35462938925`, SHA `aed4967c6e9afd9cfa9e860384426d7a21be9806`, completó el gate integral y emitió `PASS`, pero el step heredó exit `1` del `docker network inspect` esperado sobre la red bootstrap ya eliminada. Se restablece el código nativo sólo después de `SUCCEEDED`; todo fallo real sigue bloqueando. Resta publicar el commit exacto y obtener el check requerido remoto |
| Validación local de las correcciones | La corrección aprobó sintaxis y compilación del proyecto con el archivo condicional AMD64 habilitado, cero errores/advertencias; regresión de fixture y key ring portátil con PostgreSQL real `1/1`; regresión de precisión PostgreSQL y replay inmutable `1/1`; sonda PostgreSQL real del grant exacto sin `SUPERUSER` `1/1`; regresión Linux de inaccesibilidad del manifiesto `1/1`; regresión Linux con procesos reales de clausura temprana exitosa y exit no cero bloqueante `1/1`; red, wrapper, privilegio sintético y diagnóstico cerrado de restore `89` aserciones; diagnóstico `216`; stream puro `7/7`; contrato/diagnóstico/red pura HU-035 en OperationsIntegration `54/54`; sonda real de réplica `1/1`; reproducción/regresión del lector funcional y atribución de `EXPORT_BACKUP_IO_ERROR` con PostgreSQL real `1/1`; sonda Docker de stdin sin/con `--interactive`; build Release `23/23`, cero errores/advertencias; Unit `26/26`; Architecture `5/5`; validadores HU-035 y TECH-OPS; formato, protección local de `Fuentes/` y `git diff --check`. El gate integral local posterior a la corrección del pipe terminó `SUCCEEDED`, imagen Linux AMD64 `sha256:d871c162b4703baa11f46cd2cc54fa5ddb687e8c12cc061f3bb3f7f0fbeae8fd`: preparación `1/1`, matriz `18/18`, replay, concurrencia, consulta/aprobación, verificación `1/1` y limpieza. Gitleaks y espejo byte-a-byte permanecen a cargo del runner Linux: Gitleaks no está instalado localmente y `core.autocrlf=true` materializa las copias raíz en CRLF mientras `Fuentes/**` permanece binario/LF; no hay cambios Git dentro de `Fuentes/` |
| Base aceptada | `TECH-OPS-001`: PR `#54`, commit implementado `eddfdfcca0fa9b6b5a988184610bce6d90efbbb2`, pipeline requerido `SUCCESS` run `34885345138` en segundo intento, aprobación humana, merge `5ad5192663b93594771df29fe90a086c4ea5c90b`, `origin/master` verificado exactamente y ascendencia confirmada |
| Superficie | API mínima `/api/v1/continuity/reconciliations` para solicitud, consulta y aprobación; job `CAPTURE_RECOVERY_REFERENCE` disparado por outbox; comandos `complete-functional-reference` y `reconcile-functional-restore`; sin UI |
| Contrato funcional | Módulo `Continuity`, snapshots `SGOL-FUNCTIONAL-SNAPSHOT-1`, canonicalización `SGOL-CANON-1`, allowlist de 39 tablas, conteos totales/por estado, hashes por campo/fila/tabla/raíz y diferencias explícitas de identidades, vínculos, versiones, evidencia y auditoría |
| Persistencia y seguridad | Tres tablas expand-only con eventos/diferencias append-only, triggers PostgreSQL SQLSTATE `55000`, `Down()` bloqueado, idempotencia común, ETag/If-Match, autorización de servidor exclusiva a rol `DIRECCION` vigente en `LOR-001`, anti-IDOR y auditoría transaccional de solicitud, inicio, consulta, resultado y aprobación |
| Recuperación | Captura `REPEATABLE READ, READ ONLY` en UTC, snapshot PostgreSQL exportado compartido con `pg_dump`, manifiestos privados sin overwrite, restore sintético marcado/aislado distinto del primario, comparación read-only, RPO máximo 3 600 s y RTO máximo 14 400 s |
| Pruebas enfocadas | Durante implementación: contrato/API/Operations `40/40` y arquitectura/idempotencia `5/5`; proyecto PostgreSQL real y proyecto externo HU-035 opt-in compilan con cero errores/advertencias, pero sus pruebas no se ejecutaron en sesión conforme a `AGENTS.md` |
| Gates locales | Restore locked `23/23` después de sincronizar sólo lockfiles transitivos; build Release `23/23`, cero errores/advertencias; suite sin PostgreSQL/Docker: unitarias `589/589`, arquitectura `48/48`, OperationsIntegration local con salida `0` y conteos no disponibles por modo binlog, CV-02 `15/15` y CV-03 `19/19`; formato sin cambios; cero vulnerabilidades NuGet conocidas; espejo `29/29`, protección de `Fuentes/`, contrato TECH-OPS, validador HU-035 y `git diff --check` aprobados |
| Validación local actual | El 2026-09-17 se ejecutó de nuevo `dotnet build SGOL.slnx --no-restore --configuration Release`: `23/23` proyectos, cero errores y cero advertencias. Web arrancó en Development con configuración sintética; `/health/live` respondió `200` y las tres rutas HU-035 respondieron `401 application/problem+json` sin sesión, confirmando enrutamiento y denegación por defecto. No se reutilizaron los tests históricos como evidencia de esta comprobación |
| Dependencia transversal resuelta | La ausencia histórica de autenticación hospedada fue resuelta por `TECH-AUTH-001`, PR `#56`; el gate efectivo de HU-035 y CV-05 consumió autenticación real sin bypass |
| Validación final | Linux AMD64 nativo, PostgreSQL real, dos S3-compatible, snapshot compartido con `pg_dump`, restore integral, reconciliación positiva, replay autenticado, consulta, aprobación y matriz contractual aprobaron en el pipeline requerido del PR efectivo `#57`, run `35464203236` |
| Límites | Sin producción, cloud real, datos/secretos reales, reparación, compensación, fabricación, borrado, overwrite, purga, UI, plataforma general de DR, `CV-05` ni tareas posteriores |
| Cierre | Implementación, simulacro integral, publicación, check requerido, aprobación, merge y ascendencia satisfechos mediante PR `#57`; disponible para consumo contractual del frontend sin ampliar rutas ni semántica |
| Siguiente paso | Consumir el contrato integrado sólo desde una tarea frontend aprobada; no inventar rutas, respuestas ni operaciones de reparación |

Este cierre integrado no autoriza frontend, despliegue externo ni operaciones de reparación.

## Base aceptada — `TECH-OPS-001`

| Campo | Valor |
|---|---|
| Tarea | `TECH-OPS-001` — Imagen OCI, staging, respaldo y réplica verificables |
| Estado | `Terminada` en `master` conforme a la evidencia aceptada |
| Contrato | `F07_ADENDA_32_CONTRATO_DE_OPERACION_PORTABLE_TECH_OPS_001.md`, aprobada íntegramente por el responsable el 2026-09-12 antes de continuar la implementación |
| Commit implementado | `eddfdfcca0fa9b6b5a988184610bce6d90efbbb2` |
| Base aceptada | `HU-034`: PR `#53`, commit `718608dd93ed9d6c7f08a97c6c7bebd47f4f9a42`, pipeline requerido `SUCCESS` run `34717877842`, aprobación humana, merge `df980342de63b4767866e7b34fb81431b4b73ab7`, `origin/master` verificado exactamente y ascendencia confirmada |
| Imagen OCI | Dockerfile `linux/amd64` con stages restore/publish/runtime, SDK nativo en `BUILDPLATFORM`, SDK y ASP.NET 10 Noble fijados por digest, publicaciones Web/Worker/Operations sin apphost, PostgreSQL `18.6-1.pgdg24.04+2` y `age` fijados, usuario `1654:1654`, puerto interno `8080` y health local; contexto Windows materializado sin puntos de análisis y sin rutas protegidas |
| Staging | Compose normativo sin proveedor, una sola referencia de imagen por digest, Web/Worker y procesos one-shot explícitos, root filesystem read-only, tmpfs privado, capacidades eliminadas, red privada, configuración no secreta e inventario cerrado de 15 nombres de secretos |
| Continuidad técnica | Backup diario PostgreSQL custom cifrado en streaming con `age`, nombre por slot, SHA-256, manifiesto canónico e idempotencia sin overwrite; réplica horaria de cuarentena/limpio con hash origen-destino, manifiestos append-only, máximo tres intentos, fallo ante corrupción/faltantes y no propagación de borrados |
| Locking | Reutiliza `ScheduledJobRunner` y advisory locks PostgreSQL estables por nombre para impedir solapamiento entre slots de backup o réplica, sin agregar Redis, broker ni servicio separado |
| Recuperación aislada | Operations valida manifiesto/hash, `pg_restore --list`, destino nuevo y vacío, rechazo de la base primaria, restore sin `--clean`, migración esperada, constraints y key ring; emite sólo evidencia técnica minimizada |
| Salud y key ring | Liveness sin dependencias; readiness minimizada para PostgreSQL, S3 primario y protección de datos; key ring compartido en PostgreSQL y envuelto con certificado externo mediante migración aditiva con `Down()` bloqueado |
| Evidencia y pipeline | Validador estático, constructor reproducible compatible con PowerShell 5.1, aprovisionador sintético sin overwrite, orquestador neutral que exige host x86-64 nativo, runbooks, proyecto externo opt-in para dos S3-compatible, build OCI BuildKit con SBOM/procedencia mediante builder `docker-container` y escaneo Trivy fijado por SHA; el workflow fija el checkout y toda evidencia al `pull_request.head.sha`, propone ejecutar el gate integral en `ubuntu-24.04` x64 y conservar sólo layout/metadatos/evidencia pública mediante `actions/upload-artifact` fijada por SHA, excluyendo runtime privado y manifiestos de objetos |
| Pruebas enfocadas | Unitarias/host `39/39`; arquitectura `4/4`; PostgreSQL real `3/3` para migración, key ring y locking estable; S3-compatible doble opt-in `1/1`; aprovisionamiento y restricción de credenciales S3 `2 x 1/1`; validador TECH-OPS aprobado con 9 artefactos y 15 nombres de secretos |
| Gates locales | Restore locked `22/22` tras actualizar sólo los lockfiles transitivos CV-02/CV-03 detectados por el primer intento; build Release `22/22`, cero errores/advertencias; suite local sin Docker `562/562` unitarias, `43/43` arquitectura, CV-02 `15/15` y CV-03 `19/19`; formato limpio tras corregir una diferencia de whitespace; cero vulnerabilidades NuGet conocidas; espejo `29/29`, protección de `Fuentes/` y rutas de diseño, contrato TECH-OPS y `git diff --check` aprobados. La suite agregada completa no se repitió; con autorización posterior se ejecutaron aparte los gates enfocados PostgreSQL/Testcontainers y S3-compatible indicados en esta propuesta |
| Gates externos | Imagen OCI, staging, respaldo PostgreSQL, réplica S3-compatible y restauración aislada verificados con datos sintéticos; pipeline requerido `SUCCESS`, run `34885345138`, segundo intento, para el commit implementado exacto |
| Límites | Sin despliegue, recursos cloud, secretos o datos reales, firma/publicación, purga/overwrite, endpoint funcional, UI, reconciliación, medición integral RPO/RTO ni cualquier implementación o simulacro de `HU-035`/`CA-035` |
| Cierre | PR `#54` aprobado y merged; merge `5ad5192663b93594771df29fe90a086c4ea5c90b`; commit implementado y merge pertenecen a `origin/master`; `Fuentes/` protegida y sin defectos bloqueantes registrados |
| Siguiente tarea | `HU-035`, propuesta actual en rama |

Esta base aceptada no amplía el contrato de `HU-035` ni autoriza operaciones Git o despliegues.

## Base aceptada — `HU-034`

| Campo | Valor |
|---|---|
| Tarea | `HU-034` — SGOL recupera mutaciones repetidas y registra conflictos |
| Estado | `Terminada` en `master` conforme a la evidencia aceptada |
| Contrato | `F07_ADENDA_30_CONTRATO_DE_IDEMPOTENCIA_INTEGRAL_Y_CONFLICTOS_HU_034.md` y corrección `F07_ADENDA_31_CORRECCION_DE_UNICIDAD_DE_CLAVE_DE_GENERACION_HU_034.md`, aprobadas íntegramente por el responsable el 2026-09-12 antes de continuar la implementación |
| Commit implementado | `718608dd93ed9d6c7f08a97c6c7bebd47f4f9a42` |
| Base aceptada | `HU-033`: PR `#52`, commit `3d38d5efb4df0a98b0e482bb806b7a76ea39509c`, pipeline requerido `SUCCESS` run `34705323809`, aprobación humana, merge `17af655729f1595ede510d13a06911f9ec3aed62`, PostgreSQL externo `214/214`, `origin/master` verificado exactamente y ascendencia confirmada |
| Inventario | 29 mutaciones HTTP y tres productores técnicos cerrados por operación estable; no incorpora jobs, outbox, lecturas ni mutaciones naturalmente convergentes al protocolo HTTP |
| Cabecera y canonicalización | Validador único de una sola `Idempotency-Key` UUID canónica; scope `idem:v1`, canonicalización `IDEM-CANON-1`, SHA-256 y `If-Match` incluido cuando corresponde |
| Replay | Snapshot minimizado v1 con status, payload semántico, ETag y Location originales; autorización precede al lookup, replay precede a guardas mutables y filas históricas usan sólo su adaptador explícito |
| Conflicto | Reutilización autorizada de scope y clave con otro hash devuelve el conflicto aprobado y persiste `IDEMPOTENCY_CONFLICT_REJECTED` append-only con tres campos exactos; una falla de esa auditoría produce `500 IDEMPOTENCY_CONFLICT_AUDIT_FAILED` |
| Persistencia | Migración aditiva de cuatro columnas nullable en `idempotency_record`; PK conservada, sin backfill ni purga; tombstone permanente salvo horizonte de 24 horas de intención de carga; índice de generación corregido a `(requested_by,idempotency_key) UNIQUE NULLS NOT DISTINCT` |
| Concurrencia | PostgreSQL decide el ganador; reintentos existentes quedan limitados a tres con backoff acotado y jitter, y sólo reinterpretan SQLSTATE o restricciones expresamente reconocidos |
| Observabilidad | Métricas por operación, outcome, clase SQLSTATE o protocolo, sin clave, actor, recurso, hash, ETag, payload ni otros labels de alta cardinalidad |
| UI y continuidad | No aplican: sin interfaz, endpoint de conflictos, recuperación administrativa, restauración, compensación, reconciliación ni adelantos de `HU-035` |
| Pruebas locales | Suite unitaria `548/548`, incluida la matriz transversal del protocolo `12/12`; arquitectura `39/39`, incluidos los controles HU-034 `3/3`; contratos CV sin Docker `34/34`; el proyecto PostgreSQL compila y descubre pruebas sin ejecutarlas |
| Gates locales | Restore locked `20/20`; build Release `20/20`, cero errores y advertencias, más recompilación enfocada Web `11/11` tras corregir el snapshot; formato limpio tras corregir cuatro diferencias de whitespace; cero vulnerabilidades NuGet conocidas en `19/19` proyectos; espejo `29/29`, protección de `Fuentes/` y rutas de diseño aprobadas; migración sin BOM, `Down()` bloqueado, modelo EF sin cambios pendientes y `git diff --check` limpio |
| PostgreSQL externo | Primer intento: `0/215` por `PendingModelChangesWarning`, corregido conservando explícitamente el índice simple existente de `requested_by`. Segundo intento: `208/215`, cero advertencias; permitió corregir siete defectos o expectativas HU-034 sobre conjunto sin orden, tombstones de rechazo, marcador `RECUPERADA`, claves de empleo e inventario de migraciones. Tercer intento: `213/215`, cero advertencias; los dos fallos restantes eran expectativas históricas de conflicto al reutilizar una clave en otro recurso y se corrigieron para probar el aislamiento contractual por recurso. Cuarto intento: `214/215`, cero advertencias; reveló una colisión interna del fixture al reutilizar `TAR-0007` después de convertirla en recurso independiente y se aisló la prueba de rollback con `TAR-0008`. Quinto intento: `214/215`, cero advertencias; la prueba laboral concurrente reveló que el ETag se derivaba de un snapshot transitorio con dos sucesores posibles, y se corrigió para usar directamente el `RowVersion` del sucesor de la operación. Ejecución enfocada posterior aportada por el desarrollador: `ConcurrentEmploymentChanges_PersistExactlyOneSuccessor` `1/1`, cero advertencias. Suite consolidada final aportada por el desarrollador: `215/215`, cero advertencias, `550.4 s` |
| Cierre | PR `#53`, pipeline requerido `SUCCESS` run `34717877842`, aprobación humana, merge `df980342de63b4767866e7b34fb81431b4b73ab7`, `origin/master` verificado exactamente y ascendencia del commit implementado confirmada |
| Siguiente tarea | `TECH-OPS-001`, obligatoria antes de `HU-035` |

Esta propuesta no autoriza commit, publicación de rama, apertura de pull request ni merge.

## Integrada — `HU-033`

| Campo | Valor |
|---|---|
| Tarea | `HU-033` — Actor autorizado reconstruye auditoría sin borrado |
| Estado | `Integrada` y `Terminada`; PR `#52`, cabeza `3d38d5efb4df0a98b0e482bb806b7a76ea39509c`, pipeline requerido `SUCCESS` run `34705323809`, merge `17af655729f1595ede510d13a06911f9ec3aed62` y ascendencia verificada |
| Contrato | `F07_ADENDA_29_CONTRATO_DE_CONSULTA_GENERAL_DE_AUDITORIA_HU_033.md`, aprobada íntegramente por el responsable el 2026-09-11 antes de continuar la implementación |
| Commit implementado | Commit que contiene esta actualización |
| Base aceptada | `HU-032`: PR `#51`, commit `d3c8362d712fe67161a0b99c7abe1f43918fb2eb`, pipeline requerido `SUCCESS` run `34658630103`, aprobación humana, merge `46a8fdb42d9d38973e4979980ab9456ae63016cf`, PostgreSQL externo `204/204`, `origin/master` verificado exactamente y ascendencia confirmada |
| Entregable | `GET /api/v1/audit-events` y `GET /api/v1/audit-events/{id}`; consulta general o trazabilidad por obligación con período UTC obligatorio, filtros cerrados, orden determinista y cursor ligado al actor, alcance, filtros y cerca temporal |
| Autorización | Autenticación, `PER-AUDITORIA-VER`, cuenta, empleo y rol canónico vigentes en `LOR-001`; Dirección ve la sucursal, Administración y Subcoordinación ven hechos propios e inferiores históricos y Piso sólo los propios; pares, superiores y otra sucursal quedan fuera |
| Reconstrucción | `traceObligationId` enlaza exclusivamente relaciones persistidas de configuración, asignación, evidencia y validación; informa completitud por etapa sin inventar correlación, estado actual ni eventos ausentes |
| Minimización | DTO propio de lectura; `before` y `after` aplican la allowlist exacta aprobada, informan campos omitidos y no exponen motivo textual, `requestId`, `sourceIpHash`, secretos, binarios, URLs firmadas ni contenido íntegro de evidencia |
| Borrado | No existe operación funcional de borrado; un `DELETE` autenticado a las rutas de auditoría devuelve `405` y registra una sola vez `AUDIT_EVENT_DELETE_ATTEMPTED`; el intento no autenticado devuelve `401` sin auditoría funcional |
| Persistencia | Reutiliza `audit_event`, `AuditTransaction` y el trigger append-only; sin tablas, columnas, vistas, índices, migraciones, backfill, cache, agregados ni productores retroactivos nuevos |
| Consistencia | PostgreSQL `REPEATABLE READ, READ ONLY`, `AsNoTracking`, único `queriedAt`; la lectura no inserta auditoría ni modifica recursos, versiones, ETag o row version |
| UI y navegador | No aplican: la Adenda 29 excluye interfaz y define únicamente API |
| Pruebas enfocadas | Endpoint y middleware `27/27`; arquitectura `2/2`; proyecto PostgreSQL compila sin errores ni advertencias y contiene `10` casos de jerarquía, autorización vigente, anti-IDOR, reconstrucción indivisible, actores históricos, minimización, cursor/snapshot, no efectos, rechazo append-only y auditoría del intento de borrado |
| Gates locales | Restore locked `20/20`; build Release `20/20`, cero errores y advertencias; suite local sin PostgreSQL `530/530` unitarias, `36/36` arquitectura y contratos CV sin Docker `34/34`; formato limpio; cero vulnerabilidades NuGet conocidas; espejo `29/29` y protección de `Fuentes/` aprobados; `git diff --check` y archivos nuevos sin errores |
| PostgreSQL externo | Suite consolidada final aportada por el desarrollador: `214/214` pruebas aprobadas, cero advertencias, `541.4 s`. Cinco intentos anteriores permitieron corregir exclusivamente invariantes de la semilla sintética y proyecciones auxiliares no traducibles; el resultado final verifica los diez casos de `HU-033` y los `204` casos no afectados |
| Límites | Sin consulta de conflictos idempotentes de `HU-034`; sin continuidad, restauración o reconciliación de `HU-035`; sin UI, exportación, purga, retención física o reconstrucción retroactiva |
| Cierre | Condiciones satisfechas por PR `#52`, run `34705323809` y merge `17af655729f1595ede510d13a06911f9ec3aed62` |
| Siguiente tarea | Ninguna habilitada desde esta rama |

Esta propuesta no autoriza commit, publicación de rama, apertura de pull request ni merge.

## Integrada — `HU-032`

| Campo | Valor |
|---|---|
| Tarea | `HU-032` — Dirección consulta toda `LOR-001` |
| Estado | `Integrada` y `Terminada`; PR `#51`, cabeza `d3c8362d712fe67161a0b99c7abe1f43918fb2eb`, pipeline requerido `SUCCESS` run `34658630103`, merge `46a8fdb42d9d38973e4979980ab9456ae63016cf` y ascendencia verificada |
| Contrato | `F07_ADENDA_28_CONTRATO_DE_VISTA_INTEGRAL_DE_DIRECCION_HU_032.md`, aprobada íntegramente por el responsable el 2026-09-11 antes de producir código |
| Commit implementado | Commit que contiene esta actualización |
| Base aceptada | `HU-029`: PR `#50`, commit `0d669d3f17245ee796d01885637cfac7536ea702`, pipeline requerido `SUCCESS` run `34644972538`, aprobación humana, merge `f3d078b27dc665ea0851b9545e99f0f59b5750fc`, PostgreSQL externo `203/203`, `origin/master` verificado exactamente y ascendencia confirmada |
| Entregable | Único `GET /api/v1/direction/overview`; período semanal ISO obligatorio, los cuatro niveles canónicos, cuatro conteos agregados y carga activa paginada por persona |
| Autorización | Autenticación, `PER-DIRECCION-VER`, cuenta, persona, empleo y rol `DIRECCION` vigentes en `LOR-001`; permiso aislado, puesto textual y cualquier otro rol se rechazan |
| Indicadores | Reutiliza exactamente `pending`, `concluded`, `validated`, `nonCompliant` y `activeLoadByPerson` de `HU-029`; sin sexto indicador, monto, incentivo, nómina, rentabilidad ni salud de integraciones |
| Universo integral | Sin filtros de nivel/persona incluye toda obligación del período en `LOR-001`, incluida la no asignada; ésta participa en los cuatro conteos sin crear fila ficticia de carga |
| Persistencia | Sin tablas, columnas, vistas, índices, migraciones, backfill, cache ni proyecciones persistidas nuevas |
| Consistencia | PostgreSQL `REPEATABLE READ, READ ONLY`, `AsNoTracking`, único `queriedAt`, cursores ligados a actor/período/filtros y cálculo sin efectos laterales |
| UI y navegador | No aplican: la Adenda 28 establece que “tablero” no exige interfaz |
| Pruebas locales | Enfocadas de endpoint HU-032 `16/16`; arquitectura de indicadores `3/3`; suite final sin PostgreSQL `503/503` unitarias, `34/34` arquitectura y contratos CV sin Docker `34/34` |
| Gates locales | Restore locked `20/20`; build Release `20/20`, cero errores y advertencias; formato limpio; cero vulnerabilidades NuGet conocidas; espejo `29/29` y protección de `Fuentes/` aprobados; `git diff --check` sin errores |
| PostgreSQL externo | Suite consolidada final `204/204`, cero advertencias, `558.1 s`; ejecutada excepcionalmente fuera del aislamiento en esta sesión por solicitud expresa del desarrollador |
| Límites | Sin colecciones de supervisión, validaciones pendientes, auditoría general de `HU-033`, idempotencia integral de `HU-034`, continuidad de `HU-035`, exportaciones ni UI |
| Cierre | Condiciones satisfechas por PR `#51`, run `34658630103` y merge `46a8fdb42d9d38973e4979980ab9456ae63016cf` |
| Siguiente tarea | Ninguna habilitada desde esta rama |

Esta propuesta no autoriza commit, publicación de rama, apertura de pull request ni merge.

## Integrada — `HU-029`

| Campo | Valor |
|---|---|
| Tarea | `HU-029` — Superior consulta cinco indicadores objetivos |
| Estado | `Integrada` y `Terminada`; PR `#50`, cabeza `0d669d3f17245ee796d01885637cfac7536ea702`, pipeline requerido `SUCCESS` run `34644972538`, merge `f3d078b27dc665ea0851b9545e99f0f59b5750fc` y ascendencia verificada |
| Contrato | `F07_ADENDA_27_CONTRATO_DE_CINCO_INDICADORES_OPERATIVOS_HU_029.md`, aprobada íntegramente por el responsable el 2026-09-11 antes de producir código |
| Commit implementado | Commit que contiene esta actualización |
| Base aceptada | `HU-031`: PR `#49`, commit `9b75e1c2bf1d1719f960fa1ed310a1ef40eb3275`, pipeline requerido `SUCCESS` run `34550189196`, aprobación humana, merge `e47b6de6daa433ec0a62027c4e523931219086e5`, PostgreSQL externo `202/202`, `origin/master` verificado exactamente y ascendencia confirmada |
| Entregable | Único `GET /api/v1/indicators`; período semanal ISO obligatorio, alcance visible, cuatro conteos agregados y carga activa paginada por persona |
| Autorización | Autenticación, `PER-INDICADOR-VER`, cuenta, empleo, rol canónico vigente, sucursal, alcance propio e inferior y filtros; Piso sólo propio y Dirección sólo universo asignado, sin vista integral |
| Indicadores | `pending` por ejecución `PENDIENTE`; `concluded` por ejecución `CONCLUIDA`; `validated` por única decisión `VIGENTE`; `nonCompliant` sólo por decisión vigente `NO_CUMPLIDA`; carga por persona según asignación vigente |
| Denominadores | Los cuatro conteos usan `baseObligationsCount`; carga usa `pending.count`; la suma completa por persona concilia con pendientes |
| Persistencia | Sin tablas, columnas, vistas, índices, migraciones, backfill, cache ni proyecciones persistidas nuevas |
| Consistencia | PostgreSQL `REPEATABLE READ, READ ONLY`, `AsNoTracking`, único `queriedAt`, cursores ligados a actor/período/filtros y cálculo sin efectos laterales |
| UI y navegador | No aplican: la Adenda 27 no exige interfaz |
| Gates locales | Restore locked `20/20`; build Release `20/20`, cero errores y advertencias; suite local sin PostgreSQL `487/487` unitarias, `33/33` arquitectura y contratos CV sin Docker `34/34`; formato limpio; cero vulnerabilidades NuGet conocidas; espejo `29/29` y protección de `Fuentes/` aprobados; `git diff --check` sin errores |
| PostgreSQL externo | Suite consolidada ejecutada por el desarrollador fuera de la sesión: `203/203` pruebas aprobadas, `0` advertencias, en `623.1 s` |
| Límites | Sin `/api/v1/direction/overview`, vista integral de `HU-032`, auditoría general de `HU-033`, UI, monto, incentivo, nómina, porcentaje financiero ni sexto indicador |
| Cierre | Condiciones satisfechas por PR `#50`, run `34644972538` y merge `f3d078b27dc665ea0851b9545e99f0f59b5750fc` |
| Siguiente tarea | Ninguna habilitada desde esta rama |

Esta propuesta no autoriza commit, publicación de rama, apertura de pull request ni merge.

## Integrada — `HU-031`

| Campo | Valor |
|---|---|
| Tarea | `HU-031` — Superior consulta y supervisa sólo inferiores |
| Estado | `Integrada` y `Terminada`; PR `#49`, cabeza `9b75e1c2bf1d1719f960fa1ed310a1ef40eb3275`, pipeline requerido `SUCCESS` run `34550189196`, merge `e47b6de6daa433ec0a62027c4e523931219086e5` y ascendencia verificada |
| Contrato | `F07_ADENDA_26_CONTRATO_DE_SUPERVISION_JERARQUICA_Y_VALIDACIONES_PENDIENTES_HU_031.md`, aprobada íntegramente por el responsable el 2026-09-10 antes de continuar la implementación |
| Commit implementado | Commit que contiene esta actualización |
| Base aceptada | `HU-028`: PR `#48`, commit `6fdc187c6430624f134ddfecf1592f7ccb167df8`, pipeline requerido `SUCCESS` run `34541989487`, aprobación humana, merge `c87e6c94c5cd00eb3829d27619e16a2e4e6a3012`, PostgreSQL externo `198/198`, `origin/master` verificado exactamente y ascendencia confirmada |
| Entregable | `GET /api/v1/supervision/obligations` y `GET /api/v1/validations/pending`; consulta paginada, determinista y sin efectos de trabajo, evidencia vigente, decisión vigente e historial de validaciones de niveles estrictamente inferiores |
| Autorización | Autenticación, `PER-SUPERVISION-VER`, rol canónico vigente, jerarquía estricta, asignación efectiva, sucursal y filtros; permiso aislado, puesto textual, pares, superiores y recursos fuera de alcance no conceden visibilidad |
| Pendientes | Requisito `PENDIENTE` materializado o derivado para obligación concluida con política congelada y sin requisito, sin materializarlo; autoridad ordinaria o de escalamiento vigente, preservando la mutación motivada de `HU-028` |
| Evidencia e historial | Proyecta exclusivamente metadatos minimizados y versiones vigentes aprobadas; no expone binarios, URLs firmadas ni secretos; conserva snapshots e historia inmutables |
| Persistencia | Sin tablas, índices, migraciones, backfill ni proyecciones persistidas nuevas |
| Consistencia | PostgreSQL `REPEATABLE READ, READ ONLY`, un único `queriedAt`, cursores ligados a filtros y orden estable; ningún GET cambia ejecución, requisito, evidencia, decisión, ETag ni row version |
| UI y navegador | No aplican: la Adenda 26 no exige interfaz |
| Límites | Sin indicadores, conteos, porcentajes o KPI de `HU-029`; sin vista integral de Dirección de `HU-032`; sin auditoría general de `HU-033`; sin segunda mutación de escalamiento |
| Gates locales | Restore locked `20/20`; build Release final `20/20`, cero errores y advertencias; suite local sin PostgreSQL `468/468` unitarias, `31/31` arquitectura y contratos CV sin Docker `34/34`; formato limpio; cero vulnerabilidades NuGet conocidas en `19/19` proyectos; espejo `29/29` y protección de `Fuentes/` aprobados; `git diff --check` y archivos nuevos sin errores de espacios |
| PostgreSQL externo | Ejecución consolidada final aportada por el desarrollador: `202/202`, cero advertencias, `521.8 s`. Tres intentos anteriores terminaron `200/202` y permitieron eliminar sucesivamente las tres composiciones no traducibles por EF; el resultado final verifica las dos pruebas HU-031 y los `200` casos no afectados |
| Cierre | Condiciones satisfechas por PR `#49`, run `34550189196` y merge `e47b6de6daa433ec0a62027c4e523931219086e5` |
| Siguiente tarea | Ninguna habilitada desde esta rama |

Esta propuesta no autoriza commit, publicación de rama, apertura de pull request ni merge.

## Base aceptada — `HU-028`

| Campo | Valor |
|---|---|
| Tarea | `HU-028` — Superior inmediato emite/sustituye validación separada |
| Estado | `Terminada` efectiva conforme a `F07_ENMIENDA_001_CIERRE_DE_TAREA_EN_UN_PR.md` |
| Contrato | `F07_ADENDA_25_CONTRATO_DE_DECISION_DE_VALIDACION_VERSIONADA_HU_028.md`, aprobada íntegramente e incorporada |
| Pull request | `#48`, merged |
| Commit implementado | `6fdc187c6430624f134ddfecf1592f7ccb167df8` |
| Pipeline requerido | `SUCCESS`, run `34541989487` |
| PostgreSQL | Suite externa consolidada `198/198`, cero advertencias |
| Aprobación humana | Recibida explícitamente |
| Commit incorporado en `master` | `c87e6c94c5cd00eb3829d27619e16a2e4e6a3012` |
| Ascendencia | Commit implementado y merge verificados como ancestros de `origin/master`; `origin/master` coincidía exactamente con el merge al iniciar `HU-031` |
| Defectos bloqueantes | Cero conocidos |
| `Fuentes/` | Sin cambios |

## Base aceptada — `HU-027`

| Campo | Valor |
|---|---|
| Tarea | `HU-027` — Dirección versiona validación por TAR |
| Estado | `Terminada` efectiva conforme a la regla condicional de cierre en un PR |
| Contrato | `F07_ADENDA_24_CONTRATO_DE_POLITICA_DE_VALIDACION_VERSIONADA_HU_027.md`, aprobada íntegramente e incorporada |
| Pull request | `#47`, merged |
| Commit implementado | `72d4658136e8d2dfdfc30b4faac93658769b8db6` |
| Pipeline requerido | `SUCCESS`, run `34525053653`, correspondiente al commit exacto |
| Aprobación humana | Recibida explícitamente |
| Commit incorporado en `master` | `c1fc6595de21095186a54d15617a4e07c4142be2` |
| Ascendencia | Commit implementado y merge verificados como ancestros de `origin/master` |
| PostgreSQL | Incluido satisfactoriamente en el pipeline requerido |
| Defectos bloqueantes | Cero conocidos |
| `Fuentes/` | Sin cambios |

## Base aceptada — `HU-030`

| Campo | Valor |
|---|---|
| Tarea | `HU-030` — Responsable ve bandeja propia y avisos internos |
| Estado | `Terminada` efectiva conforme a la regla condicional de cierre en un PR |
| Contrato | `F07_ADENDA_22_CONTRATO_DE_BANDEJA_PROPIA_Y_AVISOS_INTERNOS_HU_030.md`, revalidada contra `HU-023`, `HU-024`, `HU-025`, `TECH-EVID-002`, `HU-026` y `HU-022`, y aprobada íntegramente el 2026-09-09 |
| Pull request | `#45`, merged |
| Commit implementado | `4d502f4df78cd19b84261cd86089d0bc264d3c45` |
| Pipeline requerido | `TECH-BASE-003 / PR gates` `SUCCESS`, run `34403972427`, correspondiente al commit exacto |
| Aprobación humana | Recibida explícitamente |
| PostgreSQL | Suite externa `25/25` satisfactoria |
| Commit incorporado en `master` | `0df916f3650890b15497e3143b7afb33e643de8d`, verificado exactamente como `origin/master` al iniciar `TECH-E2E-CV-03` |
| Ascendencia | Commits de implementación, corrección y merge verificados como ancestros de `origin/master` |
| Defectos bloqueantes | Cero conocidos |
| Base aceptada | `HU-022`: Adenda 21 aprobada; PR `#44`; commit `20849773725a63c976a9dcd636d1c3fc75bbf9a3`; pipeline requerido `TECH-BASE-003 / PR gates` `SUCCESS`, run `34300222101` sobre ese SHA; aprobación humana; PostgreSQL externo `6/6`; merge y `origin/master` exactos en `10ef4c70515549d78f967ca5f70aa27be591c91f`; commit y merge ancestros; cero defectos bloqueantes conocidos |
| Entregable | `GET /api/v1/me/inbox` y `POST /api/v1/me/notices/{id}/read`; tareas propias, evaluación informativa de evidencia, avisos propios y lectura idempotente natural |
| Autorización | `PER-BANDEJA-PROPIA`, cuenta, MFA, empleo y rol vigentes; `/me` nunca amplía por jerarquía; un aviso ajeno o inexistente converge en `404` |
| Evidencia | Política congelada y sólo versiones `VIGENTE`; reutiliza `EvidenceReviewEvaluator` sin crear snapshot, resultado ni acceso al contenido de archivos |
| Persistencia | Migración `20260909190648_AddInternalNotices`; tabla `internal_notice`, catálogos cerrados, FK `RESTRICT`, deduplicación, índices, guardas e invariancia diferible con `assignment_version` |
| Atomicidad y concurrencia | GET `REPEATABLE READ, READ ONLY`; POST `SERIALIZABLE`, `FOR UPDATE`, tres intentos y auditoría atómica; repetición devuelve el `read_at` original sin segunda auditoría |
| Pruebas | Unitarias completas `409/409`; arquitectura completa `22/22`; enfocadas HU-030 `24/24` unitarias y `2/2` de arquitectura; PostgreSQL externo afectado `25/25`, cero advertencias |
| Gates | Restore locked `19/19`; build Release `19/19`; unitarias, arquitectura, pruebas enfocadas y PostgreSQL aprobados; UI/navegador no aplica porque la Adenda 22 excluye UI por brecha de diseño; verificaciones estáticas finales documentadas en la entrega del commit |
| Límites | Sin conclusión, validación, supervisión, indicadores, UI, mensajería externa, outbox, scheduler, descarga o inspección de archivos, S3, SeaweedFS o ClamAV |
| Cierre | Pipeline exacto, aprobación humana, merge, ascendencia, PostgreSQL externo satisfactorio y cero defectos bloqueantes verificados |
| Siguiente tarea ejecutada | `TECH-E2E-CV-03` — propuesta actual en rama |

La evidencia primaria de cierre pertenece al PR que contiene la implementación. Este registro posterior materializa el estado efectivo ya adquirido y no exige un commit administrativo que reescriba retrospectivamente el encabezado de la propuesta.

## Base aceptada — `HU-022`

| Campo | Valor |
|---|---|
| Tarea | `HU-022` — Responsable concluye sólo con evidencia completa |
| Estado | `Terminada` efectiva conforme a la regla condicional de cierre en un PR |
| Contrato | Adenda 21 aprobada íntegramente e incorporada |
| Pull request | `#44`, merged |
| Commit implementado | `20849773725a63c976a9dcd636d1c3fc75bbf9a3` |
| Pipeline requerido | `TECH-BASE-003 / PR gates` `SUCCESS`, run `34300222101`, correspondiente al commit exacto |
| PostgreSQL | `6/6` satisfactorio |
| Aprobación humana | Recibida explícitamente |
| Commit incorporado en `master` | `10ef4c70515549d78f967ca5f70aa27be591c91f`; era exactamente `origin/master` al iniciar `HU-030` |
| Ascendencia | Commit implementado y merge verificados como ancestros de `origin/master` |
| Defectos bloqueantes | Cero conocidos |
| `Fuentes/` | Sin cambios |

## Base aceptada — `HU-026`

| Campo | Valor |
|---|---|
| Tarea | `HU-026` — SGOL evalúa evidencia completa e informa faltantes |
| Estado | `Terminada` efectiva conforme a la regla condicional de cierre en un PR |
| Contrato | Adenda 19 aprobada íntegramente e incorporada |
| Pull request | `#43`, merged |
| Commit implementado | `bddf95fe2957fbea4b3b65bd0a29a17bb736a34d` |
| Pipeline requerido | `TECH-BASE-003 / PR gates` `SUCCESS`, run `34277376838`, correspondiente al commit exacto |
| PostgreSQL | `3/3` satisfactorio |
| Aprobación humana | Recibida explícitamente |
| Commit incorporado en `master` | `8d28d654388fd532b9b85a6d29242f4a71494985`; era exactamente `origin/master` al iniciar `HU-022` |
| Ascendencia | Commit implementado y merge verificados como ancestros de `origin/master` |
| Defectos bloqueantes | Cero conocidos |
| `Fuentes/` | Sin cambios |

## Base aceptada — `TECH-EVID-002`

| Campo | Valor |
|---|---|
| Tarea | `TECH-EVID-002` — Aporte estructurado cerrado y hecho condicional de TAR-0092 |
| Estado | `Terminada` efectiva conforme a la regla condicional de cierre en un PR |
| Contrato | `F07_ADENDA_20_CONTRATO_DE_EVIDENCIA_ESTRUCTURADA_TECH_EVID_002.md`, aprobada íntegramente el 2026-09-07 |
| Pull request | `#42` |
| Commit implementado | `66248692f2577a95384db62882e8081dec6fdd9e` |
| Pipeline requerido | `TECH-BASE-003 / PR gates`; `SUCCESS`, run `34264672922`, correspondiente al commit exacto |
| Aceptación humana | Recibida y autenticada |
| PostgreSQL | Suite externa `7/7`, cero errores y cero omitidas |
| Commit incorporado en `master` | `bd12592660121762cdf315fb5cfc497540b6892c` |
| Ascendencia | Commit implementado y merge verificados como ancestros de `origin/master` |
| Entregable | Los 18 contratos estructurados cerrados son aportables; `evidence_version` contiene exactamente archivo o `structured_payload`; `F_ENT_001` vigente es la autoridad condicional canónica |
| Límites | Sin evaluación agregada, snapshot, conclusión, validación, UI ni operaciones S3/ClamAV para evidencia estructurada |
| Cierre | Pipeline exacto, aprobación humana, merge, ascendencia, PostgreSQL satisfactorio y cero defectos bloqueantes conocidos verificados |
| Siguiente tarea ejecutada | `HU-026` — propuesta actual en rama |

La evidencia primaria de cierre pertenece al PR que contiene la implementación. Este registro posterior sólo materializa el estado efectivo ya adquirido tras el merge y no reescribe retrospectivamente el encabezado de la propuesta.

## Base aceptada — `HU-025`

| Campo | Valor |
|---|---|
| Tarea | `HU-025` — Responsable/superior aporta o sustituye evidencia conservando versiones |
| Estado | `Terminada` efectiva conforme a la regla condicional de cierre en un PR |
| Contrato | `F07_ADENDA_18_CONTRATO_DE_APORTE_Y_SUSTITUCION_VERSIONADA_DE_EVIDENCIA_HU_025.md`, incluidas las revisiones materiales de las secciones 22, 23, 24 y 25, aprobada íntegramente el 2026-09-07 |
| Pull request | `#41` |
| Commit implementado | `9f754ea68569786054bee63261ebdb3ee2565d4f` |
| Pipeline requerido | `TECH-BASE-003 / PR gates`; `SUCCESS`, run `34171002437`, correspondiente al commit exacto |
| Aceptación humana | Recibida y autenticada |
| Commit incorporado en `master` | `e74c2e8d077d39ebf1605cd6e6f5b44ad1ff1d1f` |
| Ascendencia | Commit implementado y merge verificados como ancestros de `origin/master` |
| Base aceptada | `TECH-EVID-001`: PR `#40`, commit `eb29656f7d2ced765924115520119f36529fc0fc`, pipeline requerido `TECH-BASE-003 / PR gates` `SUCCESS` run `34153966707`, aprobación humana, suite externa SeaweedFS/ClamAV `1/1`, merge `c0c07fff5dd0a05be41d6993eb1abf10b0e74706`; ambos SHA ancestros de `origin/master`; cero defectos bloqueantes conocidos; PostgreSQL no aplicó |
| Entregable | Seis endpoints cerrados; intención PUT firmada de diez minutos; confirmación y outbox; Worker inspecciona y promueve sólo `LIMPIO`; aporte y sustitución conservan cadena completa y una sola versión `VIGENTE` |
| Autorización | Responsable vigente aporta y sustituye únicamente en `PENDIENTE`; después de `CONCLUIDA`, sólo superior jerárquico estricto con `PER-EVIDENCIA-SUSTITUIR` y motivo; consultas aplican `PER-TAREA-VER` y `DEC-069` |
| Persistencia | Migración `AddVersionedEvidenceContribution`; tablas `file_object`, `evidence_item`, `evidence_version`; FK `RESTRICT`, checks, índices parciales y guardas PostgreSQL de snapshot, vínculo limpio, inmutabilidad y cadena lineal |
| Idempotencia y atomicidad | Intención, confirmación, aporte y sustitución usan scopes separados; evidencia, idempotencia, auditoría y outbox aplicable comparten transacción; rate limit persistente 30/60 minutos por actor con advisory lock |
| Pruebas | Unitarias completas `321/321`; arquitectura completa `17/17`; enfocadas HU-025 sin Docker `69/69` unitarias y `5/5` arquitectura; PostgreSQL externo `6/6`; SeaweedFS/ClamAV externo `1/1`; cero pruebas omitidas |
| Gates | Restore locked `18/18`; build Release `18/18`; unitarias `321/321`; arquitectura `17/17`; enfocadas HU-025 `69/69` y `5/5`; PostgreSQL externo `6/6`; SeaweedFS/ClamAV externo `1/1`; formato sin diferencias después de corregir tres incidencias de whitespace detectadas por el primer intento; cero vulnerabilidades NuGet conocidas; modelo EF sin cambios pendientes; `Fuentes/` sin cambios y espejo `29/29` idéntico. `git diff --check` se informa en la entrega para no modificar el diff después del último gate |
| Riesgo operativo | SeaweedFS 4.45 agrupa `PutObject`, `PutBucketCors` y `DeleteBucketCors` bajo `Write`; SGOL no invoca mutaciones CORS y falla cerrado ante deriva, pero antes de producción se requiere control externo equivalente o aceptación explícita del riesgo residual conforme a la sección 25 |
| Límites | Sin UI, descarga, evidencia estructurada, conclusión HU-022, evaluación HU-026, validación HU-028, borrado funcional, broker, Redis, otro Worker o scheduler |
| Cierre | Commit exacto, pipeline, aprobación, merge, ascendencia, PostgreSQL y S3/ClamAV externos satisfactorios y cero defectos bloqueantes verificados |
| Siguiente tarea ejecutada | `TECH-EVID-002` — propuesta actual en rama |

La evidencia primaria de cierre pertenece al PR que contiene la implementación. Este registro posterior sólo materializa el estado efectivo ya adquirido tras el merge y no altera el entregable aceptado.

## Base aceptada — `TECH-EVID-001`

| Campo | Valor |
|---|---|
| Tarea | `TECH-EVID-001` — S3 privado local/CI, escáner adaptado y corpus seguro |
| Estado | `Terminada` efectiva conforme a la regla condicional de cierre en un PR |
| Contrato | `F07_ADENDA_17_CONTRATO_DE_INFRAESTRUCTURA_SEGURA_DE_EVIDENCIA_TECH_EVID_001.md`, aprobada íntegramente el 2026-09-05 |
| Pull request | `#40` |
| Commit implementado | `eb29656f7d2ced765924115520119f36529fc0fc` |
| Pipeline requerido | `TECH-BASE-003 / PR gates`; `SUCCESS`, run `34153966707`, correspondiente al commit exacto |
| Aceptación humana | Recibida y autenticada |
| Commit incorporado en `master` | `c0c07fff5dd0a05be41d6993eb1abf10b0e74706` |
| Ascendencia | Commit implementado y merge verificados como ancestros de `origin/master` |
| Base aceptada | `HU-024`: PR `#39`, commit `9543d417e0515b5de6bad5f10898cd262b44d1fb`, pipeline `TECH-BASE-003 / PR gates` `SUCCESS` run `33994007606`, aprobación humana y merge `8e534ddd4d6ca2ab6ce1a678beead9c2cbc7aec3`; ambos SHA ancestros de `origin/master`; PostgreSQL enfocado `17/17`; cero defectos bloqueantes conocidos |
| Entregable | Contratos propios `Sgol.Evidence`, almacenamiento S3-compatible privado SeaweedFS, adaptador `clamd`, validación acotada JPEG/PNG/PDF, SHA-256, cuarentena y corpus sintético seguro |
| Alcance | Infraestructura técnica sin consumidor funcional: objetos de 1 a 15 MiB, claves opacas, doble bucket, catálogo cerrado `LIMPIO`/`INFECTADO`/`INVALIDO`/`ERROR_ESCANEO` y denegación ante toda falla |
| Persistencia | Ninguna tabla, entidad, migración ni cambio en `SgolDbContext`; no existen `file_object`, `evidence_item` ni `evidence_version` |
| Configuración y secretos | Opciones validadas al inicio del consumidor; credenciales sólo externas; imágenes y paquetes fijados; HTTP sólo local/CI privado y HTTPS en los demás ambientes |
| Interfaz funcional | No incluida: cero endpoints, URLs firmadas, intenciones de carga, UI, asociación con obligaciones o servicios funcionales de `HU-025` |
| Pruebas | Unitarias completas `300/300`; arquitectura completa `17/17`; enfocadas sin Docker `31/31` unitarias y `2/2` arquitectura; suite externa SeaweedFS/ClamAV `1/1`, 0 errores, 0 omitidas, 41.4 s, ejecutada por el desarrollador después de corregir el manifiesto ARM64 aprobado |
| Gates | Restore locked `18/18`; build Release `18/18`; unitarias `300/300`; arquitectura `17/17`; enfocadas sin Docker `31/31` unitarias y `2/2` arquitectura; suite S3/ClamAV externa `1/1`; PostgreSQL no aplica; formato sin diferencias, con repetición técnica porque el primer proceso no devolvió código de salida; cero vulnerabilidades NuGet conocidas; modelo EF sin cambios pendientes; `Fuentes/` sin cambios y espejo `29/29` idéntico. El resultado de `git diff --check` se informa al entregar para no modificar el diff después del último gate |
| Cierre | Suite externa S3/escáner, commit exacto, pipeline, aprobación, merge, ascendencia y cero defectos bloqueantes verificados; PostgreSQL no aplicó porque la tarea no creó persistencia |
| Siguiente tarea ejecutada | `HU-025` — propuesta actual en rama |

La evidencia primaria de cierre pertenece al PR que contiene la implementación. Este registro posterior sólo materializa el estado efectivo ya adquirido tras el merge y no altera el entregable aceptado.

## Base aceptada — `HU-024`

| Campo | Valor |
|---|---|
| Última tarea Terminada | `HU-024` — Dirección versiona evidencia requerida por TAR |
| Estado | `Terminada` |
| Contrato | `F07_ADENDA_16_CONTRATO_DE_POLITICA_DE_EVIDENCIA_VERSIONADA_HU_024.md`, aprobada íntegramente el 2026-09-05 |
| Pull request | `#39` |
| Commit implementado | `9543d417e0515b5de6bad5f10898cd262b44d1fb` |
| Pipeline requerido | `TECH-BASE-003 / PR gates`; `SUCCESS`, run `33994007606`, correspondiente al commit exacto |
| Aceptación humana | Recibida y autenticada |
| Commit incorporado en `master` | `8e534ddd4d6ca2ab6ce1a678beead9c2cbc7aec3` |
| Ascendencia | Commit implementado y merge verificados como ancestros de `origin/master` |
| Pruebas PostgreSQL | Enfocadas `17/17`, 0 errores y 0 omitidas; cero defectos bloqueantes conocidos |
| Entregable | Catálogo cerrado de 27 requisitos para las ocho TAR, política versionada, único `PUT /api/v1/task-definitions/{taskCode}/evidence-policy` y proyección `currentEvidencePolicy` en la lectura TAR existente |
| Gates | Restore locked `16/16`; build Release `16/16`; unitarias `269/269`; arquitectura `15/15`; enfocadas sin Docker `16/16` unitarias y `3/3` arquitectura; PostgreSQL externo `17/17`; formato sin diferencias; cero vulnerabilidades conocidas; modelo EF sin cambios pendientes; `Fuentes/` sin cambios y espejo `29/29` idéntico; `git diff --check` satisfactorio |
| Siguiente tarea ejecutada | `TECH-EVID-001` — propuesta actual en rama |

La evidencia primaria de cierre pertenece al PR que contiene la implementación. Este registro posterior sólo materializa el estado efectivo ya adquirido tras el merge y no altera el entregable aceptado.

## Base aceptada — `HU-023`

| Campo | Valor |
|---|---|
| Tarea | `HU-023` — Usuario consulta tarea, procedencia e historia permitida |
| Estado | `Terminada` |
| Contrato | `F07_ADENDA_15_CONTRATO_DE_CONSULTA_DE_TRABAJO_E_HISTORIA_PERMITIDA_HU_023.md`, aprobada íntegramente el 2026-09-05 |
| Pull request | `#38` |
| Commit implementado | `de8e9d746687e892e5d4baf1d23ed0745aa4865b` |
| Pipeline requerido | `TECH-BASE-003 / PR gates`; `SUCCESS`, run `33986965205`, correspondiente al commit exacto |
| Aceptación humana | Recibida y autenticada |
| Commit incorporado en `master` | `8a411596086fe1c0a95ec3c04f6c3ae8542b76c6` |
| Ascendencia | Commit implementado y merge verificados como ancestros de `origin/master` |
| Pruebas PostgreSQL | Enfocadas `3/3`; cero defectos bloqueantes conocidos |
| Entregable | Contratos `Sgol.Execution`, lector PostgreSQL read-only y únicamente `GET /api/v1/obligations` y `GET /api/v1/obligations/{id}` |
| Gates | Restore locked `16/16`; build Release `16/16`; unitarias `254/254`; arquitectura `13/13`; enfocadas sin Docker `20/20` unitarias y `2/2` arquitectura; PostgreSQL externo `3/3`; Playwright no aplica; formato sin diferencias; cero vulnerabilidades conocidas; modelo EF sin cambios pendientes; `Fuentes/` sin cambios y espejo `29/29` idéntico; `git diff --check` satisfactorio |
| Siguiente tarea ejecutada | `HU-024` — propuesta actual en rama |

La evidencia primaria de cierre pertenece al PR que contiene la implementación. Este registro posterior sólo materializa el estado efectivo ya adquirido tras el merge y no altera el entregable aceptado.

## Base aceptada — `TECH-E2E-CV-02`

| Campo | Valor |
|---|---|
| Tarea | `TECH-E2E-CV-02` — Demo automatizada y cierre del corte `CV-02` |
| Estado | `Terminada` |
| Contrato | `F07_ADENDA_14_CONTRATO_DE_DEMO_AUTOMATIZADA_Y_CIERRE_CV_02_TECH_E2E_CV_02.md`, incluida su revisión de interoperabilidad `HU-018`/`HU-021`, aprobada íntegramente el 2026-09-05 |
| Pull request | `#37` |
| Commit implementado | `f74261988866819a1fc09c2a598d137538530fc5` |
| Pipeline requerido | `TECH-BASE-003 / PR gates`; `SUCCESS`, run `33983298961` |
| Aceptación humana | Implementación y merge aprobados el 2026-09-05 |
| Commit incorporado en `master` | `e2d059c11b9647e92e10c49eff00e148b87c6f7c` |
| Ascendencia | Commit implementado y merge verificados como ancestros de `origin/master` |
| Base aceptada | `HU-013`: PR `#36`, commit `c7501abd7534edd2a84115a8dec803872819cb97`, pipeline `SUCCESS` run `33975676912`, aprobación humana, merge `0b21a1173c8e31c82cc65cf5cc51b4d27464a36e` y ascendencia verificada en `origin/master` |
| Entregable | Host desechable `tests/Sgol.Cv02Demo`, PostgreSQL real, servicios aceptados, `Sgol.Worker`, Razor Pages y Playwright headless |
| Alcance demostrado | 13 escenarios para configuración, calendario, manual, recurrencia, obligación, elegibilidad, asignación, corrección, plan, publicación, recuperación, autorización negativa, historia y límites del corte |
| Persistencia | Base efímera creada desde cero; semilla `CV02-SEED-V1`; sólo `LOR-001`; capa lectora `REPEATABLE READ`, `READ ONLY` y `AsNoTracking` |
| Producción | Corrección aprobada limitada a que `HU-021` resuelva la política de una asignación automática mediante su evaluación persistida; cero migraciones, tablas, entidades, endpoints, permisos o UI productivos nuevos |
| Gates | Restore locked `15/15`; build Release `15/15`; unitarias `234/234`; arquitectura `11/11`; harness sin Docker `15/15`; PostgreSQL/Testcontainers externo `172/172`; demo externa `S01`-`S13`, Chromium 151 escritorio/teléfono, WebKit 26.5 teléfono y limpieza `PASSED`; formato sin diferencias; cero vulnerabilidades conocidas; modelo EF sin cambios pendientes; `Fuentes/` sin cambios y espejo `29/29` idéntico. El resultado de `git diff --check` se informa al entregar para no modificar el diff después del último gate |
| Cierre | Commit exacto, pipeline verde, aprobación humana, merge, ascendencia, demo satisfactoria y cero defectos bloqueantes verificados; `CV-02` cerrado técnicamente |
| Siguiente tarea | `HU-023` es la siguiente tarea efectiva; no iniciada |

La evidencia primaria de cierre pertenece al PR que contiene la implementación. Este registro posterior sólo materializa el estado efectivo ya adquirido tras el merge y no altera el entregable aceptado.

## Base aceptada — `HU-013`

| Campo | Valor |
|---|---|
| Última tarea Terminada | `HU-013` — Job genera recurrencias vencidas sin duplicar |
| Estado | `Terminada` |
| Dependencias aceptadas | `TECH-JOBS-001`, `HU-012`, `HU-015`, `HU-018`, `HU-020` y dependencias anteriores registradas |
| Pull request | `#36` |
| Commit implementado | `c7501abd7534edd2a84115a8dec803872819cb97` |
| Pipeline requerido | `TECH-BASE-003 / PR gates`; SUCCESS, run `33975676912` |
| Aceptación humana | Contrato e implementación aprobados; autorización de merge recibida explícitamente |
| Commit incorporado en `master` | `0b21a1173c8e31c82cc65cf5cc51b4d27464a36e` |
| Ascendencia | Commit implementado y merge verificados como ancestros de `origin/master` |
| `Fuentes/` | Sin cambios; protección satisfactoria y 29 documentos Markdown del espejo idénticos byte por byte |
| Pruebas | Unitarias completas `234/234`; arquitectura completa `11/11`; suite PostgreSQL/Testcontainers completa del corte corregido ejecutada externamente por el desarrollador: `169/169`, 0 errores, 0 omitidas, 515.2 s |
| Gates | Restore locked, build Release, unitarias, arquitectura, PostgreSQL/Testcontainers externo, formato, vulnerabilidades, modelo EF sin cambios pendientes, protección y espejo de `Fuentes/` y `git diff --check`: satisfactorios |
| Decisión específica | `F07_ADENDA_13_CONTRATO_DE_GENERACION_RECURRENTE_HU_013.md`: `TAR-0005`, ventanas históricas, actor sistema, cadena reanudable, resultados, telemetría y migración única |
| Siguiente tarea ejecutada | `TECH-E2E-CV-02` — `Terminada`; PR `#37`, commit `f74261988866819a1fc09c2a598d137538530fc5`, merge `e2d059c11b9647e92e10c49eff00e148b87c6f7c` |

La evidencia primaria de cierre pertenece al mismo PR que contiene la implementación y este registro histórico. No se crea un commit administrativo separado para añadir retrospectivamente el run o el hash de merge; esta base aceptada viaja con el siguiente cambio funcional.

## Base aceptada — `TECH-JOBS-001`

| Campo | Valor |
|---|---|
| Última tarea Terminada | `TECH-JOBS-001` — Host Worker, bloqueo PostgreSQL, outbox y telemetría |
| Pull request | `#35` |
| Commit implementado | `1517be53434f4d4af2aefd136f462d6c08eeb9df` |
| Pipeline requerido | `TECH-BASE-003 / PR gates`; SUCCESS, run `33923994226` |
| Aceptación humana | Recibida explícitamente |
| Commit incorporado en `master` | `f8217da95d59718c7f3bd7b21c09f4bb56122b6c` |
| Ascendencia | Commit implementado y merge verificados como ancestros de `origin/master` al iniciar `HU-013` |
| `Fuentes/` | Sin cambios; protección confirmada por preflight |
| Siguiente tarea propuesta | `HU-013` — Job genera recurrencias vencidas sin duplicar |

La propuesta histórica de `TECH-JOBS-001` que viajó dentro de su PR no se reescribe mediante un commit administrativo. La evidencia primaria anterior acredita su cierre y habilita `HU-013`.

## Base aceptada — `HU-021`

| Campo | Valor |
|---|---|
| Última tarea Terminada | `HU-021` — Superior publica su alcance incrementalmente |
| Pull request | `#34` |
| Commit implementado | `6e22847081a10043fce0fe9c22ca25157e4e18bc` |
| Pipeline requerido | `TECH-BASE-003 / PR gates`; SUCCESS, run `33917438014` |
| Aceptación humana | Recibida explícitamente |
| Commit incorporado en `master` | `dc1ba3d92c998297d0bb9b4ad638551be222f781` |
| `Fuentes/` | Sin cambios; protección confirmada al iniciar TECH-JOBS-001 |
| Siguiente tarea propuesta | `TECH-JOBS-001` — Host Worker, bloqueo PostgreSQL, outbox y telemetría |

La evidencia anterior se acepta sin crear un commit administrativo para alterar retrospectivamente el registro que viajó en el PR de `HU-021`; `6e228470` y `dc1ba3d` son ancestros verificados de `origin/master`.

## Base aceptada — `HU-020`

| Campo | Valor |
|---|---|
| Última tarea Terminada | `HU-020` — SGOL crea/recupera plan semanal único |
| Pull request | `#33` |
| Commit implementado | `89270827d9e684037c07fb287128fbda9ad3f378` |
| Pipeline requerido | `TECH-BASE-003 / PR gates`; SUCCESS, run `33911626162` |
| Aceptación humana | Recibida explícitamente |
| Commit incorporado en `master` | `3a344c8dd8bd5ff2003646ed0df8c448c2c2cdf2` |
| `Fuentes/` | Sin cambios; protección confirmada al iniciar HU-021 |
| Siguiente tarea propuesta | `HU-021` — Superior publica su alcance incrementalmente |

La evidencia anterior se acepta sin crear un commit administrativo para alterar retrospectivamente el registro que viajó en el PR de `HU-020`; `89270827` y `3a344c8d` son ancestros verificados de `origin/master`.

## Base aceptada — `HU-019`

| Campo | Valor |
|---|---|
| Última tarea Terminada | `HU-019` — Superior corrige asignación inferior con motivo |
| Pull request | `#32` |
| Commit implementado | `ca62f925d8505715775a0f245ef818d1d292812f` |
| Pipeline requerido | `TECH-BASE-003 / PR gates`; SUCCESS, run `33906693457` |
| Aceptación humana | Recibida explícitamente |
| Commit incorporado en `master` | `0e58cc10ef66d54681eadfd63858941eb584e44b` |
| `Fuentes/` | Sin cambios; protección confirmada al iniciar HU-020 |
| Siguiente tarea propuesta | `HU-020` — SGOL crea/recupera plan semanal único |

La evidencia anterior se acepta sin crear un commit administrativo para alterar retrospectivamente el registro que viajó en el PR de `HU-019`; `ca62f925` y `0e58cc10` son ancestros verificados de `origin/master`.

## Base aceptada — `HU-018`

| Campo | Valor |
|---|---|
| Última tarea Terminada | `HU-018` — SGOL asigna por carga y desempates aprobados |
| Pull request | `#31` |
| Commit implementado | `a77c908bc6f9f6c5a1c84981c2f0bd0d30f0a3c7` |
| Pipeline requerido | `TECH-BASE-003 / PR gates`; SUCCESS, run `33900052905` |
| Aceptación humana | Recibida explícitamente |
| Commit incorporado en `master` | `085f7fe54c6ca24e4ce257423361d6c67d515111` |
| Pruebas PostgreSQL/Testcontainers específicas y afectadas | Suite completa satisfactoria |
| `Fuentes/` | Sin cambios; protección confirmada al iniciar `HU-019` |
| Siguiente tarea propuesta | `HU-019` — Superior corrige asignación inferior con motivo |

La evidencia anterior se acepta sin crear un commit administrativo para alterar retrospectivamente el registro que viajó en el PR de `HU-018`; `a77c908b` es ancestro verificado de `origin/master`.

## Base aceptada — `HU-004`

| Campo | Valor |
|---|---|
| Última tarea Terminada | `HU-004` — Superior consulta carga activa correcta |
| Pull request | `#30` |
| Commit implementado | `ff95e0a4c014a6000829bda1ccbde55d991a8c49` |
| Pipeline requerido | `TECH-BASE-003 / PR gates`; SUCCESS, run `33894988459` |
| Aceptación humana | Recibida explícitamente |
| Commit incorporado en `master` | `697f402ac2a4b8c0a45bac317c8a3422c860fa0f` |
| Pruebas PostgreSQL/Testcontainers específicas y afectadas | Suite completa satisfactoria |
| `Fuentes/` | Sin cambios; protección confirmada al iniciar `HU-018` |
| Siguiente tarea propuesta | `HU-018` — SGOL asigna por carga y desempates aprobados |

La evidencia anterior se acepta sin crear un commit administrativo para alterar retrospectivamente el registro que viajó en el PR de `HU-004`; `ff95e0a4` es ancestro verificado de `origin/master`.

## Base aceptada — `HU-016`

| Campo | Valor |
|---|---|
| Última tarea Terminada | `HU-016` — SGOL calcula candidatos y explica exclusiones |
| Pull request | `#29` |
| Commit implementado | `1be3622658046f7cbfcb61e6e40fa03580b43856` |
| Pipeline requerido | `TECH-BASE-003 / PR gates`; SUCCESS, run `33890306730` |
| Aceptación humana | Recibida explícitamente |
| Commit incorporado en `master` | `4f28b5f2cd459be1944de47a5b6b98ce5e1d4ecc` |
| Pruebas PostgreSQL/Testcontainers específicas y afectadas | Suite completa satisfactoria |
| `Fuentes/` | Sin cambios; protección y ascendencia confirmadas al iniciar `HU-004` |
| Siguiente tarea propuesta | `HU-004` — Superior consulta carga activa correcta |

La evidencia anterior se acepta sin crear un commit administrativo para alterar retrospectivamente el registro que viajó en el PR de `HU-016`; `1be36226` es ancestro verificado de `origin/master`.

## Base aceptada — `HU-015`

| Campo | Valor |
|---|---|
| Última tarea Terminada | `HU-015` — SGOL crea o recupera una obligación única |
| Pull request | `#28` |
| Commit implementado | `2a8d804743d10ec1213c78f375215318f38c5348` |
| Pipeline requerido | `TECH-BASE-003 / PR gates`; SUCCESS, run `33813046606` |
| Aceptación humana | Recibida explícitamente |
| Commit incorporado en `master` | `046c1c43e4c481498cbd536cd8e84a43ac5a7848` |
| Pruebas PostgreSQL/Testcontainers específicas y afectadas | `99/99` efectivas, 0 omitidas |
| `Fuentes/` | Sin cambios; ascendencia e integridad confirmadas al iniciar `HU-016` |
| Siguiente tarea propuesta | `HU-016` — SGOL calcula candidatos y explica exclusiones |

La evidencia anterior se acepta sin crear un commit administrativo para alterar retrospectivamente el registro que viajó en el PR de `HU-015`; `2a8d8047` es ancestro verificado de `origin/master`.

## Base aceptada — `HU-014`

| Campo | Valor |
|---|---|
| Última tarea Terminada | `HU-014` — Actor autorizado solicita generación idempotente |
| Pull request | `#27` |
| Commit implementado | `150cc3d07812a4db0aba7c9d1540affe2191e9ec` |
| Pipeline requerido | `TECH-BASE-003 / PR gates`; SUCCESS, run `33810084782` |
| Aceptación humana | Recibida explícitamente |
| Commit incorporado en `master` | `22967600ba62388a3469b6d7f6a926922260ed71` |
| Pruebas PostgreSQL/Testcontainers específicas y afectadas | `93/93` efectivas |
| `Fuentes/` | Sin cambios; protección confirmada por el preflight de HU-015 |
| Siguiente tarea propuesta | `HU-015` — SGOL crea o recupera una obligación única |

La evidencia anterior se acepta sin crear un commit administrativo para alterar retrospectivamente el registro que viajó en el PR de `HU-014`; `150cc3d0` es ancestro verificado de `origin/master` en el inicio de HU-015.

## Base aceptada — `HU-012`

| Campo | Valor |
|---|---|
| Última tarea Terminada | `HU-012` — Dirección configura alta manual/recurrencia permitida |
| Pull request | `#26` |
| Commit implementado | `1a49f534c7d6831c15659ccc72bbe3464f2cc119` |
| Pipeline requerido | `TECH-BASE-003 / PR gates`; SUCCESS, run `33805254360` |
| Aceptación humana | Recibida explícitamente |
| Commit incorporado en `master` | `efdec44acccd9ba4e56ef61f636e7c9bf73abd53` |
| Pruebas PostgreSQL/Testcontainers específicas y afectadas | `88/88` efectivas, 0 omitidas; ejecución completa `86/88` y reejecución enfocada de los dos controles corregidos `2/2` |
| `Fuentes/` | Sin cambios; protección verificada en el cierre aceptado |
| Siguiente tarea propuesta | `HU-014` — Actor autorizado solicita generación idempotente |

La evidencia anterior se acepta sin crear un commit administrativo para alterar retrospectivamente el registro que viajó en el PR de `HU-012`.

## Base aceptada — `HU-017`

| Campo | Valor |
|---|---|
| Última tarea Terminada | `HU-017` — Dirección versiona elegibilidad por TAR |
| Pull request | `#25` |
| Commit implementado | `e5828fab8d919165d167e9bd1139bcbcabfade16` |
| Pipeline requerido | `TECH-BASE-003 / PR gates`; SUCCESS, run `33799055901` |
| Aceptación humana | Recibida explícitamente |
| Commit incorporado en `master` | `7ed2982b6db0d988333bb1ccf96caf2b8701dc0d` |
| `Fuentes/` | Sin cambios |
| Siguiente tarea propuesta | `HU-012` — Dirección configura alta manual/recurrencia permitida |

La evidencia anterior se acepta sin repetir los análisis ni gates históricos de `HU-017` o sus dependencias.

## Base aceptada — `HU-011`

| Campo | Valor |
|---|---|
| Última tarea Terminada | `HU-011` — Dirección mantiene sólo ocho definiciones TAR |
| Pull request | `#24` |
| Commit implementado | `611cd824e320e6344109f25c1e6c3ef7728336ea` |
| Pipeline requerido | `TECH-BASE-003 / PR gates`; SUCCESS, run `33792061129` |
| Aceptación humana | Recibida explícitamente |
| Commit incorporado en `master` | `6ee5231437a84cd8d20c370d5a39938d6bd0d54d` |
| Pruebas PostgreSQL/Testcontainers específicas y afectadas | `17/17` |
| `Fuentes/` | Sin cambios |
| Siguiente tarea propuesta | `HU-017` — Dirección versiona elegibilidad por TAR |

La evidencia anterior se acepta sin crear un commit administrativo para alterar retrospectivamente el registro que viajó en el PR de `HU-011`.

## Base aceptada anterior — `HU-010`

| Campo | Valor |
|---|---|
| Última tarea Terminada | `HU-010` — Usuario autorizado obtiene semana ISO única |
| Pull request | `#23` |
| Commit implementado | `dc2c734c5a8ee1e6fb71d0824e92c3aeca91cb1c` |
| Pipeline requerido | `TECH-BASE-003 / PR gates`; SUCCESS, run `33783854341` |
| Aceptación humana | Recibida explícitamente |
| Commit incorporado en `master` | `9b9ec7fea55b8361ec890cf992c9d58a1880df3f` |
| Pruebas PostgreSQL/Testcontainers específicas | `6/6` |
| `Fuentes/` | Sin cambios; 29 documentos Markdown idénticos byte por byte |
| Siguiente tarea propuesta | `HU-011` — Dirección mantiene sólo ocho definiciones TAR |

La evidencia anterior se acepta sin repetir los análisis ni gates históricos de `HU-010`, `HU-009`, `HU-008`, `TECH-VER-001` o sus dependencias.

## Base aceptada anterior — `HU-009`

| Campo | Valor |
|---|---|
| Última tarea Terminada | `HU-009` — Dirección administra calendario de `LOR-001` |
| Pull request | `#22` |
| Commit implementado | `5df3eddf28874aebb6320b13c581c2d69a483a8d` |
| Pipeline requerido | `TECH-BASE-003 / PR gates`; SUCCESS, run `33774798395` |
| Aceptación humana | Recibida explícitamente |
| Commit incorporado en `master` | `ba867ee418d86caa0811adabfbaa311afe8dff1f` |
| Pruebas PostgreSQL/Testcontainers específicas | `7/7` |
| `Fuentes/` | Sin cambios; 29 documentos Markdown idénticos byte por byte |
| Siguiente tarea propuesta | `HU-010` — Usuario autorizado obtiene semana ISO única |

La evidencia anterior se conserva como cierre histórico de `HU-009`.

## Base aceptada anterior — `HU-008`

| Campo | Valor |
|---|---|
| Última tarea Terminada | `HU-008` — Dirección publica configuración versionada |
| Pull request | `#21` |
| Commit implementado | `3aa62b2aa88a6983db1446cb75a466ebcc1fa8d1` |
| Commit correctivo | `c2e46a0c7fa4d77f5c56abde97d58b360acf4088` |
| Pipeline requerido | `TECH-BASE-003 / PR gates`; SUCCESS, run `33696761780` |
| Aceptación humana | Recibida explícitamente |
| Commit incorporado en `master` | `d37516e78dbc023fa5feef2994934c1b7d1c15db` |
| Pruebas PostgreSQL/Testcontainers específicas | `5/5`; prueba correctiva `1/1` |
| `Fuentes/` | Sin cambios |
| Siguiente tarea propuesta | `HU-009` — Dirección administra calendario de `LOR-001` |

La evidencia anterior se conserva como cierre histórico de `HU-008`.

## Base aceptada anterior — `TECH-VER-001`

| Campo | Valor |
|---|---|
| Última tarea Terminada | `TECH-VER-001` — Núcleo técnico reutilizable de versionado |
| Pull request | `#20` |
| Commit implementado | `9d7c001275e119c13bed81b544347d3bd2f55e16` |
| Pipeline requerido | `TECH-BASE-003 / PR gates`; SUCCESS, run `33682634449` |
| Aceptación humana | Recibida explícitamente |
| Commit incorporado en `master` | `70222cd1997bf64ad29638f9a27c5268058e118a` |
| Pruebas PostgreSQL/Testcontainers específicas | `4/4` |
| `Fuentes/` | Sin cambios |

## Base aceptada anterior — `HU-003`

| Campo | Valor |
|---|---|
| Última tarea Terminada | `HU-003` — Dirección registra disponibilidad binaria diaria |
| Corte y épica | `CV-01` / `EP-01` |
| Pull request | `#19` |
| Commit implementado | `c96090cfad5595bd02eee42988b2c4930703ad81` |
| Pipeline requerido | `TECH-BASE-003 / PR gates`; PASS, run `33678901792` |
| Aceptación humana | Recibida explícitamente |
| Commit incorporado en `master` | `89f82bafb66d4dc3b04ffee1d4fc6c80644ea826` |
| Pruebas PostgreSQL/Testcontainers específicas | `8/8` |
| `Fuentes/` | Sin cambios |

## Base aceptada anterior — `HU-007`

| Campo | Valor |
|---|---|
| Última tarea Terminada | `HU-007` — Dirección asigna un rol canónico activo |
| Corte y épica | `CV-01` / `EP-01` |
| Pull request | `#18` |
| Commit implementado | `e249ef6d934c0ddb36e7294d5431d05e67829467` |
| SHA final del PR | `5057e9e3c2ca53288141c7e96e168cc61837fb17` |
| Pipeline requerido | `TECH-BASE-003 / PR gates`; PASS, run `33674953778` |
| Aceptación humana | Recibida explícitamente |
| Commit incorporado en `master` | `b72377ee2a31a0b7033771dc5d249a913d7c7f56` |
| `Fuentes/` | Sin cambios; integridad confirmada al iniciar `HU-003` |

## Base aceptada anterior — `HU-006`

| Campo | Valor |
|---|---|
| Última tarea Terminada | `HU-006` — Dirección administra cuenta individual |
| Corte y épica | `CV-01` / `EP-01` |
| Pull request | `#17` |
| Commit implementado | `b95c29851a1f283aa6699f45008f1743c7811715` |
| Pipeline requerido | `TECH-BASE-003 / PR gates`; PASS, run `33669367457` |
| Aceptación humana | Recibida explícitamente |
| Commit incorporado en `master` | `ff8296993aa2531e4d6d6c06ef363a542969fdda` |
| `Fuentes/` | Sin cambios |

## Base aceptada anterior — `HU-002`

| Campo | Valor |
|---|---|
| Última tarea Terminada | `HU-002` — Dirección registra puesto/turno sin conceder permisos |
| Corte y épica | `CV-01` / `EP-01` |
| Pull request | `#16` |
| Commit implementado | `44b6548dc44583bbf73d897294f333b5e07dae88` |
| Pipeline requerido | `TECH-BASE-003 / PR gates`; PASS, run `33581043036` |
| Aceptación humana | Recibida explícitamente |
| Commit incorporado en `master` | `dc6d92e616e0e6d71936bf0c74839c4112941c81` |
| `Fuentes/` | Sin cambios |

## Base aceptada anterior — `HU-001`

| Campo | Valor |
|---|---|
| Última tarea Terminada | `HU-001` — Dirección crea/corrige persona y vigencia con historia |
| Corte y épica | `CV-01` / `EP-01` |
| Pull request | `#15` |
| Commit implementado | `48c549c8fc31f0c7c5a0197ca53e98b4e2769cc7` |
| Aceptación humana | Recibida el 2026-09-01 |
| Commit incorporado en `master` | `b74ab4ade5b0b8d0bab5e99530e38c05afdaff41` |

## Base aceptada anterior — `HU-005`

| Campo | Valor |
|---|---|
| Última tarea Terminada | `HU-005` — Reconocer únicamente `LOR-001` y rechazar otra sucursal |
| Corte y épica | `CV-01` / `EP-01` |
| Pull request | `#14` |
| Commit implementado | `a9588f6f7dda5d443d346e35eaebb73c95174aee` |
| Aceptación humana | Recibida el 2026-09-01 |
| Commit incorporado en `master` | `6b5183025641415c4b6dc7dde1975406bf41e66a` |

## Base aceptada anterior — `TECH-UI-001`

| Campo | Valor |
|---|---|
| Última tarea Terminada | `TECH-UI-001` — Base de interfaz compartida |
| Pull request | `#13` |
| Commit implementado | `d723d2fb91a86bf34e886ebc84dda77c72bd1dec` |
| Aceptación humana | Recibida el 2026-09-01 |
| Commit incorporado en `master` | `0209cec724c13ce27de9a27afb183f458385b9b2` |

## Base aceptada anterior — `TECH-ID-BOOT-001`

| Campo | Valor |
|---|---|
| Última tarea Terminada | `TECH-ID-BOOT-001` — bootstrap controlado y de un solo uso de la primera cuenta `DIRECCION` |
| Pull request | `#12` |
| Commit implementado | `02b43dab088e14cd70935cc97d3028b447378eaa` |
| Aceptación humana | Recibida el 2026-09-01 |
| Commit incorporado en `master` | `0843a6a` |

## Rebaselización excepcional de fuente

| Campo | Valor |
|---|---|
| Archivo | `Fuentes/SGOL v2.0 Sistema de Gestion Operativa Loretta - S050_BKP_PRE_NORMALIZACION_V1.xlsm` |
| Motivo | Guardado accidental informado por el responsable; se autoriza que el archivo resultante sea la nueva fuente |
| Blob Git anterior | `07688ab93b219317118099e1954ad2715d1004f8` |
| SHA-256 aceptado | `77C6761B9FF4F390A2D19AE9CBEAA66CF67B2992C3C3340F39372E46229FD302` |
| Aprobación humana | Recibida el 2026-09-01 en la solicitud de rebaselización |
| Eficacia | Propuesta en esta rama; efectiva sólo cuando el archivo, el manifiesto y el verificador estén incorporados juntos en `master` con el check requerido aprobado |

La excepción comprueba una sola transición desde el blob anterior al contenido aceptado. Después de su incorporación no autoriza otro guardado del libro ni modifica la regla general de sólo lectura lógica de `Fuentes/`.

## Tareas insertadas por adenda

Esta tabla forma parte de la comprobación de precedencia obligatoria antes de iniciar cualquier tarea. Una tarea con estado distinto de `Terminada` bloquea la tarea indicada en `Ejecutar antes de` y toda historia posterior que dependa de esa precedencia.

| Tarea insertada | Adenda de origen | Ejecutar antes de | Estado |
|---|---|---|---|
| `TECH-UI-001` | `F07_ADENDA_02_TAREAS_DE_BASE_DE_INTERFAZ.md` | `HU-005` | `Terminada`; PR `#13`, commit `d723d2fb91a86bf34e886ebc84dda77c72bd1dec`, merge `0209cec724c13ce27de9a27afb183f458385b9b2` |
| `TECH-VER-001` | `F07_ADENDA_03_NUCLEO_DE_VERSIONADO.md` | `HU-008` | `Terminada`; PR `#20`, commit `9d7c001275e119c13bed81b544347d3bd2f55e16`, merge `70222cd1997bf64ad29638f9a27c5268058e118a` |
| `TECH-JOBS-001` | `F07_ADENDA_12_CONTRATO_DE_WORKER_OUTBOX_Y_JOBS_TECH_JOBS_001.md` | `HU-013` | `Terminada`; PR `#35`, commit `1517be53434f4d4af2aefd136f462d6c08eeb9df`, merge `f8217da95d59718c7f3bd7b21c09f4bb56122b6c` |
| `TECH-E2E-CV-02` | `F07_ADENDA_14_CONTRATO_DE_DEMO_AUTOMATIZADA_Y_CIERRE_CV_02_TECH_E2E_CV_02.md` | Cierre de `CV-02` y `HU-023` | `Terminada`; PR `#37`, commit `f74261988866819a1fc09c2a598d137538530fc5`, pipeline `SUCCESS` run `33983298961`, aprobación humana, merge `e2d059c11b9647e92e10c49eff00e148b87c6f7c` y ascendencia verificada en `origin/master`; `HU-023` habilitada como siguiente tarea efectiva |
| `TECH-EVID-001` | `F07_ADENDA_17_CONTRATO_DE_INFRAESTRUCTURA_SEGURA_DE_EVIDENCIA_TECH_EVID_001.md` | `HU-025` | `Terminada`; PR `#40`, commit `eb29656f7d2ced765924115520119f36529fc0fc`, pipeline `SUCCESS` run `34153966707`, aprobación humana, SeaweedFS/ClamAV `1/1`, merge `c0c07fff5dd0a05be41d6993eb1abf10b0e74706` y ascendencia verificada en `origin/master` |
| `TECH-EVID-002` | `F07_ADENDA_20_CONTRATO_DE_EVIDENCIA_ESTRUCTURADA_TECH_EVID_002.md` | `HU-026` | `Terminada`; PR `#42`, commit `66248692f2577a95384db62882e8081dec6fdd9e`, pipeline `SUCCESS` run `34264672922`, aprobación humana, PostgreSQL `7/7`, merge `bd12592660121762cdf315fb5cfc497540b6892c` y ascendencia verificada en `origin/master` |
| `TECH-E2E-CV-03` | `F07_ADENDA_23_CONTRATO_DE_DEMO_AUTOMATIZADA_Y_CIERRE_CV_03_TECH_E2E_CV_03.md` | Cierre de `CV-03` y `HU-027` | `Terminada`; PR `#46`, commit `157bc43886b58a2e1a0c036dbda437114cf9f283`, pipeline `SUCCESS` run `34514719087`, aprobación humana, merge `437f1d875491097d208ca7fc3ad11d4094b26b81` y ascendencia verificada en `origin/master`; `HU-027` habilitada |
| `TECH-AUTH-001` | `F07_ADENDA_41_CONTRATO_DE_AUTENTICACION_HOSPEDADA_Y_SESION.md` | `TECH-E2E-CV-04` y frontend | `Terminada`; PR `#56`, cabeza `8cb881f3192a534e9a101d169fc38fcb0f749fc2`, pipeline `SUCCESS` run `35469361269`, merge `10309937f596e5f6702181183b0cbaaa1b52f4c4` y ascendencia verificada |
| `TECH-E2E-CV-04` | `F07_ADENDA_42_CONTRATO_DE_DEMO_AUTOMATIZADA_Y_CIERRE_CV_04_TECH_E2E_CV_04.md` | Cierre de `CV-04` y entrada a `CV-05` | `Terminada`; PR `#58`, cabeza `945c966edbc84552bab5abd91a9866e128f18ead`, pipeline `SUCCESS` run `35772495865`, merge `00da83ca26f67e523d0d6067af737f38315e62b0`; `CV-04` cerrado |
| `TECH-E2E-CV-05` | `F07_ADENDA_43_CONTRATO_DE_DEMO_AUTOMATIZADA_Y_CIERRE_CV_05_TECH_E2E_CV_05.md` | Cierre de `CV-05` y planificación frontend | `Terminada`; PR `#59`, cabeza `5f92fbc4c38acdc711513d447dac615bd8a2aed8`, pipeline `SUCCESS` run `35797088036`, merge `32c3961ff67f79670d0824da71d4f70a06d1dc01`; `CV-05` cerrado |
| `TECH-UI-PLAN-001` | `F07_ADENDA_44_PLANIFICACION_CONTRACTUAL_DEFINITIVA_DEL_FRONTEND.md` | `TECH-FRONT-001` | `Terminada`; entregables aprobados e integrados mediante PR `#60`, pipeline `35808190514` `SUCCESS`, merge `d311d967e925f4b9c1d1161bb86914d587d809af` |
| `TECH-FRONT-001` | `F07_ADENDA_45_BACKLOG_FRONTEND_DEFINITIVO.md` | `TECH-FRONT-002` | `Integrada`; auditoría y paquete aprobado en `docs/traceability/TECH_FRONT_001_AUDITORIA_BASE_UI.md`; BR-D17 corregida; render/componentes 12/12 y arquitectura/accesibilidad 8/8; PR `#62`, cabeza `7a0d8540dbfd211fc0f5156a02d834c5832bbabf`, check requerido `SUCCESS` run `35906130681`, merge `8c76c094da653903cd72acce9373dd2b0e4a2914`; ambos ancestros de `origin/master` |
| `TECH-FRONT-002` | `F07_ADENDA_45_BACKLOG_FRONTEND_DEFINITIVO.md` | `TECH-FRONT-003` | `Integrada`; BR-API11/12 documentadas en `docs/traceability/TECH_FRONT_002_CLIENTE_API.md`; cliente HTTP común, build Release y pruebas enfocadas `41/41`; PR `#63`, cabeza `a464bdac745d6dc38b4e72fe40f5af824287b38a`, pipeline `35912549181` `SUCCESS`, merge `c5313aff32a62118d01e4bde6f139cbfcc1c23f9` |
| `TECH-FRONT-003` | Adendas `F07_ADENDA_45_BACKLOG_FRONTEND_DEFINITIVO.md` y `F07_ADENDA_46_MAPA_DE_NAVEGACION_Y_SESION_FRONTEND.md` | `TECH-FRONT-004` | `Integrada`: PR `#65` merged; cabeza `72ded2f86ad7baafa567b34cd3f84dad160ed699`, check `SUCCESS` run `35929424107`, merge `35239ebd50e120dd775c07d726df0ca899393b71`; ambos ancestros de `origin/master`. La descripción previa a publicación permanece como evidencia histórica en `TECH_FRONT_003_PRUEBAS.md` y `TECH_FRONT_003_BLOQUEO_401.md` |
| `TECH-FRONT-004` | `F07_ADENDA_45_BACKLOG_FRONTEND_DEFINITIVO.md` | Historias con navegador | `Implementada localmente` en `codex/tech-front-004`: harness Playwright de Kestrel HTTPS y PostgreSQL desechable, cuatro fixtures de sesión hospedada, Chromium escritorio/WebKit móvil, shell vacío, 401, teclado/foco, landmarks, comprobación de accesibilidad crítica, capturas enmascaradas y cleanup. Build Release de 25 proyectos sin errores ni advertencias, smoke enfocado `1/1` con ocho combinaciones y arquitectura afectada `9/9` verdes; comandos y mapa en `TECH_FRONT_004_HARNESS.md`. PR `#66` abierto; run `35934604798` falló por navegadores ausentes, `35935550111` por `net::ERR_NETWORK_CHANGED` y `35936627022` pasó el smoke aislado pero falló en formato. Instalación, aislamiento y formato corregidos en esta rama; pipeline exacto e integración requieren evidencia posterior. Suite funcional completa y login visual quedan fuera de alcance |
| `TECH-FRONT-005` | `F07_ADENDA_45_BACKLOG_FRONTEND_DEFINITIVO.md` | Después de `FRONT-001..020` | `No iniciada`; este cambio no anticipa su demo integral |
| `FRONT-001..020` | `F07_ADENDA_45_BACKLOG_FRONTEND_DEFINITIVO.md` | Según orden y dependencias de la Adenda 45 | `No iniciadas`; cada brecha bloquea sólo su historia consumidora |

## Base aceptada anterior — `TOOL-PLAN-004`

| Campo | Valor |
|---|---|
| Última tarea Terminada | `TOOL-PLAN-004` — Regla permanente de construcción de interfaz a partir de `docs/design` |
| Corte y épica | Tarea de herramientas autorizada fuera del backlog; no pertenece a un CV/EP funcional |
| Aceptación humana | Recibida el 2026-08-31 |
| `Fuentes/` | Protección requerida en los checks del PR |

## Base aceptada anterior — registro histórico inmutable

| Campo | Valor |
|---|---|
| Última tarea Terminada | `TECH-AUD-001` |
| Corte y épica | Tarea técnica de base; F07 no la asigna a un CV/EP funcional |
| Pull request | `#5` |
| Commit implementado | `855cda7974265a874b353d7522c92070e922f23d` |
| Pipeline requerido | `TECH-BASE-003 / PR gates`; PASS observado en GitHub Actions run `33434304623`, 2026-08-31 |
| Aceptación humana | Recibida el 2026-08-31 |
| Commit incorporado en `master` | `602f818b4ed6c43236b0c07411a00a9d5111779b` |
| `Fuentes/` | 73 archivos sin cambios; huella agregada `97EA9F86C185D597F5F43D9FCC244897572CE29CE5C50C268EF9F2CEC81C8962` |

## Comprobación rápida para el siguiente chat

```powershell
./scripts/ci/preflight.ps1
$implementationCommit = git log -1 --format=%H -- docs/traceability/IMPLEMENTATION_STATUS.md
git merge-base --is-ancestor $implementationCommit HEAD
```

Si el preflight confirma `master`, árbol limpio e integridad de `Fuentes/`, la comprobación de ascendencia devuelve código `0`, el PR consta como merged y su check requerido pasó, el checkout contiene la base aceptada. Si no hay una contradicción concreta, no se reanalizan TECH-INIT-001, TECH-BASE-002, TECH-BASE-003, TECH-BASE-004, TECH-BASE-005, TECH-AUD-001 o TOOL-FLOW-001 ni se repiten sus gates antes de comenzar la tarea nueva.

Una discrepancia obliga a detener el inicio incremental y revisar sólo la diferencia concreta. Cada tarea futura prepara la actualización de este archivo en el mismo commit y pull request que su implementación. En ese caso, `Commit implementado` puede valer `commit que contiene esta actualización`, una autorreferencia que Git resuelve sin intentar escribir dentro del archivo el SHA del propio commit. Antes del merge, `Commit incorporado en master` puede valer `pendiente de merge`; el hash de merge y el número de run son datos opcionales porque el PR, los checks ligados al commit implementado y la historia de `master` permiten resolverlos. El registro no convierte la tarea en Terminada mientras no se cumplan todas las condiciones de eficacia indicadas arriba.
