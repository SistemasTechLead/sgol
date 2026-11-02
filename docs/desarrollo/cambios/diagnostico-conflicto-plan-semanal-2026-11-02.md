# Diagnóstico de rechazo del plan semanal — 02/11/2026

## Alcance y causa

Corrección funcional del diagnóstico, sin cambios de reglas, persistencia ni composición visual. `OperationalPlanGenerator.GenerateAsync` conserva la búsqueda y sus restricciones. Su comprobación anticipada `ForwardPossible` devolvía `false` sin registrar `PlanningAssignmentConflict`; si ninguna rama alcanzaba el rechazo detallado de `Search`, se lanzaba `PLAN_NO_AVAILABLE_WORKER` sin entidad afectada.

Ahora esa comprobación conserva código, nombre y carga de la tarea, fecha, candidatos, turnos y motivo del rechazo. `OnPostPrepareDirectionPlanAsync` ya consume ese objeto y `_DirectionLoad.cshtml` ya lo representa. El diagnóstico corresponde al conflicto encontrado en la búsqueda, no a un inventario exhaustivo de todas las tareas imposibles ni a asignaciones guardadas.

No se modifican responsables, capacidad, horarios, catálogo, historial, confirmación ni permisos. Se reutilizan el contrato y la tabla existentes. No se requieren migraciones.

## Entorno y evidencia

Repositorio inicialmente limpio. Aplicación compartida observada: `.local/runtime/correction-scheduling-20261031-final/Sgol.Web.dll`, proceso 23928. Se copió `sgol_local` del contenedor `sgol-local-postgres-1` a `sgol_task_checks_weekly_diagnostic_20261102`, puerto local 5440. Las pruebas solo escriben en la copia; la regresión sintética revierte su transacción. No se reinició ni actualizó la aplicación compartida.

Compilación mediante `dotnet run --project tools/Sgol.TaskChecks`; controles específicos `--weekly-conflict-diagnostic` y `--weekly-plan-conflict-http`, ambos con salida 0. HTML del formulario: `outputs/plan-http-20261026/prepare.html` (ruta heredada del runner, evidencia local sin publicar).

| Escenario | Resultado esperado | Prueba / entorno | Estado |
|---|---|---|---|
| Búsqueda viable que necesita retroceder | Conservar la colocación válida de ambas tareas | `FullWeeklyPlanChecks.FallbackAsync`, copia aislada | Aprobado |
| Comprobación anticipada sin espacio para tarea larga | Rechazo con tarea, fecha y motivos de ambos candidatos | `--weekly-conflict-diagnostic`, copia aislada | Aprobado |
| Rechazo sin efectos parciales | Mismo número de asignaciones | Misma regresión, copia aislada | Aprobado |
| Catálogo y carga actuales, formulario real con CSRF | HTTP 422, tabla de tarea/candidatos y ausencia de intención de guardado | `--weekly-plan-conflict-http`, Razor real en localhost:7257 | Aprobado |
| Conservación de registros existentes | Asignaciones completas idénticas antes/después | Comparación JSON del recorrido HTTP | Aprobado |
| Presentación en navegador, móvil, temas y teclado | Tabla existente legible y operable | Sin cambio de markup/CSS; no se ejecutó navegador | No ejecutado |
| Disponibilidad en aplicación compartida | Cargar binario corregido | No se realizó despliegue | No ejecutado |
| Cambios de permisos, migración, revisión o historial | Ninguno | No se modifican esas rutas | No aplicable |

La reproducción confirma que el mensaje genérico ocultaba un conflicto de capacidad. No autoriza aumentar cargas ni cambiar la persona responsable para forzar el guardado. La corrección está implementada y validada a nivel de servicio y formulario HTTP; queda pendiente la verificación visual y su carga en el entorno compartido.

## Carga autorizada en la aplicación — 02/11/2026

Tras la instrucción «Carga la corrección a la aplicación», se publicó el proyecto Web y se sustituyó el proceso compartido por `.local/runtime/weekly-conflict-20261102/Sgol.Web.dll` (PID observado 21964). Se verificaron la ruta cargada, el hash del binario, salud interna `alive`, disponibilidad interna `ready`, salud LAN y formulario de acceso LAN con HTTP 200. Evidencia local: `.local/deploy-weekly-conflict-20261102/deployment.json`. El script conserva reversión al binario anterior si falla la comprobación.

El escenario de disponibilidad en la aplicación compartida pasa a **aprobado**. No se ejecutaron migraciones ni ajustes de capacidad, responsables o asignaciones. No se repitió la creación del plan en la simulación compartida; la validación del diagnóstico mediante formulario HTTP corresponde a la copia aislada. La comprobación visual sigue pendiente.
