# FRONT-013 — propuesta mínima de contrato consumidor

Estado: APROBACIÓN PARCIAL EXPLÍCITA. El responsable aprobó lectura mínima (sección 2) y diseño de composición (sección 5), y autorizó preparar la adenda de las brechas de secciones 3 y 4 mediante «Lo autorizo». La Adenda 51 contiene las decisiones detalladas nuevas pendientes de aprobación íntegra; esta autorización no aprueba un payload ni una transacción aún no presentados.
Base verificada: `e87c64c3713fe53f572a4d4f4d8c4088022bca4a`, PR #78 integrado.

## 1. Hechos y brechas

- Adenda 45, fila 86, exige BR-API06 resuelta y formularios CAT aprobados. La búsqueda de Markdown versionado, excluyendo Fuentes, sólo encuentra BR-API06 en esa fila. La frase es una dependencia exigida, no evidencia de resolución.
- Los seis documentos de diseño tienen primitivas reutilizables y estados ACEPTADA/RECUPERADA/RECHAZADA, pero no el contrato específico UI-G01. `navegacion.md` sólo agrupa UI-G01..G06 bajo `/planificacion`.
- `GenerationRequestApiEndpoints` acepta exactamente `ruleVersionId`, `branchId`, `periodId`, `originType`, `originReference`. No acepta payload CAT. GET sólo consulta por UUID.
- `EfTaskDefinitionService` ofrece definiciones y políticas de elegibilidad/evidencia/validación, pero no el ID de activación. `EfActivationPolicyService.GetAsync` exige Dirección y devuelve historia administrativa. No hay lectura consumidora de reglas manuales visibles para Administración/Subcoordinación.
- Sucursal y período sí tienen lecturas: `GET /api/v1/branches/LOR-001` y `GET /api/v1/weeks/{isoYear}/{isoWeek}`. El segundo devuelve el período persistido; no se construyen UUID desde el cliente.
- El catálogo canónico fija seis TAR manuales con `MANUAL_REFERENCE_V1`; TAR-0005 y TAR-0026 son recurrentes. Sus formularios de alta manual no aplican.
- `ValidateFreshRequestAsync` valida cuenta/empleo/rol, sucursal, nivel de TAR, regla VIGENTE/MANUAL, TAR VIGENTE, período y referencia no vacía. No interpreta la referencia como separado, operación, expediente, evento o recepción; no valida padre TAR-0092 desde esa cadena.
- El POST manual no llama a `IWorkObligationMaterializer`. El único consumidor productivo encontrado es `EfRecurringOccurrenceProcessor`; las pruebas manuales llaman al materializador por separado. `AcceptedReplayAndConflictAreAuditedWithoutCreatingAnObligation` exige cero obligaciones después de CreateAsync. Una UI que anuncia obligación creada después del POST actual sería incorrecta.
- Los rechazos funcionales se devuelven como Problem Details; no se crea una solicitud persistida RECHAZADA en ese recorrido. RECUPERADA describe el POST de recuperación; GET devuelve el resultado persistido, sin convertirlo artificialmente en RECUPERADA.

## 2. Propuesta aprobable de lectura mínima

Agregar únicamente `GET /api/v1/generation-requests/options`, con envelope `data` de colección y `meta.count`/`meta.correlationId`. Cada elemento contiene `taskCode`, `name`, `taskDefinitionVersionId`, `ruleVersionId`, `branchId`, `originType`: datos existentes, no opciones inventadas.

La consulta revalida el mismo actor creador y alcance LOR-001; incluye sólo TAR activa, regla vigente MANUAL, política de elegibilidad vigente compatible y nivel accesible. No entrega borradores, historia administrativa ni reglas recurrentes. Devuelve 401 sin sesión, 403 al actor no creador y colección vacía para un creador sin opciones. No amplía los permisos del GET administrativo ni crea permisos nuevos. El módulo propietario expone la lectura por contrato interno; Razor no accede a tablas.

Se reutilizan las lecturas actuales de sucursal y semana. Los IDs se toman de respuestas autorizadas; nombre, tipo de origen y versión son de sólo lectura. Recargar obtiene opciones nuevas mediante GET. POST revalida todo aunque la opción acabe de consultarse. No se añade ETag/If-Match a generation-requests.

Esta lectura resuelve la obtención de opciones, pero por sí sola no resuelve los apartados 3 y 4.

## 3. Decisión funcional imprescindible sobre formularios CAT

F05 especifica entradas funcionales, pero no su representación en el POST de cinco campos ni las lecturas de referencia necesarias:

| CAT / TAR | Entrada aprobada en F05 | Lo que falta para el alta HTTP |
|---|---|---|
| CAT-001 / TAR-0005 | meta, venta, fuente; dos ventanas recurrentes | No aplica alta manual; conservar Worker |
| CAT-002 / TAR-0007 | separado, mercancía, inicio, vencimiento, referencia | Esquema de origen y clave separado+vencimiento; representación y validación de referencias |
| CAT-003 / TAR-0008 | operación, detección, al menos dos reclamantes, evidencia | Esquema de alta y unicidad de controversia activa; distinguir entrada de alta de evidencia posterior |
| CAT-004 / TAR-0011 | expediente, autorización, producto, solución, fecha | Representación y comprobación de autorización previa y unicidad por autorización |
| CAT-005 / TAR-0018 | evento, zona, planograma/lista vigente | Referencias vigentes visibles y clave evento+planograma |
| CAT-006 / TAR-0026 | servicio, vencimiento, recibo; recurrencia | No aplica alta manual; conservar Worker |
| CAT-007 / TAR-0092 | recepción, proveedor, nota, inicio, mercancía | Identidad ID_Recepcion y unicidad activa; esquema de origen |
| CAT-008 / TAR-0093 | recepción, tipo, descripción, momento | Referencia tipada al padre TAR-0092, existencia y alcance; valores cerrados de tipo |

No se propone serializar JSON en `originReference`, concatenar campos con separadores inventados, pedir UUID técnicos al usuario ni validar relaciones sólo en JavaScript. Tampoco convertir requisitos de conclusión o evidencia en campos de alta sin decisión aprobada.

Para conservar íntegro el alcance pedido, se necesita aportar el contrato aprobado de formularios o aprobar primero una adenda que cierre por TAR: nombres/tipos/límites, campos de alta frente a evidencia posterior, claves funcionales, referencias visibles, errores y persistencia versionada. Se recomienda este cierre contractual antes de la UI. No se propone aceptar seis campos libres como prueba de CAT-001..008 ni reducir silenciosamente el criterio.

## 4. Propuesta de conexión manual HU-014/HU-015

El recorrido HTTP debe componer solicitud y materialización en el servidor, sin llamar al materializador desde Razor ni crear un endpoint de asignación. La adenda debe fijar una frontera transaccional para que un fallo no deje obligación parcial, preservar identidad/versiones/auditoría y permitir que el replay recupere el mismo resultado. El contrato también debe definir la respuesta de solicitudes aceptadas históricas sin obligación y de errores: no se inventará una fila RECHAZADA donde hoy sólo existe Problem Details.

Propuesta funcional para esa adenda: POST nuevo confirmado devuelve solicitud y obligación vinculadas; reenvío idéntico conserva ambos IDs; contenido distinto con la misma clave conserva 409; GET refleja el vínculo persistido. Conservar cuerpos históricos idempotentes y no reejecutar una operación ya confirmada exige diseñar explícitamente compatibilidad antes de modificar código. No basta añadir una llamada después del commit actual.

## 5. Propuesta de extensión de docs/design

Destino: `componentes.md`, `estados-y-mensajes.md`, `navegacion.md` y aclaración de presentación de solicitudes en `estados-de-dominio.md`. Conservar tokens y reglas de accesibilidad existentes.

- UI-G01 será una sección «Alta manual» de `/planificacion`, conservando semana y calendario. El enlace usa un ancla dentro de esa ruta. Sólo se presenta con PER-OBLIGACION-CREAR; cada API reautoriza.
- Selección GET de TAR canónica allowlisted; consulta de resultado por `generationRequestId` UUID en la misma ruta. Ninguna referencia, intención, CSRF ni dato de formulario entra en URL. 404 de recurso inexistente/fuera de alcance es convergente. No hay enlaces API de menú.
- Catálogo autorizado, sucursal de sólo lectura, año/semana ISO y rango recibido, regla vigente de sólo lectura, formulario CAT cerrado cuando su contrato exista. Las dos TAR recurrentes no ofrecen alta manual. No hay editor JSON.
- Confirmación corta «Crear solicitud manual», resumen TAR/período/origen, «Cancelar» y «Crear solicitud». Sin motivo nuevo porque el POST actual no lo acepta. Cancelar recibe foco; Escape restaura el disparador. Tras cambio contractual, el texto sólo promete la obligación si el servidor confirma su vínculo.
- Una intención conserva clave y cuerpo exactos para recuperar un envío de respuesta incierta. La acción «Recuperar resultado» reenvía explícitamente el mismo contenido; nunca es un alta nueva. Ediciones posteriores exigen decisión explícita para otra intención.
- Conflicto 409: «Esta solicitud ya se envió con otros datos». Ofrecer recuperación de la intención original sólo si se conserva su cuerpo; ofrecer «Preparar otra solicitud» sin envío automático. No rotar clave ni reenviar silenciosamente.
- Normal: valores recibidos y versiones legibles. Carga: región aria-busy y «Enviando solicitud…», botones inactivos. Vacío de catálogo: «No hay tareas disponibles para alta manual en tu alcance», acción «Recargar opciones». No ofrecer formulario con IDs inventados.
- ACEPTADA: «Solicitud aceptada». RECUPERADA: «Se recuperó la misma solicitud; no se creó otra». Rechazo HTTP: «No se pudo crear la solicitud», código funcional traducido y correlationId; no fingir ID ni estado persistido. GET conserva el resultado real y muestra el vínculo de obligación sólo si existe.
- Errores: REGLA_MANUAL_REQUERIDA → «La regla vigente no permite alta manual»; TAR_INACTIVA → «Esta tarea no admite nuevas generaciones»; PERIODO_INVALIDO → «Selecciona un período válido»; ORIGEN_INVALIDO → «Revisa la referencia de origen»; ACCESO_DENEGADO → «No tienes permiso para crear esta solicitud»; GENERATION_REQUEST_NO_ENCONTRADA → «No existe o no está disponible en tu alcance». Desconocidos usan mensaje seguro común. No se reflejan title/detail técnicos.
- Errores asociados al campo y resumen enfocable; 403 retira datos de la sección afectada. Al consultar otro resultado, esperar el GET nuevo. El estado anterior no acredita el POST siguiente.
- Móvil: una columna, select con tratamiento de overflow aprobado, sin desbordamiento de html/body; sólo tablas pueden desplazar en su contenedor. Teclado, foco visible y controles 44×44 según tokens. No se añade CSS antes de aprobar el consumidor.

## 6. Condición de reanudación y validación

Aprobar lectura/diseño permite implementar esos apartados cuando la semántica de formularios esté cerrada; no supone por sí mismo aprobación de un payload CAT o de cambios transaccionales. La decisión pendiente debe resolver también los apartados 3 y 4 para completar FRONT-013.

Después: API/PostgreSQL por seis altas manuales y dos rechazos recurrentes, autorización/alcance/nivel, origen incompleto y padre fuera de alcance, unicidad/auditoría/versiones/no efecto, replay/409, y Playwright con Kestrel HTTPS real. Ejecutar Category=FRONT_BROWSER completa serializada y capturas sintéticas sanitizadas de escritorio/móvil antes de solicitar commit. No iniciar FRONT-014, TECH-FRONT-005 ni tareas posteriores.

## Seguimiento de aprobación

Lectura básica aprobada y composición incorporada en cuatro documentos de docs/design. F07_ADENDA_51_CONTRATO_CONSUMIDOR_FRONT_013.md desarrolla los formularios y la materialización como propuesta pendiente de aprobación íntegra. Su lectura adicional de padres, campos, mensajes nuevos, temporalidad y compatibilidad no se incorporan por arrastre. FRONT-013 continúa parcial; sin commit ni publicación.

## Resolución posterior

El usuario aprobó íntegramente la Adenda 51 mediante «Si la apruebo». Esta propuesta conserva los hechos de la base y las etapas de aprobación como historia. El contrato operativo completo es `F07_ADENDA_51_CONTRATO_CONSUMIDOR_FRONT_013.md`; no queda pendiente la aprobación de sus formularios/materialización. La implementación y sus resultados vigentes se registran en `FRONT_013_GENERACION_MANUAL.md`.