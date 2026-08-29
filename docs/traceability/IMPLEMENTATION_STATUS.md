# Estado de implementación

Este archivo permite iniciar cada tarea de forma incremental. Registra evidencia aceptada; no sustituye la fila del backlog, las fuentes autorizadas de la tarea actual ni sus gates de salida.

## Base aceptada

| Campo | Valor |
|---|---|
| Última tarea Terminada | `TECH-BASE-003` |
| Corte y épica | `CV-00` — Base verificable; `EP-00` — Base de ingeniería |
| Pull request | `#1` |
| Commit implementado | `94692c872ccfdd3e7b4abffb736b4c79738f78b1` |
| Commit incorporado en `master` | `7c2410250e8b0b5b8632bc2496e2245d93f2eca3` |
| Gates remotos | `TECH-BASE-003 / PR gates`: PASS, GitHub Actions run `33221369418`, 2026-08-28 |
| Aceptación humana | Recibida el 2026-08-28 |
| `Fuentes/` | 73 archivos sin cambios; huella agregada `97EA9F86C185D597F5F43D9FCC244897572CE29CE5C50C268EF9F2CEC81C8962` |
| Siguiente tarea propuesta | `TECH-BASE-004` — Pruebas de límites modulares y plantilla de trazabilidad HU/CA/CP |

## Incremento actual pendiente de aceptación

| Campo | Valor |
|---|---|
| Tarea | `TECH-BASE-004` — Pruebas de límites modulares y plantilla de trazabilidad HU/CA/CP |
| Estado | Implementada y con pipeline PR satisfactorio; **no Terminada** |
| Evidencia candidata | Reglas `ARCH-001` a `ARCH-004`, caso positivo del repositorio, validación negativa sintética y plantilla `TEST_EVIDENCE_TEMPLATE.md` |
| Pull request | `#3` |
| Commit implementado | `42ca8c28492088c136837ff2e7000e6ebd272b8b` |
| Commit incorporado | Pendiente |
| Pipeline PR | `TECH-BASE-003 / PR gates`: PASS, GitHub Actions run `33259956795`, 2026-08-29 |
| Revisión y aceptación humana | Pendiente |

La tabla de **Base aceptada** permanece en `TECH-BASE-003`. Este apartado sólo podrá promoverse a esa tabla y declarar `TECH-BASE-004` Terminada después de pipeline PR satisfactorio, revisión humana e incorporación a `master`.

## Comprobación rápida para el siguiente chat

```powershell
git status --short --branch
git rev-parse HEAD
git merge-base --is-ancestor 7c2410250e8b0b5b8632bc2496e2245d93f2eca3 HEAD
git diff --exit-code HEAD -- Fuentes/
dotnet --version
git --version
docker version
```

Si la comprobación de ascendencia devuelve código `0`, el checkout contiene la base aceptada. Si además no hay cambios ajenos ni diferencias en `Fuentes/`, no se reanalizan TECH-INIT-001, TECH-BASE-002 o TECH-BASE-003 ni se repiten sus gates antes de comenzar la tarea nueva.

Una discrepancia obliga a detener el inicio incremental y revisar sólo la diferencia concreta. Cada tarea actualiza este archivo al recibir aceptación humana e incorporarse a la rama base.
