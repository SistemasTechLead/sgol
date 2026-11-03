# Conservar pendientes protegidos al actualizar asignaciones — 03/11/2026

## Alcance y diagnóstico

Cambio mixto en /tareas/actualizar. El usuario eligió conservar íntegra la asignación afectada y actualizar las demás. No se autoriza retirar el pendiente ni resolver su solicitud automáticamente.

Consulta de solo lectura en la simulación local (PostgreSQL 127.0.0.1:5440, sgol_local): TAR-0367, «Realizar depósitos de efectivo», Viri, 03/11/2026 09:00–10:30 America/Mexico_City. Cero fotografías, adjuntos, entradas de avance, transferencias y reemplazos; sin entrega ni revisión. Existe una sesión de captura iniciada a las 10:40, sin consumo ni intentos de carga, y una solicitud POSPONER PENDIENTE para 04–10/11. No es una corrección programada. El catálogo mensual tiene día preferido 29; la instantánea previa no tenía día preferido. La reconciliación pretende quitar la ocurrencia del plan y el mensaje anterior mezclaba todas las causas posibles.

Entorno previo: Web .local/runtime/process-errors-20261103/Sgol.Web.dll, proceso 24328, acceso LAN mediante proxy; no son dos bases distintas. Se detectaron múltiples cambios previos y se conservaron. La incorporación propia a Program.cs es únicamente --calendar-preservation.

## Implementación y recorrido

- AssignmentRefreshService.BuildAsync reconcilia calendario, consulta fotografías, adjuntos, progreso, transferencias, capturas, reemplazos y solicitudes, y ofrece una decisión por asignación protegida. Incluye también checklist y texto registrado para evitar pérdidas silenciosas.
- PreserveCalendarAssignmentIds viaja del checkbox al POST Preview y como campo oculto al POST Apply. El servidor vuelve a construir la propuesta y verifica autorización, fingerprint y bloqueo operativo. La autorización es por revisión, no una excepción persistente.
- La fila conservada se excluye de escrituras y eliminaciones. Mantiene su reserva en CapacityLedger y su identificador en affectedIds: no puede reintroducirse como vecina a reacomodar. Se conservan responsable, fechas, carga, instantánea, revisión, evidencias y solicitudes.
- Los productores de ejecución y revisión siguen utilizando la misma asignación. La solicitud sigue pendiente para su resolución habitual. Se conserva el historial; no hay nueva tabla ni migración. El catálogo nuevo se usa en nuevas asignaciones.
- Refresh.cshtml reutiliza las tarjetas, estilos y comportamiento de opciones existentes. Muestra tarea, responsable, intervalo, plan y causas concretas. El título de conflictos restantes pasa a «Estos casos requieren atención».
- Si el pendiente vuelve a incluirse en otra actualización futura, la decisión se solicitará de nuevo. No se simula haber sincronizado su definición.

## Validación

Bases dedicadas sgol_task_checks_calendar_20261103 y sgol_task_checks_calendar_final_20261103. Fixture sintético reproduce la retirada por calendario mensual, con captura y solicitud pendiente; agrega evidencia después de la vista previa para probar concurrencia. Se usa la página Razor real con sus formularios, antiforgery y servicios; solo identidad y reloj son controlados por el host de pruebas.

| Escenario | Resultado esperado | Prueba / entorno | Estado |
|---|---|---|---|
| Captura sin archivos y posposición pendiente | Causas específicas, conservación explícita | Servicio, base aislada | Aprobado |
| Consentimiento retirado del POST | Rechazo sin aplicar lote | Servicio, base aislada | Aprobado |
| Archivo agregado después de revisar | Propuesta obsoleta rechazada | Servicio, base aislada | Aprobado |
| Marcar, revisar y confirmar | Actualiza otra tarea y conserva íntegra la protegida | GET y POST Razor reales | Aprobado |
| Repetir confirmación | Sin duplicación ni cambios adicionales | POST Apply repetido | Aprobado |
| Comparación antes/después | Asignación y solicitud serializadas idénticas; captura y archivo presentes | Base aislada | Aprobado |
| Rol de colaborador | 403 | GET real | Aprobado |
| Teclado, textos largos, temas, móvil | Checkbox con Space, revisión, confirmación deshabilitada al cambiar opciones, sin overflow | Edge, 320/1280, light/dark/special | Aprobado |
| Revisión visual | Contenido legible sin recortes | Captura real móvil oscura | Aprobado |
| Primera ejecución visual | Cargar JS del sitio | Host de pruebas apuntaba a webroot incorrecto | Fallido inicialmente; corregido y repetido con éxito |
| Dispositivo físico y lector de pantalla | Uso accesible completo | No disponible en esta validación | No ejecutado |
| Cada combinación de reemplazos, progreso y transferencias | Conservar causas correspondientes | Inspección del predicado; no fixture individual por combinación | No ejecutado |
| Confirmación sobre simulación compartida | Decisión final del usuario | No se envió Apply compartido | No ejecutado, deliberado |

Herramientas: tools/Sgol.TaskChecks/CalendarPreservationChecks.cs (--calendar-preservation), tools/Sgol.MobileChecks/calendar-preservation-checks.cjs. SGOL_CALENDAR_UI=1 mantiene el host en 7297 hasta .local/calendar-ui-done. Playwright se resuelve mediante NODE_PATH. Evidencia: .local/calendar-final-checks.log y .local/calendar-preservation-ui/. Compilación y publicación local correctas.

## Activación y límites

Versión activada en .local/runtime/calendar-preserve-20261103/Sgol.Web.dll, proceso 16684 verificado. Live y ready internos, live LAN y /acceso LAN devuelven 200; evidencia en .local/calendar-deployment-health.json. No se cambiaron asignaciones ni solicitudes de la simulación. No se ejecutó migración compartida, commit ni push. Las pruebas autenticadas de formularios se hicieron en el host aislado; en LAN se verificó arranque y acceso.
