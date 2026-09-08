# Estado de implementación

Este archivo permite iniciar cada tarea de forma incremental. Registra evidencia aceptada conforme a `F07_ENMIENDA_001_CIERRE_DE_TAREA_EN_UN_PR.md`; no sustituye la fila del backlog, las fuentes autorizadas de la tarea actual ni sus gates de salida.

Una sección preparada en una rama de pull request es una propuesta de base aceptada. Sólo adquiere eficacia como `Terminada` cuando el registro y el commit implementado están incorporados en `master`, el PR consta como merged, el check requerido pasó para ese commit, existe aceptación humana y `Fuentes/` permaneció protegida.

## Propuesta actual en rama — `HU-026`

| Campo | Valor |
|---|---|
| Tarea | `HU-026` — SGOL evalúa evidencia completa e informa faltantes |
| Estado | Propuesta implementada en `codex/hu-026`. Este mismo registro adquiere automáticamente estado efectivo `Terminada` en `master`, sin commit administrativo posterior, sólo cuando el commit exacto tenga pipeline requerido verde, aprobación humana, merge, ascendencia verificada en `origin/master`, PostgreSQL satisfactorio y cero defectos bloqueantes conocidos |
| Contrato | `F07_ADENDA_19_CONTRATO_DE_EVALUACION_ESTRUCTURAL_DE_EVIDENCIA_HU_026.md`, revisada tras `TECH-EVID-002` y aprobada íntegramente el 2026-09-08 |
| Commit implementado | Commit que contiene esta actualización |
| Base aceptada | `TECH-EVID-002`: Adenda 20 aprobada; PR `#42`; commit `66248692f2577a95384db62882e8081dec6fdd9e`; pipeline requerido `TECH-BASE-003 / PR gates` `SUCCESS`, run `34264672922`; aprobación humana; PostgreSQL externo `7/7`; merge `bd12592660121762cdf315fb5cfc497540b6892c`; commit y merge ancestros de `origin/master`; cero defectos bloqueantes conocidos |
| Entregable | Único `GET /api/v1/obligations/{id}/evidence-review`; evalúa todos los requisitos de la política congelada con versiones `VIGENTE`, devuelve `COMPLETA` o `INCOMPLETA` y la lista determinista de faltantes |
| TAR-0092 | `F_ENT_001` vigente es la única autoridad de `DIFERENCIA_O_DANO`; si falta, la condición queda `NO_RESUELTA` y la revisión falla cerrada como `INCOMPLETA` |
| Autorización | Reutiliza `PER-TAREA-VER`, alcance de responsable/superiores/Dirección y convergencia `404` contra IDOR; el servidor decide todo el alcance |
| Persistencia | Migración `AddEvidenceReviewSnapshots`; tabla inmutable `evidence_review_snapshot`, JSONB canónico y de proyección, versiones usadas, huella única, FK `RESTRICT`, checks, índices y guarda PostgreSQL |
| Atomicidad y concurrencia | Evaluación y eventual inserción usan `SERIALIZABLE`; huella canónica reutiliza el mismo snapshot, una nueva versión produce otra historia y auditoría se inserta en la misma transacción |
| Pruebas | Pruebas unitarias, HTTP, arquitectura y PostgreSQL añadidas; los totales definitivos y el corte PostgreSQL externo se registran al completar los gates |
| Gates | Pendientes de ejecución final sobre el corte definitivo; PostgreSQL se solicita al desarrollador después de los cinco primeros gates |
| Límites | Sin conclusión, `execution_result`, validaciones, cambios de `execution_status`, UI, descarga, S3, SeaweedFS, ClamAV, outbox ni motor general de reglas |
| Cierre | Requiere commit exacto, pipeline verde, aprobación humana, merge, ascendencia, PostgreSQL externo satisfactorio y cero defectos bloqueantes |
| Siguiente tarea | Ninguna queda habilitada desde esta rama: `HU-022` sólo procede tras el cierre efectivo de `HU-026` |

Esta propuesta no autoriza commit, publicación de rama, apertura de pull request ni merge, y todavía no habilita `HU-022`.

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
