# Conservar pendientes protegidos al actualizar asignaciones — actualizado 04/11/2026

## Alcance y diagnóstico

Cambio mixto en /tareas/actualizar. El usuario eligió conservar íntegra la asignación afectada y actualizar las demás. No se autoriza retirar el pendiente ni resolver su solicitud automáticamente.

Consulta de solo lectura en la simulación local (PostgreSQL 127.0.0.1:5440, sgol_local): TAR-0367, «Realizar depósitos de efectivo», Viri, 03/11/2026 09:00–10:30 America/Mexico_City. Cero fotografías, adjuntos, entradas de avance, transferencias y reemplazos; sin entrega ni revisión. Existe una sesión de captura iniciada a las 10:40, sin consumo ni intentos de carga, y una solicitud POSPONER PENDIENTE para 04–10/11. No es una corrección programada. El catálogo mensual tiene día preferido 29; la instantánea previa no tenía día preferido. La reconciliación pretende quitar la ocurrencia del plan y el mensaje anterior mezclaba todas las causas posibles.

Entorno previo: Web .local/runtime/process-errors-20261103/Sgol.Web.dll, proceso 24328, acceso LAN mediante proxy; no son dos bases distintas. Se detectaron múltiples cambios previos y se conservaron. La incorporación propia a Program.cs es únicamente --calendar-preservation.

## Implementación y recorrido

- AssignmentRefreshService.BuildAsync reconcilia calendario, consulta fotografías, adjuntos, progreso, transferencias, capturas, reemplazos y solicitudes, y ofrece una decisión por asignación protegida. Incluye también checklist y texto registrado para evitar pérdidas silenciosas.
- PreserveCalendarAssignmentIds viaja del checkbox al POST Preview y como campo oculto al POST Apply. El servidor vuelve a construir la propuesta y verifica autorización, fingerprint y bloqueo operativo. Desde la decisión del usuario del 04/11/2026, la confirmación guarda una excepción hasta el siguiente cambio de catálogo; la selección de la vista previa por sí sola no la persiste.
- La fila conservada se excluye de escrituras y eliminaciones. Mantiene su reserva en CapacityLedger y su identificador en affectedIds: no puede reintroducirse como vecina a reacomodar. Se conservan responsable, fechas, carga, instantánea, revisión, evidencias y solicitudes.
- Los productores de ejecución y revisión siguen utilizando la misma asignación. La solicitud sigue pendiente para su resolución habitual. Se conserva el historial; no hay nueva tabla ni migración. El catálogo nuevo se usa en nuevas asignaciones.
- Refresh.cshtml reutiliza las tarjetas, estilos y comportamiento de opciones existentes. Muestra tarea, responsable, intervalo, plan y causas concretas. El título de conflictos restantes pasa a «Estos casos requieren atención».
- La excepción confirmada se excluye del contador y de la propuesta mientras coincide la huella completa de la definición (incluida revisión). Un nuevo cambio de catálogo exige otra revisión; no se simula haber adoptado la definición.

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

## Corrección del 04/11/2026: conservación duradera

Estado: publicada en la app compartida el 04/11/2026; sin migración, commit ni push. Confirmación operativa pendiente del usuario.

La pantalla podía mostrar cero cambios porque retiraba de la propuesta la fila elegida para conservar, mientras CountsAsync seguía contando sus diferencias originales. Además CanApply exigía diferencias materiales y no permitía confirmar solo una decisión. El usuario eligió guardar la excepción hasta que cambie otra vez el catálogo.

Se registra `OPERATIONAL_ASSIGNMENT_CALENDAR_PRESERVED` en audit_event, con asignación, huella del catálogo, actor, motivo y clave del lote, dentro de la misma transacción y bloqueo de ApplyAsync. La tabla de auditoría es inmutable (trigger existente impide UPDATE/DELETE); no hay tabla nueva ni migración. No se cambia ningún campo de la asignación, ni su revisión o snapshot. Los productores de captura, ejecución, evidencia, arrastre y revisión siguen usando la asignación intacta.

CountsAsync y BuildAsync consultan la última decisión de cada asignación. Las excepciones vigentes se conservan como reservas, no se reacomodan como vecinas y cuentan como ocurrencias protegidas en la conciliación. La huella de actualización incluye las excepciones vigentes y pendientes (algoritmo 6), de modo que una confirmación concurrente invalida una vista previa anterior. Los registros históricos sin esta decisión no se convierten automáticamente en excepciones permanentes.

La página muestra decisiones de conservación por confirmar aunque haya cero actualizaciones, permite Confirmar conservación con motivo y deja de ofrecer acciones cuando todo está resuelto. El catálogo deshabilita Actualizar Asignaciones cuando no quedan diferencias sin resolver y evita afirmar que las excepciones adoptaron el catálogo.

| Escenario | Esperado | Entorno / prueba | Estado |
|---|---|---|---|
| Confirmación mixta | Actualizar otra tarea y registrar conservación | CalendarPreservationChecks, HTTP real | Aprobado |
| Confirmación con cero diferencias | Guardar solo decisión e historial | Misma suite y copia tras actualización previa | Aprobado |
| Recarga y contador | Sin repetir checkbox; botón del catálogo deshabilitado | GET Razor real /tareas y /tareas/actualizar | Aprobado |
| Reintento | Una auditoría por versión confirmada | POST repetido | Aprobado |
| Catálogo vuelve a cambiar | Reactivar contador y decisión | Cambio de descripción y revisión | Aprobado |
| Otra tarea cambia de carga | No reabrir ni reacomodar excepción | Servicio, comparación íntegra de asignación | Aprobado |
| Captura, evidencia, solicitud e historial | Preservar íntegros | Comparaciones JSON y presencia de registros | Aprobado |
| Consentimiento manipulado / evidencia concurrente | Rechazo atómico | Suite original ampliada | Aprobado |
| CSRF y colaborador | Rechazar | HTTP en copia | Aprobado |
| Estado de la captura del usuario | Cero cambios, una conservación confirmable | Replay en copia del estado tras actualización | Aprobado |
| 320/768/1440, tres temas, teclado | Sin overflow, motivo y botón operables | Chromium/Edge y WebKit; 18 casos | Aprobado |
| Global y procesos | Mantener contratos | Suites existentes, 24 y 30 aserciones | Aprobado |
| Despliegue Web | Nueva publicación; datos y proxy intactos | Procesos, listeners y HTTP | Aprobado |
| Confirmar sobre datos compartidos | Revisión del usuario | No se envía Apply compartido | No ejecutado |

Evidencias privadas en `.local/calendar-preservation-20261104/`; navegador `tools/Sgol.MobileChecks/calendar-preservation-confirmation.cjs`. La primera comprobación HTTP de la leyenda falló por buscar texto sin decodificar entidades HTML; corregida la aserción, el recorrido completo pasó. Compilación corregida por nulabilidad de ResourceId de auditoría; las ejecuciones finales corresponden al código compilado sin errores.
Resultados finales del 04/11: 54 aserciones de conservación, 10 del replay con cero cambios materiales, 24 globales y 30 de procesos: 118 comprobaciones dirigidas; 18 escenarios de navegador. Host aislado con sesión y reloj controlados; no equivale a una confirmación autenticada en la app compartida.

Publicación efectiva: `.local/runtime/calendar-preservation-20261104/Sgol.Web.dll`, SHA256 7AABA1333C50935E5140ACBB2415EB2137556FD4D67B5F92883585EBD8C1CAE1, idéntica a la Web de las pruebas. Lanzador 5028, hijo 16340 en 127.0.0.1:7241; proxy 20652/lanzador 23392 conservado. Readiness interno y /acceso local/LAN: 200 con TLS normal. Las 789 asignaciones compartidas conservaron huella completa antes/después de publicar. No se creó una excepción en esos datos ni se enviaron confirmaciones compartidas. La publicación anterior refresh-search-20261104 permanece disponible para recuperación.

Se consultaron Inicio, dominio, reglas, referencia de actualización y diseño; para publicar se reutilizó el recorrido de instalación, infraestructura, base documental y acceso LAN ya leído en esta sesión, contrastado de nuevo con procesos y listeners. Actualizadas las referencias existentes de actualización, patrones, instalación y base documental. Mapas e inventario conservan sus entradas; enlaces verificados y Graphify actualizado. Copia versionada pendiente de la preparación de un commit documental; no hubo sincronización masiva.