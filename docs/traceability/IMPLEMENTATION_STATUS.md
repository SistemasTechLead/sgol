# Estado de implementación

Este archivo permite iniciar cada tarea de forma incremental. Registra evidencia aceptada conforme a `F07_ENMIENDA_001_CIERRE_DE_TAREA_EN_UN_PR.md`; no sustituye la fila del backlog, las fuentes autorizadas de la tarea actual ni sus gates de salida.

En una rama de pull request, la sección siguiente es una propuesta de base aceptada. Sólo adquiere eficacia como `Terminada` cuando el registro y el commit implementado están incorporados en `master`, el PR consta como merged, el check requerido pasó para ese commit, existe aceptación humana y `Fuentes/` permaneció protegida.

## Base aceptada

| Campo | Valor |
|---|---|
| Última tarea Terminada | `TOOL-FLOW-001` — Cierre de tarea en un solo pull request |
| Corte y épica | Tarea de herramientas autorizada fuera del backlog; no pertenece a un CV/EP funcional |
| Pull request | PR que incorpora esta actualización |
| Commit implementado | Commit que contiene esta actualización |
| Pipeline requerido | `TECH-BASE-003 / PR gates`; checks asociados al commit implementado |
| Aceptación humana | Aprobación requerida en el PR que incorpora este registro |
| Commit incorporado en `master` | `pendiente de merge` |
| `Fuentes/` | Protección requerida en los checks del PR |
| Siguiente tarea propuesta | `TECH-UI-001` — Base de interfaz compartida; depende de `TECH-BASE-005`, `TECH-AUD-001` y `TECH-ID-BOOT-001`; insertada antes de `HU-005` por `F07_ADENDA_02_TAREAS_DE_BASE_DE_INTERFAZ.md` |

Mientras este archivo permanezca fuera de `master`, la tabla anterior es una propuesta y `TOOL-FLOW-001` no está Terminada. Al cumplirse las condiciones de eficacia indicadas al inicio, los valores autorreferenciales se resuelven con el PR, sus checks y la historia Git sin reescribir el documento.

## Tareas insertadas por adenda

Esta tabla forma parte de la comprobación de precedencia obligatoria antes de iniciar cualquier tarea. Una tarea con estado distinto de `Terminada` bloquea la tarea indicada en `Ejecutar antes de` y toda historia posterior que dependa de esa precedencia.

| Tarea insertada | Adenda de origen | Ejecutar antes de | Estado |
|---|---|---|---|
| `TECH-UI-001` | `F07_ADENDA_02_TAREAS_DE_BASE_DE_INTERFAZ.md` | `HU-005` | Pendiente |

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
