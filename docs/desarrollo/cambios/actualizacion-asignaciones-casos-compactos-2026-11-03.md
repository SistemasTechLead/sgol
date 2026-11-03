# Casos compactos al actualizar asignaciones — 03/11/2026

## Alcance

Cambio de interfaz y metadatos de diagnóstico de /tareas/actualizar. El usuario solicita identificar tarea, causa y solución sin párrafos repetidos, para todos los tipos de error de esta pantalla. Se conservan las decisiones de negocio, selección por grupo o asignación, formularios, autorización, fingerprint, bloqueo, confirmación e historial.

## Diseño e implementación

Cada tarjeta reúne:
1. Lista de códigos y nombres de tarea; responsable y fecha para decisiones individuales.
2. Motivo breve.
3. Solución directamente debajo, con checkbox y consecuencia visible cuando requiere autorización, o indicación de cómo resolver un bloqueo.
4. Ver detalles conserva contexto completo, plan y explicación extensa.

Las decisiones agrupadas siguen aplicando a todas las tareas enumeradas; no se crean falsas elecciones independientes. La consecuencia de retirar avances/correcciones o cambiar plazos permanece visible. Los conflictos generales sin una tarea identificada se presentan como Actualización general, sin inventar una entidad.

AssignmentRefreshService agrega Issues estructurados con código, tarea y contexto para los 15 bloqueos actuales; conserva los mensajes anteriores en Conflicts para compatibilidad y reglas. Las siete decisiones reciben presentación compacta en RefreshCaseText. Los conflictos de unidad, responsable y etapa ahora identifican su tarea mediante datos, no extrayéndola de un párrafo.

Se reutilizan tarjetas, tokens, checkbox y details nativos existentes. Sin biblioteca, migración ni cambios al cálculo de agenda. Los campos enviados mantienen nombre y valores. Los mensajes completos están disponibles en Ver detalles.

## Matriz y evidencia

| Caso | Resultado esperado | Prueba / entorno | Estado |
|---|---|---|---|
| Siete decisiones: inactiva, excluida, calendario anterior, tiempo restante, plazo aprobado, proceso y conservación de calendario | Causa, acción y efecto específicos | Presentación de los siete códigos | Aprobado |
| Quince bloqueos: especialización, modalidad/evidencia de proceso, unidad, responsable, fecha, horario, criticidad, tamaño, capacidad, límite de búsqueda, arrastre y tres restricciones de etapas | Causa y solución específicas | Presentación de los quince códigos | Aprobado |
| Avance incompatible | Tarea identificada y motivo/solución en su tarjeta | Servicio y GET Razor reales, base aislada | Aprobado |
| Conservación individual y dos tareas inactivas agrupadas | Lista visible y solución asociada sin nombres duplicados fuera de detalles | Razor real en navegador | Aprobado |
| Teclado y detalles | Space selecciona; Enter abre/cierra detalles; controles conservan foco nativo | Edge, 320 y 1280, tres temas | Aprobado |
| Texto largo y reflujo | Sin desbordamiento global | Seis combinaciones de ancho/tema | Aprobado |
| Revisar y confirmar selección mixta | Binding correcto y consecuencia persistida | POST Razor reales | Aprobado |
| Alteración de consentimiento, evidencia nueva, reintento y rol colaborador | Rechazo, obsolescencia, idempotencia y 403 | Servicio/HTTP, base aislada | Aprobado |
| Conservación de asignación y solicitud | Serialización idéntica; captura y archivo presentes | Comparación antes/después | Aprobado |
| Cada regla operativa de los 15 bloqueos | Misma condición de rechazo | Inspección del diff; no se provocaron las 15 condiciones | No ejecutado individualmente |
| Dispositivo físico y lector de pantalla | Interacción accesible | Fuera de esta comprobación | No ejecutado |

Base sgol_task_checks_refresh_compact_20261103 en localhost:5440. Herramientas CalendarPreservationChecks (--calendar-preservation) y calendar-preservation-checks.cjs; host Razor aislado 7297. Evidencia en .local/compact-refresh-checks.log y .local/compact-refresh-ui/. Compilación final sin advertencias ni errores. Se corrigió un error inicial de sintaxis Razor antes de ejecutar las pruebas. Revisión visual de captura de escritorio oscuro.

## Entorno

Se conservan cambios previos del workspace. No se aplican actualizaciones a la simulación compartida, ni commit ni push. El usuario pidió recargar al terminar. La herramienta de control de Brave se detuvo al no poder verificar su URL actual; no se intentó eludir esa restricción.

Activación: .local/runtime/refresh-compact-20261103/Sgol.Web.dll. Live/ready internos, live LAN y acceso LAN 200; CSS nuevo servido por LAN. Evidencia: .local/compact-refresh-health.json. La recarga de Brave queda pendiente por la restricción de la herramienta indicada arriba.
