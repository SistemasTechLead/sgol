# Conservar el proceso de asignaciones existentes

## Objetivo y decisión

Cambio mixto solicitado el 28/10/2026. Cuando la pertenencia de una tarea cambia, mostrar cada asignación pendiente afectada con código, nombre, responsable, plan, inicio, vencimiento y procesos anterior/nuevo. Debajo aparece una casilla individual para conservar su proceso y usar el nuevo en nuevas ejecuciones. No se preselecciona ni persiste nada hasta la confirmación final.

Decisión explícita del usuario: conservar solamente el proceso anterior; carga, requisitos y demás parámetros simultáneos siguen las reglas habituales de actualización, que pueden ajustar la agenda.

## Trazabilidad

- `OperationalProcessCatalogService.AddTaskAsync` cambia la pertenencia sin convertir las asignaciones independientes anteriores. `AbandonAsync` puede devolver tareas de borrador a General.
- `OperationalTaskService.SaveAsync` mantiene el catálogo y captura instantáneas antiguas conocidas cuando corresponde.
- `AssignmentDefinition.Differences` compara la pertenencia original y la resolución explícita guardada en la instantánea. La instantánea mantiene `ProcessId` original; `_PreservedProcessTargetId` registra qué pertenencia actual se reconoció sin trasladar el pendiente. Una pertenencia distinta vuelve a requerir decisión. Las adopciones posteriores conservan esta distinción.
- `AssignmentRefreshService.CountsAsync`, `BuildAsync` y `ApplyAsync` incluyen asignaciones independientes y etapas pendientes. La decisión identifica la asignación o reserva concreta, entra en la huella de concurrencia y se audita con el motivo. Los bloqueos por modalidad PROGRESO conservan una causa separada; esta casilla no los omite.
- `/tareas/actualizar` usa formularios Razor existentes, CSRF, revisión y confirmación. La selección viaja como lista de identificadores, no como una autorización global.
- `ProcessPlanningService` y `OperationalProcessService` siguen creando nuevas ejecuciones desde su versión publicada, con las validaciones habituales de publicación, habilitación y membresía. Resolver un pendiente no publica ni inicia procesos.
- No cambia el esquema. Las entregas, evidencias y versiones publicadas no se reescriben.

## Invariantes

Conservar proceso por sí solo mantiene responsable, carga, fechas, estado y evidencia. No genera etapas, asignaciones duplicadas ni transferencias. La resolución no oculta otros parámetros diferentes. Las operaciones rechazadas no escriben parcialmente. Los reintentos usan el recibo existente. No se aplica la resolución a la simulación compartida durante las pruebas.

## Matriz de validación

| Escenario | Resultado esperado | Entorno y prueba | Estado |
|---|---|---|---|
| Dos pendientes de una tarea trasladada desde General | Dos problemas y casillas independientes, con tarea, plan, responsable, fechas y causa | Base aislada, servicios reales y formulario Razor HTTP | Aprobado |
| Solo una casilla seleccionada | Continúa bloqueada la otra asignación | Base aislada, servicio | Aprobado |
| Revisar sin confirmar | Ninguna escritura de asignaciones | Base aislada, GET/POST Razor | Aprobado |
| Confirmar solo conservación | Mantiene toda la operación y proceso originales, conserva evidencia y deja auditoría | Base aislada, POST Razor y consultas | Aprobado |
| Entregada con el proceso anterior | Permanece idéntica y no pide resolución | Base aislada, comparación completa | Aprobado |
| Nueva revisión y reintento | No reaparece el conflicto; no duplica escrituras | Base aislada, HTTP y servicio | Aprobado |
| Cambio de proceso más otros parámetros | Conserva proceso y actualiza otros datos según reglas normales | Base aislada, servicio e integración | Aprobado |
| Cambio posterior de carga | Mantiene resolución anterior sin ocultar la nueva carga | Base aislada, servicio | Aprobado |
| Otro cambio posterior de pertenencia | Nueva decisión específica | Base aislada, editor real y servicio | Aprobado |
| Edición concurrente entre revisión y confirmación | Rechazo atómico por propuesta vencida | Base aislada, servicio | Aprobado |
| Etapa de ejecución existente | Conserva estructura y dependencias, actualiza parámetros permitidos | Base aislada, integración | Aprobado |
| Nueva ejecución después de publicar | Usa proceso nuevo con el flujo habitual | Base aislada, planificación/activación | Aprobado |
| Escritorio, móvil, temas y teclado | Aviso legible y casillas accesibles, confirmación invalidada al cambiar selección | Aplicación Razor con base aislada, navegador | Aprobado |

## Evidencia

Compilación aprobada sin errores ni advertencias; 30 comprobaciones específicas aprobadas con `--process-refresh-only`, incluidos formularios Razor reales con sesión de Dirección de prueba, CSRF, revisión, confirmación y reintento. Regresiones aprobadas: `--assignment-refresh-global-only`, `--assignment-refresh-only`, `--expired-refresh-only` y `--process-add-defaults-only`.

Navegador: página real sobre base aislada, temas claro/oscuro/especial, escritorio (960 y 1280 px) y móvil (320 px), teclado, texto largo, asociación del problema con su casilla y bloqueo de confirmación al modificar la selección. Sin desbordamiento horizontal observado. Captura: `.local/process-refresh-evidence/desktop-dark.png`.

También se comprobaron pendientes vencidos sin actividad y asignaciones antiguas sin instantánea; sus cierres históricos se conservaron sin cambios.

Compilación aislada necesaria: existen archivos generados anidados en carpetas `artifacts` de proyectos que hacen fallar la compilación estándar por atributos duplicados; el ajuste local de compilación excluye esas fuentes generadas sin modificar el producto ni borrar archivos.


Los registros detallados están en .local/process-refresh-tests.log, .local/process-refresh-global-tests.log, .local/process-refresh-lifecycle-tests.log, .local/process-refresh-expired-tests.log, .local/process-refresh-defaults-tests.log y .local/process-refresh-build.log. No se modificaron asignaciones de la simulación compartida. No hay migraciones nuevas.

## Disponibilidad local

El usuario autorizó reiniciar la aplicación local, pero la revisión automática rechazó el reinicio dos veces con el motivo genérico «bloqueado por política». No se ejecutó ese reinicio. El proceso activo sigue cargando .local/runtime/security-20261028-final/Web/Sgol.Web.dll; la versión nueva se prepara en .local/runtime/process-refresh-20261028/Web/. La comprobación de la versión nueva en la sesión habitual queda **no ejecutada** por ese bloqueo; las verificaciones de aplicación realizadas arriba usaron el host real de Razor con base aislada.
