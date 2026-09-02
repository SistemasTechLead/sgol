# Estado de implementación

Este archivo permite iniciar cada tarea de forma incremental. Registra evidencia aceptada conforme a `F07_ENMIENDA_001_CIERRE_DE_TAREA_EN_UN_PR.md`; no sustituye la fila del backlog, las fuentes autorizadas de la tarea actual ni sus gates de salida.

En una rama de pull request, la sección siguiente es una propuesta de base aceptada. Sólo adquiere eficacia como `Terminada` cuando el registro y el commit implementado están incorporados en `master`, el PR consta como merged, el check requerido pasó para ese commit, existe aceptación humana y `Fuentes/` permaneció protegida.

## Propuesta actual en rama

| Campo | Valor |
|---|---|
| Tarea | `HU-003` — Dirección registra disponibilidad binaria diaria |
| Estado | Propuesta implementada en rama; no `Terminada` |
| Dependencias aceptadas | `HU-001`, `HU-007`; `HU-007` es la última base aceptada |
| Pull request | PR que incorpora esta actualización |
| Commit implementado | Commit que contiene esta actualización |
| Pipeline requerido | `TECH-BASE-003 / PR gates`; checks asociados al commit implementado |
| Aceptación humana | Pendiente de revisión de implementación |
| Commit incorporado en `master` | Pendiente de merge |
| `Fuentes/` | Protección requerida en los checks del PR |
| Siguiente tarea propuesta | Ninguna; no iniciar `HU-008` ni otra historia hasta que esta propuesta cumpla pipeline, revisión humana y merge |

Esta propuesta no habilita dependencias ni modifica el cierre histórico. `HU-003` sólo será `Terminada` cuando el mismo cambio esté incorporado en `master` con pipeline satisfactorio y aprobación humana.

## Base aceptada

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
| Siguiente tarea propuesta | `HU-003` — Dirección registra disponibilidad binaria diaria |

La evidencia anterior se acepta sin repetir los análisis ni gates históricos de `HU-007`.

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
| `TECH-VER-001` | `F07_ADENDA_03_NUCLEO_DE_VERSIONADO.md` | `HU-008` | Pendiente |

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
