# Entrega de proceso: rollback y recarga del bono

## Alcance y diagnóstico

Cambio funcional del 02/11/2026. No modifica reglas de agenda, responsables, cantidades ni presentación.

Los POST `/mi-trabajo?handler=CompleteProcess` de las 17:10–17:11 (Ciudad de México) fallaron con `DbUpdateConcurrencyException`. El proceso activo conservaba su primera etapa EXCEL, intento activo y asignación sin entrega. La aplicación efectiva era `dotnet watch` sobre `src/Sgol.Web`, HTTP interno 7241 y proxy LAN HTTPS 7440, base `127.0.0.1:5440/sgol_local`. Las migraciones del bono ya estaban aplicadas. Se conservaron los cambios previos del velocímetro.

En copia aislada de la simulación se reprodujo la cadena: `DecideAsync` guarda evidencia y revisiones dentro de la transacción; `EnableItemRecipient` detecta `PROCESS_ITEM_CAPACITY`; la transacción se revierte, pero EF conserva revisiones aceptadas y entidades pendientes. El handler captura el conflicto y llama a `LoadOperationalAsync`, que consulta el bono. `TaskBonusService.CalculateAsync` llama a `SaveChangesAsync` sobre el mismo contexto y vuelve a intentar guardar el estado revertido: error 500 que oculta las opciones de agenda.

La reproducción usó un Excel sintético, una unidad y reloj 17:10. La siguiente etapa no encontraba un hueco continuo. No se recuperó el archivo ni la cantidad del POST original; no se afirma que esos valores fueran los enviados por el usuario.

## Corrección

`OperationalProcessService.DecideAsync` revierte explícitamente su transacción y limpia el seguimiento de EF ante una excepción. La limpieza ocurre antes de que el handler recargue la página. El interceptor de rollback existente limpia los identificadores pendientes del bono. Se mantienen los conflictos, la confirmación de excepciones, la autorización, la idempotencia y la atomicidad.

Los hosts de `ProcessEditorChecks` ahora incluyen el bono real. `ProcessItemChecks.CapacityAsync` comprueba que renderizar el conflicto conserva asignaciones e historial económico, además de intentos. El fixture elige días distintos del descanso que deja deliberadamente la fase anterior; no se deshabilitan validaciones de disponibilidad.

## Validación

| Escenario | Resultado esperado / prueba | Entorno | Estado y evidencia |
|---|---|---|---|
| Caso histórico copiado, antes del cambio | Conflicto de capacidad seguido de fallo al consultar bono | `sgol_task_checks_delivery_copy_20261102` | Aprobado como reproducción: `.local/process-delivery-copy.log`, stack en `CalculateAsync` |
| Mismo caso, corregido | Conflicto original y consulta del bono sin guardar estado revertido | Misma copia, sin cambios operativos persistidos por la entrega fallida | Aprobado: `.local/process-delivery-copy-fixed.log` |
| Formularios de evidencias mixtas y devolución | Excel, comentario, fotos, capturas, PDF, checklist; conservación e idempotencia | Base aislada | Baseline aprobado antes de la corrección: `.local/process-delivery-repro.log`; no atribuirlo a la versión corregida |
| Conteos, snapshots y devolución | Conservación de cantidades históricas, omisión por cero y cierre | `sgol_task_checks_delivery_items_20261102` | Aprobado tras la corrección: `--process-items-only`, `.local/process-delivery-items.log`, `ITEMS_PHASE_CORE_PASS` |
| Conflicto en formulario real con bono | HTTP 409 con opciones; asignaciones, intentos e historial económico sin cambios | Misma base aislada, Razor HTTP real | Aprobado: `--process-items-more`, `.local/process-delivery-final.log` |
| Propuesta obsoleta, fuerza autorizada, pendiente, permisos, reintentos concurrentes | Rechazo explícito o una sola operación válida, sin perder procedencia | Mismo host HTTP aislado | Aprobado: `ITEMS_CAPACITY_HTTP_PASS`; 136 mensajes PASS en el comando, incluyendo reglas comunes |
| Preparación inicial de la prueba ampliada | No usar un día de descanso para el caso de capacidad | Base aislada | Falló inicialmente por `SCHEDULE_ABSENT` antes del escenario; fixture corregido y ejecución final aprobada |
| Compilación y diff | Compilar pruebas y web; conservar cambios previos | Artifacts separados `.local/process-delivery-build` | Aprobado; diff sin errores con `core.whitespace=cr-at-eol` |
| Aplicación compartida | Ensamblado actualizado y servicio accesible | Local/LAN | Aprobado: watch recargó a las 17:19:47, DLL posterior al cambio; readiness local 200 y `/acceso` LAN HTTPS 200 |
| Entrega del usuario en simulación compartida | Usuario vuelve a enviar archivo y decide ante conflicto vigente | Aplicación compartida | No ejecutado: no se entregó por el usuario. Asignación sigue revisión 1, sin `CompletedAt` |
| Navegador/dispositivos y visual | Sin cambios de HTML/CSS/JS | — | No ejecutado; comprobación de formularios HTTP, no validación visual |

No hay migración, commit ni push en esta corrección. Se crearon bases y archivos de diagnóstico locales; no se modificaron registros operativos compartidos. El error genérico está corregido, pero un conflicto de agenda sigue requiriendo la decisión explícita prevista por el producto.
