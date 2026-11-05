# Plan: origen de conteo configurable por etapa

Fecha: 2026-11-05 · Zona operativa: America/Mexico_City.
Estado: implementación autorizada; cambio implementado en checkout; recorridos principales aislados aprobados, matriz ampliada parcialmente pendiente. Cambio mixto: reglas, persistencia, agenda e interfaz. Sin autorización de commit, push ni despliegue compartido.

## Objetivo

Cada tarea receptora de un proceso podrá indicar de qué etapa anterior toma el conteo para calcular su carga. Un mismo conteo podrá alimentar varias tareas, aunque no sean consecutivas. La fuente de cantidad será independiente del orden de ejecución y de la condición de entrega/aprobación.

Ejemplo: la etapa 1 cuenta 12 elementos; las etapas 3 y 5 usan ese conteo; las etapas 2 y 4 conservan su carga propia. Reportar 12 no activa anticipadamente las etapas 3 y 5. Otra etapa podrá producir un conteo distinto sin sobrescribir el primero.

## Decisiones y límites

Confirmado por el usuario durante la planificación:

- Un conteo cero omite solo las tareas que lo consumen; las demás continúan.
- Al corregir un conteo se conserva el historial y se revisa el impacto antes de crear correcciones de tareas con trabajo o entregas.

Tarifa confirmada: cada receptora seleccionada usa cantidad y minutos por unidad del origen. Con A=12 y tarifa 2, C y E reciben 24 min si ambas seleccionan A. Una etapa sin origen conserva la carga propia. No se introducen tarifas por receptora.

Decisiones confirmadas durante la implementación:

1. Aplicar la nueva configuración solo a nuevas versiones; conservar planes preparados, ejecuciones iniciadas e historial con su semántica anterior. No migrar retroactivamente su comportamiento.
2. Si una etapa omitida también producía otro conteo, sus consumidores quedan bloqueados hasta resolver su origen. No inventar cero ni reutilizar un intento sustituido. Las etapas independientes anteriores al bloqueo continúan en orden; no se autorizó ejecución fuera de orden.
3. La auditoría final de nuevas versiones conserva carga propia y no se omite ni consume conteo. La implementación histórica sí permite carga calculada positiva en auditoría inmediata, aunque cero restaura su estimación; preservar esa compatibilidad histórica.
4. Instrucción final del usuario: configurar «Tomar conteo de» en **Editar detalles de la etapa**, junto a las reglas del proceso. Esto sustituye su respuesta intermedia que situaba la selección en Planificación. No hay segundo selector en el plan ni selección durante la operación; preparar captura automáticamente los vínculos de la versión.
5. Una revisión omitida por cero permite continuar la etapa independiente con la última entrega real, sin crear una aprobación ficticia.
6. El aviso de capacidad identifica cada tarea afectada y permite guardar pendiente; no busca ni fuerza responsables desde este aviso nuevo. La semántica histórica conserva sus opciones anteriores.
7. El responsable del origen reporta nuevamente un conteo devuelto. Antes de crear correcciones de consumidoras se revisa su impacto; se conservan intentos y evidencia anteriores.
8. Se conserva la exclusión expresamente acordada de versiones nuevas con conteos en la generación semanal automática; se preparan desde Planificación → Procesos. La selección de vínculos ya está fijada en Editar detalles y no se repite allí.

Alcance inicial propuesto: una fuente por receptor, una cantidad entera por productor, fuentes anteriores del mismo proceso y versión. No suma de conteos, fórmulas libres, dependencias futuras, ejecución paralela ni conexiones entre ejecuciones. El parámetro pertenece a la etapa del proceso, no a la tarea global del catálogo.

## Investigación original del plan y límites de evidencia (estado histórico)

- Se leyó AGENTS.md, RTK.md y el protocolo de implementación. Se consultó Graphify con `procesos conteo etapas`: índice actualizado y hashes verificados; después Inicio, mapa de dominio, reglas, procesos, carga por ítems y referencias visuales de diseño, componentes y patrones.
- Checkout en `master`. Existían cambios ajenos en archivos de arrastre, pruebas y un plan de autosupervisión; se conservaron. Este trabajo añade únicamente este documento.
- Proceso Web observado: PID 33712, ensamblado `.local/runtime/carryover-self-direction-20261105/Sgol.Web.dll`. Identificar su ruta no demuestra equivalencia binaria con todas las fuentes inspeccionadas. No se certifica aquí el comportamiento funcional del host.
- Se inspeccionaron código, restricciones y pruebas existentes. No se consultaron filas de la base compartida, no se ejecutaron suites, no se probaron formularios ni navegador y no se modificó aplicación, datos o despliegue.
- Antes de implementar: inventario de solo lectura de versiones, enlaces de conteo, planes preparados, ejecuciones, intentos y omisiones existentes; reproducción de ejemplos relevantes en copia aislada. No asumir que los registros reales cumplen todos los supuestos del editor actual.

## Comportamiento encontrado en el código

| Tramo | Evidencia propietaria | Hallazgo |
|---|---|---|
| Definir y copiar | [OperationalSequences.cs](../../../src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/OperationalSequences.cs), [OperationalProcessCatalogService.cs](../../../src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/OperationalProcessCatalogService.cs): `ProcessDraftInput`, `SaveAsync`, `PublishAsync`, `CopyStep`, `ValidateRulesCore` | La etapa productora guarda `OutputItemId` y un solo `OutputTargetTaskId`. La validación exige destino inmediato. No es un selector de origen por receptor. |
| Errores y editor | [ProcessValidationIssue.cs](../../../src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/ProcessValidationIssue.cs), [Processes.cshtml](../../../src/Sgol.Web/Pages/Tasks/Processes.cshtml), [Processes.cshtml.cs](../../../src/Sgol.Web/Pages/Tasks/Processes.cshtml.cs), [process-rules.js](../../../src/Sgol.Web/wwwroot/js/process-rules.js) | `/tareas/procesos` muestra “Reportar cantidad para la siguiente tarea”; JavaScript llena un destino oculto y verifica que siga siendo vecino al reordenar. El estado de cambios y la copia del formulario enumeran explícitamente los campos. |
| Persistencia | [ProcessItems.cs](../../../src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/ProcessItems.cs): `Configure`, `CaptureAsync` | La FK destino usa versión y tarea; `CK_process_item_link` acopla producir un ítem con tener un destino único. Las tarifas son versionadas. Cambiar solo el formulario no resuelve el modelo. |
| Preparar, reservar y activar | [ProcessPlanningService.cs](../../../src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/ProcessPlanningService.cs), [WeeklyProcessPlanning.cs](../../../src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/WeeklyProcessPlanning.cs), [PreparedProcessStart.cs](../../../src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/PreparedProcessStart.cs), `StartAsync` en [OperationalProcessService.cs](../../../src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/OperationalProcessService.cs) | `ItemRatesJson` captura tarifas. `IsEstimate` consulta la posición anterior. La activación copia tarifa al productor, pero no un enlace explícito de entrada al receptor. |
| Entregar y avanzar | [OperationalProcessService.Items.cs](../../../src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/OperationalProcessService.Items.cs): `ApplyItemCount`, `EnableItemRecipient`; `DecideAsync` | Cantidad positiva modifica `stages.First()`. Cero omite todas las posteriores anteriores a auditoría. El resultado del cálculo elige además la siguiente etapa activa: hay que separar cálculo y navegación. |
| Programar y reintentar | [OperationalProcessService.Scheduling.cs](../../../src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/OperationalProcessService.Scheduling.cs), `EnableActive`, `TickAsync`, `Reservation` | La agenda combina reserva, dependencia, carga, márgenes y anclas. Actualmente la resolución de capacidad está asociada al productor inmediato; debe admitir consumidores lejanos sin activar tareas fuera de orden. |
| Devolver y preservar | [OperationalProcessService.PendingCounts.cs](../../../src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/OperationalProcessService.PendingCounts.cs): `PendingCountProducer`, `ReturnPendingCountAsync`, `ResetReturnedLoad`; `DecideAsync`; [OperationalProcessService.Continuation.cs](../../../src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/OperationalProcessService.Continuation.cs) | La devolución reinicia un tramo desde el productor y sustituye intentos posteriores. La continuación conserva material. Ese tramo no equivale al conjunto de consumidores del conteo. |
| Consultar y ejecutar | [formulario de proceso](../../../src/Sgol.Web/Pages/MyWork/_ProcessActionForm.cshtml), [process-items.js](../../../src/Sgol.Web/wwwroot/js/process-items.js), [Processes.cshtml.cs](../../../src/Sgol.Web/Pages/MyWork/Processes.cshtml.cs), [ProcessActionForm.cs](../../../src/Sgol.Web/Interface/MyWork/ProcessActionForm.cs) | `/mi-trabajo/procesos` muestra una carga recibida y una fórmula de carga siguiente. También se integra en Mi trabajo. Los textos de cero y los avisos de capacidad presuponen un solo receptor. |
| Otros consumidores | [AssignmentDefinition.cs](../../../src/Sgol.Web/Infrastructure/Persistence/Planning/AssignmentDefinition.cs), [OperationalTasks.cs](../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalTasks.cs), [OperationalCapacityService.cs](../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalCapacityService.cs), [OperationalCarryover.cs](../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalCarryover.cs) | `ItemLoadSourceAttemptId` protege carga calculada frente a actualización de catálogo; `PendingItemDemand` conserva demanda superior a 4800; el arrastre incorpora el origen en su estructura. Preservar esos contratos en cada receptor. |

Las pruebas existentes `ProcessItemChecks`, `ProcessItemIntegrityChecks`, `ProcessItemBoundaryChecks`, `ProcessItemLegacyReplayChecks` y `ProcessRetryBrowserChecks` aportan fixtures y escenarios de carga, cero, reintentos, evidencia, actualización y exportación. Fueron inspeccionadas, no ejecutadas para este plan.

## Diseño funcional propuesto

### Configuración

En cada receptora añadir **“Tomar conteo de”**, con “Sin conteo: usar carga propia” y las etapas anteriores que producen cantidad. Mostrar número, código, nombre e ítem. Mantener separado **“Reportar conteo”** en la productora. Una etapa puede consumir A y producir B.

Guardar identidad estable de la tarea origen dentro de la versión (`InputCountSourceTaskId`, nombre propuesto), no solo su número. Reordenar conserva el vínculo; si el origen queda después del receptor, mostrar error y exigir corregirlo. Eliminar el productor o quitar su conteo identifica todos los consumidores; no borrar sus vínculos silenciosamente. Validar también en servidor y en publicación, incluida manipulación HTTP.

### Cálculo y ejecución

1. Preparar con estimaciones mientras no exista cantidad. Congelar relación y tarifa al preparar/activar conforme al modelo elegido; cambios posteriores del catálogo no alteran snapshots.
2. Al confirmar una entrega válida, resolver todos los receptores del productor en la misma ejecución y calcular sus cargas pendientes. Registrar intento productor, cantidad, tarifa utilizada y resultado. Un receptor ajeno al conteo no cambia de carga, aunque su fecha pueda verse afectada por una dependencia secuencial.
3. Recalcular las reservas afectadas en orden, con los responsables reservados y límites actuales. Diagnosticar cada conflicto; no cambiar responsables, ampliar turnos, dividir cargas ni forzar automáticamente. La política propuesta conserva revisión de impacto y opción explícita de pendiente cuando no quepa; presentar conflictos futuros sin activar esas etapas.
4. Avanzar únicamente a la siguiente etapa ejecutable del orden, satisfaciendo su regla de entrega/aprobación. Tener cantidad disponible no satisface esa dependencia. Si falta un conteo válido, bloquear con origen y acción concreta; no usar estimación como si fuera cantidad definitiva.
5. Al activar un receptor lejano, verificar que el intento origen siga vigente. Para capacidad y permisos distinguir actor actual, productor del conteo y receptor: no heredar autoridad por estar conectado a una etapa de Dirección. Mantener las restricciones actuales hasta definir cualquier ampliación explícita.

### Cero

Registrar omisiones por receptor con intento origen y motivo; liberar sus reservas sin crear entregas ni aprobaciones ficticias. Continuar tareas independientes y auditoría. Si hay varias omisiones no contiguas, el avance debe saltarlas al alcanzarlas y transportar el documento vigente sin simular eventos humanos.

No basta con buscar “la siguiente no omitida”: revisar `DependencyEventId`, condición de entrada, documento, revisión de la última entrega real y terminación de la ejecución. Si una etapa omitida era productora, aplicar la decisión pendiente sobre sus consumidores; nunca generar una cantidad que nadie reportó.

### Correcciones

Crear una vista previa de impacto que distinga consumidores directos, consumidores transitivos de nuevos conteos, tareas afectadas solo por secuencia/documentos y tareas independientes. Mostrar estado, carga anterior/propuesta, responsable, fechas, evidencia y acción propuesta.

Recalcular pendientes sin trabajo únicamente dentro de la operación confirmada. Para tareas con avance, entregas o revisión, conservar intentos, autoría, evidencia y carga histórica; crear nuevas correcciones solo después de revisar el impacto. No invocar sin adaptación el reinicio de todo el tramo actual. Revalidar revisiones y agenda al confirmar; rechazar propuestas caducadas sin escrituras parciales. No descontar “unidades ya trabajadas”: el conteo actual no registra ese concepto.

## Implementación por fases

1. **Cerrar decisiones e inventariar datos.** Confirmar tarifa, compatibilidad, auditoría y productor omitido; identificar versiones antiguas, JSON de planes y ejecuciones con devoluciones. Seleccionar registros para copia aislada y conservar comparación anterior/posterior sin publicar datos privados.
2. **Modelo y compatibilidad.** Introducir fuente por receptor y versión explícita de semántica de conteo. Añadir FK de origen en la misma versión e identidad del origen dentro de cada ejecución; restricciones de integridad y validación de orden. Desacoplar producción de `OutputTargetTaskId` para versiones nuevas. Mantener lectura de versiones antiguas y sus reintentos. Al crear un nuevo borrador a partir de una versión antigua, convertir los enlaces productor-destino en fuente por receptor y mostrar diferencias de cero/auditoría antes de publicar. No realizar conversión retroactiva silenciosa.
3. **Snapshots y planificación.** Actualizar `CopyStep`, payloads del editor, captura de tarifas, `ItemRatesJson` o su sustituto versionado, `StartAsync`, vistas previas y relecturas de planes. Reemplazar inferencias `posición - 1` por vínculo explícito. Cubrir inicio ocasional, calendario recurrente y operación semanal. Si hay tarifa por receptor, capturar su ID/revisión/minutos por separado del ítem producido.
4. **Motor.** Separar propagación de cargas, resolución de omisiones, avance secuencial y programación. Usar la misma resolución de origen en entrega, activación, temporizador y devolución. Actualizar huellas de capacidad e idempotencia para incluir todos los receptores, intentos, tarifas y revisiones afectados. Una entrega y sus cambios comprometidos deben ser atómicos; rollback limpia también el contexto EF y conserva reintentos de evidencia.
5. **Corrección revisable.** Añadir propuesta/confirmación con permisos equivalentes a los actuales y motivo, tanto desde receptor con asignación como desde pendiente. Conservar material mediante continuaciones y distinguir una corrección de conteo de una devolución documental ordinaria. Integrar cierre, revisión y supervisión sin reescribir decisiones históricas.
6. **Interfaz y consultas.** Modificar `/tareas/procesos`, `/planificacion/procesos`, `/mi-trabajo/procesos` y su integración en Mi trabajo. Reutilizar `campo`, selectores y errores del editor, diálogos y patrones de propuesta/confirmación. Mostrar “Usa conteo de etapa N”, cantidad/tarifa/origen real en detalle, lista de receptores y efectos al entregar. Revisar reportes/exportación, actualización de asignaciones y arrastre para que conserven carga y trazabilidad por receptor.
7. **Validación y entrega posterior.** Ejecutar la matriz en entorno aislado, revisar migración y diff; desplegar solo dentro del alcance que se acuerde entonces, siguiendo el recorrido obligatorio de infraestructura. Tras activar y verificar la app compartida, actualizar las notas existentes de procesos, carga por ítems, planificación, ejecución/historial y diseño afectadas; revisar mapa, inventario y enlaces y actualizar Graphify. Mientras sea propuesta, mantenerla fuera de la bóveda. Sincronizar notas pertinentes a `docs/boveda/` al preparar un commit documental, revisando divergencias.

## Matriz de validación propuesta

Esta es la matriz original **propuesta**. Los resultados efectivamente ejecutados y sus límites se registran en la sección final de implementación y validación. “Aislado” significa aplicación con base `sgol_task_checks_*` comprobada; “copia” significa copia aislada de la simulación. Las pruebas HTTP deben usar formularios y servicios reales, no solo llamadas al cálculo.

| Caso / precondición y acción | Resultado esperado | Prueba / entorno | Estado |
|---|---|---|---|
| Crear A productor; C y E consumen A; guardar, reabrir y publicar | Se conservan ambas referencias; B y D sin fuente | HTTP + persistencia / aislado | Propuesto |
| Reordenar, quitar origen o cambiar a la vez origen, ítem y regla de entrada | Identidad estable o errores por tarea; sin enlaces reasignados silenciosamente | HTTP manipulado + navegador / aislado | Propuesto |
| Fuente propia, posterior, ajena a versión o sin conteo | Rechazo de servidor, sin publicación parcial | Reglas + HTTP / aislado | Propuesto |
| A=12 alimenta C/E, B/D independientes | C/E reciben fórmula correspondiente; B/D conservan carga; solo B se activa | Integración + formulario / aislado | Propuesto |
| B consume A y produce otro conteo para D; E sigue usando A | Orígenes y tarifas no se mezclan | Integración / aislado | Propuesto |
| A=0 y consumidores no contiguos | Omite solo consumidores, libera reservas; independientes y auditoría continúan | Flujo completo hasta cierre / aislado | Propuesto |
| Omitir una receptora que también produce conteo | No inventa cantidad; aplica política explícita aún pendiente | Integración / aislado | Propuesto |
| Vacío, negativo, decimal, máximo entero y carga >4800 | Vacío no es cero; inválidos rechazados; demanda grande conservada sin truncamiento | HTTP + límites / aislado | Propuesto |
| Receptor inmediato o lejano antes de auditoría; varios omitidos seguidos | Avance, revisión, documento y cierre correctos, sin aprobaciones ficticias | Formularios + integración / aislado | Propuesto |
| Guardar plan, cambiar tarifa o publicar otra versión, activar plan guardado | Conserva relaciones/tarifas capturadas | Integración / aislado | Propuesto |
| Inicio ocasional, recurrente y semanal; dos ejecuciones simultáneas | Misma lógica; conteos aislados por ejecución, sin duplicados | Integración / aislado | Propuesto |
| Varios receptores aumentan carga, uno no cabe; combinar ancla crítica y plazo fijo | Diagnóstico por tarea; no invasión de anclas, cambio silencioso de responsable ni efectos parciales | Agenda + HTTP / aislado | Propuesto |
| Fuente inválida o sustituida al activar; temporizador simultáneo con entrega | No activa con dato obsoleto ni duplica asignación | Concurrencia / aislado | Propuesto |
| Corregir con receptor pendiente, activo con evidencia, entregado y aprobado | Vista previa distingue efectos; conserva historial y confirma nuevas correcciones | Flujo completo / aislado | Propuesto |
| Conteo positivo a cero y cero a positivo tras devolución | Omisiones y reservas coherentes; no recupera intentos antiguos como actuales | Integración / aislado | Propuesto |
| Corrección junto con cambio de responsable, arrastre o incidencia | Propuesta caduca o se recalcula; respeta permisos y bloqueos existentes | Concurrencia + integración / aislado | Propuesto |
| Reenvío idéntico, misma clave con distinta cantidad y fallo después de evidencia | Una transición; conflicto al cambiar intención; rollback sin huérfanos | HTTP concurrente + fallo inducido / aislado | Propuesto |
| Actor sin permiso, receptor ajeno y productor con rol elevado | No amplía lectura, devolución, búsqueda ni forzado por el nuevo vínculo | HTTP por rol / aislado | Propuesto |
| Registros antiguos: borradores, publicados, preparados, activos, entregados y omitidos | Migración preserva semántica acordada, snapshots, evidencia y reintentos | Migración + comparación de datos / copia | Propuesto |
| Actualizar catálogo, consultar Mi trabajo, reportes/exportar y arrastrar receptor | Carga calculada y origen permanecen; carga histórica no se reescribe | HTTP + integración / aislado | Propuesto |
| Editor y entrega a 320 px, móvil y escritorio; temas, teclado y textos largos | Selectores y efectos legibles, foco visible, error asociado, sin desbordes | Navegador autenticado real / aislado | Propuesto |
| Carga, vacío, error, éxito y reintento con Excel/fotos/checklist | Estado comprensible y evidencia retenida/validada conforme a reglas actuales | Navegador + formularios / aislado | Propuesto |

## Riesgos y criterios de aceptación

El principal riesgo es confundir dependencias de cantidad con dependencias de secuencia o documento. La implementación debe poder recalcular varias cargas sin adelantar trabajo y omitir receptores sin falsificar entregas. El segundo riesgo es reinterpretar versiones antiguas o invalidar trabajo independiente al devolver un conteo. El tercero es recalcular solo la primera reserva y dejar capacidad futura incorrecta.

Se aceptará cuando un proceso real de prueba permita seleccionar un mismo origen para dos etapas no consecutivas, guardar/publicar/preparar/activar, entregar cantidad, ejecutar independientes, comprobar cargas e historial, corregir con impacto revisado y cerrar. Debe aprobar también cero selectivo, compatibilidad, permisos, concurrencia, atomicidad y el recorrido visual completo. Compilar o pasar pruebas del cálculo por sí solas no cumple este criterio.

## Estado al entregar el plan

Investigación estática realizada y propuesta documentada. Decisiones de cero selectivo y revisión del impacto confirmadas; tarifa y límites detallados arriba pendientes. No hay funcionalidad implementada ni validación funcional nueva. No se realizó migración, reinicio, despliegue, commit o push. Actualización de referencias de comportamiento en la bóveda pendiente de una futura implementación y activación.

## Inicio de implementación: comprobaciones del 05/11/2026

Esta sección actualiza el estado de trabajo; el apartado anterior conserva el alcance de la entrega original. Se leyeron las instrucciones y el protocolo, se actualizó y verificó Graphify (42 notas) y se consultaron Inicio, dominio, reglas, procesos, carga por ítems y diseño consolidado. Se conservaron cambios previos en capacidad, arrastres, navegación, documentación y herramientas de pruebas.

Las decisiones consultadas quedaron resueltas en el apartado «Decisiones y límites», incluida la instrucción final de mantener el selector en Editar detalles y simplificar la operación. Los ejemplos de las consultas fueron propuestas; no se cuentan como pruebas ejecutadas.

La inspección actual confirmó el destino inmediato en `ApplyItemCount`, el salto global hasta auditoría para cero, la inferencia de estimación por posición anterior y la sustitución del tramo completo en `ReturnPendingCountAsync`. Esta última no satisface por sí sola la corrección selectiva y revisable solicitada.

Inventario agregado ejecutado mediante transacciones de solo lectura en `sgol_local`: 20 versiones, todas publicadas; 112 etapas de definición, ocho productoras y 14 auditorías; ningún vínculo legado no adyacente. Una ejecución en curso con tres etapas: una concluida, una pendiente de asignación con carga calculada y una en espera; un inicio activado con tarifas capturadas, sin inicios preparados pendientes. Dos intentos preparados y uno entregado. No se modificaron esos registros. Proceso Web observado: PID 22788, artefacto `.local/runtime/capacity6-20261105/Sgol.Web.dll`; esta observación no certifica equivalencia binaria con el checkout ni constituye prueba funcional compartida.

### Evidencia ejecutada antes de modificar funcionalidad

| Caso | Resultado esperado | Prueba / entorno | Estado y evidencia |
|---|---|---|---|
| Línea base existente de catálogo, formulario de planificación, tarifas congeladas, activación, entrega, devolución, reintento, cero y cierre | Conservar las expectativas de la suite actual, incluida su semántica histórica de cero | `rtk proxy dotnet run --project tools/Sgol.TaskChecks -- --process-items-only`; base nueva `sgol_task_checks_count_origin_baseline_20261105`, localhost:5440; host de pruebas propio | Aprobado: salida 0, 151 líneas PASS y `ITEMS_PHASE_CORE_PASS`; `.local/count-origin-20261105/baseline.log` |

La suite combina formularios HTTP reales y servicios con persistencia. Usa identidad y escáner de pruebas; no certifica autenticación real, antivirus ni navegación visual. Este resultado valida la línea base existente, **no** los escenarios nuevos de la matriz propuesta: varios consumidores no consecutivos, cero selectivo y corrección con vista previa siguen pendientes. No se modificaron expectativas para obtener el resultado.

La documentación de comportamiento de la bóveda no se actualizó: el cambio del checkout no está desplegado. Su reconciliación y la de la copia versionada siguen pendientes de una futura activación autorizada.

## Implementación y validación aislada (05/11/2026)

La matriz propuesta anterior se conserva como alcance original; esta tabla registra lo realmente ejecutado. No todas sus combinaciones han sido probadas.

Implementado: selector opcional «Tomar conteo de» en Editar detalles; validación de origen anterior/productor; captura inmutable en planes y ejecuciones; tarifa compartida del origen; varios receptores no consecutivos; avance secuencial; cero selectivo; bloqueo por productor omitido; conflictos de capacidad por tarea con guardado pendiente; devolución al responsable del origen y revisión de impacto con conservación de intentos y evidencia. Las versiones históricas mantienen su semántica. Migración: `20261105162939_AddStageCountSources`.

Decisión adicional confirmada tras prueba: A=12, C entrega con 24 minutos, D entrega trabajo independiente, E devuelve A para reportar 15. Después de repetir C con 30 minutos, E revisa conjuntamente la corrección de C y la entrega conservada de D. El formulario identifica ambas; se conservan autoría e historial. La primera regresión de cierre detectó D pendiente de revisión y falló correctamente; no se relajó esa expectativa.

| Recorrido ejecutado | Entorno/evidencia privada | Resultado |
|---|---|---|
| Catálogo, guardar/reabrir/publicar, preparar/activar, A=12 → C/E=24, independientes, entrega y cierre | Base `sgol_task_checks_count_sources_v7_20261105`; `count-sources-v7.log` | Aprobado mediante formularios HTTP y servicios reales |
| Cero selectivo, revisión omitida, auditoría con carga propia; productor omitido bloquea E; devolución y recuperación | Misma suite v7 | Aprobado |
| Corrección 12→15, revisión de impacto, continuaciones; Excel anterior conservado byte por byte e independientes sin reinicio | Misma suite v7 | Aprobado; comprobación adicional de revisión de todas las entregas al cierre detectó el caso descrito arriba |
| Plan recurrente preparado, nueva publicación y cambio de tarifa antes de activar | Misma suite v7 | Aprobado: conserva vínculos y tarifa capturados |
| Demanda 4 294 967 294 minutos en dos receptoras; capacidad insuficiente, huella caducada y guardar pendiente | Misma suite v7 | Aprobado: diagnóstico por tarea, demanda sin truncar y rollback |
| Solicitudes concurrentes idénticas y misma clave con intención distinta | Misma suite v7 | Aprobado: idempotencia y rechazo del cambio de intención |
| Sin CSRF y actor no autorizado | Misma suite v7 | Aprobado: 400 y 403; no equivale a matriz completa de roles |
| Editor real Chromium y WebKit; 320/768/1440 px; temas claro/oscuro/especial; teclado, cambios sin guardar, origen inválido por reordenación | 18 combinaciones y dos recorridos de guardar/reabrir/restaurar; capturas `editor-*.png` | Aprobado |
| Migración sobre base de la suite histórica con registros existentes; comparar valores de definiciones, planes, ejecuciones, asignaciones y evidencia; repetir migración | `sgol_task_checks_count_origin_baseline_20261105`; `legacy-migration.log` | Aprobado, modelo coincide y datos históricos conservados |
| Activar plan histórico por formulario, entregar cero y repetir solicitud | Misma base migrada; `legacy-runtime.log` | Aprobado: semántica histórica conservada |

Logs y capturas en `.local/count-origin-20261105/`, sin versionar. Las suites usan PostgreSQL aislado y el host real de Razor/servicios con identidad y escáner de prueba: no certifican login real ni antivirus. Las pruebas históricas usan datos generados por la suite anterior, no copia de la simulación compartida.

Fallos conservados en bases/logs anteriores: restricción de etapa accionable durante devolución; enumeración diferida del impacto; localización del runtime de navegador; fixture recurrente sin día fijo; contaminación de capacidad entre escenarios de la misma suite. Se corrigieron código o preparación según la causa, sin modificar expectativas para aprobar. La regresión adicional de revisión conjunta se documenta separadamente al finalizar.

Pendientes de la matriz ampliada: todos los roles con login real; fotos/checklist y fallos del escáner; concurrencia temporizador/entrega; combinaciones con arrastre, incidencias, anclas críticas y plazos fijos; reportes/exportación y todas las transiciones de corrección positivo→cero. No presentar estas combinaciones como certificadas por compilación ni por la suite principal.

No se publicaron cambios, no se reinició la app compartida y no se hizo commit ni push. Las notas consolidadas de procesos, carga por ítems, planificación, ejecución/historial, modelo y diseño permanecen pendientes de reconciliar cuando se autorice activar el cambio y se compruebe en la app compartida. Se conservaron las modificaciones documentales ajenas; no se añadieron planes ni evidencias a la bóveda.

Regresión final aprobada: `correction-joint.log`, base `sgol_task_checks_count_correction_joint_20261105`, marcador `COUNT_CORRECTION_PASS`. Repite el recorrido completo positivo y corregido con Excel, confirmación de impacto, solicitudes concurrentes y auditoría; comprueba que ninguna entrega vigente quede pendiente de revisión al cierre. El cambio final de revisión conjunta se validó con esta regresión específica; la matriz de navegador anterior no se repitió para el nuevo aviso textual. Revisión del diff de los archivos afectados y comprobación de espacios completadas; cambios ajenos conservados.

### Publicación autorizada posteriormente

El usuario autorizó recargar la app compartida para ver los cambios, sin `--force`. Publicado en `.local/runtime/count-sources-20261105`, Web 38728/lanzador 14328, proxy 20652 conservado; respaldo previo privado y migración `20261105162939_AddStageCountSources` aplicada. Hash publicado igual al ensamblado compilado. Readiness interno y HTTPS local/LAN: 200 con TLS normal. Referencias existentes de procesos, carga por ítems, instalación y base documental reconciliadas en la bóveda; índice actualizado. Sin commit/push ni recorrido autenticado compartido; siguen vigentes los límites de pruebas ampliadas anteriores.
