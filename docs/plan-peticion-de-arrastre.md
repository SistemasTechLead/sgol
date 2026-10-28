# Plan de implementación: petición de arrastre

Fecha: 22 de octubre de 2026. Estado: plan para implementación; no ejecuta migraciones ni modifica asignaciones.

## 1. Resultado y acuerdos

El colaborador solicita tiempo adicional por un incidente desde Mi trabajo. Un supervisor revisa el alcance y aprueba o rechaza en Supervisión. Únicamente la aprobación cambia vencimientos de asignaciones todavía pendientes. Los huecos disponibles absorben el retraso, total o parcialmente.

Las entregas realizadas mientras se espera la decisión conservan sus fechas y pueden quedar identificadas como «Entrega tardía con arrastre autorizado». La aprobación no equivale a validar evidencia, concluir una tarea, eximir responsabilidad ni autorizar un nuevo turno.

Acuerdos del usuario:

- Botón «Petición de arrastre» en Mi trabajo.
- Causa oficial, duración y aceptación que envía la solicitud a Supervisión.
- Cambios de cierre solamente después de aprobación del supervisor.
- Conservación de entregas y reconocimiento posterior del arrastre autorizado.
- Huecos que absorben los minutos de demora.
- Posibilidad de exceder la salida como excepción explícita de vencimiento.
- Apartado de arrastres antes de «Pendientes de revisión» cuando existan solicitudes pendientes; inmediatamente después cuando no existan.

Las reglas adicionales de este documento son propuestas de implementación. Las limitaciones operativas del apartado 11 deben quedar resueltas antes de habilitar el flujo completo.

## 2. Base de código comprobada

- Mi trabajo: `src/Sgol.Web/Pages/MyWork/Index.cshtml` e `Index.Operational.cs`.
- Asignaciones y revisiones: `Infrastructure/Persistence/Planning/OperationalTasks.cs`, `OperationalTaskService.cs` y `OperationalTaskService.History.cs`.
- Supervisión: `Pages/Validations/Index.cshtml`, `Index.cshtml.cs` y `OperationalSupervisionService.cs`. La ruta es `/validaciones`.
- Actualmente se muestra Capacidad antes de las secciones; la primera sección es «Pendientes de revisión», seguida de secciones por rol.
- Dirección tiene un filtro particular en «Pendientes de revisión». No reutilizar ese filtro como regla general de autorización de arrastres.
- Existe autorización por sucursal, vigencia de cuenta/empleo/rol, jerarquía y asignación. La nueva operación debe verificar permisos nuevamente al decidir.
- `OperationalAssignment` conserva `StartsAt`, `DueAt`, `CompletedAt`, `Revision`, evidencia y revisión. Ya existe bloqueo transaccional de planificación mediante `pg_advisory_xact_lock(734651001)`.
- Reportes tiene estados de ciclo de vida y una métrica de entregas fuera de plazo; incorporar puntualidad/arrastre como dimensión separada.
- Hay cambios locales previos en muchos de estos archivos. Implementar sobre su contenido vigente sin sobrescribirlos ni regenerar planes.

## 3. Catálogo cerrado de causas

Persistir código estable y versión del catálogo; mostrar descripción legible. Validar en servidor y conservar el texto aplicado como snapshot histórico.

| Código | Descripción |
| --- | --- |
| FALTA_TIEMPO_OPERATIVO | Sin capacidad operativa suficiente. |
| ALTA_AFLUENCIA_CLIENTES | La atención al cliente impidió terminar. |
| PRIORIDAD_COMERCIAL | Actividad comercial prioritaria autorizada. |
| FALTA_RESPONSABLE | Responsable ausente o no disponible. |
| OMISION_RESPONSABLE | Incumplimiento atribuible al responsable. |
| FALTA_EVIDENCIA | Evidencia insuficiente. |
| ERROR_DE_ASIGNACION | Error de puesto, persona, fecha o capacidad. |
| DEPENDENCIA_EXTERNA | Dependencia de proveedor, cliente, sistema o tercero. |
| FALLA_SISTEMA_HERRAMIENTA | Falla técnica o falta de herramienta. |
| CAUSA_NO_IDENTIFICADA | No fue posible determinar la causa. |

Propuesta: explicación obligatoria para CAUSA_NO_IDENTIFICADA y referencia a la autorización para PRIORIDAD_COMERCIAL. Ninguna causa seleccionada acredita por sí sola que el incidente esté justificado. FALTA_RESPONSABLE y ERROR_DE_ASIGNACION pueden requerir una corrección o reasignación por los mecanismos existentes; el arrastre no las ejecuta.

## 4. Flujo del colaborador

1. Abrir «Petición de arrastre» en las acciones de Mi trabajo.
2. Seleccionar causa, duración en minutos enteros positivos y hora de término del incidente, inicialmente «Ahora». Inicio calculado = término menos duración. Usar America/Mexico_City para captura/presentación y UTC para persistencia. No aceptar término futuro.
3. Capturar explicación y obtener vista previa calculada por el servidor: tareas, cierres actuales/propuestos, minutos por tarea, origen directo/propagado y advertencias.
4. «Aceptar y enviar» crea la solicitud pendiente y sus detalles. No cambia DueAt, colores ni puntualidad efectiva. Guardar idempotencia y revisiones del alcance.
5. Mostrar confirmación e historial propio: pendiente, aprobada o rechazada; causa, minutos, decisión y comentario.

Mientras una petición está pendiente, la tarea sigue pudiendo entregarse y revisarse por los flujos actuales. Mostrar «Arrastre solicitado, pendiente de autorización» sin presentarlo como concedido. Una solicitud enviada es inmutable; una corrección requiere otro registro y control de solapamiento. No añadir edición o borrado del historial.

## 5. Cálculo del alcance y propagación

### 5.1 Fotografía al enviar

Trabajar con asignaciones existentes de la persona y sucursal, de todos sus planes pertinentes; no limitar al plan visible en pantalla. Congelar el conjunto candidato y sus fechas/revisiones al enviar. No incorporar asignaciones creadas después por el paso del tiempo.

Excluir entregadas, reasignadas y resueltas al enviar. No incluir automáticamente pendientes de días anteriores. Las tareas ordinarias del bloque/jornada afectado se calculan independientemente del orden de sus identificadores.

Semántica de intervalos: para el incidente [I, T), una tarea es directamente afectada si `StartsAt < T && DueAt > I`, está activada al término del incidente y continúa pendiente al enviar. El contacto exacto con un extremo no produce afectación directa; el contacto entre tareas sí permite propagación. Cada afectación directa suma la duración completa una sola vez por incidente, conforme al ejemplo acordado de tareas paralelas.

### 5.2 Propagación con absorción de huecos

Usar los vencimientos efectivos de la fotografía, incluyendo ajustes previamente aprobados. Conservar también el vencimiento original para historia. Separar cadenas desconectadas; una tarea de larga ventana no debe unir artificialmente toda la semana.

Recorrido por inicio, agrupando inicios iguales para obtener un resultado independiente del orden:

- Mantener frontera anterior F (fin del bloque antes de este incidente) y frontera propuesta G (fin después del incidente).
- Una tarea directamente afectada recibe D minutos, la duración del incidente.
- Para otra tarea posterior, con inicio S, la extensión propagada es `max(0, G - max(F, S))` minutos. Si no hay bloque afectado anterior, es cero.
- Si una tarea tiene afectación directa y propagada, aplicar el máximo de ambas, nunca la suma.
- Para cada grupo, calcular con las fronteras de entrada y luego actualizar F al máximo cierre anterior del bloque y G al máximo cierre propuesto. No sumar minutos entre tareas paralelas.
- Si el siguiente inicio está en G o después, el hueco agotó esa propagación. Un incidente puede tener otro bloque directamente afectado, que se trata por separado.
- Las tareas entregadas antes de solicitar no reciben extensión ni crean por sí mismas puentes de propagación. No interpretar sus ventanas planificadas como trabajo pendiente.
- Descansos y ausencias no constituyen huecos recuperables. Calcular la absorción con disponibilidad efectiva; no consumir comida para reducir retraso. Esta adaptación debe cubrir explícitamente la aritmética de fronteras al implementar.

Guardar versión del algoritmo y resultado por tarea. Reutilizar el mismo cálculo para vista previa y envío. No recalcular fechas al pintar tarjetas.

### 5.3 Ejemplos de aceptación

| Caso | Resultado |
| --- | --- |
| Incidente 14:10–14:40, 30 min; tareas pendientes 14:00–14:30 y 14:30–15:00 | Cierran 15:00 y 15:30. |
| Tarea posterior 18:30–19:00 | Conserva 19:00 por absorción completa del hueco. |
| Incidente 18:10–18:20, 10 min; tareas 18:00–18:30 y 18:30–19:00 | Cierran 18:40 y 19:10, con aviso de excepción si excede salida. |
| Bloque termina originalmente 15:00, nuevo fin 15:30; siguiente 15:20–15:50 | El hueco absorbe 20; la siguiente recibe 10 y cierra 16:00. |
| Mismo bloque; siguiente comienza 15:30 | Sin extensión propagada. |
| Primera tarea ya entregada al enviar | Sus fechas no cambian; la segunda puede afectarse directamente. |
| Tarea vencida antes del inicio del incidente | No se incluye como afectación directa. |
| Extensión insuficiente para alcanzar el momento actual | La tarea puede seguir vencida; mostrarlo. |

## 6. Supervisión y orden dinámico

Crear un único componente/partial «Peticiones de arrastre», con contador de pendientes autorizadas, lista de solicitudes y acceso al historial de aprobadas/rechazadas. Listar pendientes por antigüedad, con persona, causa, intervalo, minutos y tareas alcanzadas. Cargar detalles sin bytes de evidencia.

Orden exacto, conservando Capacidad en su ubicación actual:

| Hay pendientes autorizadas para este supervisor | Orden |
| --- | --- |
| Sí | Capacidad → Peticiones de arrastre → Pendientes de revisión → secciones por rol. |
| No | Capacidad → Pendientes de revisión → Peticiones de arrastre → secciones por rol. |

El predicado es `PendingAuthorizedCarryoverCount > 0`; no depende del número de filas después de búsqueda/paginación ni de tener historial. Al resolver la última solicitud pendiente, el apartado pasa después de Pendientes de revisión en la respuesta/refresco siguiente. Estado vacío: «No hay peticiones de arrastre pendientes». Un error de carga no se muestra como cero solicitudes.

Renderizar el apartado una sola vez en el orden real del DOM, de lectura y tabulación; no cambiar solo el orden con CSS. Conservar filtros y posición contextual al abrir/cerrar detalle. No agregar sondeo continuo como requisito inicial: actualizar al cargar y al resolver, con acción explícita de actualizar si es necesaria.

Detalle de solicitud: fotografía enviada, diferencias actuales, tareas que ya se entregaron, cierres que sí se modificarán, reconocimiento histórico, cruce de salida/día, causa y explicación. Acciones «Aprobar arrastre» y «Rechazar», con comentario de decisión obligatorio como propuesta de trazabilidad. La aprobación es integral, sin selección parcial de tareas ni edición de minutos; para cambiar el incidente se rechaza y se solicita corregir.

## 7. Aprobación, permisos y concurrencia

Servicio dedicado, por ejemplo `OperationalCarryoverService`, y calculador puro `OperationalCarryoverCalculator`. Contratos nuevos en Planning; entidades/configuración EF y registro de servicio siguiendo los patrones existentes.

- Crear solo para la persona autenticada; nunca confiar en PersonId enviado por formulario.
- Listar y decidir por sucursal y jerarquía de supervisión vigente. No copiar el filtro visual especial de Dirección como autorización.
- Propuesta: impedir autoaprobación también a Dirección. Los casos sin supervisor elegible deben mostrarse como conflicto de configuración, sin autoaprobar.
- Comprobar autorización sobre todo el alcance al decidir. Una petición con alcance mixto solo puede aprobarla alguien autorizado sobre todas sus tareas.
- POST protegido por antiforgery; claves idempotentes y control de revisión para envío y decisión.
- Dentro de una transacción, tomar el bloqueo existente, releer solicitud/asignaciones y verificar nuevamente permisos, estado y revisiones.
- El tiempo transcurrido no amplía el conjunto ni la duración. Las tareas activadas durante la espera solo son alcanzables si ya estaban incluidas por propagación al enviar.
- Si una tarea se entregó durante la espera: conservar DueAt y CompletedAt; registrar el reconocimiento autorizado usando el cierre propuesto de la fotografía. Conservar la cadena original para no eliminar retrospectivamente el efecto en tareas posteriores.
- Si hubo reasignación, cambio de fechas, otro arrastre aprobado, modificación del alcance o cambio estructural: devolver conflicto y mostrar vista previa vigente al supervisor. Una aprobación posterior debe referir la nueva huella revisada; no reintentar automáticamente ni aplicar resultados invisibles.
- Cambios de revisión sin efecto en elegibilidad/fechas deben distinguirse de conflictos estructurales. Si una tarea ya tiene decisión final, el arrastre solo puede agregar historia/puntualidad, nunca cambiar esa decisión.
- Actualizar DueAt y Revision únicamente de las pendientes elegibles; insertar detalles efectivos, decisión y auditoría atómicamente. Si falla una precondición, no aplicar parcialmente.
- Rechazar registra decisión y motivo sin cambiar cierres. Decisiones terminales no se revierten ni borran en este alcance.

## 8. Modelo de datos y auditoría

Proponer entidades nuevas, sin reutilizar el comentario de entrega:

1. `OperationalCarryoverRequest`: identidad, sucursal, persona, solicitante, código/descripción/version de causa, explicación, intervalo, minutos, fecha de envío, estado, versión de regla, huella y revisión.
2. `OperationalCarryoverRequestItem`: solicitud/asignación, revisión y estado al enviar, inicio/cierre de referencia, cierre propuesto, minutos, afectación directa/propagada y vínculo de cadena.
3. Decisión inmutable: supervisor, fecha, aprobación/rechazo, comentario y huella aprobada. Puede ser entidad o campos terminales protegidos en la solicitud.
4. Resultado por tarea: cierre antes/después realmente aplicado o reconocimiento sin cambio, entrega observada al decidir y motivo de no modificación. Vincular ajustes acumulados para reconstrucción.
5. Vencimiento original de asignación: snapshot persistente o reconstrucción inequívoca desde el primer ajuste. No inferir el original del catálogo actual. Registrar corte de incorporación para no presentar fechas históricas desconocidas como originales comprobadas.

Índices por sucursal/estado/envío y persona/intervalo; unicidad de solicitud/asignación e idempotencia; minutos positivos, intervalo consistente y transiciones válidas. Detectar solapamiento de incidentes bajo bloqueo: no conceder dos veces el mismo tiempo. Propuesta inicial: impedir aprobación de incidentes solapados de una misma persona y exigir su corrección, en vez de sumar o recortar silenciosamente.

Auditar solicitud enviada, aprobada/rechazada y cierres modificados/reconocidos, con identificadores y antes/después. No guardar evidencia binaria ni secretos en auditoría. La migración es aditiva; no recalcular vencimientos, regenerar planes ni alterar entregas existentes.

## 9. Puntualidad, tablero y reportes

Separar estado operativo (asignada/pendiente de revisión/concluida/etc.) de puntualidad y autorización. Una tarea puede ser «Pendiente de revisión» y tener «Entrega tardía con arrastre autorizado».

Presentar: cierre original conocido, cierre efectivo, hora real de entrega, cierre reconocido si no se modificó por estar entregada y referencia a arrastres aprobados.

- Entregada antes de solicitar: queda fuera de esa nueva petición.
- Entregada durante la espera y después de DueAt: mostrar «Entrega tardía con arrastre autorizado» tras aprobación; indicar además si quedó dentro o fuera del plazo reconocido. Aprobar un incidente no cubre demora ilimitada.
- Entregada antes de DueAt: conservar «A tiempo», con referencia al incidente si corresponde.
- Pendiente al aprobar: usar el nuevo DueAt persistido en Mi trabajo, tablero y reportes; mostrar que tiene ajuste autorizado.
- Rechazada: no reconocer arrastre ni cambiar métricas de puntualidad por esa petición.
- OMISION_RESPONSABLE conserva su causa/responsabilidad; autorización no equivale a causa ajena al colaborador.

Mantener la métrica factual de entregas fuera de plazo y desglosar cuántas tienen arrastre autorizado y cuántas quedaron cubiertas por el plazo reconocido, evitando doble conteo de tareas con varios incidentes. Extender filtros, detalle, exportaciones y plantillas relacionadas. No cambiar denominadores de cumplimiento ni satisfacción sin una regla explícita.

Revisar inclusión en el tablero al cruzar medianoche: hoy agrupa por DueAt en fecha local. El cambio puede trasladar una tarjeta de día y debe mostrarse en la vista previa. Conservar historia y progreso global de acuerdo con las reglas actuales.

## 10. Fases y verificación

| Fase | Entrega | Condición para avanzar |
| --- | --- | --- |
| 1 | Contratos, catálogo, estados, calculador y reglas de elegibilidad. | Casos de ejemplos, paralelismo, huecos y límites deterministas. |
| 2 | Entidades/migración, solicitud, decisión, idempotencia y auditoría. | Integración en base aislada, permisos y concurrencia; sin escrituras parciales. |
| 3 | Mi trabajo: diálogo, vista previa, envío e historial. | Enviar no cambia vencimientos; formulario móvil/escritorio y errores conservan datos. |
| 4 | Supervisión: bandeja, orden dinámico, detalle y decisión. | Orden real correcto con 0/1/varias pendientes, filtros, último caso resuelto y permisos. |
| 5 | Historia, puntualidad, tablero, reportes y exportación. | Entregas durante espera conservadas y clasificación factual coherente. |
| 6 | Integración de reglas especiales y prueba completa. | Resolver apartado 11; build, pruebas funcionales y visuales pertinentes. |
| 7 | Activación controlada. | Respaldo verificable, revisión de migración y comprobación del flujo autorizado en entorno objetivo. |

Ampliar las herramientas existentes `Sgol.TaskChecks`, `Sgol.ReportChecks`, `Sgol.BoardChecks` y las verificaciones móviles según el cambio, sin modificar pruebas para hacer pasar resultados incorrectos.

Pruebas obligatorias:

- Ambos ejemplos del usuario; hueco parcial/completo y contacto exacto entre tareas.
- Dos tareas paralelas, inicios iguales, varios planes y cadena de tres o más tareas; sin multiplicación de minutos.
- Atraso previo al incidente excluido; duración que no alcanza para volver vigente una tarea.
- Enviar sin mutación de DueAt; aprobar/rechazar; doble clic, reintento y dos supervisores simultáneos.
- Entrega y revisión concurrentes a la aprobación; reconocimiento posterior sin cambiar fechas ni decisión de calidad.
- Segundo incidente independiente, solicitudes solapadas y cambios de asignación durante espera.
- Descansos no absorbentes, salida, medianoche y jornada siguiente.
- Rechazo por cuenta/empleo/rol inválidos, sucursal ajena, alcance mixto y autoaprobación.
- Orden de secciones con filtros sin coincidencias, historial sin pendientes y fallos de carga; navegación con teclado y lector.
- Coherencia entre lista, detalle, temporizadores, tablero normal/kiosco, reportes y exportaciones.
- Migración sin cambios retroactivos y respaldo/restauración comprobados en copia antes de uso operativo.

Para este cambio documental no corresponde ejecutar compilación ni migraciones. Durante implementación, usar base aislada con datos representativos antes de tocar asignaciones en piso.

## 11. Casos especiales que deben cerrarse al implementar

- PROGRESO y tareas de ventana semanal: no usarlas como puentes de propagación. Requieren una regla específica de afectación directa; mostrar conflicto explícito si la solicitud depende de esa regla aún no implementada.
- Procesos: integrar con su vencimiento efectivo/activación y dependencias. No basta cambiar DueAt si la etapa consulta otro plazo. No desplazar ni conceder tiempo a otras personas automáticamente. No habilitar solicitudes incompatibles hasta cubrir el recorrido completo.
- Tareas críticas: el arrastre puede extender su cierre por aprobación expresa, sin mover StartsAt. Mostrar que una fecha límite real puede seguir incumplida; no presentar el cambio de fecha como recuperación de la operación.
- Fin de turno permitido por acuerdo. Cruce al siguiente día/turno e incidentes de varias jornadas necesitan una regla explícita: propuesta inicial, mostrar conflicto para revisión en lugar de propagar silenciosamente a la jornada siguiente. No inventar un máximo de minutos sin política.
- Registro tardío: conservar fechas de incidente y solicitud; limitar a asignaciones pendientes al enviar. Definir una ventana de antigüedad antes de admitir solicitudes de jornadas anteriores.
- Solapamientos y descansos: completar el cálculo en tiempo disponible y comprobar que un mismo minuto de incidente no se concede dos veces.

## 12. Criterio de terminación

El trabajo estará completo cuando el colaborador pueda enviar una petición, el supervisor autorizado la encuentre en el orden solicitado y resuelva su efecto con una vista previa vigente; los huecos absorban correctamente; las entregas e historia se conserven; reportes distingan el arrastre autorizado y su cobertura; y las pruebas anteriores acrediten que no se modifican asignaciones ajenas ni se amplía implícitamente el turno.
