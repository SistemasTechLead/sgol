# Plan: reasignación con programación y diagnóstico de horario

Fecha: 31/10/2026. Estado: implementado y desplegado en desarrollo compartido; validación automatizada aprobada con limitaciones explícitas en §9.
Clasificación: mixto (reglas, persistencia, consultas e interfaz).
Petición: ofrecer Cuanto antes, Primer espacio libre y Personalizada al reasignar; explicar conflictos junto a la tarea; integrar móvil/escritorio sin desperdiciar espacio.

## 1. Reglas confirmadas por el usuario

- Opción predeterminada: **Cuanto antes**, confirmada durante la implementación; un error conserva la modalidad elegida.

- Cuanto antes: puede superponerse con tareas y superar capacidad diaria, pero solo dentro del horario permitido.
- Siempre debe caber carga más margen completa en un tramo. Si faltan 5 minutos y necesita 15, buscar el siguiente tramo; no recortar el plazo.
- Primer espacio libre: buscar el primer hueco entre tareas, aunque se supere el límite diario de carga. No aplicar CanFit como condición de capacidad.
- Personalizada: buscar hueco en el día/rango solicitado; si no cabe, avisar sin guardar. No forzar ni salir del intervalo elegido.
- Las búsquedas automáticas llegan hasta 7 días desde hoy y muestran la fecha antes de guardar.
- Cambios posteriores de horario/ausencia: avisar a Supervisión y exigir reprogramación explícita; nunca mover silenciosamente.

- Personalizada también permite superar el límite diario de carga; exige turno y hueco.
- Personalizada se limita igualmente hasta 7 días desde hoy.
- Se permite programar la corrección después del fin del plan original, conservándolo como origen histórico sin crear otro plan. Verificar visibilidad por fecha efectiva en Mi trabajo, Supervisión y reportes.

Criterios propuestos de implementación (no respuestas adicionales del usuario):
1. Interpretar el horizonte como desde ahora hasta el fin del día local hoy+7, inclusive; mostrar fechas explícitas en pantalla.
2. Conservar la entrega anticipada actual. Programar el inicio no prohíbe enviar evidencia antes.
3. En reasignación por rol, ordenar por el inicio válido más temprano y usar los desempates actuales para igualdad; no elegir una fecha posterior solo para balancear carga.
4. Las tareas de procesos mantienen su flujo propio; esta ampliación no autoriza saltar etapas.

Las ocho preguntas de negocio enviadas quedaron respondidas. La petición de implementación del 31/10/2026 autorizó backend, interfaz, migración, actualización del entorno compartido, commit y push. Se incorporó después la preferencia de Cuanto antes como valor predeterminado.
## 2. Comportamiento observado y trazabilidad

Repositorio master, limpio al iniciar la investigación. No se modificaron simulaciones ni servicios.

- OperationalSupervisionService.cs: ReviewAsync/ReviewCoreAsync autorizan, verifican Revision, crean una corrección, conservan entrega original, asignan ReassignedFromId y ReassignmentComment y cierran origen como REASIGNADA. La evidencia requerida se toma del catálogo actual; la carga utiliza AssignmentDefinition.ConfiguredLoad. Preservar esa política, incluyendo sus snapshots históricos.
- OperationalSupervisionService.Reassignment.cs: CorrectionPlacement usa ahora, ignora ocupación/capacidad y recorta al fin del turno. Debe sustituirse su política para este flujo, sin cambiar indiscriminadamente los planificadores.
- OperationalSupervisionService.Batch.cs: SupervisionReview y ReviewBatchAsync; guardado atómico, rollback total y primera tarea fallida identificada. Mantener atomicidad.
- OperationalCapacityService.cs: LedgerAsync incluye asignaciones y reservas de procesos; CapacityLedger separa CheckTime, CanFit y CanAssign. CanAssign mezcla capacidad con agenda; no sirve sin adaptación para la regla confirmada.
- OperationalScheduling.cs: OperationalAgenda.Duration calcula carga+margen; FindForward busca huecos dentro de un día, con anclas de criticidad, circuito de caja y tratamiento distinto para PROGRESO. No usarlo como buscador genérico de correcciones sin definir esas excepciones.
- PersonalSchedules.cs: TemporalEligibilityContext.Day/Evaluate consideran turnos, excepciones, ausencia y cobertura; Evaluate comprueba recepción, no garantiza por sí solo que toda la duración quepa. La nueva política debe comprobar ambos extremos dentro del mismo intervalo.
- PersonalScheduleService.cs: PreviewAsync/PreviewExceptionAsync ya producen ScheduleImpact y fingerprint; integrar diagnóstico posterior en vez de crear un calendario paralelo.
- OperationalTaskService.cs y OperationalTaskService.History.cs: consulta, entrega anticipada e historial de reasignación. RequireIndependentActionAsync impide actuar por fuera del proceso.
- OperationalWeeklyRepairService.cs y OperationalCapacityService.cs: las correcciones están protegidas de ciertos reacomodos. Mantener esa protección y añadir una ruta explícita para reprogramación autorizada.
- Validations/Index.cshtml.cs y Index.Batch.cs: handlers individual y lote; actualmente el motivo aparece en el resumen y la tarjeta solo dice que falló.
- Validations/Index.cshtml y _ValidationCards.cshtml: dos composiciones del formulario que deben consumir un parcial común.
- wwwroot/js/supervision.js y validation-cards.js: bindings distintos. El lote deshabilita textarea/select de secciones ocultas, pero los nuevos inputs de fecha exigen ampliar manejo de controles y restauración pageshow.
- wwwroot/css/validation-cards.css: grid adaptable con mínimo 320px, hechos en dos columnas y formulario en columna.
- wwwroot/css/mobile.css, mobile-hierarchy.css y docs/design/ESTILO_VISUAL_V2.md: mantener un solo DOM, tokens, áreas 44px, descripción contextual y restitución escritorio/impresión.
- MyWork/_OperationalTaskTable.cshtml, _OperationalTaskDialog.cshtml e Index.Operational.cs; wwwroot/js/my-work-timing.js: mostrar origen y comentario, conservar estados por fecha y no confundir tarea diaria con corrección.
- OperationalSupervisionService.Paging.cs, OperationalWorkPlan.cs, OperationalTaskService.MineProgressAsync, Reporting/ReportData.cs y consultas de tablero: revisar filtros, progreso e historial al cruzar día/semana.
- tools/Sgol.TaskChecks/ReassignmentLifecycleChecks.cs y OperationalSchedulingChecks.cs: bases de regresión existentes; no se ejecutaron en esta planificación.

## 3. Diseño funcional

Separar «A quién» (misma persona/rol) de «Cuándo» (las tres modalidades). Ofrecerlas al seleccionar reasignar, no solo después de un error.

Crear un contrato de solicitud de programación y un resultado de diagnóstico comunes para revisión individual y lote. Incluir modo, desde/hasta, tarea, revisión, persona resuelta, inicio, vencimiento, duración, motivo de bloqueo y política aplicada. Fechas en America/Mexico_City; el reloj del navegador no decide elegibilidad.

Crear un planificador específico de correcciones que reutilice ledger, calendarios y cálculo de duración:
1. Obtener candidatos con permisos, cuenta, empleo, rol y especialidad válidos.
2. Capturar una sola instantánea de agenda/turnos por vista previa; consultar fechas dentro de límites explícitos.
3. Cuanto antes: encontrar primer tramo válido con duración completa; permitir superposición y exceso diario, sin desplazar tareas.
4. Primer espacio libre: restar bloques existentes y reservas al turno; buscar el primer hueco contiguo suficiente, ignorando solo el tope de carga.
5. Personalizada: mismo cálculo de huecos, limitado a las fechas elegidas.
6. Respetar ausencias, descanso, fin de empleo/rol y reglas especiales de cierre/proceso. No extender turnos por el hecho de forzar.
7. Sin resultado: explicar si falta horario, turno, destinatario, hueco, duración suficiente o si la búsqueda quedó inconclusa. «No hay hueco» no equivale a «sin capacidad».

No introducir un trabajo en segundo plano para activar: crear la asignación con StartsAt futuro, usando el comportamiento temporal existente. Reservar el hueco desde el guardado.

Vista previa y aplicación:
- Calcular el conjunto seleccionado, no una llamada por cada tarjeta visible.
- Resultado por tarea y resumen; ninguna escritura durante la vista previa.
- Fingerprint de solicitud, revisión del origen, contratos, candidatos, agenda, horarios/excepciones/ausencias y políticas relevantes.
- Guardar bajo bloqueo/transacción existentes; volver a comprobar permisos y propuesta.
- Si cambia destinatario/fecha/plazo o el instante propuesto queda en el pasado, devolver propuesta actualizada para revisar; no aplicar una fecha distinta silenciosamente.
- Para «ahora», definir vigencia corta y recalcular el inicio efectivo al confirmar con su vencimiento; si se pierde el tramo completo, exigir nueva confirmación.
- En lote, incorporar cada propuesta al ledger para no ocupar dos veces un hueco. Orden determinista visible. Cuanto antes puede solaparse, pero advertir la concentración de correcciones.
- Conservar rollback de todo el grupo. Mostrar todas las causas previsibles encontradas en vista previa; una carrera durante guardado identifica la tarea afectada.
- Doble clic/reintento: conservar vínculo único al origen y revisión optimista; añadir clave de intención si se ofrece repetir una respuesta incierta. No repetir automáticamente un POST.

## 4. Persistencia, historial y cambios posteriores

Añadir metadatos de programación de la corrección (modo, rango solicitado, política/version, momento y actor de programación) a una estructura propia vinculada a assignment; decidir entidad o columnas al cerrar contrato. Guardar fechas efectivas en StartsAt/DueAt y mantener ReassignedFromId. No almacenar el motivo únicamente en logs.

Compatibilidad: metadatos nuevos nulos en históricos; no reinterpretar ni reprogramar correcciones existentes. Migración aditiva sin alterar evidencias ni plazos históricos. Mantener selección original y decisión efectiva auditadas.

Si cambia el horario o aparece ausencia:
- Ampliar los puntos de cambio existentes para detectar correcciones pendientes afectadas; comprobar intervalo completo.
- Mostrar conflicto persistente o derivado con estado/version verificable en Supervisión y detalle de Mi trabajo, sin notificaciones externas.
- Reprogramar la misma corrección mediante preview/apply y motivo, preservando su ID, origen y comentario; auditar fechas antes/después.
- Corrección entregada: no mover; revisión normal. Con evidencia/borrador/avance: conservarlos y mostrar impacto, sin reiniciar carga/progreso por accidente.
- Evitar que una fecha invalidada siga apareciendo simplemente «Activa» sin aviso. No introducir un nuevo bloqueo de entrega sin decidir expresamente su consecuencia.

## 5. Interfaz compacta y arquitectura

Parcial Razor compartido propuesto: Validations/_ReassignmentSchedule.cshtml, con modelo tipado y prefijo de binding para formulario individual o Reviews[id]. Usar los handlers existentes y nuevos handlers de preview del mismo módulo; lógica de negocio en servicio, no JS.

Composición:
- Comentario obligatorio.
- «Destinatario» y «Cuándo»; dos columnas solo si el contenedor tiene espacio real, una en tarjetas estrechas y móvil.
- Un select para las modalidades, sin tres paneles abiertos.
- Personalizada revela «Mañana / Un día / Rango», fechas nativas y texto de rango efectivo. «Mañana» es el día siguiente, no el próximo laborable; si no cabe, no trasladar sin permiso.
- Resumen compacto: persona · día · inicio–vencimiento · carga+margen. En Cuanto antes indicar que puede coincidir con otras tareas; en Primer espacio libre, que no limita por capacidad diaria.
- Aviso junto a la tarea: «Alejandra está fuera de turno. Próximo tramo: … Necesita 15 min. Elige cuándo programar la corrección». Cuando no hay horario, no inventar próxima entrada.
- Enviar foco al resumen del error y enlace a tarjeta; mensaje concreto también dentro de ella. Conservar comentario, fechas, decisiones, filtros y selección después de fallar.
- Acciones Vista previa / Guardar según etapa, con estado de carga y errores anunciados; no añadir un botón primario por tarjeta que compita con Guardar grupo.
- Mi trabajo: etiqueta «Corrección de la entrega del …», inicio/vencimiento reales y comentario visible al abrir. Mantener el resto del historial desplegable.

Reutilizar campo, campo__control, alerta, boton, tokens, parciales y diálogo existentes. No añadir biblioteca, calendario pesado, formularios anidados ni modales dentro del diálogo de revisión. El componente funciona por ancho disponible; escritorio no garantiza tarjetas anchas.
JS compartido para activar campos y preview; ignorar campos incompatibles también en servidor. Evitar que pageshow reactive fechas ocultas. IDs únicos en lote. Conservar lectura sin depender exclusivamente de JS para el diagnóstico.

## 6. Conflictos y mitigaciones

| Riesgo | Tratamiento |
|---|---|
| Quedan menos minutos que duración completa | Siguiente tramo; jamás recortar ni partir trabajo entre descanso y regreso. |
| Hueco libre, capacidad diaria agotada | Permitir según decisión confirmada; auditar excepción, sin modificar capacidad de la persona. |
| Tareas vencidas de días anteriores | Conservar como pendientes; no ocupar automáticamente todos los huecos futuros ni replanificarlas implícitamente. |
| Entrega anticipada y bloque todavía reservado | Conservar tratamiento del ledger vigente; no liberar el hueco antes de tiempo sin regla explícita. |
| Reservas de procesos y tareas de progreso | Incluir reservas reales; distinguir carga distribuida de bloques de ejecución. No asumir que PROGRESO carece de trabajo. |
| Tarea crítica/circuito de caja | El modo de corrección no puede heredar ancla vencida y volverse imposible, ni ignorar una dependencia obligatoria. Diseñar excepción localizada y prueba específica; no cambiar criticidad del catálogo. |
| Cambio de catálogo mientras se revisa | Invalidar propuesta; preservar entrega original y snapshot de corrección conforme a política vigente. |
| Dos supervisores o varias tarjetas | Bloqueo, revisión, ledger acumulativo, fingerprint e índice único; ninguna asignación parcial. |
| Más tarde/otro día cambia rol, empleo o especialidad | Validar elegibilidad a fecha elegida; ledger de trabajadores actuales no demuestra por sí solo vigencia futura. |
| Nueva ausencia o cambio de turno | Conflicto y reprogramación explícita; no movimiento silencioso. |
| Día/rango no laborable o sin hueco | Sin guardado; mostrar motivo y fechas alternativas fuera del rango solo como propuestas. |
| Siguiente semana y plan ya terminado | Permitido por el usuario; revisar visibilidad, filtros y reportes por fecha efectiva, sin duplicar plan. |
| Original y corrección con nombre idéntico | Origen visible y comentario; no fusionar con la ocurrencia diaria. |
| Edición del rango durante preview | Invalidar respuesta anterior, cancelar/ignorar respuesta tardía y recalcular. |
| Búsqueda costosa | Horizonte limitado, calendario y ledger una vez, búsqueda por límites de intervalos; cancelación y diagnóstico de búsqueda inconclusa. |
| Cambios de reloj/medianoche | Reloj servidor, fechas locales y límites explícitos; revalidar al guardar. |

## 7. Secuencia de implementación propuesta

1. Formalizar contratos con las reglas confirmadas y criterios propuestos; fijar ejemplos concretos de fecha/plazo.
2. Planificador y diagnóstico estructurado, pruebas deterministas del reloj y agenda.
3. Preview/apply individual y lote, concurrencia, metadatos y migración.
4. Detección de cambios posteriores y reprogramación explícita con auditoría.
5. Parcial compartido, bindings, avisos locales, resumen y preservación del borrador.
6. Mi trabajo, Supervisión, tablero/reportes e historial: origen, comentario y fechas futuras.
7. Integración real aislada, navegador y revisión de diff. Activación compartida solo cuando corresponda a una petición de implementación/despliegue.

## 8. Matriz de validación

Estado actualizado tras la implementación. Las pruebas automatizadas usan la copia aislada `sgol_task_checks_correction_20261031`. Aprobado indica los subcasos descritos por la evidencia de §9; las omisiones se indican expresamente.

| Escenario/precondición y acción | Resultado esperado | Prueba / entorno | Estado |
|---|---|---|---|
| Entre turnos; Cuanto antes | Inicio al siguiente tramo con duración completa | Servicio + BD aislada con reloj fijo | Aprobado |
| En turno ocupado y capacidad llena; Cuanto antes | Permite superposición, sin mover vecinos | Integración, comparación antes/después | Aprobado |
| Cinco minutos antes de salir; duración 15 | Pasa al siguiente tramo, conserva 15 | Regla + integración | Aprobado |
| Hueco de 15, capacidad diaria llena | Primer espacio libre permite | Servicio + formulario HTTP aislado | Aprobado |
| Huecos cortos o tarea mayor a todos los tramos | Error específico sin escritura | Regla + BD aislada | Aprobado |
| Mañana/día/rango; descanso/ausencia intermedia | Solo dentro de rango, sin forzar | Integración + formulario real | Aprobado |
| Límite hoy+7 y cruce domingo/mes | Límites exactos; no fechas fuera | Reloj fijo + consultas de semana | Aprobado |
| Misma persona/rol/especialidad, baja futura | Destinatario autorizado en fecha efectiva | Integración de roles y vigencias | Aprobado |
| Reserva proceso, legado, PROGRESO, crítica/circuito | Respeta restricciones específicas y diagnostica | Extender TaskChecks aislados | Aprobado |
| Lote mixto aprobar/cerrar/reasignar y dos huecos competidos | Atomicidad y propuestas sin doble ocupación | Servicio + HTTP real | Aprobado |
| Dos supervisores, doble envío, propuesta vieja | Conflicto/repetición segura sin duplicado | Concurrencia BD aislada | Aprobado |
| Cambiar horario/ausencia después de guardar | Aviso y reprogramación explícita, misma identidad | Flujo completo Personas → Supervisión → Mi trabajo | Aprobado |
| Evidencia anticipada/borrador/avance al reprogramar | Conservación; entregada no se mueve | Integración e historial | Aprobado |
| Corrección después del fin del plan original | Visible por su fecha efectiva en Mi trabajo, Supervisión y reportes; conserva PlanId y origen, sin crear plan | Integración de consultas + navegador real | Aprobado |
| Original, diaria y corrección de otro día | Identidad clara, comentario visible, métricas coherentes | Consultas + navegador real | Aprobado |
| Históricos sin nuevos metadatos | Sin cambios en plazos/evidencia | Migración en copia aislada y comparación | Aprobado |
| Individual/lote, error de servidor, atrás/recarga | Borrador conservado, controles coherentes | Navegador Chromium/WebKit | No ejecutado |
| 320/390/768/1280px, claro/oscuro, zoom 200%, texto largo | Sin overflow global ni truncar causa, campos accesibles | Navegador real y capturas | Aprobado |
| Tab/Shift+Tab/Escape, foco, teclado móvil, lector | Acceso a campos/errores y retorno correctos | Navegador y dispositivo disponible | No ejecutado |
| Preview lento/error/vacío/éxito y JS sin cargar | Sin guardar propuesta obsoleta; fallback legible | HTTP + navegador aislado | No ejecutado |

La investigación original solo inspeccionó código y capturas. La evidencia de ejecución que sustituye ese estado está en la sección siguiente.


## 9. Implementación y evidencia — 31/10/2026

### Resultado y decisiones técnicas

- `CorrectionScheduling.Find` busca intervalos completos hasta el fin del día local hoy+7. Cuanto antes es el valor predeterminado de los contratos y de los dos formularios. Primer espacio libre y Personalizada omiten únicamente el tope diario; mantienen turnos, ausencias, vigencias, especialización, cierre y circuito de caja.
- La criticidad original se conserva; la corrección no hereda una hora crítica ya vencida. El trabajo distribuido PROGRESO conserva su carga, pero su plazo semanal no se interpreta como una reserva continua: se respeta la distinción del ledger existente. Una corrección de progreso sí reserva su intervalo explícito. Las reservas reales de procesos se mantienen en el ledger; una tarea de proceso no admite revisión por la vía independiente.
- `PreviewReviewsAsync` simula sin `SaveChanges`, con bloqueo y transacción, un calendario/ledger compartido por conjunto y orden de formulario. Cada propuesta anterior reserva su hueco para la siguiente. Se muestran destinatario, inicio, vencimiento y duración con margen antes de confirmar.
- `ApplyReviewsAsync` revalida el fingerprint de origen/revisión, solicitud, catálogo, candidatos, agenda, horarios, ausencias y política. Vigencia: dos minutos. Solo un inicio inmediato puede avanzar al instante efectivo de confirmación conservando toda la duración; si pierde ese tramo/hueco o cambia el destinatario se exige otra vista previa. Las fechas futuras deben coincidir exactamente. No hay reintento automático del POST. Un reenvío de una confirmación ya aplicada devuelve conflicto y no duplica la corrección; no se promete una respuesta idempotente de éxito.
- Lotes mixtos se guardan con un solo `SaveChanges` y commit. Se conserva el índice único existente sobre `ReassignedFromId` y el bloqueo operacional compartido.
- Migración `20261031222522_AddCorrectionScheduling`: una columna JSONB nullable `CorrectionScheduleJson` en `operational_assignment`. Guarda solicitud, versión de política, actor y momento. Históricos quedan nulos. Las copias que generan tareas diarias eliminan estos metadatos; no se convierten accidentalmente en correcciones.
- Reprogramación explícita de la misma asignación, con revisión optimista, motivo, preview/apply y auditoría de fechas anteriores/nuevas. No modifica entrega original, vínculo, comentario original de devolución, evidencia, borrador o progreso. Una corrección entregada no se reprograma. La entrega anticipada sigue permitida.
- Conflictos derivados del calendario vigente: aviso global en Supervisión, aviso junto a la tarea, enlace para reprogramar y aviso en Mi trabajo. No se mueve automáticamente ni se bloquea la entrega. La vista previa de cambios de horario comprueba ahora el intervalo completo de las correcciones. Capacidad y reparación semanal las conservan, incluso correcciones históricas sin política de agenda.
- `_ReassignmentSchedule.cshtml` es el parcial tipado compartido por revisión individual, grupo y reprogramación. Fechas solo para Personalizada; botón Mañana y Hasta opcional para un rango. Las fechas incompatibles no se envían desde el cliente y se ignoran en servidor. Confirmar queda invalidado si se edita el formulario. Errores locales y resumen enlazado a la tarjeta; foco después de abrir el diálogo.
- Mi trabajo distingue la corrección de la entrega diaria y muestra el comentario al abrir. Historial incorpora autor y fechas anteriores/nuevas de reprogramación. Reportes conserva el comentario de corrección y filtra por fechas efectivas. Se inspeccionaron `OperationalWorkPlan`, `MineQuery`, `LiveBoardReader` y reportes: el fin histórico del plan no recorta estas asignaciones. No se crea otro plan.

### Pruebas ejecutadas

Evidencia local privada bajo `.local/correction-20261031/`; no se publica su contenido, respaldos, sesiones ni capturas. Pruebas mantenibles en `tools/Sgol.TaskChecks/CorrectionSchedulingChecks.cs`, `CorrectionHttpChecks.cs`, `ReassignmentLifecycleChecks.cs` y `tools/Sgol.MobileChecks/correction-scheduling-checks.cjs`.

| Comprobación | Entorno / evidencia | Estado |
|---|---|---|
| Reglas y recorridos completos: duración, descansos, capacidad cero, superposición, huecos, rangos, hoy+7/hoy+8, empleo, especialización, PROGRESO, legado, criticidad y circuito | `--correction-scheduling`; `final-all.log` | Aprobado |
| Preview sin escrituras, lote mixto, fallo de lote sin cambios parciales, reenvío y dos supervisores simultáneos | BD aislada; misma suite | Aprobado |
| Original con comentario/autoría/fechas y bytes de adjunto conservados; borrador y avance no cero con autor conservados al reprogramar | BD aislada; misma suite | Aprobado |
| Cambio de horario, ausencia registrada, avisos, reprogramación de la misma identidad y conservación del historial | Servicio de horarios y formularios HTTP reales de Supervisión/Mi trabajo | Aprobado |
| Plan terminado, visibilidad de corrección y reporte por fecha efectiva, entrega anticipada y rechazo de reprogramar entregada | Formularios Razor, servicios, SQL y exportación real de Reportes | Aprobado |
| Permisos de supervisión y guardia de una asignación real de proceso en la copia | Misma suite | Aprobado |
| Formularios individual, índice de grupo, preview/confirmación, CSRF y conflictos de reenvío | Host HTTPS aislado; autenticación sustituida explícitamente por `TEST_ONLY`, no el resto del recorrido | Aprobado |
| Total de comprobaciones específicas de la suite final | 57 líneas `PASS correction:` en `final-all.log`, salida 0 | Aprobado |
| Regresión de reasignaciones previas, contrato de evidencia vigente, política de legado y protección ante redistribución | `--reassignment-lifecycle`; `lifecycle.log`; expectativas actualizadas para no recortar | Aprobado |
| Formularios reales a 320/390/768/1280 px, temas claro/oscuro/especial, texto largo, aumento de texto al 200 %, Tab/Escape, foco del preview, invalidación al editar, valor inicial Cuanto antes y conservación de Personalizada tras error | Chromium/Edge y WebKit; 26 casos, `browser-results.json`; capturas inspeccionadas | Aprobado |
| Diagnóstico legible sin JavaScript | Ambos motores; `noscript`, sin pretender que el diálogo funcione sin JS | Aprobado |
| Migración de una segunda copia intacta de la simulación: comparación de todos los campos previos de cada asignación | `sgol_task_checks_correction_history_20261031`; `migration-result.txt`; hash antes/después idéntico | Aprobado |
| Compilación Debug de TaskChecks, publicación Release Web, modelo EF sin cambios pendientes y `git diff --check` | CLI local; no sustituyen los recorridos anteriores | Aprobado |

Los tres renglones de §8 marcados **No ejecutado** son escenarios compuestos que no se completaron íntegramente: sí pasaron error y éxito de formulario, conservación de datos, Tab/Escape/foco y diagnóstico sin JS; no se ensayaron navegación atrás con caché de página, pérdida de red/respuesta lenta, teclado físico móvil ni lector de pantalla. No se presenta ese subconjunto como cobertura completa.

### Entorno compartido y publicación

- Destino comprobado: PostgreSQL `127.0.0.1:5440/sgol_local`; aplicación interna `http://127.0.0.1:7241`; proxy LAN `https://192.168.1.92:7440`; acceso local `https://localhost:7240` redirige al proxy LAN.
- Respaldo completo antes de migrar: `.local/correction-20261031/before-shared-migration.dump` (185,535,109 bytes). Se conservó también el respaldo previo usado para las copias. No se borraron ni reiniciaron los datos simulados.
- Se aplicó la migración con el rol migrador, sin exponer secretos, y se verificó su registro en `__EFMigrationsHistory`.
- Versión publicada `1.0.0+correction-scheduling-20261031`. Binario final activo: `.local/runtime/correction-scheduling-20261031-final/Sgol.Web.dll`. PID comprobado: **23928**, 31/10/2026 16:42 America/Mexico_City.
- SHA-256 del ensamblado activo: `ED7A03A736B3B19AB213067DC1D0CEC48115F58446DEC3BC05BCC1E9A3C71B5F`. `health/ready=ready`, LAN `health/live=alive`. Las rutas local y LAN de Supervisión responden y redirigen al acceso al no tener sesión vigente.
- La revisión automática rechazó inicialmente un comando que combinaba todas las operaciones, sin explicar más que «blocked by policy». Se completaron de forma separada el respaldo, la migración y el reinicio condicionado al PID/binario esperados; todas esas operaciones finalizaron correctamente.
- No se verificó la interfaz **autenticada** en el host compartido: las sesiones de prueba almacenadas habían expirado, y Computer Use detuvo el control de Brave al no poder determinar la URL con seguridad. No se modificaron credenciales ni sesiones para sortearlo. La interfaz autenticada sí se verificó en el host aislado con formularios reales.
- Remoto comprobado: `origin`, `https://github.com/SistemasTechLead/sgol.git`; rama `master`. El usuario autorizó expresamente publicar también los commits previos `1a3afe5` y `ca142f1`. El commit de este documento contiene únicamente el alcance nuevo; sin force push.

### Limitaciones pendientes

- Safari/iPhone físico, lector de pantalla y teclado móvil real no ejecutados; WebKit en Windows no equivale a Safari físico.
- Atrás/recarga con BFCache y fallos de transporte/lentitud forzados no ejecutados. Los reenvíos, conflictos, estados de error y conservación de datos sí se probaron.
- No se hizo una prueba de carga de 500 revisiones ni una matriz exhaustiva de cruces entre todas las reglas históricas. Las reservas de procesos se consumen desde el ledger existente; se probó la guardia de proceso, no un proceso completo iniciado y terminado desde esta funcionalidad independiente.
- Verificación visual autenticada del despliegue compartido pendiente por la limitación del navegador descrita; no confundir los HTTP 200 de la página de acceso con una sesión autenticada.
