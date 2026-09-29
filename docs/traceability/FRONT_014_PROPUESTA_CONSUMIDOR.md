# FRONT-014 — propuesta mínima de composición consumidora

Fecha: 2026-09-29, America/Mexico_City. **Secciones 2–7 APROBADAS expresamente por el responsable mediante «La apruebo».**
La aprobación autoriza esta composición, mensajes y navegación; no autoriza publicación. El responsable solicita ver las pantallas antes de autorizar el commit.

## 1. Hechos y alcance

La fila 87 de Adenda 45 limita FRONT-014 a UI-G02/G03/G04 y HU-004/HU-016/HU-019. FRONT-013/005/007 y TECH-FRONT-001 constan integradas; HU-004/016/019 tienen backend aceptado. No hay dependencia funcional anterior pendiente en la tabla de tareas insertadas. BR-D04 y BR-D13 aprobaron primitivas, no esta composición. Los cinco documentos obligatorios de diseño no contienen secciones específicas UI-G02/G03/G04.

Base: rama local codex/front-014 desde 4a9626b7a85e93ba0a0532c13a9883b1fea19f61, conservando los dos commits de continuidad. Repositorio y proyecto Codex SGOL mantienen su ubicación existente. No hay publicación autorizada.

Fuentes: CA/CP-004/016/019, RN-003/004/011/012/013/026/027; F06_CONTRATO_DE_API.md §§2/3/4/5.4/7; Adendas 06, 09 y 46; docs/design; contratos y endpoints actuales. Elegibilidad consulta exclusivamente el último snapshot confirmado. No hay cálculo, ranking, autoridad ni asignación automática en cliente.

## 2. Composición y navegación propuestas

Las tres unidades viven dentro de la ruta ya registrada `/planificacion`, sin nueva página de bandeja ni ruta API. Se añade una sección «Asignaciones» y otra «Carga activa». Para llegar a una obligación se usa un campo GET «Identificador de obligación» validado como UUID y `obligationId` allowlisted en la misma ruta. El resultado confirmado de UI-G01 puede enlazar «Consultar asignación» usando exclusivamente su obligationId real. No se implementa listado de obligaciones ni descubrimiento de FRONT-015 o de la bandeja.

La selección presenta únicamente identidad mínima de la obligación (ID, TAR, período, estado, responsable vigente), historia de asignaciones recibida y cursor de historia, explicación y corrección. El historial muestra sólo eventos de asignación que ya entrega el GET, con sus responsables, estados, referencias anterior/sucesora, actor e instante; no inventa eventos ni un endpoint de auditoría. Si hay otras clases de evento en una página, se permite paginar sin afirmar que la cadena está completa. Motivos confirmados son texto codificado en historia autorizada; nunca se reflejan en URL, capturas o logs.

La selección y representación requieren PER-TAREA-VER; elegibilidad, PER-ASIGNACION-EXPLICAR; carga, PER-CARGA-VER; corrección, PER-ASIGNACION-CORREGIR. El snapshot request-scoped sólo gobierna presentación. Cada API reautoriza permiso, recurso, jerarquía y estado. La UI no calcula superioridad ni agrega una affordance de corrección. Un 403 de sección retira sus datos y controles; un 404 concreto usa el mensaje convergente sin revelar existencia.

Carga usa únicamente filtros reales `personId` UUID opcional y cursor opaco (`loadPersonId` y `loadCursor` en Razor para separarlos de otras secciones). No incluye filtros de semana, estado, búsqueda por nombre o sucursal que GET /loads no acepta. La paginación usa las primitivas aprobadas, con historial de cursores de lectura según el cliente existente, sin interpretar ni generar cursores API.

## 3. UI-G02 — explicación recibida

Ficha de evaluación: ID, evaluatedAt, eligibilityDate y origen, policyVersionId, requiredRole, requiredShift y result. Los instantes se presentan en America/Mexico_City con zona visible; la fecha de elegibilidad es la recibida, nunca la del navegador.

Una tabla semántica conserva el orden recibido: código estable, condición «Elegible»/«Excluido» y todas las razones recibidas en su orden. El código estable identifica candidatos sin inventar nombres que el endpoint no devuelve. Las entradas de empleo, rol, disponibilidad y turno ya recibidas se pueden consultar como texto en un detalle nativo por fila. UUID de versiones son referencias, nunca campos editables. Carga, última asignación y rank del snapshot se muestran sólo si fueron recibidos; null se presenta como «No informado», nunca cero. WinnerPersonId sólo se identifica si viene confirmado, sin elegir un ganador en UI.

Traducciones propuestas para el catálogo cerrado de Adenda 06:

| Razón literal | Texto |
|---|---|
| PERSONA_INACTIVA | Persona inactiva |
| EMPLEO_NO_VIGENTE | Sin empleo vigente |
| SUCURSAL_NO_COINCIDE | La sucursal no coincide |
| ROL_ACTIVO_AUSENTE | Sin rol activo |
| ROL_REQUERIDO_NO_COINCIDE | El rol no coincide con el requerido |
| DISPONIBILIDAD_AUSENTE | Sin disponibilidad registrada |
| DISPONIBILIDAD_NO_POSITIVA | No disponible para la fecha evaluada |
| TURNO_NO_COINCIDE | El turno no coincide |

Un código de razón desconocido dice «Razón no reconocida» sin derivar elegibilidad ni reflejar datos técnicos arbitrarios. El resultado usa «Hay candidatos elegibles» o «No hay candidatos elegibles en esta evaluación». Son textos de presentación, no nuevos estados de dominio. Una lista vacía dice «Esta evaluación no contiene candidatos». La ayuda dice «Consultar muestra la evaluación confirmada; no vuelve a calcular elegibilidad». «Recargar asignación» vuelve a consultar obligación y snapshot; no dispara HU-016.

## 4. UI-G03 — carga activa

Tabla semántica: código, nombre recibido, «Obligaciones pendientes asignadas» e instante de cálculo. Se muestran los conteos del servidor y no se suman otras tablas ni se filtran por semana. El valor cero recibido se conserva. Ausencia de fila no equivale a carga cero.

Ayuda: «La carga cuenta obligaciones pendientes con asignación vigente». Vacío: «No hay personas visibles para esta consulta», con «Quitar filtro» cuando corresponda. Carga: «Consultando carga activa…». Acción explícita: «Recargar carga». No se promete que el instante de esta consulta coincida con el snapshot histórico de elegibilidad.

## 5. UI-G04 — corrección motivada

Select «Nuevo responsable» con candidatos marcados elegibles por el snapshot recibido, identificados por código. El responsable vigente no se ofrece como cambio; esto evita una elección sin efecto, sin sustituir la guarda ASIGNACION_SIN_CAMBIO del servidor. Sin candidatos, asignación vigente o ETag autorizado no se habilita el envío; la obligación concluida conserva sólo consulta. La UI no evalúa las condiciones vivas del candidato.

Confirmación nativa motivada BR-D04: título «Corregir asignación», resumen de obligación/TAR, responsable anterior y nuevo, y texto «La asignación anterior quedará en historia; la obligación seguirá pendiente». Motivo obligatorio con ayuda «Escribe un motivo de 10 a 500 caracteres después de normalizar espacios». La normalización y longitud canónicas permanecen en el servidor según Adenda 09; no se reemplazan por una regla distinta del navegador. Botones «Cancelar» y «Corregir asignación»; Cancelar recibe foco inicial, Escape cierra y devuelve foco al disparador.

La preparación protege intención ligada a actor, obligación, clave, cuerpo y ETag; se reutiliza Data Protection y el límite de ocho horas de sesión, sin almacenamiento de navegador ni motivo en URL. El JSON contiene exactamente newResponsiblePersonId, eligibilityEvaluationId y reason. API recibe CSRF, Idempotency-Key e If-Match. Tras enviar, la intención queda de sólo lectura. «Recuperar la misma corrección» sólo existe si conserva íntegramente intención original y requiere acción explícita; no modifica clave, cuerpo ni ETag, ni reintenta automáticamente. «Preparar otra corrección» es decisión explícita y no envía. Intención inválida o vencida no crea otra automáticamente.

201 CREADA: «Asignación corregida; la anterior permanece en historia». 200 RECUPERADA: «Se recuperó la misma corrección; no se creó otra asignación». Se muestran únicamente IDs y datos confirmados; el éxito puede consultar nuevamente representación e historia y carga autorizadas, pero no ejecuta otra mutación. Si la consulta posterior falla, el resultado confirmado permanece distinguido del fallo de lectura.

412: «Esta obligación cambió mientras preparabas la corrección». Bloquea el envío y ofrece «Recargar asignación»; esa acción consulta sin reenviar. Después de revisar el estado actualizado se requiere «Preparar otra corrección» para una clave nueva. IF_MATCH_INVALIDO/REQUERIDO exige igualmente recarga; no se adopta automáticamente una versión nueva en la intención enviada.

## 6. Mensajes funcionales propuestos

| Respuesta | Mensaje y efecto |
|---|---|
| 403 ACCESO_DENEGADO en elegibilidad | No tienes permiso para consultar esta elegibilidad. Retira datos de esa sección. |
| 403 ACCESO_DENEGADO en carga | No tienes permiso para consultar carga activa. Retira tabla y controles. |
| 403 ACCESO_DENEGADO en corrección | No tienes permiso para corregir esta asignación. No explica qué comprobación de jerarquía falló. |
| 404 de obligación o evaluación visible | No existe o no está disponible en tu alcance. Sin datos del recurso rechazado. |
| 400 FILTRO_CARGA_INVALIDO | Revisa el filtro de persona. Error asociado al campo. |
| 400 MOTIVO_INVALIDO | Escribe un motivo de 10 a 500 caracteres después de normalizar espacios. Error asociado a motivo. |
| 400 SOLICITUD_CORRECCION_INVALIDA | Revisa el responsable y el motivo. No anuncia corrección. |
| 400 IDEMPOTENCY_KEY_INVALIDA | No se pudo verificar la intención de corrección. No rota clave automáticamente. |
| 409 IDEMPOTENCY_CONFLICT | Esta intención ya se envió con otros datos. Conserva intención original; ninguna recuperación con un cuerpo alterado. |
| 409 ASIGNACION_VIGENTE_NO_ENCONTRADA | No hay una asignación vigente para corregir. Ofrece recarga de lectura. |
| 409 EVALUACION_ELEGIBILIDAD_DESACTUALIZADA | Hay una evaluación más reciente; recarga antes de preparar otra corrección. |
| 409/422 EVALUACION_ELEGIBILIDAD_INCOMPATIBLE | La evaluación no permite esta corrección. Ofrece recarga de lectura. |
| 422 RESPONSABLE_INELEGIBLE | El responsable seleccionado no es elegible para esta corrección. No recalcula el snapshot. |
| 422 ASIGNACION_SIN_CAMBIO | La persona seleccionada ya es responsable vigente. |
| 422 OBLIGACION_NO_CORREGIBLE | Esta obligación no admite corrección. Retira acción de escritura. |
| 409 ASSIGNMENT_CORRECTION_CONCURRENCY_CONFLICT / ASSIGNMENT_CORRECTION_CONFLICT | No se pudo confirmar la corrección. Consulta la asignación antes de decidir otro intento. |
| Intención vencida o inválida | La intención venció o no se pudo verificar; consulta la asignación antes de preparar otra corrección. |

401, CSRF inválido y errores desconocidos conservan el contrato seguro común. Los errores muestran correlationId cuando existe, nunca title/detail/JSON crudos. Un fallo de lectura no se convierte en una colección vacía ni un éxito de mutación.

## 7. Estados y diseño

Todas las secciones tienen normal, foco, deshabilitado, error, cargando y vacío. Normal muestra datos recibidos; foco sigue orden DOM con :focus-visible; envío bloquea controles y usa aria-busy y «Corrigiendo asignación…»; consultas usan esqueleto de tabla y textos de carga. Error usa resumen enfocable y asociación por campo. No se oculta una falta de permiso como control deshabilitado.

Vacío de selección: «Selecciona una obligación para consultar su asignación». Sin responsable: «Esta obligación no tiene asignación vigente». Historia sin asignaciones en la página: «No hay asignaciones en esta página de historia». Sin opciones: «No hay otro responsable elegible en esta evaluación». Estados VIGENTE/SUSTITUIDA y RECUPERADA conservan texto/icono/semántica aprobados; CREADA se comunica como resultado contractual confirmado, sin inventar un estado de solicitud ACEPTADA.

Se usan únicamente componentes y tokens existentes. No se agrega paleta, tipografía ni token. En móvil, campos y diálogo refluyen a una columna; sólo tablas desplazan dentro de su contenedor. Controles con área mínima, etiquetas, errores asociados, texto/icono y semántica WCAG 2.2 AA.

Tras aprobación se incorporarán estas decisiones a componentes.md, estados-y-mensajes.md, estados-de-dominio.md y navegacion.md; no se modificarán Fuentes ni documentos congelados.

## 8. Defecto contractual independiente y validación

Hecho: GET /api/v1/obligations/{id} no emitía ETag aunque F06 §7.2 y Adenda 09 §4 lo exigen. Corrección mínima autorizada por el alcance de FRONT-014: transportar row_version desde la misma lectura autorizada al contrato interno y emitir encabezado ETag. No añade endpoint, propiedad JSON pública, migración ni acceso separado sin autorización. Los rechazos no exponen versión.

Validación inmediata: build Release; ObligationQueryTests, AssignmentCorrectionTests, EligibilityEvaluationTests y ActiveLoadTests con filtro real y conteos. Prueba PostgreSQL del lector se amplía para cotejar la versión persistida, sin ejecutarla por tarea conforme a AGENTS vigente. API PostgreSQL, concurrencia real, rollback y navegador quedan diferidos para hito; no se hereda excepción TLS de FRONT-013. Tras aprobar diseño, se agregan pruebas de presentación/intención/autorización/conflicto/no reenvío y las de arquitectura directamente afectadas.

Punto de parada original: vistas, navegación y mensajes específicos esperaban aprobación de §§2–7. El responsable la otorgó mediante «La apruebo»; se incorporó por referencia en docs/design antes de escribir UI. La corrección del ETag consume un contrato previamente aprobado. La evidencia de implementación se registra en FRONT_014_ASIGNACIONES.md, no se deduce de esta aprobación. Commit local pendiente de la autorización solicitada por el responsable después de ver pantallas; publicación no autorizada.
