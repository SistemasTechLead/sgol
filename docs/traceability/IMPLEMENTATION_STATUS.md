# Estado de implementación

Este archivo permite iniciar cada tarea de forma incremental. Registra evidencia aceptada conforme a `F07_ENMIENDA_001_CIERRE_DE_TAREA_EN_UN_PR.md`; no sustituye la fila del backlog, las fuentes autorizadas de la tarea actual ni sus gates de salida.

En una rama de pull request, la sección siguiente es una propuesta de base aceptada. Sólo adquiere eficacia como `Terminada` cuando el registro y el commit implementado están incorporados en `master`, el PR consta como merged, el check requerido pasó para ese commit, existe aceptación humana y `Fuentes/` permaneció protegida.

## Propuesta actual en rama

| Campo | Valor |
|---|---|
| Tarea | `HU-019` — Superior corrige asignación inferior con motivo |
| Estado | Propuesta implementada en rama; no `Terminada` |
| Dependencias aceptadas | `HU-018` y `TECH-AUD-001` `Terminada`; reutiliza `work_obligation.row_version`, historia de `assignment_version`, snapshot HU-016, idempotencia y auditoría |
| Pull request | PR que incorpora esta actualización |
| Commit implementado | Commit que contiene esta actualización |
| Pipeline requerido | `TECH-BASE-003 / PR gates`; checks asociados al commit implementado |
| Aceptación humana | Contrato de `F07_ADENDA_09_CONTRATO_DE_CORRECCION_DE_ASIGNACION_HU_019.md` aprobado íntegramente; commit, publicación y merge permanecen pendientes de autorizaciones independientes |
| Commit incorporado en `master` | Pendiente de merge |
| `Fuentes/` | Protección requerida en los checks del PR |
| Pruebas | Unitarias `202/202`; arquitectura `9/9`; PostgreSQL/Testcontainers `122/122` efectivas, 0 omitidas: ejecución completa `120/122` y reejecución enfocada corregida `2/2` |
| Gates de propuesta | Restore bloqueado, build Release, formato, paquetes vulnerables, modelo EF sin cambios pendientes y protección/espejo de `Fuentes/` satisfactorios |
| Decisión específica | `F07_ADENDA_09_CONTRATO_DE_CORRECCION_DE_ASIGNACION_HU_019.md`: endpoint/comando, ETag de obligación, idempotencia, jerarquía estricta, revalidación, cadena, explicación, concurrencia, errores y auditoría |
| Siguiente tarea propuesta | Ninguna; no iniciar `HU-020` ni otra historia dependiente hasta que HU-019 cumpla suite PostgreSQL, pipeline, revisión humana y merge |

Esta propuesta no habilita dependencias ni modifica el cierre histórico. `HU-019` sólo será `Terminada` cuando el mismo cambio esté incorporado en `master` con pipeline satisfactorio y aprobación humana.

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
