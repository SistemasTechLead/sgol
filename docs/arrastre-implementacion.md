# Petición de arrastre: implementación y verificación

Fecha: 22 de octubre de 2026.

## Flujo disponible

- Mi trabajo incorpora «Petición de arrastre», con formulario en `/mi-trabajo/arrastre`, catálogo cerrado, duración, término en hora de Ciudad de México, explicación, vista previa y envío explícito.
- Enviar conserva los vencimientos. El historial distingue pendiente, aprobada y rechazada.
- Supervisión muestra un solo apartado. Con pendientes autorizadas: Capacidad → Arrastres → Pendientes de revisión. Sin pendientes: Capacidad → Pendientes de revisión → Arrastres. El orden cambia en el DOM y conserva los filtros al decidir.
- La aprobación integral ajusta únicamente DueAt y Revision de tareas pendientes. Las entregas y decisiones de calidad posteriores al envío conservan todos sus campos y reciben reconocimiento histórico.
- Mi trabajo, Supervisión y tablero muestran la referencia al arrastre. Reportes añade puntualidad, plazo reconocido, referencia previa al primer arrastre, identificadores, filtros y columnas exportables/guardables en plantillas. El indicador factual de entregas fuera de plazo sigue comparando entrega y vencimiento efectivo; el desglose no cuenta dos veces una misma asignación.

## Persistencia y concurrencia

Servicio dedicado y calculador determinista. Migración `20261022195107_AddOperationalCarryover` aditiva, sin actualizaciones de asignaciones existentes. La solicitud es un agregado con fotografía y resultados por tarea en JSONB, versión del algoritmo/catálogo, clave idempotente, revisión y decisión terminal. Un trigger protege fotografía e historial contra edición y borrado. El primer ajuste conserva la fecha de referencia; no se inventa un vencimiento original para asignaciones históricas sin arrastre.

Envío y decisión usan transacción y el bloqueo de planificación existente. La identidad se obtiene de la sesión. Se comprueban cuenta, empleo, sucursal y rol vigentes, jerarquía, todo el alcance y prohibición de autoaprobación. Las revisiones por entrega/progreso/calidad se distinguen de cambios estructurales. Se validan todas las precondiciones antes de modificar cualquier tarea. Los incidentes pendientes o aprobados solapados se bloquean.

Cuando cambia la estructura o el horario, la decisión informa conflicto: se rechaza y se envía una nueva petición con fotografía y huella nuevas; no se reescribe la solicitud original ni se aplica un recálculo invisible.

## Reglas especiales adoptadas

Solo incidentes de la jornada local actual, terminados, enteramente dentro de disponibilidad verificable. Un descanso no absorbe propagación y tampoco se concede como tiempo de incidente. No se impone un máximo arbitrario de minutos.

Se bloquean explícitamente solicitudes que dependan de PROGRESO, ventanas de varias jornadas, procesos con dependencias o cierres propuestos que crucen al día siguiente. Estos casos requieren sus reglas específicas antes de habilitarlos. El vencimiento puede superar la salida dentro del mismo día con advertencia y aprobación expresa; no cambia StartsAt ni el turno. Las tareas críticas muestran la advertencia de incumplimiento de la fecha real.

## Evidencia reproducible

Respaldo `.local/sgol-carryover-before-20261022.dump`, comprobado con `pg_restore --list` y restaurado en bases dedicadas `sgol_task_checks_carryover_*`. La comparación serializada antes/después de migración conserva las 243 asignaciones. Las huellas de tablas operativas antes/después de aplicar la migración en `sgol_local` coinciden (`.local/carryover-source-before.txt` y `.local/carryover-source-after.txt`). No se enviaron peticiones operativas de prueba en `sgol_local`.

- `Sgol.TaskChecks --carryover-rules`: ejemplos de 30/10 minutos, paralelismo, inicios iguales, cadenas, orden independiente, absorción parcial/completa, extremos, entregadas/vencidas, descansos y medianoche.
- `Sgol.TaskChecks --carryover-lifecycle` sobre copia restaurada: alcance de dos asignaciones actuales, envío/rechazo sin cambios, reintentos, autoaprobación y cuenta inactiva rechazadas, solapamientos, conflicto estructural atómico, entrega y revisión durante espera preservadas exactamente, reconocimiento, cambios únicamente dentro del alcance, historial inmutable, segundo incidente y dos decisiones concurrentes con una sola decisión/auditoría. Registro: `.local/carryover-acceptance.log`.
- `Sgol.ReportChecks`: 75 comprobaciones previas y 6 nuevas sobre puntualidad, cobertura, filtros y exportación. Registro: `.local/carryover-report-final.log`.
- `Sgol.BoardChecks`: comprobaciones de identidad, temporización, progreso y pertenencia diaria/semanal. Registro: `.local/carryover-board-checks.log`.
- Navegador, copia aislada en localhost:7250: solicitud pendiente creada por el servicio usando asignaciones existentes; aprobación mediante formulario Razor protegido, sin errores; orden real `capacity,carryovers,pending` antes y `capacity,pending,carryovers` después. Resultado persistido APROBADA con un ajuste de un minuto.
- Navegador, localhost operativo: botón visible, formulario y vista previa sin mutación; Supervisión con cero pendientes y orden correcto. Localhost recargado tras actualizar el runtime.
- Vista móvil a 390 px: formulario y controles dentro del ancho disponible, sin desbordamiento horizontal (clientWidth = scrollWidth = 375); tamaño restaurado después de verificar.

No se han regenerado planes ni reasignado personas, ni cambiado evidencia o historial operativo para probar la funcionalidad.

## Posponer tareas vencidas · 27 de octubre de 2026

La pantalla incorpora una lista de vencidas pendientes, selección múltiple y un panel flotante con preparación y envío explícitos. El arrastre por incidente sigue disponible en un apartado desplegable independiente; cada formulario valida solamente sus propios campos. La selección o el motivo modificados invalidan el botón de envío hasta preparar otra propuesta.

La ventana se congela al enviar: mañana hasta hoy más siete días, inclusive, en Ciudad de México. Una petición del martes 27 de octubre busca entre el miércoles 28 y el martes 3 de noviembre. No se extiende por descansos o por una autorización tardía. Cada tarea conserva su ID, persona, plan y clave de ocurrencia. Solo la aprobación mueve inicio, cierre y ancla nominal, captura la decisión temporal y aumenta la revisión.

El distribuidor procesa criticidad, carga descendente y vencimiento; elige la menor ocupación proyectada después de incorporar cada tarea. Comprueba capacidad configurada, disponibilidad, empleo/rol, especialización, huecos continuos, descansos, margen y compromisos existentes. Las críticas conservan su hora nominal y muestran advertencia. No redistribuye tareas ajenas para abrir espacio; si no encuentra solución para toda la selección, impide enviar el lote parcial.

Cuando falta un Plan de Operaciones Semanal asignado para el destino, estima demanda semanal con las asignaciones del mismo día de la semana anterior, incluidas las ya entregadas. Añade solamente la diferencia positiva respecto a la carga semanal ya programada; conserva además cargas mensuales, ocasionales y reservas de procesos. Las posposiciones aprobadas cuentan como demanda adicional, no sustituyen las ocurrencias ordinarias futuras; para reconstruir la referencia se conserva el día original de su primera posposición aprobada. Los días sin datos históricos verificables se excluyen de la búsqueda. La propuesta muestra la carga real, la estimación adicional, las tareas pospuestas, la capacidad y la fecha usada como referencia. La estimación no crea tareas ni garantiza que un plan posterior idéntico quepa; las tareas efectivamente pospuestas quedan en el libro de capacidad compartido para la planificación posterior.

La autorización corresponde al nivel inmediato activo de la jerarquía existente: Piso de ventas, Subcoordinación, Administración, Dirección. Se omiten niveles sin personal activo; Dirección conserva visibilidad y facultad de decisión sobre toda la sucursal, aunque exista un supervisor inmediato activo. Los demás niveles no pueden saltar uno vigente. Se impide autoaprobar y cada supervisor debe estar autorizado sobre todo el alcance. Los procesos, el trabajo por progreso, las asignaciones sin agenda verificable y el circuito diario de caja se muestran bloqueados con su motivo específico.

Las solicitudes usan `Kind = POSPONER`, versión 2, ventana explícita y fotografía inmutable con horarios propuestos y explicación del cálculo. La migración aditiva `20261027180000_AddCarryoverPostponement` conserva los registros anteriores como `INCIDENTE`. Envío, rechazo y reintento no cambian asignaciones. La aprobación usa la transacción y el bloqueo de planificación; los cambios de contexto, estructura o capacidad y los horarios propuestos que ya pasaron producen conflicto. Una tarea entregada durante la espera conserva todos sus campos, sin reconocimiento retroactivo de la posposición. Repetir una decisión no duplica cambios.

### Verificación de esta ampliación

- Respaldo nuevo restaurado en `sgol_task_checks_postponement_20261027_final`; las solicitudes de prueba se crearon únicamente en copias aisladas. El verificador rechaza conexiones fuera de `localhost` y bases sin prefijo `sgol_task_checks_`.
- `Sgol.TaskChecks --postponement-rules`: ventana martes a martes, previsión del lunes, reparto determinista, capacidad, turnos, falta de espacio y advertencia de críticas.
- `Sgol.TaskChecks --postponement-current`: dos asignaciones reales; envío y rechazo sin cambios; idempotencia; selección duplicada en otra petición y autoaprobación bloqueadas; bandeja del supervisor inmediato; cambio de capacidad rechazado atómicamente; entrega durante la espera preservada íntegramente; aprobación exacta de la pendiente y conservación de todas las asignaciones ajenas. El modelo EF coincide con su snapshot.
- Navegador sobre host exclusivamente de prueba en `127.0.0.1:7258`: selección múltiple, propuesta, invalidación al cambiar motivo, envío real con antiforgery y confirmación persistida. La vista previa original por incidente también funciona. A 390 px, el ancho útil y el ancho total coinciden en 375 px y el panel flotante queda dentro de la pantalla.
- Compilaciones sin errores ni advertencias, sintaxis JavaScript válida, reglas originales de arrastre, 103 comprobaciones de reportes y 202 de tablero correctas.
- Antes de activar, coincidieron las huellas de las 89 tablas operativas. Tras la migración local, solo difieren el registro de migración y la representación de la solicitud anterior por las tres columnas agregadas. Al excluir esas columnas nuevas, la solicitud anterior conserva exactamente su huella. No se modificaron asignaciones, entregas, revisiones, evidencia, planes, horarios ni capacidades reales; no se crearon posposiciones operativas de prueba.

Registros privados de prueba, respaldo y capturas: `.local/postponement-20261027/`. La instancia local se reinició con su configuración de red y `/health/ready` respondió `ready`.

### Recuperación del entorno al cierre

Docker Desktop y los procesos locales dejaron de estar disponibles al finalizar. Se iniciaron nuevamente Docker, PostgreSQL, almacenamiento, escáner, proxy y web mediante los scripts habituales, registrando los procesos en `.local/processes.json`. Después de la recuperación: `/health/ready` responde `ready`, la ruta del formulario responde HTTP 200 y las huellas operativas siguen idénticas, con cero solicitudes de prueba en la base operativa. La última recarga automática del navegador quedó bloqueada por la política de acceso a su pestaña de error; se requiere F5 en esa pestaña para salir de la página de conexión fallida. Las comprobaciones funcionales y visuales descritas arriba se habían completado antes de esta interrupción.

### Corrección de bandeja de Dirección · 27 de octubre de 2026

Se eliminó la exclusión accidental de Dirección en las solicitudes POSPONER cuando existe Administración activa. Se corrigieron conjuntamente la bandeja y la autorización de decisión; se conservan la sucursal, autorización de todo el alcance y prohibición de autoaprobación. La solicitud ya enviada no se modifica ni necesita reenviarse para aparecer.

La prueba --postponement-inbox-current sobre una copia nueva de la solicitud real de Subcoordinación verificó visibilidad de Dirección y Administración, exclusión de la propia solicitante y de Piso, y decisión de Dirección. La aprobación detectó una fotografía desactualizada y no alteró asignaciones; el rechazo se comprobó únicamente en la copia. La base operativa conserva la petición pendiente. Registros privados: .local/carryover-inbox-20261027/.

## Límite exclusivo por día y corrección de prenómina/nómina · 27 de octubre de 2026

El panel flotante añade «Asignar antes del» con día de la semana opcional. «Sin límite adicional» conserva mañana hasta hoy más siete días. Un día elegido limita la búsqueda a la próxima ocurrencia de ese día, exclusivamente; si es el mismo día de hoy, toma la próxima semana. Si el día elegido es mañana, informa que no hay días intermedios. Se validan valores 0–6, se invalida la propuesta al modificar el selector y el servidor comprueba el límite al enviar y aprobar. La fotografía conserva BeforeDate y las solicitudes limitadas usan AlgorithmVersion 3. La migración 20261027210000_AddCarryoverDeadlineWindow permite ventanas de uno a siete días sin modificar solicitudes anteriores.

El usuario confirmó expresamente que prenómina y nómina pueden programarse independientemente. La simulación con respaldo actual y el formulario real en copia aislada dieron: nómina TAR-0363, miércoles 28/10/2026 13:01–13:24; prenómina TAR-0389, jueves 29/10/2026 09:00–09:38. El sábado 31 queda excluido. El formulario persistió la condición; cambiarla invalida el envío anterior. A 390 px, el ancho útil y total son 375 px y el panel permanece dentro de pantalla.

La restauración autorizada fue una corrección compensatoria de las asignaciones 01a1797f-b6bb-7146-b1b3-12448b6f8875 y 01a1797f-b6bf-7e28-afa8-68ced8fc3b78, ambas de Viri. Se comprobó que seguían pendientes e idénticas a la aprobación del 3 de noviembre antes de recuperar sus fechas originales del 26 de octubre, ancla nominal y decisión temporal. La revisión aumenta; no se retrocede. Se conservó íntegramente la aprobación 01a17efe-16a1-710e-8755-281ad35133be. Dos eventos OPERATIONAL_POSTPONEMENT_RESTORED documentan antes/después. El lector de puntualidad excluye esa aprobación restaurada de los plazos vigentes y el historial señala la corrección, sin borrar ni editar la decisión anterior.

Después de validar restauración, reintento, envío y aprobación en la copia, se aplicó únicamente la restauración y un nuevo envío autorizado en la base operativa. La petición 01a17f09-33da-70d9-b89a-023fcca721f4 queda PENDIENTE para Viri, del 28 al 30 de octubre, con BeforeDate 31/10/2026. Sus dos tareas continúan vencidas hasta que Supervisión autorice; la aprobación real no fue ejecutada por la asistencia. El evento OPERATIONAL_POSTPONEMENT_SUBMITTED_ON_BEHALF identifica al operador y explica que se trató de un envío asistido autorizado, sin atribuir un clic personal a Viri.

El operador reproducible tools/Sgol.CarryoverCorrection está restringido a esos IDs, solicitud y fecha, rechaza otras conexiones y distingue --check-isolated de --apply-authorized-viri-before-saturday. La prueba aislada verificó permisos, fechas exclusivas, ventana vacía, valor inválido, idempotencia, huella incompatible al cambiar el día, recepción de Dirección, aplicación exacta y exclusión del ajuste restaurado en puntualidad. Las otras 537 asignaciones permanecieron idénticas; evidencia, entregas, revisiones, planes, horarios, capacidades y demás tablas protegidas conservaron sus huellas. En la base real solo cambiaron las dos asignaciones autorizadas, el registro de la migración, los cuatro eventos de auditoría y la nueva solicitud pendiente. Los tres registros anteriores de arrastre permanecieron inmutables.

Compilaciones sin errores ni advertencias; reglas originales de arrastre y posposición correctas; 103 verificaciones de reportes y 202 de tablero correctas. Evidencia privada: .local/carryover-before-saturday-20261027/ (source.dump, isolated.log, isolated-result.json, live.log, live-result.json, panel-desktop.png, panel-mobile.png y huellas antes/después).