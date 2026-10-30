# Diagnóstico de conflictos al preparar un proceso

## Alcance y evidencia

29/10/2026. Cambio mixto de diagnóstico: conservar exactamente las decisiones de asignación y explicar su rechazo. El usuario pidió corregir el mensaje sin modificar la carga, los responsables ni el límite de Viri, y ampliar los mensajes para descansos y personas fuera de turno.

Reproducción de solo lectura sobre `sgol_local`, con conexión runtime configurada `default_transaction_read_only=on`: proceso «Fotografías faltantes en Sizes & Colors», ocasión 29/10/2026 a las 17:00, Viri / Aleatorio / Vale. La primera etapa se rechaza por capacidad de Viri: carga computada 435 min, capacidad 240 min, tarea de 10 min. Vale tiene horario guardado de 17:00 a 20:00, lunes a sábado, capacidad efectiva 180 min y carga 0. Su reserva pasa las comprobaciones de horario y capacidad en las simulaciones de candidatos. Log privado local: `.local/process-plan-probe-final.log`.

La carga diaria agrega pendientes y reservas; no representa solo los minutos libres desde la hora elegida. Tener horario no amplía por sí solo el límite operativo diario. No se modificó ningún registro de la simulación para superar el rechazo.

## Trazabilidad

- `ProcessPlanningService.PreviewCoreAsync`: conserva validación y huella; los rechazos de reservas pasan por `RequireReservation`.
- `ProcessPlanningService.Reservations`: adjunta etapa, destinatario, fecha, hora evaluada y tramos al error original; conserva código HTTP y regla de colocación.
- `ProcessPlanningService.Selection.ResolvePeople`: conserva la clasificación y el sorteo; adjunta las causas de los candidatos descartados. El fracaso de una etapa manual dentro de una combinación con Aleatorio conserva la etapa realmente afectada.
- `ProcessPlanningService.TimeConflict`: distingue descanso explícito, turno no iniciado, pausa y turno terminado a partir del horario registrado. No deduce presencia física ni cambia la agenda para aceptar una asignación.
- `Pages/Planning/Processes.cshtml.cs`: mismo formateador en Preview y Prepare. Reutiliza los mensajes existentes de Supervisión para las demás causas de horario.
- `Pages/Tasks/Index.cshtml.cs`: textos de causa y acción para exceso de capacidad y estados de turno. La alerta existente conserva `role=alert`, estilos y comportamiento de escape HTML.

Persistencia, copias de definición, conteos, entregas, revisión, historial, permisos, concurrencia, idempotencia y activación no cambian. Los diagnósticos viven en la excepción de la solicitud y no se escriben en tablas. Sin migración.

## Escenarios

| Acción / precondición | Resultado esperado | Prueba y entorno | Estado |
|---|---|---|---|
| Reproducir selección del usuario | Identificar Viri, etapa 1, 435/240 y 10 min adicionales | Servicio real, conexión compartida solo lectura | Aprobado |
| Etapa manual sin capacidad | Misma regla de rechazo; causa, etapa, persona y valores | HTTP real Preview y Prepare; BD aislada | Aprobado |
| Mezcla manual/Aleatorio y primera etapa en Aleatorio | Mantener la identidad del conflicto y causas de candidatos | Misma suite | Aprobado |
| Turno futuro no iniciado, pausa y turno finalizado | Mensajes diferentes; hora y tramos correctos | Proceso de prueba con primera etapa crítica; formularios reales | Aprobado |
| Descanso explícito | No confundir descanso con falta de horario o capacidad | Excepción guardada mediante servicio y POST real | Aprobado |
| Rechazo del plan | Sin plan preparado ni inicios parciales | Consultas de persistencia aislada | Aprobado |
| Flujo posterior válido con conteo | Conservar preparación, activación, tarifas, correcciones, cero e historial | Suite `--process-items-only` | Aprobado |
| Presentación de mensaje largo | Texto completo y sin desbordamiento | Razor real aislado, escritorio y móvil | Aprobado |

La programación flexible que encuentra un bloque válido conserva su aceptación. Los nuevos mensajes explican rechazos, no introducen una prohibición de reacomodar tareas.

Compilación de comprobación con `-t:Rebuild`: se detectó una compilación incremental que no incorporó cambios posteriores de los mismos archivos. La regresión HTTP de Aleatorio lo evidenció; no se consideró aprobado aquel artefacto. La compilación completa es requisito de esta entrega.

## Resultado final

Compilación completa aprobada sin advertencias ni errores. Suite `--process-items-only` aprobada en `sgol_task_checks_process_diagnostics_r4_20261029`, incluyendo los seis casos de capacidad (Preview/Prepare × manual/mixto/automático), tres franjas de turno y descanso. Evidencia: `.local/process-diagnostics-checks-r4.log`.

La revisión de la pantalla Razor real aislada confirmó texto completo, alerta accesible y ausencia de desbordamiento en escritorio y 320 px. Capturas privadas: `.local/process-diagnostics-desktop.png`, `.local/process-diagnostics-mobile.png`. Tema claro comprobado; tema oscuro no ejecutado visualmente, se conservaron los estilos y tokens de la alerta existente.

Se cargó `.local/runtime/process-diagnostics-20261029/Web/Sgol.Web.dll` mediante `.local/cargar-diagnostico-procesos.ps1`. Salud local y LAN aprobadas; proceso web 28800. El SHA-256 del ensamblado publicado coincide con el probado. No se modificaron capacidades, horarios, responsables, asignaciones ni planes de Viri o Vale.