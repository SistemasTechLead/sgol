# Resolver conflictos desde la fila del plan — 02/11/2026

## Alcance y decisiones

Cambio mixto autorizado: botón **Revisar** en la fila de la tarea bloqueada, diálogo contextual, elección y recálculo sin salir del plan. Se reutiliza la revisión antes de confirmar de `/tareas/actualizar`, los diálogos compartidos y los estilos existentes.

Decisiones expresas del usuario:

- Los aumentos de capacidad se guardan junto con el plan, solo en las fechas afectadas, y quedan disponibles para otras asignaciones de la misma persona en esas fechas.
- Se permiten reasignaciones y reprogramaciones dentro de las reglas actuales. No se omiten tareas.

El motor ya busca personas y horarios compatibles. La nueva ventana no ofrece una reasignación ficticia que repita un reparto demostrado imposible. Para falta de capacidad presenta, cuando el horario lo permite, cubrir la demanda del intento o dedicar la capacidad permitida por el horario (máximo 480 minutos). Ambas opciones declaran su efecto antes de seleccionarlas. El motor recalcula responsables y horarios sin alterar catálogo, especializaciones, criticidad o plazos. Sin una alternativa de capacidad válida, informa de la limitación; no elude incompatibilidades de horario o cualificación.

## Implementación y recorrido

- `PlanningCandidateConflict.PersonId` identifica al candidato sin usar el nombre como clave.
- `PlanningConflictResolution.Propose` calcula opciones por persona/fecha y descarta propuestas incompatibles con la capacidad horaria conocida. Son propuestas; la elección recalcula el plan completo y puede revelar otro conflicto.
- `PlanCapacityAdjustment` conserva el contrato anterior y añade fecha y minutos opcionales como pareja. La normalización y el parser HTTP rechazan duplicados, datos mal formados y límites inválidos.
- `PlanningCapacity.Changes` conserva los ajustes generales anteriores; una excepción por fecha divide sus intervalos sin solapamientos. `AgendaFingerprint` incluye las decisiones para impedir confirmar una propuesta distinta.
- `Index.DirectionPlans.cs` conserva elecciones entre intentos, permite deshacerlas y adjunta las alternativas al conflicto. `_DirectionPlans.cshtml` y `_DirectionConflictReview.cshtml` contienen la fila, el diálogo y el resumen pendiente.
- Al confirmar, `EfDirectionPreparedPlanService.CreateAsync` vuelve a validar la agenda y la concurrencia. Su transacción existente guarda plan, ajustes, snapshots de capacidad, auditoría y asignaciones. No se guarda capacidad durante la revisión. La eliminación del plan conserva el mecanismo existente de restauración mediante `StoredPlanCapacityAdjustment`.
- El generador detecta anticipadamente una condición necesaria: cuando tareas de fecha fija tienen una única persona elegible, su demanda diaria conjunta debe caber. No aplica esa suma a tareas distribuibles, mensuales ni de progreso, ni cuenta ocurrencias ya reservadas. Evita explorar permutaciones incapaces de resolver un exceso demostrado.

No hay migraciones. Se mantienen registros históricos, permisos, las reservas y la confirmación final. Los cambios previos del diagnóstico de conflictos se conservaron.

## Entornos y comprobaciones

Aplicación compartida al iniciar: PID 21964, `.local/runtime/weekly-conflict-20261102/Sgol.Web.dll`. Pruebas sobre `sgol_task_checks_plan_resolutions_20261102`, copia aislada del estado utilizado para reproducir el bloqueo. Servidor de formularios real de pruebas: `https://localhost:7257`, sesión de pruebas confinada a ese host.

| Escenario | Esperado | Prueba / entorno | Estado |
|---|---|---|---|
| Abrir Revisar | Diálogo de la tarea correcta en su fila | Razor real, Chromium/Edge y WebKit | Aprobado |
| Temas y tamaños | Sin desbordamiento horizontal; controles disponibles | 3 temas × 320/390/768/1280 px × 2 motores | Aprobado |
| Teclado y cierre | Foco dentro, Escape y retorno al botón | `plan-resolution-checks.cjs` | Aprobado |
| Elegir y deshacer | Propuesta conservada; deshacer elimina el ajuste | Navegador, formularios reales en copia | Aprobado |
| Límite manipulado | Rechazar 481 minutos en servidor | Formulario HTTP en copia | Aprobado |
| Confirmación completa, reintento e historial | Persistencia atómica y ausencia de duplicados | `--weekly-plan-resolutions-http --fixture`, copia aislada con personal y tareas sintéticos | Aprobado |
| Cambio simultáneo del catálogo | Rechazar confirmación anterior sin escrituras parciales | Mismo formulario HTTP | Aprobado |
| Capacidad histórica y fuera de fechas elegidas | Conservar todos sus valores | Comparación de registros después de guardar | Aprobado |
| Contratos antiguos y excepciones por fecha | Parseo compatible y segmentos sin solapamiento | `--plan-resolution-rules` | Aprobado |
| Reparto viable con retroceso y exceso inevitable | Conservar solución viable; diagnosticar exceso fijo exacto | `--weekly-conflict-diagnostic` | Aprobado |
| Catálogo de la simulación sin solución de capacidad | Informar conflicto, sin intención de guardado ni cambios parciales | `--weekly-plan-conflict-http`, copia sin sustituir selección semanal | Aprobado |
| Dispositivos físicos y lector de pantalla | Interacción accesible | Sin equipos físicos en esta sesión | No ejecutado |
| Entorno compartido | Carga del binario verificado | Proceso 17268; salud interna y LAN; acceso HTTP 200 | Aprobado |

Evidencia local de navegador: `.local/plan-resolution-ui-20261102-final/`; formularios: `outputs/plan-resolutions-20261102/`. Se detectó y corrigió una salida del foco del diálogo. Dos ensayos del catálogo completo con la búsqueda anterior se interrumpieron por demora excesiva, sin guardar ajustes: no se consideran pruebas aprobadas. Motivaron la comprobación anticipada de demanda fija descrita arriba.

## Resultado y límite operativo

El catálogo copiado tiene 535 minutos de demanda fija más carga existente para una única candidata en la fecha bloqueada, por encima del límite de 480 minutos. No se validó ni guardó ese plan. La ventana informa que no existe un aumento permitido que lo resuelva. Resolverlo requiere una decisión adicional sobre cobertura capacitada o cambio de calendario; esas decisiones no se simularon ni se aplicaron automáticamente.

El recorrido completo aprobado usa dos nuevas tareas y un trabajador de pruebas con especialización exclusiva, conserva las asignaciones históricas y restringe los cambios a la base aislada `sgol_task_checks_plan_resolution_final_20261102`. No confundir esa prueba con la viabilidad del catálogo compartido. Los ensayos iniciales de esa fixture corrigieron un código de tarea demasiado largo, una interferencia de la agenda real y el nombre de semana fijo del runner. La ejecución final pasó íntegramente: `.local/plan-resolution-final.log`. Reglas: 10 comprobaciones aprobadas. Regresión del generador: `.local/plan-resolution-generator.log`. Caso sin alternativa: `.local/plan-resolution-no-alternative.log`. Navegador adicional sobre fixture real: `.local/plan-resolution-ui-20261102-fixture/`.

Se publicó y cargó `.local/runtime/plan-resolutions-20261102/Sgol.Web.dll`. Evidencia de hash, ruta y salud: `.local/deploy-plan-resolutions-20261102/deployment.json`. No se ejecutaron migraciones ni se cambió capacidad o asignaciones de la simulación compartida. No se hizo commit ni push.

## Ampliación autorizada: mover ocurrencias y excluir vencidas

Cambio mixto, 02/11/2026. El usuario autorizó mover una ocurrencia nueva FLEXIBLE de días fijos a un día posterior de la misma semana como excepción de ese plan, mostrando y confirmando fecha y vencimiento nuevos. No cambia el catálogo, no omite tareas, no mueve críticas ni asignaciones existentes. Esta decisión sustituye la limitación de calendario descrita arriba.

El usuario indicó además que los minutos pendientes vencidos no deben consumir capacidad de un plan nuevo. `PlanningCapacity.ReadAsync` captura los IDs existentes pendientes con `DueAt <= now`; `CapacityLedger.Footprint` los excluye solo en el ledger de planificación y conserva esa exclusión al clonar. Las nuevas ocurrencias siguen consumiendo capacidad aunque su hora nominal haya pasado. Supervisión conserva su cálculo operativo; no se completan, eliminan ni modifican asignaciones vencidas. La vista previa muestra por separado las exclusiones. El fingerprint incluye esos IDs para detectar cambios al confirmar.

`PlanOccurrenceMove` viaja por normalización, parser HTTP estricto, formulario, intención firmada, generación y guardado transaccional. El generador conserva la clave de la ocurrencia original para impedir duplicarla y audita `PLAN_OCCURRENCE_MOVED`. `PlanningConflictResolution.ProposeMovesAsync` comprueba elegibilidad, reservas, repetición, capacidad prevista y un horario posible. Cada selección recalcula toda la agenda; la propuesta no garantiza por sí sola la viabilidad global. La confirmación muestra el horario y vencimiento definitivos y permite deshacer.

El diagnóstico original de 535 minutos correspondía a 290 existentes y 245 nuevos. En la copia posterior, dos cambios del catálogo redujeron Capturar pedidos de 120 a 60 y Diseñar imágenes de 90 a 40: total 425. Excluir 140 vencidos deja 285 (90 vigentes + 195 nuevos), todavía 45 por encima de 240. Son demandas demostradas del bloqueo, no un plan completo guardado. Evidencia: `.local/plan-person-after-rule.log`.

| Escenario | Esperado | Prueba / entorno | Estado |
|---|---|---|---|
| Vencidas anteriores y vencimiento exacto | Excluir del nuevo plan, conservar vigentes y registros | `--new-plan-capacity-checks`, copia aislada; `.local/new-plan-capacity.log` | Aprobado |
| Nueva ocurrencia con hora pasada | Seguir contando tras clonar ledger | Misma prueba; 90 + 11 = 101 | Aprobado |
| Contrato de movimientos | Rechazar día igual, otra semana, tarea no seleccionada; parser estricto | `--plan-resolution-rules`, 14 comprobaciones | Aprobado |
| Generación y crítica | Mantener búsqueda viable y rechazar mover crítica | `--weekly-conflict-diagnostic`; `.local/plan-move-generator.log` | Aprobado |
| Flujo real de movimiento | Preparar, mostrar vencimiento, guardar, reintentar sin duplicar | `--weekly-plan-moves-http --ui`, fixture aislada; `outputs/plan-moves-20261102` | Aprobado |
| Concurrencia e historial | Rechazar catálogo cambiado sin escrituras parciales; preservar reservas, catálogo e historial | Mismo flujo HTTP y comparación de registros | Aprobado |
| Interfaz real | Opciones visibles, selección, deshacer, foco y Escape | Edge/Chromium y WebKit, 3 temas, 320/390/768/1280; `.local/plan-move-current-ui-20261102/results.json` | Aprobado |
| Primer toque móvil | Abrir el desplegable sin cerrarlo por focusin | Corregido `mobile.js`; regresión en flujo anterior | Aprobado |
| Dispositivos físicos y lector de pantalla | Validación adicional de accesibilidad | No disponibles en sesión | No ejecutado |
| Migración | Sin cambio de esquema | Inspección de entidades | No aplicable |

Binario preparado: `.local/runtime/plan-moves-overdue-20261102/Sgol.Web.dll`; evidencia de despliegue en `.local/deploy-plan-moves-overdue-20261102/`. No se guarda un plan de prueba en la simulación compartida.
